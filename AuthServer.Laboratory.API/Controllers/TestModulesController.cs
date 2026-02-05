using AuthServer.Laboratory.API.Data;
using AuthServer.Laboratory.API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Laboratory.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestModulesController : ControllerBase
    {
        private readonly LabDbContext _context;

        public TestModulesController(LabDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var modules = await _context.TestModules.ToListAsync();
            return Ok(modules);
        }

        [HttpPost]
        public async Task<IActionResult> Create(TestModule module)
        {
            // ID'yi biz oluşturalım ki veritabanı şaşırmasın
            module.Id = Guid.NewGuid();
            module.CreatedDate = DateTime.UtcNow;

            _context.TestModules.Add(module);
            await _context.SaveChangesAsync();
            return Ok(module);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var module = await _context.TestModules.FindAsync(id);
            if (module == null)
            {
                return NotFound();
            }

            _context.TestModules.Remove(module);
            await _context.SaveChangesAsync();

            return NoContent(); // 204: Başarılı ama geriye veri dönmüyorum demek
        }
    }
}