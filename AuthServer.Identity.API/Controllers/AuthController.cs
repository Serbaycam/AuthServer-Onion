using AuthServer.Identity.Application.Features.Auth.Commands.Login;
using AuthServer.Identity.Application.Features.Auth.Commands.RefreshToken;
using AuthServer.Identity.Application.Features.Auth.Commands.Revoke;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Identity.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginCommand command)
        {
            var response = await _mediator.Send(command);

            if (response.Succeeded)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(RefreshTokenCommand command)
        {
            var response = await _mediator.Send(command);

            if (response.Succeeded)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        [HttpPost("revoke-token")]
        public async Task<IActionResult> Revoke(RevokeTokenCommand command)
        {
            var response = await _mediator.Send(command);

            if (response.Succeeded)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
    }
}