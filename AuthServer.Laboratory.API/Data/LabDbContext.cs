using AuthServer.Laboratory.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Laboratory.API.Data
{
    public class LabDbContext : DbContext
    {
        public LabDbContext(DbContextOptions<LabDbContext> options) : base(options)
        {
        }

        // Tablolarımız buraya eklenecek
        public DbSet<TestModule> TestModules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Fluent API ayarlarını buraya yazabiliriz (Opsiyonel)
            // Örn: Name alanı zorunlu olsun
            modelBuilder.Entity<TestModule>().Property(x => x.Name).IsRequired().HasMaxLength(200);

            base.OnModelCreating(modelBuilder);
        }
    }
}