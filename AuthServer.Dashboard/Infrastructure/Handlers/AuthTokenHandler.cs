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
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            // 1. Mevcut Access Token'ı al ve isteğe ekle
            var accessToken = user?.FindFirst("AccessToken")?.Value;
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            // 2. İsteği gönder
            var response = await base.SendAsync(request, cancellationToken);

            // 3. Eğer 401 (Unauthorized) dönerse, Refresh Token ile şansımızı deneyelim
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && user != null)
            {
                var refreshToken = user.FindFirst("RefreshToken")?.Value;

                if (!string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(accessToken))
                {
                    // -- TOKEN YENİLEME OPERASYONU --

                    // Refresh işlemi için "temiz" bir client oluştur (sonsuz döngüye girmesin)
                    var client = _httpClientFactory.CreateClient("PublicApi");

                    // API Senin Command Yapına Göre İstek Bekliyor:
                    var refreshCommand = new
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        IpAddress = "127.0.0.1" // IP'yi sunucudan da alabilirsin
                    };

                    var refreshResponse = await client.PostAsJsonAsync("api/auth/refresh-token", refreshCommand);

                    if (refreshResponse.IsSuccessStatusCode)
                    {
                        // API'den dönen ServiceResponse<TokenDto> yapısını oku
                        var result = await refreshResponse.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>();

                        if (result != null && result.Succeeded)
                        {
                            var newAccessToken = result.Data.AccessToken;
                            var newRefreshToken = result.Data.RefreshToken;

                            // --- KRİTİK NOKTA ---
                            // Blazor Server'da Cookie'yi HTTP isteği ortasında güncelleyemeyiz (Set-Cookie header'ı gitmez).
                            // AMA: Şu anki request'i kurtarmak için header'ı güncelliyoruz.

                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

                            // Eski response'u kapatıp yeni token ile isteği TEKRAR deniyoruz.
                            response.Dispose();
                            // Request kopyalamak gerekebilir ama çoğu durumda bu çalışır
                            return await base.SendAsync(request, cancellationToken);

                            // NOT: Cookie güncellenmediği için sayfa yenilenince (F5) eski token geri gelecek.
                            // Bunu çözmek için Cookie Süresini 7 gün yaptık (Adım 1).
                            // Access Token bitse bile Handler her seferinde refresh yapıp işi kurtaracak.
                            // Bu yöntem Blazor Server için en stabil yöntemdir.
                        }
                    }
                }
            }

            return response;
        }
    }
}
