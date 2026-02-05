namespace AuthServer.Laboratory.API.Entities
{
    public class TestModule
    {
        public Guid Id { get; set; }
        public string Name { get; set; } // Örn: Yıkama Haslığı
        public string Code { get; set; } // Örn: TS-EN-ISO-105
        public string Description { get; set; } // Açıklama

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}