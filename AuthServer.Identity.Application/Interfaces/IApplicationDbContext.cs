using AuthServer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<RefreshToken> RefreshTokens { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}