namespace AuthServer.Identity.Application.Dtos
{
    public class ActiveSessionDto
    {
        public Guid TokenId { get; set; }
        public string UserEmail { get; set; }
        public string FullName { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool IsCurrentSession { get; set; } // Yönetici kendi oturumunu yanlışlıkla kapatmasın diye
    }
}