# AuthServer-Onion

.NET 10, ASP.NET Core Identity, PostgreSQL ve React ile kullanıcı, rol, izin ve oturum yönetimi. API yönetim uçları güncel `SuperAdmin` üyeliği gerektirir.

Bu proje özel bir JWT kimlik API'sidir. OpenID Connect/OAuth 2.0 sağlayıcısı değildir; discovery, authorization code/PKCE, istemci kaydı, consent ve SSO protokolleri uygulanmış değildir. Birden çok uygulamada standart SSO gerekiyorsa bu protokol katmanı ayrıca tasarlanmalıdır. Mevcut JWT doğrulamasını tek başına SSO olarak değerlendirmeyin.

## Katmanlar

- **Domain:** kullanıcı/rol/oturum/audit modelleri ve mevcut laboratuvar izin kataloğu.
- **Application:** MediatR komutları, sorgular, servis arayüzleri ve iş kuralları.
- **Persistence:** Identity/EF Core, PostgreSQL migration'ları ve yönetim komutları için transaction davranışı.
- **Infrastructure:** JWT üretimi, audit, istek kimliği ve güncel izin kontrolü.
- **API:** JWT doğrulama, oturum doğrulama, yetkilendirme, istek doğrulama ve hız sınırı.
- **AdminPanel:** kullanıcı/rol/oturum yönetimi. `/api` aynı origin üzerinden Nginx ile API'ye yönlendirilir.

Domain halen Identity modellerini, Application halen Identity/EF Core tiplerini kullanır. Bu, mevcut projeye uyumlu pragmatik katmanlaşmadır; framework'ten tamamen bağımsız saf Onion mimarisi olduğu iddia edilmez.

## İlk kurulum

Gereksinimler: Docker Compose veya .NET 10 SDK, Node.js 22 ve PostgreSQL 16.

```bash
cp .env.example .env
```

`.env` içinde birbirinden bağımsız, rastgele `POSTGRES_PASSWORD` ve en az 32 baytlık `JWT_SECRET` ayarlayın. Örnek bir anahtar üretmek için `openssl rand -base64 48` kullanılabilir. `BOOTSTRAP_ADMIN_EMAIL` ve en az 12 karakterli `BOOTSTRAP_ADMIN_PASSWORD` belirleyin; ilk kontrollü kurulumda `DATABASE_INITIALIZE=true` yapın.

```bash
docker compose up -d --build
```

Panel yerel makinede `http://localhost:3000` adresindedir. API ve PostgreSQL host portları dışarı açılmaz. İnternet/intranet üzerinden kullanımda panelin önüne HTTPS reverse proxy koyun ve [HTTPS yapılandırmasını](docs/ADMIN_PANEL.md#https-dağıtımı) uygulayın. Bu Compose yapılandırması yerel başlangıç içindir; TLS sonlandırmasını kendisi sağlamaz.

Başarılı kurulumdan sonra `DATABASE_INITIALIZE=false` yapın, bootstrap e-posta/şifre değerlerini kaldırın ve API konteynerini yeniden oluşturun. Başlangıç hataları artık gizlenmez. Bootstrap mevcut e-posta hesabını otomatik yönetici yapmaz veya şifresini değiştirmez. Yeni kurulumlarda laboratuvar rollerine otomatik izin verilmez; yönetim panelinden açıkça atanır.

Var olan `my-postgres-env_pgdata` volume adı korunur. Mevcut PostgreSQL volume'unda `.env` parolasını değiştirmek veritabanı rolünün parolasını değiştirmez; iki tarafı kontrollü biçimde eşleştirin. Veriyi korumak için normal güncellemede `docker compose down -v` kullanmayın.

## Mevcut sürümden geçiş

1. PostgreSQL yedeği alın ve API trafiğini durdurun. Eski ve yeni API sürümlerini aynı anda çalıştırmayın.
2. Kaynak kodda daha önce bulunan JWT anahtarını, bootstrap yönetici şifresini ve veritabanı parolasını değiştirin. Bu değişiklik git geçmişini temizlemez ve mevcut hesabın şifresini kendiliğinden değiştirmez.
3. Yapılandırmayı ortam değişkenlerine taşıyın. Production migration'ını kontrollü dağıtım adımı olarak çalıştırın veya tek instance için geçici `DATABASE_INITIALIZE=true` kullanın.
4. `20260910090000_HardenRefreshTokens` eski refresh token sırlarını geri alınamaz biçimde siler ve mevcut oturumları iptal eder. Kullanıcılar tekrar giriş yapar. Kullanıcı, rol ve audit kayıtları korunur.
5. API ve paneli birlikte dağıtın. Yeni access token `sid` ve `security_stamp` taşır. Başka API tüketicilerinin yeni oturum davranışını desteklediğini doğrulayın.

Migration geri alınsa bile eski token sırları geri gelmez. Önceki sürüme dönmek gerekiyorsa yeni bir giriş zorunludur; eski oturumları yeniden açmayın.

## Oturum ve güvenlik davranışı

- Refresh token 32 rastgele bayttır; sunucuda yalnızca SHA-256 özeti tutulur. Token ham değeri yalnızca istemciye verilir.
- Access token belirli refresh kaydının `sid` değerine bağlıdır. İlgili oturum iptal edildiğinde başka açık oturum onu geçerli kılamaz.
- Her refresh yeni bir oturum kaydı üretir ve önceki access token'ı da geçersizleştirir. İstemci yeni token çiftini birlikte değiştirmelidir.
- Daha önce iptal edilmiş refresh token tekrar kullanılırsa kullanıcının açık oturumları iptal edilir. Ağ hatasından sonra refresh isteğini körlemesine tekrar göndermeyin; yeniden giriş gerekebilir.
- Eşzamanlı refresh işlemlerinde optimistic concurrency yalnızca bir yenilemenin kaydedilmesine izin verir.
- Pasif/kilitli hesap, değişmiş security stamp, süresi bitmiş veya iptal edilmiş oturum reddedilir. Rol üyeliği ve izinler güncel veritabanından kontrol edilir.
- Beş başarısız parola denemesi 15 dakika kilitler. Yeni şifreler en az 12 karakterdir. Kimlik uçları ağdaki doğrudan istemci adresi başına dakikada 30 istekle sınırlıdır.
- Proxy üzerinden gelen istekler varsayılan olarak aynı IP sınırını paylaşır. `X-Forwarded-For` istemci başlığına güvenilmez. Çok instance için ortak rate limiter ve açıkça güvenilen proxy yapılandırması ayrıca gerekir.
- Son aktif `SuperAdmin` pasifleştirilemez veya rolü kaldırılamaz. `SuperAdmin`/`Basic` rol adları korunur.
- Kullanıcı oluşturma, rol atama, izin değiştirme ve audit yazımı aynı yönetim transaction'ında tamamlanır. Transaction çakışması 409 döner.
- Panel varsayılan 12 saat geçerli, HttpOnly ve SameSite=Strict cookie kullanır. Sayfa yenilendiğinde sunucudaki oturum geri yüklenir. Değiştiren isteklerde CSRF doğrulanır; logout oturumu veritabanında da iptal eder. API/JWT tüketicilerinin token sözleşmesi korunur. Ayrıntılar: [panel oturum ve dağıtım belgesi](docs/ADMIN_PANEL.md).

## Geliştirme ve doğrulama

API için `ConnectionStrings__DefaultConnection` ve `JwtSettings__Secret` ortam değişkenlerini ayarlayın. Yalnızca yerel HTTP geliştirmede `AdminSession__RequireHttps=false` kullanın. Panel geliştirme proxy'si API'yi `http://localhost:8080` adresinde bekler; API'yi bu HTTP portunda çalıştırabilir veya `vite.config.ts` içindeki hedefi değiştirebilirsiniz. Panel `/api` üzerinden aynı origin gerektirir; farklı bir sunucuya doğrudan cookie isteği göndermez. Ayrı origin kullanan JWT istemcileri için `Cors:AllowedOrigins` açıkça tanımlanmalıdır.

```bash
dotnet build AuthServer.sln
npm ci --prefix AuthServer-AdminPanel
npm run build --prefix AuthServer-AdminPanel
npm run lint --prefix AuthServer-AdminPanel
npm test --prefix AuthServer-AdminPanel
```

Entegrasyon testleri yalnızca **silinebilir**, adı `_tests` ile biten PostgreSQL veritabanında çalışır. Her test bu veritabanını siler ve migration ile yeniden oluşturur; production bağlantısı vermeyin.

```bash
export AUTH_TEST_DATABASE='Host=localhost;Database=authserver_tests;Username=test;Password=test'
dotnet test AuthServer.Identity.Tests/AuthServer.Identity.Tests.csproj
```

GitHub Actions PostgreSQL üzerinde migration ve güvenlik entegrasyon testlerini, panel TypeScript/build/lint, oturum ve arayüz regresyon testlerini çalıştırır. Ayrıntılı inceleme ve sınırlar: [docs/REFACTORING.md](docs/REFACTORING.md).
