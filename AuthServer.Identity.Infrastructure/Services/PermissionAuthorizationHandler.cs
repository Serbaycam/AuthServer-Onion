using AuthServer.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler(UserManager<AppUser> users, RoleManager<AppRole> roles)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null || !user.IsActive) return;
        foreach (var name in await users.GetRolesAsync(user))
        {
            if (name == "SuperAdmin") { context.Succeed(requirement); return; }
            var role = await roles.FindByNameAsync(name);
            if (role != null && (await roles.GetClaimsAsync(role)).Any(c => c.Type == "permission" && c.Value == requirement.Permission))
            { context.Succeed(requirement); return; }
        }
    }
}
