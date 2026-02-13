namespace AuthServer.Dashboard.Models
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; } // Kullanıcı aktif mi?
        public DateTime CreatedDate { get; set; }
    }
}