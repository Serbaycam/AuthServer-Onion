using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Identity.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class TestController : ControllerBase
  {
    // 1. Herkes görebilir (Giriş yapmasına bile gerek yok)
    [HttpGet("public")]
    public IActionResult GetPublic()
    {
      return Ok("Bunu herkes görebilir. Kapı açık.");
    }

    // 2. Sadece Giriş Yapmış Kullanıcılar (Token'ı olan herkes)
    [Authorize]
    [HttpGet("secured")]
    public IActionResult GetSecured()
    {
      return Ok("Bunu sadece giriş yapmış kullanıcılar görebilir.");
    }

    // 3. Sadece 'Admin' veya 'SuperAdmin' Rolü Olanlar
    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpGet("admin-only")]
    public IActionResult GetAdminOnly()
    {
      return Ok("Patron sensin! Bunu sadece Adminler görebilir.");
    }
  }
}