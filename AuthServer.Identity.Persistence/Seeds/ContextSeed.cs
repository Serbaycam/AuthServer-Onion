using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Domain.Enums;
using System.Linq;
using System.Threading.Tasks;

namespace AuthServer.Identity.Persistence.Seeds
{
  public static class ContextSeed
  {
    public static async Task SeedRolesAsync(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
      // 1. Rolleri Veritabanına Ekle
      // Enum'daki her bir değeri gez ve veritabanında yoksa oluştur.
      await roleManager.CreateAsync(new AppRole { Name = Roles.SuperAdmin.ToString() });
      await roleManager.CreateAsync(new AppRole { Name = Roles.Admin.ToString() });
      await roleManager.CreateAsync(new AppRole { Name = Roles.Moderator.ToString() });
      await roleManager.CreateAsync(new AppRole { Name = Roles.Basic.ToString() });
    }

    public static async Task SeedSuperAdminAsync(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
      // 2. Default Süper Admin Kullanıcısını Oluştur
      var superUser = new AppUser
      {
        UserName = "superadmin",
        Email = "superadmin@AuthServer.com", // Bu maili değiştirebilirsin
        FirstName = "Plms",
        LastName = "SuperAdmin",
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
          await userManager.AddToRoleAsync(superUser, Roles.Basic.ToString());
          await userManager.AddToRoleAsync(superUser, Roles.Moderator.ToString());
          await userManager.AddToRoleAsync(superUser, Roles.Admin.ToString());
          await userManager.AddToRoleAsync(superUser, Roles.SuperAdmin.ToString());
        }
      }
    }
  }
}