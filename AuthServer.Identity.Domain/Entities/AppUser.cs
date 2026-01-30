using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Domain.Entities
{
    public class AppUser : IdentityUser<Guid>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation Property: Bir kullanıcının birden fazla refresh token'ı olabilir
        // (Telefondan girdi, Webden girdi vs.)
        public ICollection<RefreshToken> RefreshTokens { get; set; }
    }
}