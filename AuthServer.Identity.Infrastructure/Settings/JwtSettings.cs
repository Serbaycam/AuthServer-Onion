namespace AuthServer.Identity.Infrastructure.Settings
{
  public class JwtSettings
  {
    public string Secret { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public double AccessTokenExpirationMinutes { get; set; }
    public double RefreshTokenExpirationDays { get; set; }
  }
}