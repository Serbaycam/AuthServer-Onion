namespace AuthServer.Identity.WebPanel.Models
{
    public class DashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int PassiveUsers { get; set; }
        public int TotalActiveSessions { get; set; }
        // Son 5 aktiviteyi gösterelim
        public List<AuditLogDto> LatestActivities { get; set; }
    }

    public class AuditLogDto
    {
        public string UserEmail { get; set; }
        public string Action { get; set; } // Login, KillSession, UpdateRole vb.
        public DateTime Date { get; set; }
        public string IpAddress { get; set; }
    }
}
