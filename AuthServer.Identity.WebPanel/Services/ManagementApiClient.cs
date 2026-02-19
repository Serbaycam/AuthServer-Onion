using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Identity.WebPanel.Models;
using AuthServer.Identity.WebPanel.Models.Management;
using Microsoft.Extensions.Options;

namespace AuthServer.Identity.WebPanel.Services;

public sealed class ManagementApiClient
{
    private readonly HttpClient _http;
    private readonly AuthApiOptions _opt;

    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ManagementApiClient(HttpClient http, IOptions<AuthApiOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    private Uri Url(string relative)
        => new(new Uri(_opt.BaseUrl, UriKind.Absolute), relative.TrimStart('/'));

    private async Task<ServiceResponse<T>?> SendAsync<T>(
        HttpMethod method,
        string relativeUrl,
        string bearerAccessToken,
        object? body = null,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(method, Url(relativeUrl));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken);

        if (body is not null)
            req.Content = JsonContent.Create(body, options: JsonOpt);

        using var resp = await _http.SendAsync(req, ct);

        // 404 gibi durumlarda wrapper gelmeyebilir
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new ServiceResponse<T> { Succeeded = true, Data = default, Message = "NotFound" };

        ServiceResponse<T>? wrapper = null;
        try
        {
            wrapper = await resp.Content.ReadFromJsonAsync<ServiceResponse<T>>(JsonOpt, ct);
        }
        catch
        {
            // ignore
        }

        if (wrapper is not null)
            return wrapper;

        return new ServiceResponse<T>
        {
            Succeeded = false,
            Message = $"{(int)resp.StatusCode} {resp.ReasonPhrase}"
        };
    }

    // ---------------- USERS ----------------
    public Task<ServiceResponse<List<UserWithRolesDto>>?> GetAllUsersAsync(string bearer, CancellationToken ct = default)
        => SendAsync<List<UserWithRolesDto>>(HttpMethod.Get, "api/UserManagement/all-users", bearer, null, ct);

    public Task<ServiceResponse<object>?> CreateUserAsync(string bearer, CreateUserByAdminVm vm, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/UserManagement/create-user", bearer, new
        {
            firstName = vm.FirstName,
            lastName = vm.LastName,
            email = vm.Email,
            password = vm.Password,
            roles = vm.Roles ?? new List<string>()
        }, ct);

    public Task<ServiceResponse<object>?> UpdateUserAsync(string bearer, UpdateUserVm vm, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Put, "api/UserManagement/update-user", bearer, new
        {
            userId = vm.UserId,
            firstName = vm.FirstName,
            lastName = vm.LastName,
            email = vm.Email
        }, ct);

    public Task<ServiceResponse<object>?> AssignRolesAsync(string bearer, AssignRolesVm vm, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/UserManagement/assign-roles", bearer, new
        {
            userId = vm.UserId,
            roles = vm.Roles ?? new List<string>()
        }, ct);

    public Task<ServiceResponse<object>?> UpdateUserStatusAsync(string bearer, Guid userId, bool isActive, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/UserManagement/update-status", bearer, new
        {
            userId,
            isActive
        }, ct);

    public Task<ServiceResponse<object>?> AdminChangePasswordAsync(string bearer, Guid userId, string newPassword, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/UserManagement/change-password", bearer, new
        {
            userId,
            newPassword
        }, ct);

    public Task<ServiceResponse<object>?> RevokeAllSessionsAsync(string bearer, Guid userId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/UserManagement/revoke-all", bearer, new
        {
            userId
        }, ct);

    // ---------------- ROLES ----------------
    public Task<ServiceResponse<List<RoleDto>>?> GetRolesAsync(string bearer, CancellationToken ct = default)
        => SendAsync<List<RoleDto>>(HttpMethod.Get, "api/RoleManagement/roles", bearer, null, ct);

    public Task<ServiceResponse<object>?> CreateRoleAsync(string bearer, string roleName, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/RoleManagement/role", bearer, new { roleName }, ct);

    public Task<ServiceResponse<object>?> UpdateRoleAsync(string bearer, string roleId, string newRoleName, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Put, "api/RoleManagement/role", bearer, new { roleId, newRoleName }, ct);

    public Task<ServiceResponse<object>?> DeleteRoleAsync(string bearer, string roleId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Delete, $"api/RoleManagement/role/{roleId}", bearer, null, ct);

    public Task<ServiceResponse<List<string>>?> GetAllPermissionsAsync(string bearer, CancellationToken ct = default)
        => SendAsync<List<string>>(HttpMethod.Get, "api/RoleManagement/permissions", bearer, null, ct);

    /// <summary>
    /// Opsiyonel: API'ye ekleyeceğimiz endpoint ile role ait mevcut permission'ları çekmek için.
    /// Endpoint yoksa NotFound döner ve boş listeye düşeriz.
    /// </summary>
    public async Task<List<string>> GetRolePermissionsBestEffortAsync(string bearer, string roleId, CancellationToken ct = default)
    {
        var resp = await SendAsync<List<string>>(HttpMethod.Get, $"api/RoleManagement/role/{roleId}/permissions", bearer, null, ct);
        return resp?.Data ?? new List<string>();
    }

    public Task<ServiceResponse<object>?> UpdateRolePermissionsAsync(string bearer, string roleId, List<string> permissions, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, "api/RoleManagement/permissions", bearer, new
        {
            roleId,
            permissions = permissions ?? new List<string>()
        }, ct);
}
