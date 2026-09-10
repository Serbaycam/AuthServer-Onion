using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.CreateRole
{
    public class CreateRoleHandler : IRequestHandler<CreateRoleCommand, ServiceResponse<string>>
    {
        private readonly RoleManager<AppRole> _roleManager;
        public CreateRoleHandler(RoleManager<AppRole> roleManager) => _roleManager = roleManager;

        public async Task<ServiceResponse<string>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
        {
            if (await _roleManager.RoleExistsAsync(request.RoleName))
                return new ServiceResponse<string>("Bu rol zaten mevcut.");

            var result = await _roleManager.CreateAsync(new AppRole { Name = request.RoleName });
            if (!result.Succeeded) return new ServiceResponse<string>("Rol oluşturulamadı.") { Errors = result.Errors.Select(e => e.Description).ToList() };
            return new ServiceResponse<string>(request.RoleName, "Rol başarıyla oluşturuldu.");
        }
    }
}
