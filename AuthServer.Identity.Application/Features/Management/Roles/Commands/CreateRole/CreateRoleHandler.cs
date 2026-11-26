using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.CreateRole
{
    public class CreateRoleHandler : IRequestHandler<CreateRoleCommand, ServiceResponse<string>>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;
        public CreateRoleHandler(RoleManager<AppRole> roleManager, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _roleManager = roleManager;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<string>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
        {
            if (await _roleManager.RoleExistsAsync(request.RoleName))
                return new ServiceResponse<string>("Bu rol zaten mevcut.");

            var role = new AppRole { Name = request.RoleName };
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded) return new ServiceResponse<string>("Rol oluşturulamadı.") { Errors = result.Errors.Select(e => e.Description).ToList() };
            await _auditService.LogAsync(_currentUserService.UserId ?? "System", "CreateRoleCommand", "AppRole",
                role.Id.ToString(), new { before = (object?)null, after = new { name = role.Name } },
                _currentUserService.IpAddress ?? "Unknown", cancellationToken);
            return new ServiceResponse<string>(request.RoleName, "Rol başarıyla oluşturuldu.");
        }
    }
}
