using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.CreateUserByAdmin
{
    public class CreateUserByAdminHandler : IRequestHandler<CreateUserByAdminCommand, ServiceResponse<Guid>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public CreateUserByAdminHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<Guid>> Handle(CreateUserByAdminCommand request, CancellationToken cancellationToken)
        {
            request.Roles = request.Roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
            if (request.Roles.Count == 0) request.Roles.Add("Basic");
            foreach (var role in request.Roles)
                if (!await _roleManager.RoleExistsAsync(role)) return new ServiceResponse<Guid>("Rol bulunamadı.");
            // 1. Email kontrolü
            var userExists = await _userManager.FindByEmailAsync(request.Email);
            if (userExists != null)
                return new ServiceResponse<Guid>("Bu email adresi zaten kullanımda.");

            // 2. User nesnesini oluştur
            var user = new AppUser
            {
                Email = request.Email,
                UserName = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = true,
                EmailConfirmed = true // Yönetici oluşturduğu için onaylı varsayıyoruz
            };

            // 3. Veritabanına kaydet
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                var response = new ServiceResponse<Guid>("Kullanıcı oluşturulamadı.");
                response.Errors = errors;
                return response;
            }

            // 4. Rol Atama
            // Eğer rol listesi boşsa "Basic" ata
            if (request.Roles == null || !request.Roles.Any()) request.Roles = new List<string> { "Basic" };

            var roleResult = await _userManager.AddToRolesAsync(user, request.Roles.Distinct(StringComparer.OrdinalIgnoreCase));
            if (!roleResult.Succeeded) return new ServiceResponse<Guid>("Roller atanamadı.") { Errors = roleResult.Errors.Select(e => e.Description).ToList() };

            // 5. Audit Log
            await _auditService.LogAsync(
                _currentUserService.UserId ?? "System",
                "CreateUserByAdmin",
                "AppUser",
                user.Id.ToString(),
                new { user.Email, AssignedRoles = request.Roles },
                _currentUserService.IpAddress ?? "Unknown"
            );

            return new ServiceResponse<Guid>(user.Id, "Kullanıcı başarıyla oluşturuldu.");
        }
    }
}