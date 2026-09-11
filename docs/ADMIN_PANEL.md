# AdminPanel incelemesi ve oturum düzeltmesi

İncelenen temel: `c1147925e5672a159e29de6dd2dba5db73d4190c` (master). Bu çalışma önceki API refactoring'inin üzerine uygulanır.

## Neden yenilemede login açılıyordu?

Panel token'ları yalnızca modül belleğinde tutuyordu. Tam sayfa yenilemesi bu belleği sıfırlıyordu; router da kimlik bilgisini bulamayınca login'e yönlendiriyordu. Bu, önceki değişikliğin kullanılabilirlik gerilemesiydi. Artık panel için ayrı, sunucuda iptal edilebilen bir cookie oturumu var. JWT login/refresh uçları diğer API istemcileri için korunur.

| Bulgu | Uygulanan düzeltme |
|---|---|
| Yenilemede oturum kayboluyordu. | HttpOnly cookie; açılışta sunucu oturum kontrolü; kontrol bitmeden yönlendirmeme. |
| Ağ hatası ile geçersiz oturum ayrılmıyordu. | Açılışta tekrar deneme, mevcut oturumda geçici hatada kimliği koruma; gerçek 401'de çıkış. |
| Tarayıcıda kalıcı kimlik bilgisi/CSRF tasarımı yoktu. | SameSite=Strict, Secure varsayılanı, identity-bound CSRF header, JS'e JWT vermeyen session endpoint. |
| Çıkış tamamlanmadan istemci oturumu silinebiliyordu. | Sunucu onayı sonrası çıkış; başarısız istekte görünür hata ve tekrar deneme. |
| Eski sekme/istek cevapları yeni durumu etkileyebiliyordu. | Oturum işlem sürümü, ortak bootstrap isteği, AbortController, kullanıcı değişince ekranı yeniden oluşturma, sekmeler arası yalnızca değişiklik bildirimi. |
| Kullanıcı düzenleme arayüzü eksikti; prompt/alert akışları kısıtlıydı. | Oluşturma, bilgi, şifre, rol, durum ve oturum kapatma için doğrulanan formlar ve onay pencereleri. |
| API hataları console'a yazılıp kullanıcıya gösterilmiyordu. | Ortak hata ayrıştırma; validation/iş kuralı, 401/403/429 ve bağlantı mesajları. |
| Rol değiştirirken önceki izin yanıtı yeni seçimi ezebiliyordu. | Rol id'sine bağlı bileşen ve iptal edilen eski sorgu; izin yüklenmeden kaydetmeme. |
| Formlarda çift gönderim, yanlış şifre tekrarı ve sabit rol seçenekleri vardı. | İşlem sırasında kilitleme, 12 karakter/şifre eşleşmesi, güncel rol kataloğu. |
| Büyük listeler ve dar ekran kullanımı zordu. | Arama, durum filtresi, 20 satırlı istemci sayfalaması, taşan tablo kapsayıcısı ve responsive düzen. |
| Dashboard hatada sıfırmış gibi görünüyordu. | Yükleniyor/hata/boş durumlarının ayrılması ve son işlemler tablosu. |
| Oturum yenilemeleri çakışabiliyordu. | Biten sorgu sonrası 30 saniyelik polling, unmount'ta timer/istek iptali, mevcut oturum etiketi. |
| API yeniden oluşturulunca cookie anahtarları kaybolabilirdi. | Compose `auth_keys` volume ve Data Protection application name. |
| HTTPS dış proxy sonlandırmasında sunucu şeması yanlıştı. | Açıkça güvenilen proxy/network ile yalnızca X-Forwarded-Proto işleme; Nginx başlığı yapılandırmadan üretir. |

## Oturum sözleşmesi

- `GET /api/admin-session`: 200 envelope içinde `user` (anonimde null), `expiresAt`, `csrfToken`. Yanıt cache edilmez.
- `POST /api/admin-session/login`: email/password ve `X-CSRF-Token`. Yalnızca aktif, kilitlenmemiş, MFA gerektirmeyen SuperAdmin giriş alabilir. Başarısız parolalar lockout sayacına eklenir ve giriş rate limit'i uygulanır.
- `POST /api/admin-session/logout`: CSRF doğrulaması sonrası oturumu veritabanında iptal eder, cookie'yi siler.
- Cookie ile yapılan bütün POST/PUT/PATCH/DELETE API eylemleri CSRF gerektirir. Kimlik cookie'si ve CSRF cookie'si HttpOnly, SameSite=Strict ve `/api` path'indedir. CSRF request token'ı tek başına oturum kimlik bilgisi değildir.
- Tarayıcı session kimliği mevcut RefreshTokens tablosundaki tek kayda bağlıdır. İstemciye refresh sırrı verilmez. Şifre/security stamp, aktiflik, kilit, MFA, bitiş ve revocation her istekte denetlenir. Roller güncel veritabanından alınır.
- Varsayılan mutlak süre 12 saattir; `AdminSession__LifetimeHours` ile 1–168 saat ayarlanabilir. Sliding renewal yoktur; süre bitince yeniden giriş gerekir. Sayfa veya tarayıcı yeniden açıldığında süre dolmadıysa oturum geri yüklenir.
- API istemcisi yalnızca açık `csrf_invalid` reddini bir kez yenileyebilir; kimlik değiştiyse işlemi yeniden göndermez. Ağ/500 hatalarında değiştiren istekler otomatik tekrarlanmaz.
- Logout, kullanıcı pasifleştirme, şifre değiştirme ve force logout hem browser hem JWT oturumlarına uygulanır. İptal edilmiş bir oturumu yeniden iptal etmek başarıyla sonuçlanır.

## HTTPS dağıtımı

Panel ve API bu değişiklikle birlikte dağıtılmalıdır. Yeni migration gerekmez. Eski panel JWT oturumları cookie'ye çevrilmez; güncellemeden sonra bir kez giriş yapılır.

Yerel Compose varsayılanı `http://localhost:3000` içindir. `.env` içindeki `ADMIN_SESSION_REQUIRE_HTTPS=false` ve `PANEL_PUBLIC_SCHEME=http` yalnızca bu yerel HTTP kullanımını sağlar.

HTTPS reverse proxy arkasında:

1. Dış proxy'de HTTPS sertifikasını ve HTTP'den HTTPS'ye yönlendirmeyi kurun. Panelin loopback portunu sadece bu proxy üzerinden sunun.
2. `ADMIN_SESSION_REQUIRE_HTTPS=true` ve `PANEL_PUBLIC_SCHEME=https` yapın. Nginx template'i API'ye bu sabit şemayı gönderir; istemciden gelen X-Forwarded-Proto'yu kopyalamaz.
3. `TRUSTED_PROXY_NETWORK` değerini yalnızca bu kurulumun Docker ağına ait CIDR yapın; `docker network inspect <compose-projesi>_default` ile subnet'i bulun. Başka/güvenilmeyen uygulamalarla paylaşılan geniş bir ağı güvenilen proxy olarak tanımlamayın. Compose dışında tek proxy IP'si için `ReverseProxy__KnownProxies__0`, CIDR için `ReverseProxy__KnownNetworks__0` kullanılabilir. Varsayılan loopback güveni korunur; tüm IP'lere güvenen ayar yoktur.
4. `docker compose up -d --build` ile API ve paneli yeniden oluşturun. HTTPS adresinde giriş, sayfa yenileme ve çıkış kontrolü yapın. Cookie'ler Secure ve HttpOnly olmalıdır. Proxy güveni eksikse Secure antiforgery HTTP isteğini kabul etmez; güvenlik seçeneğini kapatmak yerine proxy şemasını düzeltin.
5. `auth_keys` volume'unu koruyun. Birden çok API instance aynı Data Protection anahtar deposunu ve application name'i kullanmalıdır. Anahtarları erişim izinleri, şifreli disk/key vault ve yedek politikası ile korumak dağıtım sorumluluğudur. Volume'u silmek açık cookie'leri geçersiz kılar.

Bu değişiklik X-Forwarded-For'u güvenilir yapmaz: audit ve rate limit doğrudan peer IP'sini kullanır. Ortak proxy arkasındaki istemciler hâlâ aynı rate limit havuzunu paylaşabilir.

ASP.NET Core referansları: [cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0), [trusted proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

## Doğrulama ve sınırlar

`npm test` gerçek TypeScript API modülünü ve gerçek React ekranlarını derleyerek çalıştırır. Oturum geri yükleme, tek bootstrap, bağlantı hatası, CSRF retry, kimlik değişimi, logout hatası, form hata koruması, kullanıcı düzenleme sözleşmesi, arama/sayfalama, gecikmiş rol yanıtları ve tek oturum kapatma kapsanır. DOM testleri jsdom kullanır; native dialog focus/top-layer ve CSS görünümü gerçek tarayıcı testi yerine geçmez.

Backend entegrasyon testleri gerçek PostgreSQL ve migration'larla çalışır. Cookie ile yeni sayfa oturumu, HttpOnly/Secure/SameSite, CSRF, logout/revocation, güncel rol kontrolü, yönetici olmayan kullanıcı girişi ve güvenilen proxy şeması; önceki JWT güvenlik senaryolarıyla birlikte doğrulanır. Yerelde .NET SDK bulunmadığı için sonuç GitHub Actions üzerinden doğrulanır.

Bu çalışma sırasında tarayıcı ortamı loopback önizleme adresini `ERR_BLOCKED_BY_CLIENT` ile engelledi; görsel ve gerçek tarayıcı uçtan uca testi tamamlanmış sayılmaz. `tests/preview-server.mjs` manuel arayüz kontrolü için atılabilir fixture sağlar.

Liste sayfalaması istemci tarafındadır; API hâlâ tüm kayıtları getirir ve kullanıcı rol sorgularında N+1 devam eder. Çok büyük veri için sunucu sayfalaması/sorgu projeksiyonu ayrıca gerekir. OIDC/SSO, MFA enrollment/recovery, self-service parola sıfırlama ve e-posta doğrulama bu panel düzeltmesiyle eklenmiş değildir. Bu değişiklik bulunabilecek bütün hataların veya production dağıtım koşullarının tamamlandığı iddiası taşımaz.
