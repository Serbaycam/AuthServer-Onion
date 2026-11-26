using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.DeleteRole
{
    public class DeleteRoleHandler : IRequestHandler<DeleteRoleCommand, ServiceResponse<bool>>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteRoleHandler(RoleManager<AppRole> roleManager, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _roleManager = roleManager;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return new ServiceResponse<bool>("Rol bulunamadı.");

            // Temel rolleri silmeyi engelleyebilirsin (Opsiyonel Güvenlik)
            if (role.Name == "SuperAdmin" || role.Name == "Basic")
                return new ServiceResponse<bool>("Bu temel sistem rolü silinemez.");

            var before = new { name = role.Name };
            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                await _auditService.LogAsync(_currentUserService.UserId ?? "System", "DeleteRoleCommand", "AppRole",
                    role.Id.ToString(), new { before, after = (object?)null },
                    _currentUserService.IpAddress ?? "Unknown", cancellationToken);
                return new ServiceResponse<bool>(true, "Rol silindi.");
            }

            return new ServiceResponse<bool>("Silme işlemi başarısız.");
        }
    }
}
