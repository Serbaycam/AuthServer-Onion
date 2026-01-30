using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Domain.Entities
{
    public class AppRole : IdentityRole<Guid>
    {
        // Şimdilik boş, standart IdentityRole özellikleri yeterli.
        // İstersen public string Description { get; set; } ekleyebilirsin.
    }
}