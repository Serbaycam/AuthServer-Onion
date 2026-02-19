using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Identity.WebPanel.Models;
using Microsoft.Extensions.Options;

namespace AuthServer.Identity.WebPanel.Services;

public sealed class AuthApiOptions
{
    public string BaseUrl { get; set; } = "";

    public string LoginPath { get; set; } = "";
    public string RefreshPath { get; set; } = "";
    public string RevokePath { get; set; } = "";
    public string DashboardPath { get; set; } = "";

    // Users
    public string UsersAllPath { get; set; } = "";
    public string UsersCreatePath { get; set; } = "";
    public string UsersUpdatePath { get; set; } = "";
    public string UsersChangePasswordPath { get; set; } = "";
    public string UsersAssignRolesPath { get; set; } = "";
    public string UsersUpdateStatusPath { get; set; } = "";
    public string UsersRevokeAllPath { get; set; } = "";

    // Roles & Permissions
    public string RolesListPath { get; set; } = "";
    public string RoleCreatePath { get; set; } = "";
    public string RoleUpdatePath { get; set; } = "";
    public string RoleDeletePath { get; set; } = "";
    public string PermissionsListPath { get; set; } = "";
    public string PermissionsUpdatePath { get; set; } = "";

    // Optional (API eklemesiyle)
    public string RolePermissionsGetPath { get; set; } = "";
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

    private Uri Url(string path) => new(new Uri(_opt.BaseUrl), path);

    private async Task<ServiceResponse<T>> ReadWrapper<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var wrapper = await resp.Content.ReadFromJsonAsync<ServiceResponse<T>>(JsonOpt, ct);
            if (wrapper is not null) return wrapper;
        }
        catch { /* ignore */ }

        return new ServiceResponse<T>
        {
            Succeeded = false,
            Message = $"API hata: {(int)resp.StatusCode} {resp.ReasonPhrase}"
        };
    }

    private static void Bearer(HttpRequestMessage req, string accessToken)
        => req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    // ---------------- Auth ----------------

    public async Task<TokenDto?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(Url(_opt.LoginPath),
            new LoginRequest { Email = email, Password = password }, JsonOpt, ct);

        var wrapper = await ReadWrapper<TokenDto>(resp, ct);
        return wrapper.Succeeded ? wrapper.Data : null;
    }

    public async Task<TokenDto?> RefreshAsync(string accessToken, string refreshToken, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(Url(_opt.RefreshPath),
            new RefreshTokenRequest { AccessToken = accessToken, RefreshToken = refreshToken }, JsonOpt, ct);

        var wrapper = await ReadWrapper<TokenDto>(resp, ct);
        return wrapper.Succeeded ? wrapper.Data : null;
    }

    public async Task<bool> RevokeAsync(string token, string? bearerAccessToken = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.RevokePath))
        {
            Content = JsonContent.Create(new RevokeTokenRequest { Token = token }, options: JsonOpt)
        };

        if (!string.IsNullOrWhiteSpace(bearerAccessToken))
            Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        var wrapper = await ReadWrapper<bool>(resp, ct);
        return wrapper.Succeeded;
    }

    public async Task<DashboardStatsDto?> GetStatsAsync(string bearerAccessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Url(_opt.DashboardPath));
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        var wrapper = await ReadWrapper<DashboardStatsDto>(resp, ct);
        return wrapper.Succeeded ? wrapper.Data : null;
    }

    // ---------------- Users ----------------

    public async Task<ServiceResponse<List<UserWithRolesDto>>> GetUsersAsync(string bearerAccessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Url(_opt.UsersAllPath));
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<List<UserWithRolesDto>>(resp, ct);
    }

    public async Task<ServiceResponse<Guid>> CreateUserAsync(string bearerAccessToken, CreateUserRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.UsersCreatePath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<Guid>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> UpdateUserAsync(string bearerAccessToken, UpdateUserRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, Url(_opt.UsersUpdatePath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> UpdateUserStatusAsync(string bearerAccessToken, UpdateUserStatusRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.UsersUpdateStatusPath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> AssignRolesAsync(string bearerAccessToken, AssignRolesRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.UsersAssignRolesPath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> AdminChangePasswordAsync(string bearerAccessToken, AdminChangePasswordRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.UsersChangePasswordPath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> RevokeAllAsync(string bearerAccessToken, RevokeAllTokensRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.UsersRevokeAllPath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    // ---------------- Roles ----------------

    public async Task<ServiceResponse<List<RoleDto>>> GetRolesAsync(string bearerAccessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Url(_opt.RolesListPath));
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<List<RoleDto>>(resp, ct);
    }

    public async Task<ServiceResponse<string>> CreateRoleAsync(string bearerAccessToken, CreateRoleRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.RoleCreatePath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<string>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> UpdateRoleAsync(string bearerAccessToken, UpdateRoleRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, Url(_opt.RoleUpdatePath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> DeleteRoleAsync(string bearerAccessToken, string roleId, CancellationToken ct = default)
    {
        var path = _opt.RoleDeletePath.Replace("{id}", Uri.EscapeDataString(roleId));
        using var req = new HttpRequestMessage(HttpMethod.Delete, Url(path));
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }

    public async Task<ServiceResponse<List<string>>> GetAllPermissionsAsync(string bearerAccessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Url(_opt.PermissionsListPath));
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<List<string>>(resp, ct);
    }

    public async Task<ServiceResponse<bool>> UpdateRolePermissionsAsync(string bearerAccessToken, UpdateRolePermissionsRequest body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(_opt.PermissionsUpdatePath))
        {
            Content = JsonContent.Create(body, options: JsonOpt)
        };
        Bearer(req, bearerAccessToken);

        var resp = await _http.SendAsync(req, ct);
        return await ReadWrapper<bool>(resp, ct);
    }
}
