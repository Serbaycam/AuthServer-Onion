using System.Data;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthServer.Identity.Persistence;

public sealed class UserSessionState(AppDbContext db, UserManager<AppUser> users) : IUserSessionState
{
    private readonly HashSet<Guid> heldUsers = [];

    public async Task<IUserSessionTransaction> LockAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        if (heldUsers.Count != 0)
            throw new InvalidOperationException("Acquire all affected users in one session lock operation.");
        IDbContextTransaction? owned = null;
        try
        {
            if (db.Database.CurrentTransaction == null)
                owned = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var locked = new Dictionary<Guid, AppUser>();
            foreach (var id in userIds.Distinct().OrderBy(id => id))
            {
                var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (user == null) continue;
                // Authentication/UserManager may already have tracked this account before the lock.
                await db.Entry(user).ReloadAsync(cancellationToken);
                locked.Add(id, user);
                heldUsers.Add(id);
            }
            return new SessionTransaction(owned, locked, heldUsers);
        }
        catch
        {
            heldUsers.Clear();
            if (owned != null) await owned.DisposeAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<Guid>> RevokeAllAsync(AppUser user, string reason, string? ipAddress,
        CancellationToken cancellationToken, bool rotateSecurityStamp = true)
    {
        if (!heldUsers.Contains(user.Id) || db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Account-wide session invalidation requires the user's session lock.");
        cancellationToken.ThrowIfCancellationRequested();
        if (rotateSecurityStamp)
        {
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException("The account security version could not be updated.");
        }
        // The stamp also invalidates rows not visible to a Serializable caller's older snapshot.
        var active = await db.RefreshTokens.Where(session => session.UserId == user.Id && session.RevokedDate == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var session in active)
        {
            session.RevokedDate = now;
            session.RevokedByIp = ipAddress;
            session.ReasonRevoked = reason;
        }
        return active.Select(session => session.Id).ToArray();
    }

    private sealed class SessionTransaction(IDbContextTransaction? owned,
        IReadOnlyDictionary<Guid, AppUser> users, HashSet<Guid> heldUsers) : IUserSessionTransaction
    {
        public AppUser? FindUser(Guid userId) => users.GetValueOrDefault(userId);
        public Task CommitAsync(CancellationToken cancellationToken)
            => owned?.CommitAsync(cancellationToken) ?? Task.CompletedTask;
        public async ValueTask DisposeAsync()
        {
            try { if (owned != null) await owned.DisposeAsync(); }
            finally { heldUsers.Clear(); }
        }
    }
}
