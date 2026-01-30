using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Infrastructure.Settings;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthServer.Identity.Infrastructure.Services
{
  public class TokenService : ITokenService
  {
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
      _jwtSettings = jwtSettings.Value;
    }

    public Task<TokenDto> CreateTokenAsync(AppUser user, IList<string> roles)
    {
      // 1. Claim'leri Hazırla (Kimlik bilgileri)
      var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Token ID
                new Claim("fullName", user.FullName ?? ""),
                new Claim("location", user.Location ?? "Global")// Custom Claim
            };

      // Rolleri ekle
      foreach (var role in roles)
      {
        claims.Add(new Claim(ClaimTypes.Role, role));
      }

      // 2. Anahtarı Hazırla
      var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
      var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

      // 3. Süreyi Belirle
      var expiration = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

      // 4. Token'ı Oluştur
      var token = new JwtSecurityToken(
          issuer: _jwtSettings.Issuer,
          audience: _jwtSettings.Audience,
          claims: claims,
          expires: expiration,
          signingCredentials: creds
      );

      // 5. Refresh Token Üret
      var refreshToken = GenerateRefreshToken();

      return Task.FromResult(new TokenDto
      {
        AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
        AccessTokenExpiration = expiration,
        RefreshToken = refreshToken,
        RefreshTokenExpiration = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
      });
    }

    public string GenerateRefreshToken()
    {
      var randomNumber = new byte[32];
      using var rng = RandomNumberGenerator.Create();
      rng.GetBytes(randomNumber);
      return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
      var tokenValidationParameters = new TokenValidationParameters
      {
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
        ValidateLifetime = false // Süresi dolsa bile kim olduğunu öğrenmek istiyoruz
      };

      var tokenHandler = new JwtSecurityTokenHandler();
      var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

      if (securityToken is not JwtSecurityToken jwtSecurityToken ||
          !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
      {
        throw new SecurityTokenException("Invalid token");
      }

      return principal;
    }
  }
}