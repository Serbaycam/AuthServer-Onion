using MediatR;
using Microsoft.AspNetCore.Mvc;
using AuthServer.Identity.Application.Features.Auth.Commands.Login;
using AuthServer.Identity.Application.Features.Auth.Commands.RefreshToken;
using AuthServer.Identity.Application.Features.Auth.Commands.Register;
using AuthServer.Identity.Application.Features.Auth.Commands.Revoke;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

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

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(CreateUserCommand command)
    {
      // İsteği Application katmanındaki Handler'a gönderir
      var response = await _mediator.Send(command);

      if (response.Succeeded)
      {
        return Ok(response);
      }

      return BadRequest(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command)
    {
      // IP Adresini yakalıyoruz
      // Eğer proxy/cloudflare arkasında çalışacaksan "X-Forwarded-For" headerına bakmak gerekir.
      // IP Adresini yakalıyoruz
      command.IpAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "127.0.0.1"; // Eğer IP okuyamazsa (Localhost) varsayılan değer ata

      var response = await _mediator.Send(command);

      if (response.Succeeded)
      {
        // Opsiyonel: Refresh Token'ı Cookie olarak da dönebilirsin (HttpOnly Cookie daha güvenlidir)
        // SetRefreshTokenCookie(response.Data.RefreshToken);

        return Ok(response);
      }

      return BadRequest(response);
    }
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenCommand command)
    {
      // IP Adresini yakala
      command.IpAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "127.0.0.1";

      var response = await _mediator.Send(command);

      if (response.Succeeded)
      {
        return Ok(response);
      }

      return BadRequest(response); // Veya 401 Unauthorized dönebilirsin duruma göre
    }
    [HttpPost("revoke-token")]
    public async Task<IActionResult> Revoke(RevokeTokenCommand command)
    {
      command.IpAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "127.0.0.1";

      var response = await _mediator.Send(command);

      if (response.Succeeded)
      {
        return Ok(response);
      }

      return BadRequest(response);
    }
  }
}