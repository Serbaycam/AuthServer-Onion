using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AuthServer.Identity.Domain.Entities;

namespace AuthServer.Identity.Persistence.Configurations
{
  public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
  {
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
      builder.HasKey(x => x.Id);

      builder.Property(x => x.Token).IsRequired().HasMaxLength(200);

      // --- DEĞİŞİKLİK BURADA: IsRequired(false) diyoruz ---
      builder.Property(x => x.ReplacedByToken).IsRequired(false);
      builder.Property(x => x.RevokedByIp).IsRequired(false);
      // ----------------------------------------------------

      builder.HasOne(x => x.User)
             .WithMany(x => x.RefreshTokens)
             .HasForeignKey(x => x.UserId);

      builder.ToTable("RefreshTokens");
    }
  }
}