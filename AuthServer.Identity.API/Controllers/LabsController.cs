using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthServer.Identity.Domain.Constants; // Permissions

namespace AuthServer.Identity.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabsController : ControllerBase
    {
        // 1. Listeyi herkes (Teknisyen + Şef) görebilir
        [HttpGet]
        [Authorize(Policy = Permissions.Laboratories.View)]
        public IActionResult GetLabs()
        {
            return Ok("Laboratuvar Listesi: Kimya Lab, Fizik Lab...");
        }

        // 2. Yeni Lab'ı sadece Şef oluşturabilir
        [HttpPost]
        [Authorize(Policy = Permissions.Laboratories.Create)]
        public IActionResult CreateLab()
        {
            return Ok("Yeni Laboratuvar oluşturuldu!");
        }

        // 3. Lab'ı sadece Şef silebilir
        [HttpDelete]
        [Authorize(Policy = Permissions.Laboratories.Delete)]
        public IActionResult DeleteLab()
        {
            return Ok("Laboratuvar silindi!");
        }
    }
}