using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
namespace AuthServer.Identity.WebPanel.Services
{
    public sealed class MemoryCacheTicketStore : ITicketStore
    {
        private readonly IMemoryCache _cache;
        public MemoryCacheTicketStore(IMemoryCache cache) => _cache = cache;

        public Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var key = "tk:" + Guid.NewGuid().ToString("N");
            RenewAsync(key, ticket);
            return Task.FromResult(key);
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(1);
            _cache.Set(key, ticket, expiresUtc);
            return Task.CompletedTask;
        }

        public Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            _cache.TryGetValue(key, out AuthenticationTicket? ticket);
            return Task.FromResult(ticket);
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
