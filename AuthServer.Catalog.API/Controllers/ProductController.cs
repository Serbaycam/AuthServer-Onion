using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Catalog.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ProductController : ControllerBase
  {
    // 1. Herkesin görebileceği açık endpoint
    [HttpGet]
    public IActionResult GetAll()
    {
      var products = new List<string> { "Laptop", "Mouse", "Keyboard", "Monitor" };
      return Ok(products);
    }

    // 2. Sadece Token'ı olanların görebileceği endpoint
    [HttpGet("secured")]
    [Authorize]
    public IActionResult GetSecured()
    {
      // Token'dan gelen kullanıcı adını (veya emailini) okuyalım
      var user = User.Identity.Name ?? "Bilinmeyen Kullanıcı";
      return Ok($"Merhaba {user}, burası Catalog API'nin kilitli odasıdır!");
    }
  }
}