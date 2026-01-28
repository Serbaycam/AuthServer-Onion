using MediatR;
using Microsoft.AspNetCore.Identity;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Register
{
  public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, ServiceResponse<string>>
  {
    private readonly UserManager<AppUser> _userManager;

    public CreateUserCommandHandler(UserManager<AppUser> userManager)
    {
      _userManager = userManager;
    }

    public async Task<ServiceResponse<string>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
      // 1. Eşleşme kontrolü (Validation katmanında da yapılabilir ama burada garanti olsun)
      if (request.Password != request.ConfirmPassword)
        return new ServiceResponse<string>("Şifreler eşleşmiyor.");

      // 2. Email kontrolü
      var userExists = await _userManager.FindByEmailAsync(request.Email);
      if (userExists != null)
        return new ServiceResponse<string>("Bu email adresi zaten kullanımda.");

      // 3. User nesnesini oluştur
      var user = new AppUser
      {
        Email = request.Email,
        UserName = request.Email, // Genelde email username olarak kullanılır
        FirstName = request.FirstName,
        LastName = request.LastName,
        IsActive = true
      };

      // 4. Veritabanına kaydet (Şifreyi hashleyerek)
      var result = await _userManager.CreateAsync(user, request.Password);

      if (!result.Succeeded)
      {
        // Identity hatalarını toplayıp dönüyoruz
        var errors = result.Errors.Select(e => e.Description).ToList();
        var response = new ServiceResponse<string>("Kullanıcı oluşturulamadı.");
        response.Errors = errors;
        return response;
      }
      await _userManager.AddToRoleAsync(user, Roles.Basic.ToString());
      return new ServiceResponse<string>(user.Id.ToString(), "Kullanıcı başarıyla oluşturuldu.");
    }
  }
}