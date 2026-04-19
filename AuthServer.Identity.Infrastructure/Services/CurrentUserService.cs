using AuthServer.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AuthServer.Identity.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        public string? IpAddress
        {
            get
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                if (request == null) return "Unknown";

                var forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    return forwardedFor;
                }

                return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            }
        }
    }
}
