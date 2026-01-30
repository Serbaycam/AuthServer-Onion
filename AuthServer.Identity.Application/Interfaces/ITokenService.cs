using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Domain.Entities;
using System.Security.Claims;

namespace AuthServer.Identity.Application.Interfaces
{
    public interface ITokenService
    {
        Task<TokenDto> CreateTokenAsync(AppUser user, IList<string> roles);
        string GenerateRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}