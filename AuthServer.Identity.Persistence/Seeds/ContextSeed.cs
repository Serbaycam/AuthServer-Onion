using AuthServer.Identity.Domain.Constants;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthServer.Identity.Persistence.Seeds
{
    public static class ContextSeed
    {
        public static async Task SeedRolesAsync(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
        {
            // 1. Rolleri Veritabanına Ekle
            // Enum'daki her bir değeri gez ve veritabanında yoksa oluştur.
            await roleManager.CreateAsync(new AppRole { Name = Roles.SuperAdmin.ToString() });
            await roleManager.CreateAsync(new AppRole { Name = Roles.LabManager.ToString() });
            await roleManager.CreateAsync(new AppRole { Name = Roles.LabTechnician.ToString() });
            await roleManager.CreateAsync(new AppRole { Name = Roles.Basic.ToString() });
        }

        public static async Task SeedSuperAdminAsync(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
        {
            // 2. Default Süper Admin Kullanıcısını Oluştur
            var superUser = new AppUser
            {
                UserName = "superadmin",
                Email = "superadmin@AuthServer.local", // Bu maili değiştirebilirsin
                FirstName = "admin",
                LastName = "super",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true
            };

            if (userManager.Users.All(u => u.Id != superUser.Id))
            {
                var user = await userManager.FindByEmailAsync(superUser.Email);
                if (user == null)
                {
                    // Kullanıcıyı oluştur (Şifre: Pa$$word123!)
                    await userManager.CreateAsync(superUser, "Pa$$word123!");

                    // Kullanıcıya Rolleri Ata
                    await userManager.AddToRoleAsync(superUser, Roles.SuperAdmin.ToString());
                }
            }
        }
        public static async Task SeedRoleClaimsAsync(RoleManager<AppRole> roleManager)
        {
            // A) LabManager Rolünü Bul
            var labManagerRole = await roleManager.FindByNameAsync(Roles.LabManager.ToString());
            if (labManagerRole != null)
            {
                // Şefe Laboratuvar ile ilgili TÜM yetkileri ver
                await AddClaimIfNotExists(roleManager, labManagerRole, Permissions.Laboratories.Create);
                await AddClaimIfNotExists(roleManager, labManagerRole, Permissions.Laboratories.Edit);
                await AddClaimIfNotExists(roleManager, labManagerRole, Permissions.Laboratories.View);
                await AddClaimIfNotExists(roleManager, labManagerRole, Permissions.Laboratories.Delete);
            }

            // B) LabTechnician Rolünü Bul
            var technicianRole = await roleManager.FindByNameAsync(Roles.LabTechnician.ToString());
            if (technicianRole != null)
            {
                // Teknisyene SADECE Görüntüleme ver
                await AddClaimIfNotExists(roleManager, technicianRole, Permissions.Laboratories.Create);
                await AddClaimIfNotExists(roleManager, technicianRole, Permissions.Laboratories.Edit);
                await AddClaimIfNotExists(roleManager, technicianRole, Permissions.Laboratories.View);
            }
        }

        // Yardımcı Metod (Aynı yetkiyi 2 kere eklememek için)
        private static async Task AddClaimIfNotExists(RoleManager<AppRole> roleManager, AppRole role, string permission)
        {
            var allClaims = await roleManager.GetClaimsAsync(role);
            if (!allClaims.Any(a => a.Type == "permission" && a.Value == permission))
            {
                await roleManager.AddClaimAsync(role, new Claim("permission", permission));
            }
        }
    }
}