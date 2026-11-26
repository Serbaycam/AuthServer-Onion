# AuthServer-Onion

.NET 10, ASP.NET Core Identity ve PostgreSQL üzerinde çalışan; kullanıcı, rol, izin ve iptal edilebilir oturum yönetimi sunan kimlik API'si. React/TypeScript yönetim paneli aynı origin üzerinden API'ye bağlanır.

**İki ayrı giriş modeli vardır:** API istemcileri JWT/access + refresh token kullanır; yönetim paneli HttpOnly cookie ve CSRF koruması kullanır. Yönetim uçları güncel `SuperAdmin` üyeliği ister.

Bu repo özel bir kimlik API'sidir. OIDC/OAuth 2.0 discovery, authorization code + PKCE, istemci kaydı, consent, JWKS ve standart SSO protokolleri uygulanmış değildir. MFA enrollment/challenge, kullanıcıya açık kayıt, self-service parola sıfırlama ve e-posta doğrulama uçları da bulunmaz.

## Belgeler

| Belge | İçerik |
|---|---|
| [Ayrıntılı proje rehberi](docs/PROJECT_GUIDE.md) | Mimari, kurulum, tüm ayarlar, veri modeli, oturum akışları, 23 endpoint, panel, geliştirme, test, dağıtım ve sorun giderme |
| [Güncel teknik analiz](docs/PROJECT_ANALYSIS.md) | Kaynak kodla desteklenen bulgular, öncelikler, test kanıtları ve geliştirme önerileri |
| [Oturum güvenliği tasarımı](docs/SESSION_SECURITY_DESIGN.md) | Security stamp, sıralı kullanıcı kilitleri, 30 günlük aile süresi ve güvenli migration kararı |
| [Refactoring ve özellik planı](docs/REFACTORING_PLAN.md) | Öncelikli iş dilimleri, eksik özellikler, kabul kriterleri ve ilk uygulama turu |
| [Panel oturumu](docs/ADMIN_PANEL.md) | Cookie/CSRF tasarımının ve HTTPS dağıtımının önceki incelemesi |
| [Refresh ve oturum kapatma](docs/REFRESH_SESSION_ANALYSIS.md) | Rotation zinciri ve eski oturum kaydından kapatma düzeltmesinin geçmişi |
| [İlk refactoring](docs/REFACTORING.md) | İlk güvenlik iyileştirmelerinin tarihsel kaydı; bazı panel açıklamaları eski sürüme aittir |

Güncel rehber ve analiz, **26 Kasım 2026** tarihinde `9f08e66` kaynak sürümü incelenerek hazırlanmış ve bu çalışmadaki A ve B refactoring dilimlerine göre güncellenmiştir. Eski belgelerdeki test sonuçları ve ortam kısıtları kendi inceleme tarihlerine aittir.

## Mimari

| Proje | Sorumluluk |
|---|---|
| `AuthServer.Identity.Domain` | Identity tabanlı kullanıcı/rol, refresh oturumu, audit modeli; rol ve izin sabitleri |
| `AuthServer.Identity.Application` | MediatR command/query handler'ları, DTO'lar, servis arayüzleri ve iş kuralları |
| `AuthServer.Identity.Persistence` | EF Core/PostgreSQL, Identity store, migration, seed ve yönetim transaction davranışı |
| `AuthServer.Identity.Infrastructure` | JWT üretimi, audit, istek sahibi bilgisi ve permission handler |
| `AuthServer.Identity.API` | HTTP sözleşmeleri, JWT/cookie doğrulama, CSRF, rate limit ve middleware |
| `AuthServer.Identity.Tests` | Gerçek PostgreSQL üzerinde 68 entegrasyon test vakası |
| `AuthServer-AdminPanel` | React 19, TypeScript 6 ve Vite 8 ile yönetim arayüzü |

Domain Identity paketine, Application da Identity/EF Core tiplerine bağlıdır. Katmanlar mevcut modele uyarlanmıştır; framework'ten bütünüyle bağımsız bir Domain değildir.

## Docker Compose ile ilk kurulum

Gerekenler: çalışan Docker Engine ve Docker Compose v2. Bu yol için host'ta .NET veya Node.js gerekmez.

PowerShell ile repo kökünde:

```powershell
Copy-Item .env.example .env
```

`.env` içinde aşağıdaki alanları doldurun:

```dotenv
POSTGRES_PASSWORD=<veritabanina-ozel-rastgele-parola>
JWT_SECRET=<en-az-32-bayt-bagimsiz-rastgele-sir>
DATABASE_INITIALIZE=true
BOOTSTRAP_ADMIN_EMAIL=<yonetici-e-postasi>
BOOTSTRAP_ADMIN_PASSWORD=<en-az-12-karakterli-guclu-parola>
ADMIN_SESSION_REQUIRE_HTTPS=false
PANEL_PUBLIC_SCHEME=http
TRUSTED_PROXY_NETWORK=
```

Yer tutucuları gerçek değerlerle değiştirin; JWT sırrını veritabanı parolasından bağımsız üretin. PowerShell 7 ile bir JWT sırrı üretmek için:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

`.env` Git tarafından dışlanır. Dosyayı paylaşmayın. Yukarıdaki HTTP ayarları yalnızca yerel kullanım içindir.

```powershell
docker compose up -d --build
docker compose ps
docker compose logs --tail 100 authserver-api
```

Panel: [http://localhost:3000](http://localhost:3000). PostgreSQL ve API host portu yayınlanmaz; yalnız panel `127.0.0.1:3000` adresine bağlanır. İlk başlangıçta migration'lar uygulanır, roller ve yeni bootstrap yöneticisi oluşturulur.

Giriş başarılı olduktan sonra `.env` içinde `DATABASE_INITIALIZE=false` yapın; `BOOTSTRAP_ADMIN_EMAIL` ve `BOOTSTRAP_ADMIN_PASSWORD` değerlerini boşaltın. Ardından:

```powershell
docker compose up -d --force-recreate authserver-api
```

Bootstrap, var olan SuperAdmin hesabının şifresini/rolünü değiştirmez. E-posta yönetici olmayan mevcut hesapla eşleşirse başlangıcı açık hatayla durdurur; sessizce yükseltmez. Eksik roller ve yeni yönetici aynı transaction içinde oluşturulur; hata varsa tamamı geri alınır. Seed yalnız initialize açıkken çalışır; yeni laboratuvar rollerine otomatik izin atanmaz.

`my-postgres-env_pgdata` mevcut veritabanı volume adıdır. Mevcut volume üzerinde `.env` parolasını değiştirmek PostgreSQL kullanıcısının parolasını değiştirmez. Normal güncellemede `docker compose down -v` kullanmayın; veri volume'ları silinir.

## Yerel geliştirme

Gerekenler: .NET 10 SDK, PostgreSQL 16 ve Node.js. CI ve panel Dockerfile'ı Node 22 kullanır; lockfile'daki test bağımlılıkları için 22 serisinde **22.22.2 veya üzerini** kullanın. `npm ci` lockfile'daki sürümleri kurar.

Yerel PostgreSQL üzerinde ayrı bir geliştirme veritabanı ve kullanıcı hazırlayın. Compose veritabanı varsayılan olarak host'a açık olmadığından aşağıdaki `localhost:5432` bağlantısı için ayrıca yerel PostgreSQL gerekir. Repo kökünde, PowerShell terminalinde:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=authserver_dev;Username=authserver_dev;Password=<gelistirme-parolasi>'
$env:JwtSettings__Secret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$env:AdminSession__RequireHttps = 'false'
$env:Database__Initialize = 'true'
$env:Bootstrap__AdminEmail = '<yonetici-e-postasi>'
$env:Bootstrap__AdminPassword = '<en-az-12-karakterli-parola>'
dotnet run --project AuthServer.Identity.API --no-launch-profile --urls http://localhost:8080
```

İlk kurulumdan sonra API'yi durdurun, initialize değerini `false` yapın ve bootstrap değişkenlerini kaldırarak yeniden başlatın. JWT sırrını oturumların korunması gereken her yeniden başlatmada sabit tutun. `.env` dosyası `dotnet run` tarafından otomatik yüklenmez.

İkinci terminalde:

```powershell
npm ci --prefix AuthServer-AdminPanel
npm run dev --prefix AuthServer-AdminPanel
```

Panel için Vite'ın yazdığı adresi kullanın (varsayılan `http://localhost:5173`). `/api` istekleri `vite.config.ts` ile `http://localhost:8080` adresine proxy edilir. Mevcut launch profili yalnız `https://localhost:7023` kullanır; yukarıdaki komut bu port farkını açıkça giderir. Development ortamında OpenAPI JSON `/openapi/v1.json` adresindedir; Swagger UI eklenmiş değildir.

## Oturum ve yetkilendirme sözleşmesi

- JWT varsayılan olarak 15 dakika, refresh 7 gün geçerlidir. JWT girişinin mutlak üst süresi **30 gündür** (`JwtSettings__AbsoluteSessionLifetimeDays`, 1–90); rotation bu süreyi uzatmaz, access/refresh bitişleri aile sınırını aşamaz. İmza algoritması HS256; issuer ve audience doğrulanır, saat toleransı sıfırdır.
- Access token `sid` ve `security_stamp` taşır. Korunan isteklerde oturum, hesap durumu, MFA bayrağı ve güncel roller veritabanından doğrulanır. Refresh kaydı da üretildiği security stamp'i saklar; dış Identity stamp değişimi eski refresh'i geçersiz kılar.
- Refresh sırrı 32 rastgele bayttan üretilir; veritabanında yalnız SHA-256 özeti saklanır. Başarılı refresh yeni `sid` üretir ve eski access token'ı AuthServer üzerinde hemen geçersiz kılar. İstemci token çiftini birlikte değiştirmelidir.
- İptal edilmiş refresh sırrını **yenilemek** için tekrar kullanmak kullanıcının tüm iptal edilmemiş oturumlarını kapatır. Yenilemeleri seri çalıştırın; sonucu belirsiz isteği otomatik tekrarlamayın.
- `revoke-token` veya yönetici tarafından oturum kapatma, eski kayıt verilse bile replacement zincirindeki güncel ardılı da kapatır. Diğer bağımsız login'ler korunur.
- Panel `AuthServer.AdminSession` HttpOnly, SameSite=Strict cookie kullanır. Varsayılan mutlak süre 12 saattir; sliding renewal yoktur. Sayfa açılışında `/api/admin-session` ile oturum geri yüklenir.
- Cookie kullanan değiştirme isteklerinde `X-CSRF-Token` gerekir. Panel tarayıcıya JWT/refresh sırrı vermez; CSRF token'ını bellekte tutar.
- Yeni parolalar en az 12 karakterdir. Beş hatalı parola denemesi 15 dakika kilitler. JWT kimlik uçları ve panel login, doğrudan peer IP'sine göre dakikada 30 istek sınırını paylaşır.
- Yönetim erişimi `SuperAdmin` ister. Laboratuvar permission atamak panel erişimi vermez. Son aktif yönetici pasifleştirilemez veya `SuperAdmin` rolünden çıkarılamaz; `SuperAdmin` ve `Basic` silinemez/yeniden adlandırılamaz.
- `IManagementCommand` ile işaretli 11 yönetim komutu, `revoke-all` dahil, Serializable transaction ve audit ile tamamlanır. Her başarılı/no-op yönetim işlemi tek, aktör/hedef/iz kimliği/değişim bilgili audit olayı yazar; sır alanları dışlanır ve audit hatasında işlem geri alınır. Kullanıcı satır kilitleri session üretimi/yenileme/iptali sıralar. JWT login de oturum ve audit kaydını paylaşılan DbContext ve kullanıcı kilidi transaction'ında yazar; hatalı parola/lockout sayımı bu birimin dışında korunur. Başlangıç hataları ve düzeltme kanıtları [analizde](docs/PROJECT_ANALYSIS.md).

Yalnız JWT imzasını doğrulayan başka bir servis, AuthServer'daki anlık iptali kendiliğinden öğrenmez. Harici tüketiciler için oturum doğrulama/revocation sözleşmesi ayrıca tasarlanmalıdır.

## Test ve kalite kontrolleri

```powershell
dotnet build AuthServer.sln --configuration Release
npm ci --prefix AuthServer-AdminPanel
npm run build --prefix AuthServer-AdminPanel
npm run lint --prefix AuthServer-AdminPanel
npm test --prefix AuthServer-AdminPanel
npm audit --prefix AuthServer-AdminPanel --audit-level=low
```

**Backend testleri her test öncesinde hedef veritabanını siler ve migration'larla yeniden oluşturur.** Yalnızca atılabilir, adı `_tests` ile biten ayrı PostgreSQL veritabanı verin; mevcut geliştirme veya production bağlantısını kullanmayın.

```powershell
$env:AUTH_TEST_DATABASE = 'Host=localhost;Port=5432;Database=authserver_tests;Username=test;Password=<yalniz-test-parolasi>'
dotnet test AuthServer.Identity.Tests/AuthServer.Identity.Tests.csproj --configuration Release
Remove-Item Env:AUTH_TEST_DATABASE
```

[GitHub Actions workflow'u](.github/workflows/verify.yml) PostgreSQL 16 ile backend testlerini; panel audit/build/lint/test ve Compose/Nginx yapılandırma kontrollerini tanımlar. Bu, son uzak CI çalışmasının burada doğrulandığı anlamına gelmez. Bu incelemede fiilen çalıştırılan kontroller ve sonuçları [teknik analizde](docs/PROJECT_ANALYSIS.md#verification) kayıtlıdır.

**26 Kasım 2026, B turu yerel sonucu:** PostgreSQL 16 üzerinde **68/68 backend**, panelde **16/16 test**, toplam **84/84**; build/lint başarılı, npm audit **0 bildirim**, NuGet taramasında bilinen açık yok. Seed/session/audit hataları önce başarısız testlerle yeniden üretildi; tam koşuda bulunan iç exception zinciri hatası da düzeltilip bütün suite tekrar geçirildi. Docker/Nginx runtime ve gerçek tarayıcı E2E ayrıca doğrulanmalıdır; kanıtlar [analizde](docs/PROJECT_ANALYSIS.md#verification).

## Dağıtım ve mevcut sürümden geçiş

1. PostgreSQL ve Data Protection anahtarlarını yedekleyin; geri yüklemeyi ayrı ortamda deneyin. Eski/yeni API sürümlerini migration sırasında birlikte çalıştırmayın.
2. Eski sürümlerde kaynakta bulunmuş JWT, yönetici ve veritabanı sırlarını kontrollü biçimde değiştirin. Bu işlem Git geçmişini temizlemez; mevcut hesap şifresini bootstrap ile değiştiremezsiniz.
3. `20260910090000_HardenRefreshTokens` ve yeni **`20261126112131_SessionSecurityPolicy`** migration'ları eski refresh sırlarını geri alınamaz biçimde silip mevcut oturumları iptal eder. Yeni policy geçişinde bütün JWT/panel oturumları yeniden giriş yapmalıdır. Kullanıcı/rol/audit verisi korunur; geri migration da eski sırları veya açık oturumları geri getirmez.
4. Production migration'ını ayrı kontrollü adımla çalıştırın; tek instance bootstrap gerekiyorsa initialize seçeneğini geçici açıp kapatın. API ve paneli uyumlu sürümlerle dağıtın.
5. Dış HTTPS proxy kurun; `ADMIN_SESSION_REQUIRE_HTTPS=true`, `PANEL_PUBLIC_SCHEME=https` ve yalnız gerekli Docker subnet'ini içeren `TRUSTED_PROXY_NETWORK` ayarlayın. [Adım adım dağıtım](docs/PROJECT_GUIDE.md#deployment).
6. `auth_keys` volume'unu koruyun. Birden çok API instance aynı Data Protection deposunu/application name'i kullanmalıdır; rate limiter ise halen instance belleğindedir.

`X-Forwarded-For` işlenmez; audit ve rate limit proxy'nin peer IP'sini görebilir. API/panel readiness endpoint'i, merkezi izleme, veri saklama/temizleme politikası ve dağıtık rate limit henüz sağlanmaz. Ayrıntılı işletim ve sorun giderme adımları [proje rehberindedir](docs/PROJECT_GUIDE.md).

## Lisans

[MIT](LICENSE).
