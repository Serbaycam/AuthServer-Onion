using Microsoft.EntityFrameworkCore;
using AuthServer.Identity.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace AuthServer.Identity.Application.Interfaces
{
  public interface IApplicationDbContext
  {
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
  }
}