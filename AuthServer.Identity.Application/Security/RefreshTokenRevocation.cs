using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Security;

public static class RefreshTokenRevocation
{
    // The caller may hold a pre-rotation credential or an old row from the admin list.
    // Follow its replacements before saving; never acknowledge logout while a successor is usable.
    // SaveChanges' RevokedDate concurrency check arbitrates races with another rotation.
    public static async Task<IReadOnlyList<Guid>> RevokeChainAsync(IApplicationDbContext db,
        RefreshToken first, string reason, string? ipAddress, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var revoked = new List<Guid>();
        var token = first;
        var now = DateTime.UtcNow;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(token.Id)) throw new InvalidOperationException("Refresh token replacement chain contains a cycle.");
            if (token.RevokedDate == null)
            {
                token.RevokedDate = now;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = reason;
                revoked.Add(token.Id);
            }

            if (string.IsNullOrEmpty(token.ReplacedByToken)) return revoked;
            var successorHash = token.ReplacedByToken;
            // A malformed cross-user link must never revoke a different account's credentials.
            token = await db.RefreshTokens.SingleOrDefaultAsync(candidate => candidate.UserId == first.UserId &&
                candidate.Token == successorHash, cancellationToken)
                ?? throw new InvalidOperationException("Refresh token replacement chain is incomplete.");
        }
    }
}
