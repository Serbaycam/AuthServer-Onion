using AuthServer.Identity.WebPanel.Models;
using AuthServer.Identity.WebPanel.Models.Management;
using AuthServer.Identity.WebPanel.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Identity.WebPanel.Controllers;

[Authorize]
public sealed class RolesController : Controller
{
    private readonly ManagementApiClient _api;

    public RolesController(ManagementApiClient api) => _api = api;

    private async Task<string?> GetAccessTokenAsync()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Properties is null) return null;
        return AuthTicketTokenStore.GetAccessToken(result.Properties);
    }

    private IActionResult ForbidOrLogin() => RedirectToAction("Login", "Account");

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var resp = await _api.GetRolesAsync(token, ct);
        if (resp?.Succeeded == true && resp.Data is not null)
            return View(resp.Data);

        TempData["Error"] = resp?.Message ?? "Roller alınamadı.";
        return View(new List<RoleDto>());
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateRoleVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        if (string.IsNullOrWhiteSpace(vm.RoleName))
        {
            ModelState.AddModelError("", "Rol adı zorunlu.");
            return View(vm);
        }

        var resp = await _api.CreateRoleAsync(token, vm.RoleName.Trim(), ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Rol oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", resp?.Message ?? "Rol oluşturulamadı.");
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var roles = await _api.GetRolesAsync(token, ct);
        var r = roles?.Data?.FirstOrDefault(x => x.Id == id);
        if (r is null) return NotFound();

        return View(new UpdateRoleVm { RoleId = r.Id, NewRoleName = r.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateRoleVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        if (string.IsNullOrWhiteSpace(vm.NewRoleName))
        {
            ModelState.AddModelError("", "Rol adı zorunlu.");
            return View(vm);
        }

        var resp = await _api.UpdateRoleAsync(token, vm.RoleId, vm.NewRoleName.Trim(), ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Rol güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", resp?.Message ?? "Rol güncellenemedi.");
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var resp = await _api.DeleteRoleAsync(token, id, ct);
        TempData[resp?.Succeeded == true ? "Success" : "Error"] = resp?.Message ?? "Silme başarısız.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(string id, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var roles = await _api.GetRolesAsync(token, ct);
        var role = roles?.Data?.FirstOrDefault(x => x.Id == id);
        if (role is null) return NotFound();

        var allPermResp = await _api.GetAllPermissionsAsync(token, ct);
        var allPerms = allPermResp?.Data ?? new List<string>();

        // Mevcut role permission'larını (opsiyonel endpoint) best-effort çek
        var selected = await _api.GetRolePermissionsBestEffortAsync(token, id, ct);
        var selectedSet = new HashSet<string>(selected);

        var items = allPerms
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(p =>
            {
                // Permissions.Laboratories.View
                var parts = p.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var module = parts.Length >= 3 ? parts[1] : "General";
                var action = parts.Length >= 3 ? parts[2] : p;

                return new PermissionItemVm
                {
                    Value = p,
                    Module = module,
                    Action = action,
                    Selected = selectedSet.Contains(p)
                };
            })
            .OrderBy(x => x.Module).ThenBy(x => x.Action)
            .ToList();

        return View(new RolePermissionsVm
        {
            RoleId = role.Id,
            RoleName = role.Name,
            Permissions = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(RolePermissionsVm vm, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return ForbidOrLogin();

        var selected = vm.Permissions?
            .Where(x => x.Selected)
            .Select(x => x.Value)
            .Distinct()
            .ToList() ?? new List<string>();

        var resp = await _api.UpdateRolePermissionsAsync(token, vm.RoleId, selected, ct);
        if (resp?.Succeeded == true)
        {
            TempData["Success"] = resp.Message ?? "Yetkiler güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = resp?.Message ?? "Yetkiler güncellenemedi.";
        return RedirectToAction(nameof(Permissions), new { id = vm.RoleId });
    }
}
