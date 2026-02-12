namespace AuthServer.Dashboard.Models
{
    public class TokenResponseModel
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        // Eğer API'den 'Expiration' gibi başka alanlar da dönüyorsa buraya ekleyebilirsin
    }
}