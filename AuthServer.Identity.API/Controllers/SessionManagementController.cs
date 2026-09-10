using AuthServer.Identity.Application.Features.Management.Sessions.Commands.KillSession;
using AuthServer.Identity.Application.Features.Management.Sessions.Queries.GetActiveSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthServer.Identity.API.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("api/[controller]")]
    [ApiController]
    public class SessionManagementController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SessionManagementController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("active-sessions")]
        public async Task<IActionResult> GetActiveSessions()
        {
            // Mevcut isteği yapan token'ı (Access Token) ayırt edemeyiz ama
            // eğer RefreshToken'ı header veya cookie ile gönderiyorsan buraya ekleyebilirsin.
            // Şimdilik boş gönderiyoruz.
            var query = new GetActiveSessionsQuery { CurrentSessionId = Guid.TryParse(User.FindFirstValue("sid"), out var id) ? id : null };
            var response = await _mediator.Send(query, HttpContext.RequestAborted);
            return response.Succeeded ? Ok(response) : BadRequest(response);
        }

        [HttpPost("kill-session")]
        public async Task<IActionResult> KillSession(KillSessionCommand command)
        {
            var response = await _mediator.Send(command, HttpContext.RequestAborted);
            return response.Succeeded ? Ok(response) : BadRequest(response);
        }
    }
}