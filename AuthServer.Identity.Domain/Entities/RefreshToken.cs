using AuthServer.Identity.Domain.Common;

namespace AuthServer.Identity.Domain.Entities
{
    public class RefreshToken : BaseEntity
    {
        public string Token { get; set; }
        public DateTime Expires { get; set; }
        public bool IsExpired => DateTime.UtcNow >= Expires;

        public string CreatedByIp { get; set; }

        public DateTime? RevokedDate { get; set; }

        // --- DEĞİŞİKLİK BURADA: string? yaptık ---
        public string? RevokedByIp { get; set; }
        public string? ReplacedByToken { get; set; }
        // ----------------------------------------

        public bool IsActive => RevokedDate == null && !IsExpired;

        public Guid UserId { get; set; }
        public AppUser User { get; set; }
    }
}