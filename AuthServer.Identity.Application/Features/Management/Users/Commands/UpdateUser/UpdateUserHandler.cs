using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUser
{
    public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IAuditService _auditService;

        public UpdateUserHandler(UserManager<AppUser> userManager, IAuditService auditService)
        {
            _userManager = userManager;
            _auditService = auditService;
        }

        public async Task<ServiceResponse<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null) return new ServiceResponse<bool>("Kullanıcı bulunamadı.");

            // 1. E-Posta Değişikliği ve Çakışma Kontrolü
            if (request.Email != user.Email)
            {
                var emailOwner = await _userManager.FindByEmailAsync(request.Email);
                // Eğer bu maili kullanan biri varsa VE o kişi şu an düzenlediğimiz kişi değilse hata ver.
                if (emailOwner != null && emailOwner.Id != user.Id)
                {
                    return new ServiceResponse<bool>($"'{request.Email}' adresi başka bir kullanıcı tarafından kullanılıyor.");
                }

                // Email değişince UserName'i de güncellemek genelde standarttır
                user.Email = request.Email;
                user.UserName = request.Email;
                user.EmailConfirmed = true; // Yönetici değiştirdiği için onaylı sayıyoruz
            }

            // 2. Diğer Bilgileri Güncelle
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;

            // 3. Veritabanına Yaz
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return new ServiceResponse<bool>("Güncelleme sırasında hata oluştu.");
            }

            // 4. Audit Log
            await _auditService.LogAsync(
                request.AdminId ?? "System",
                "UpdateUserInfo",
                "AppUser",
                user.Id.ToString(),
                new { UpdatedEmail = user.Email, user.FirstName, user.LastName },
                request.IpAddress ?? "Unknown"
            );

            return new ServiceResponse<bool>(true, "Kullanıcı bilgileri güncellendi.");
        }
    }
}