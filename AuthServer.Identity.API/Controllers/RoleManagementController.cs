using AuthServer.Identity.Application.Features.Management.Roles.Commands.CreateRole;
using AuthServer.Identity.Application.Features.Management.Roles.Commands.DeleteRole;
using AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRole;
using AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRolePermissions;
using AuthServer.Identity.Application.Features.Management.Roles.Queries.GetRolePermissions;
using AuthServer.Identity.Application.Features.Management.Roles.Queries.GetRoles;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Security.Claims;

namespace AuthServer.Identity.API.Controllers
{
    [Authorize(Roles = "SuperAdmin")] // Sadece Patron Girebilir
    [Route("api/[controller]")]
    [ApiController]
    public class RoleManagementController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RoleManagementController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // 1. Tüm Rolleri Listele
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            return Ok(await _mediator.Send(new GetRolesQuery()));
        }

        // 2. Yeni Rol Oluştur
        [HttpPost("role")]
        public async Task<IActionResult> CreateRole(CreateRoleCommand command)
        {
            return Ok(await _mediator.Send(command));
        }

        // 3. Rol İsmini Güncelle
        [HttpPut("role")]
        public async Task<IActionResult> UpdateRole(UpdateRoleCommand command)
        {
            return Ok(await _mediator.Send(command));
        }

        // 4. Rol Sil
        [HttpDelete("role/{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            return Ok(await _mediator.Send(new DeleteRoleCommand { RoleId = id }));
        }

        // 5. Sistemdeki Tüm Yetkileri (Static Permissions) Listele
        [HttpGet("permissions")]
        public IActionResult GetAllPermissions()
        {
            // Reflection ile Permissions static class'ındaki tüm stringleri okuyoruz
            // Not: Namespace'ini projene göre kontrol et (AuthServer.Identity.Domain.Constants veya Application.Constants olabilir)
            var permissions = typeof(AuthServer.Identity.Domain.Constants.Permissions).GetNestedTypes()
                .SelectMany(c => c.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                .Select(f => f.GetValue(null).ToString())
                .ToList();

            return Ok(new ServiceResponse<List<string>>(permissions));
        }

        // 6. Bir Rolün Yetkilerini Güncelle
        [HttpPost("permissions")]
        public async Task<IActionResult> UpdatePermissions(UpdateRolePermissionsCommand command)
        {
            // Audit Log için Admin ID ve IP'yi ekliyoruz
            command.AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            command.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            return Ok(await _mediator.Send(command));
        }
        [HttpGet("role-permissions/{id}")]
        public async Task<IActionResult> GetRolePermissions(string id)
        {
            return Ok(await _mediator.Send(new GetRolePermissionsQuery { RoleId = id }));
        }
    }
}