using AuthServer.Dashboard.Models;
using System.Net.Http.Headers;
using System.Security.Claims;
namespace AuthServer.Dashboard.Infrastructure.Handlers
{
    public class AuthTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthTokenHandler(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 1. İLK DENEME: Mevcut Token ile git
            var user = _httpContextAccessor.HttpContext?.User;
            var accessToken = user?.FindFirst("AccessToken")?.Value;

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var response = await base.SendAsync(request, cancellationToken);

            // 2. KONTROL: Eğer 401 (Yetkisiz) hatası aldıysak ve elimizde Refresh Token varsa
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && user != null)
            {
                var refreshToken = user.FindFirst("RefreshToken")?.Value;

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    // 3. TOKEN YENİLEME OPERASYONU
                    // Yeni bir client oluştur (Kendi kendini çağırmasın diye factory kullanıyoruz)
                    var client = _httpClientFactory.CreateClient("PublicApi");

                    // Refresh Token Endpoint'ine istek at
                    var refreshResponse = await client.PostAsJsonAsync("https://localhost:7023/api/auth/refresh-token", new
                    {
                        AccessToken = accessToken, // Bazı yapılar expired access token'ı da ister
                        RefreshToken = refreshToken,
                        IpAddress = "127.0.0.1"
                    });

                    if (refreshResponse.IsSuccessStatusCode)
                    {
                        // Yeni tokenları aldık!
                        var result = await refreshResponse.Content.ReadFromJsonAsync<ServiceResponse<TokenResponseModel>>();

                        if (result != null && result.Succeeded)
                        {
                            var newAccessToken = result.Data.AccessToken;

                            // ÖNEMLİ: Cookie'yi güncellemek Blazor Server'da zordur.
                            // O yüzden şimdilik sadece bu isteği kurtarmak için header'ı güncelliyoruz.
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

                            // 4. İKİNCİ DENEME: Yeni token ile tekrar dene
                            // Eski request nesnesi kullanıldığı için klonlamak gerekebilir ama genelde bu çalışır
                            return await base.SendAsync(request, cancellationToken);
                        }
                    }
                }
            }

            return response;
        }
    }
}
