using AuthServer.Identity.Application.Features.Management.Users.Commands.AssignRoles;
using AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUserStatus;
using AuthServer.Identity.Application.Features.Management.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthServer.Identity.Application.Features.Management.Users.Commands.AdminChangePassword;
using AuthServer.Identity.Application.Features.Management.Users.Commands.CreateUserByAdmin;
using System.Security.Claims;
using AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUser;
using AuthServer.Identity.Application.Features.Auth.Commands.RevokeAll;

namespace AuthServer.Identity.API.Controllers
{
    [Authorize(Roles = "SuperAdmin")] // Sadece SuperAdmin girebilir
    [Route("api/[controller]")]
    [ApiController]
    public class UserManagementController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserManagementController(IMediator mediator) => _mediator = mediator;

        // Tüm Kullanıcıları Listele
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAll()
        {
            var response = await _mediator.Send(new GetUsersWithRolesQuery());
            return Ok(response);
        }
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser(CreateUserByAdminCommand command)
        {
            command.AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            command.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            var response = await _mediator.Send(command);
            if (response.Succeeded) return Ok(response);
            return BadRequest(response);
        }
        [HttpPut("update-user")]
        public async Task<IActionResult> UpdateUser(UpdateUserCommand command)
        {
            command.AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            command.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            var response = await _mediator.Send(command);
            if (response.Succeeded) return Ok(response);
            return BadRequest(response);
        }
        // 2. Yönetici Tarafından Şifre Değiştirme
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(AdminChangePasswordCommand command)
        {
            command.AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            command.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            var response = await _mediator.Send(command);
            if (response.Succeeded) return Ok(response);
            return BadRequest(response);
        }
        // Kullanıcıya Rol Ata (Eksik olan kısım buydu)
        [HttpPost("assign-roles")]
        public async Task<IActionResult> AssignRoles(AssignRolesCommand command)
        {
            var response = await _mediator.Send(command);
            if (response.Succeeded) return Ok(response);
            return BadRequest(response);
        }

        // Kullanıcıyı Aktif/Pasif Yap
        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus(UpdateUserStatusCommand command)
        {
            var response = await _mediator.Send(command);
            if (response.Succeeded) return Ok(response);
            return BadRequest(response);
        }
        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAll([FromBody] RevokeAllTokensCommand command)
        {
            var response = await _mediator.Send(command);
            if (response.Succeeded) return Ok(response);
            return BadRequest(response);
        }
    }
}