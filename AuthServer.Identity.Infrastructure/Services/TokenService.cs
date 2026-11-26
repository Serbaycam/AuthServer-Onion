using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AuthServer.Identity.Infrastructure.Services;

public sealed class TokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings settings = options.Value;

    public DateTime GetAbsoluteSessionExpiration(DateTime createdAt)
        => createdAt.AddDays(settings.AbsoluteSessionLifetimeDays);

    public Task<TokenDto> CreateTokenAsync(AppUser user, IList<string> roles, Guid sessionId,
        DateTime? absoluteExpiration = null)
    {
        var now = DateTime.UtcNow;
        var deadline = absoluteExpiration ?? GetAbsoluteSessionExpiration(now);
        var accessExpiration = Min(now.AddMinutes(settings.AccessTokenExpirationMinutes), deadline);
        var refreshExpiration = Min(now.AddDays(settings.RefreshTokenExpirationDays), deadline);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId.ToString()),
            new("security_stamp", user.SecurityStamp ?? ""),
            new("fullName", user.FullName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            expires: accessExpiration, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Task.FromResult(new TokenDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            AccessTokenExpiration = accessExpiration,
            RefreshToken = GenerateRefreshToken(),
            RefreshTokenExpiration = refreshExpiration
        });
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;
}
