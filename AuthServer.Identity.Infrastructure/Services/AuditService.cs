using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuthServer.Identity.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly IApplicationDbContext _context;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "newPassword", "confirmPassword", "passwordHash", "securityStamp",
            "token", "accessToken", "refreshToken", "resetToken", "secret", "authorization", "cookie"
        };

        public AuditService(IApplicationDbContext context, IHttpContextAccessor? httpContextAccessor = null)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task LogAsync(string userId, string action, string entityName, string entityId, object details, string ipAddress)
            => LogAsync(userId, action, entityName, entityId, details, ipAddress,
                _httpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None);

        public async Task LogAsync(string userId, string action, string entityName, string entityId, object details,
            string ipAddress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Handlers project an explicit field whitelist. This additional recursive
            // guard excludes credential fields if a future caller includes them.
            var changes = JsonSerializer.SerializeToNode(details) ?? new JsonObject();
            RemoveSensitiveProperties(changes);
            var correlationId = _httpContextAccessor?.HttpContext?.TraceIdentifier
                ?? Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    correlationId,
                    outcome = "Succeeded",
                    changes
                }),
                IpAddress = ipAddress,
                CreatedDate = DateTime.UtcNow // BaseEntity'den geliyor
            };

            _context.AuditLogs.Add(log);
            // This shared scoped context also flushes staged session writes from login.
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static void RemoveSensitiveProperties(JsonNode? node)
        {
            if (node is JsonObject value)
                foreach (var property in value.ToArray())
                {
                    if (SensitiveProperties.Contains(property.Key)) value.Remove(property.Key);
                    else RemoveSensitiveProperties(property.Value);
                }
            else if (node is JsonArray array)
                foreach (var item in array) RemoveSensitiveProperties(item);
        }
    }
}
