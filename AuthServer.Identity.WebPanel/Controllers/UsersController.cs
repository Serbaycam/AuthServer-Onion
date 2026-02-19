using AuthServer.Identity.WebPanel.Models;
using AuthServer.Identity.WebPanel.Models.Management;
using AuthServer.Identity.WebPanel.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Identity.WebPanel.Controllers;

[Authorize]
public sealed class UsersController : Controller
{
    private readonly ManagementApiClient _api;

    public UsersController(ManagementApiClient api) => _api = api;

    private async Task<string?> GetAccessTokenAsync()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Properties is null) return null;
        return AuthTicketTokenStore.GetAccessToken(result.Properties);
    }

    private IActionResult ForbidOrLogin()
    {
        // UI tarafında basit: login'e at.
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var resp = await _api.GetAllUsersAsync(token, ct);
        if (resp?.Succeeded == true && resp.Data is not null)
            return View(resp.Data);

        TempData["Error"] = resp?.Message ?? "Kullanıcılar alınamadı (yetki/bağlantı).";
        return View(new List<UserWithRolesDto>());
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var roles = await _api.GetRolesAsync(token, ct);

        return View(new CreateUserByAdminVm
        {
            AllRoles = roles?.Data ?? new List<RoleDto>()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserByAdminVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        // Checkbox post: Roles null gelmesin
        vm.Roles ??= new List<string>();

        if (!ModelState.IsValid)
        {
            var roles = await _api.GetRolesAsync(token, ct);
            vm.AllRoles = roles?.Data ?? new List<RoleDto>();
            return View(vm);
        }

        var resp = await _api.CreateUserAsync(token, vm, ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Kullanıcı oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", resp?.Message ?? "Kullanıcı oluşturulamadı.");
        if (resp?.Errors is not null)
            foreach (var e in resp.Errors) ModelState.AddModelError("", e);

        var rolesAgain = await _api.GetRolesAsync(token, ct);
        vm.AllRoles = rolesAgain?.Data ?? new List<RoleDto>();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var users = await _api.GetAllUsersAsync(token, ct);
        var u = users?.Data?.FirstOrDefault(x => x.Id == id);
        if (u is null) return NotFound();

        return View(new UpdateUserVm
        {
            UserId = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateUserVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        if (!ModelState.IsValid)
            return View(vm);

        var resp = await _api.UpdateUserAsync(token, vm, ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Kullanıcı güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", resp?.Message ?? "Güncelleme başarısız.");
        if (resp?.Errors is not null)
            foreach (var e in resp.Errors) ModelState.AddModelError("", e);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Roles(Guid id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var users = await _api.GetAllUsersAsync(token, ct);
        var u = users?.Data?.FirstOrDefault(x => x.Id == id);
        if (u is null) return NotFound();

        var roles = await _api.GetRolesAsync(token, ct);

        return View(new AssignRolesVm
        {
            UserId = u.Id,
            Email = u.Email,
            Roles = u.Roles?.ToList() ?? new List<string>(),
            AllRoles = roles?.Data ?? new List<RoleDto>()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Roles(AssignRolesVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        vm.Roles ??= new List<string>();

        var resp = await _api.AssignRolesAsync(token, vm, ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Roller güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", resp?.Message ?? "Rol atama başarısız.");
        var roles = await _api.GetRolesAsync(token, ct);
        vm.AllRoles = roles?.Data ?? new List<RoleDto>();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ChangePassword(Guid id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var users = await _api.GetAllUsersAsync(token, ct);
        var u = users?.Data?.FirstOrDefault(x => x.Id == id);
        if (u is null) return NotFound();

        return View(new AdminChangePasswordVm
        {
            UserId = u.Id,
            Email = u.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(AdminChangePasswordVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        if (string.IsNullOrWhiteSpace(vm.NewPassword) || vm.NewPassword.Length < 6)
        {
            ModelState.AddModelError("", "Yeni şifre en az 6 karakter olmalı.");
            return View(vm);
        }

        var resp = await _api.AdminChangePasswordAsync(token, vm.UserId, vm.NewPassword, ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Şifre değiştirildi.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", resp?.Message ?? "Şifre değiştirilemedi.");
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, string currentIsActive, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var resp = await _api.UpdateUserStatusAsync(token, id, currentIsActive == "true" ? false : true, ct);
        TempData[resp?.Succeeded == true ? "Success" : "Error"] = resp?.Message ?? "İşlem tamamlanamadı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KillSessions(Guid id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var resp = await _api.RevokeAllSessionsAsync(token, id, ct);
        TempData[resp?.Succeeded == true ? "Success" : "Error"] = resp?.Message ?? "Oturumlar sonlandırılamadı.";
        return RedirectToAction(nameof(Index));
    }
}
