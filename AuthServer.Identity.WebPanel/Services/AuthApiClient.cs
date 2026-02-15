using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Identity.WebPanel.Models;
using Microsoft.Extensions.Options;
namespace AuthServer.Identity.WebPanel.Services
{
    public sealed class AuthApiOptions
    {
        public string BaseUrl { get; set; } = "";
        public string LoginPath { get; set; } = "";
        public string RefreshPath { get; set; } = "";
        public string RevokePath { get; set; } = "";
        public string DashboardPath { get; set; } = "";
    }

    public sealed class AuthApiClient
    {
        private readonly HttpClient _http;
        private readonly AuthApiOptions _opt;

        private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public AuthApiClient(HttpClient http, IOptions<AuthApiOptions> opt)
        {
            _http = http;
            _opt = opt.Value;
        }

        public async Task<TokenDto?> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            var url = new Uri(new Uri(_opt.BaseUrl), _opt.LoginPath);
            var resp = await _http.PostAsJsonAsync(url, new LoginRequest { Email = email, Password = password }, JsonOpt, ct);

            if (!resp.IsSuccessStatusCode) return null;

            var wrapper = await resp.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>(JsonOpt, ct);
            if (wrapper?.Succeeded != true || wrapper.Data is null) return null;

            return wrapper.Data;
        }

        public async Task<TokenDto?> RefreshAsync(string accessToken, string refreshToken, CancellationToken ct = default)
        {
            var url = new Uri(new Uri(_opt.BaseUrl), _opt.RefreshPath);
            var req = new RefreshTokenRequest { AccessToken = accessToken, RefreshToken = refreshToken };

            var resp = await _http.PostAsJsonAsync(url, req, JsonOpt, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var wrapper = await resp.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>(JsonOpt, ct);
            if (wrapper?.Succeeded != true || wrapper.Data is null) return null;

            return wrapper.Data;
        }
        public async Task<bool> RevokeAsync(string token, string? bearerAccessToken = null, CancellationToken ct = default)
        {
            var url = new Uri(new Uri(_opt.BaseUrl), _opt.RevokePath);

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new RevokeTokenRequest { Token = token }, options: JsonOpt)
            };

            if (!string.IsNullOrWhiteSpace(bearerAccessToken))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerAccessToken);

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return false;

            // wrapper: ServiceResponse<bool>
            var wrapper = await resp.Content.ReadFromJsonAsync<ServiceResponse<bool>>(JsonOpt, ct);
            return wrapper?.Succeeded == true;
        }
        public async Task<DashboardStatsDto?> GetStatsAsync(string bearerAccessToken, CancellationToken ct = default)
        {
            var url = new Uri(new Uri(_opt.BaseUrl), _opt.DashboardPath);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerAccessToken);
            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var wrapper = await resp.Content.ReadFromJsonAsync<ServiceResponse<DashboardStatsDto>>(JsonOpt, ct);
            if (wrapper?.Succeeded != true || wrapper.Data is null) return null;
            return wrapper.Data;
        }
    }
}
