using AuthServer.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Security;

public static class AdministratorGuard
{
    public static async Task<bool> IsLastActiveAdministratorAsync(UserManager<AppUser> users, AppUser user)
    {
        if (!user.IsActive || !await users.IsInRoleAsync(user, "SuperAdmin")) return false;
        return !(await users.GetUsersInRoleAsync("SuperAdmin")).Any(u => u.Id != user.Id && u.IsActive);
    }
}
