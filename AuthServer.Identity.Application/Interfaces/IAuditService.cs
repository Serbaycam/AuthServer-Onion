namespace AuthServer.Identity.Application.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string userId, string action, string entityName, string entityId, object details, string ipAddress);

        // Existing adapters can keep the original contract; the database implementation
        // overrides this overload to pass request cancellation through to SaveChanges.
        Task LogAsync(string userId, string action, string entityName, string entityId, object details,
            string ipAddress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return LogAsync(userId, action, entityName, entityId, details, ipAddress);
        }
    }
}
