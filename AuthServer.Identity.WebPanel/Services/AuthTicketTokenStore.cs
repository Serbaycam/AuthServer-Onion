using Microsoft.AspNetCore.Authentication;
using System.Globalization;

namespace AuthServer.Identity.WebPanel.Services
{
    public static class AuthTicketTokenStore
    {
        // Key'lerin başına .Token. koymaya gerek yok, düz string olarak saklayalım
        private const string AccessTokenKey = "access_token";
        private const string RefreshTokenKey = "refresh_token";
        private const string AccessExpiresAtKey = "access_expires_at";
        private const string RefreshExpiresAtKey = "refresh_expires_at";

        public static void Set(AuthenticationProperties props, string accessToken, string refreshToken,
            DateTimeOffset accessExpUtc, DateTimeOffset refreshExpUtc)
        {
            // UpdateTokenValue yerine doğrudan Items koleksiyonunu kullanıyoruz
            if (props.Items.ContainsKey(AccessTokenKey)) props.Items[AccessTokenKey] = accessToken;
            else props.Items.Add(AccessTokenKey, accessToken);

            if (props.Items.ContainsKey(RefreshTokenKey)) props.Items[RefreshTokenKey] = refreshToken;
            else props.Items.Add(RefreshTokenKey, refreshToken);

            var accessExpStr = accessExpUtc.ToString("o", CultureInfo.InvariantCulture);
            if (props.Items.ContainsKey(AccessExpiresAtKey)) props.Items[AccessExpiresAtKey] = accessExpStr;
            else props.Items.Add(AccessExpiresAtKey, accessExpStr);

            var refreshExpStr = refreshExpUtc.ToString("o", CultureInfo.InvariantCulture);
            if (props.Items.ContainsKey(RefreshExpiresAtKey)) props.Items[RefreshExpiresAtKey] = refreshExpStr;
            else props.Items.Add(RefreshExpiresAtKey, refreshExpStr);
        }

        public static string? GetAccessToken(AuthenticationProperties props)
        {
            // GetTokenValue yerine Items'dan çekiyoruz
            return props.Items.TryGetValue(AccessTokenKey, out var val) ? val : null;
        }

        public static string? GetRefreshToken(AuthenticationProperties props)
        {
            return props.Items.TryGetValue(RefreshTokenKey, out var val) ? val : null;
        }

        public static DateTimeOffset? GetAccessExp(AuthenticationProperties props)
        {
            if (props.Items.TryGetValue(AccessExpiresAtKey, out var val)) return Parse(val);
            return null;
        }

        public static DateTimeOffset? GetRefreshExp(AuthenticationProperties props)
        {
            if (props.Items.TryGetValue(RefreshExpiresAtKey, out var val)) return Parse(val);
            return null;
        }

        private static DateTimeOffset? Parse(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                return dto.ToUniversalTime();
            return null;
        }
    }
}