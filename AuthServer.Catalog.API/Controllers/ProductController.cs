using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthServer.Catalog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            // 1. User.Identity.Name kontrolü
            var userName = User.Identity?.Name;

            // 2. ID kontrolü (Subject)
            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                         ?? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            // 3. Email'i manuel çekmeyi dene
            var email = User.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            // DEBUG: Token'dan gelen TÜM verileri görelim
            var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

            return Ok(new
            {
                IdentityName = userName, // Burası dolu gelmeli artık
                UserId = userId,
                Email = email,
                AllClaimsInToken = allClaims // Burayı incele
            });
        }
    }
}