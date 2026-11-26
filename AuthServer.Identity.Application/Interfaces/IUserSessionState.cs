using AuthServer.Identity.Domain.Entities;

namespace AuthServer.Identity.Application.Interfaces;

// Serialize session issuance and account-wide invalidation on the same user rows.
public interface IUserSessionState
{
    Task<IUserSessionTransaction> LockAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> RevokeAllAsync(AppUser user, string reason, string? ipAddress,
        CancellationToken cancellationToken, bool rotateSecurityStamp = true);
}

public interface IUserSessionTransaction : IAsyncDisposable
{
    AppUser? FindUser(Guid userId);
    Task CommitAsync(CancellationToken cancellationToken);
}
