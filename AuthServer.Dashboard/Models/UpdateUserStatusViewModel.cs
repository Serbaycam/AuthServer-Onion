namespace AuthServer.Dashboard.Models
{
    public class UpdateUserStatusViewModel
    {
        public Guid UserId { get; set; }
        public bool IsActive { get; set; }
    }
}