namespace AuthServer.Dashboard.Models
{
    public class DashboardStatsViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int PassiveUsers { get; set; }
        public int TotalActiveSessions { get; set; }
        public List<AuditLogViewModel> LatestActivities { get; set; } = new();
    }

    public class AuditLogViewModel
    {
        public string UserEmail { get; set; }
        public string Action { get; set; }
        public DateTime Date { get; set; }
        public string IpAddress { get; set; }
    }
}