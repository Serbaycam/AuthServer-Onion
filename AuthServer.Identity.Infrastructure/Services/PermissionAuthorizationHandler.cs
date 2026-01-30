using AuthServer.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

// Policy Gereksinimi (İsim tutucu)
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

// Handler (Mantık İşleyici)
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryCache _cache;

    public PermissionAuthorizationHandler(IServiceScopeFactory serviceScopeFactory, IMemoryCache cache)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // 1. Kullanıcı ID'sini al
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // 2. SuperAdmin ise her kapı açıktır (GOD MODE)
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        if (userId == null) return;

        // 3. Kullanıcının yetkilerini CACHE'den getir (Yoksa DB'den çekip Cache'e yaz)
        // Key: UserPermissions_GUID
        var userPermissions = await _cache.GetOrCreateAsync($"UserPermissions_{userId}", async entry =>
        {
            // Cache süresi: 30 dakika (Performans için)
            entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

            // Yeni bir Scope aç (Singleton içinde Scoped servis çağırmak için şart)
            using var scope = _serviceScopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            // A) Kullanıcının Rollerini Bul
            var user = await userManager.FindByIdAsync(userId);
            var userRoles = await userManager.GetRolesAsync(user);

            // B) Bu rollerin sahip olduğu TÜM permission claimlerini topla
            var permissions = new List<string>();
            foreach (var roleName in userRoles)
            {
                var role = await roleManager.FindByNameAsync(roleName);
                if (role != null)
                {
                    var claims = await roleManager.GetClaimsAsync(role);
                    // Sadece "permission" tipindeki claimleri al
                    permissions.AddRange(claims.Where(c => c.Type == "permission").Select(c => c.Value));
                }
            }
            // Tekrar edenleri temizle (Distinct)
            return permissions.Distinct().ToList();
        });

        // 4. İstenen yetki listede var mı?
        if (userPermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}