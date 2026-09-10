using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AuthServer.Identity.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly IApplicationDbContext _context;

        public AuditService(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string userId, string action, string entityName, string entityId, object details, string ipAddress)
        {
            // Manuel Scope oluşturarak Scoped servislere (DbContext) erişiyoruz
            var context = _context;

            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = JsonSerializer.Serialize(details),
                IpAddress = ipAddress,
                CreatedDate = DateTime.UtcNow // BaseEntity'den geliyor
            };

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync(default);
        }
    }
}