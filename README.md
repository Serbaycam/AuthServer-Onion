# AuthServer-Onion

**TR | EN** — Onion/Clean Architecture yaklaşımıyla tasarlanmış **JWT tabanlı bir Auth (Identity) API** ve onu kullanan **Blazor Dashboard** örneği.

> Bu repo; ASP.NET Identity + EF Core + JWT Access Token + Refresh Token (rotation) + Audit Log gibi temel kimlik/doğrulama ihtiyaçlarını, katmanlı (Onion) mimariyle örnekler.

---

## İçindekiler / Table of Contents

- [Özet](#özet)
- [Mimari](#mimari)
- [Proje Yapısı](#proje-yapısı)
- [Özellikler](#özellikler)
- [Gereksinimler](#gereksinimler)
- [Kurulum](#kurulum)
- [Çalıştırma](#çalıştırma)
- [API Kullanımı](#api-kullanımı)
- [Güvenlik Notları](#güvenlik-notları)
- [Yetkilendirme (Permission) Yapısı](#yetkilendirme-permission-yapısı)
- [Veritabanı](#veritabanı)
- [Lisans](#lisans)

---

## Özet

Bu çözüm; **kullanıcı doğrulama** ve **JWT token üretimi** yapan bir Auth API ve örnek bir **Dashboard** içerir. Proje; domain kurallarını, iş mantığını ve dış bağımlılıkları ayrı katmanlarda tutarak test edilebilirlik ve sürdürülebilirlik hedefler.

---

## Mimari

Onion/Clean yaklaşımında bağımlılıklar dıştan içe doğru akar:

```
┌───────────────────────────────┐
│        Presentation           │  AuthServer.Identity.API
│  (Controllers / Endpoints)    │  AuthServer.Dashboard
└───────────────▲───────────────┘
                │
┌───────────────┴───────────────┐
│          Application          │  CQRS / MediatR / DTO / Validation
└───────────────▲───────────────┘
                │
┌───────────────┴───────────────┐
│            Domain             │  Entities / Rules
└───────────────▲───────────────┘
                │
┌───────────────┴───────────────┐
│   Infrastructure & Persistence │  JWT, Audit, EF Core, Identity
└───────────────────────────────┘
```

---

## Proje Yapısı

- **AuthServer.Identity.API**  
  REST API katmanı. `AuthController` üzerinden login/refresh/revoke gibi uçları sunar.

- **AuthServer.Identity.Domain**  
  Çekirdek varlıklar: `AppUser`, `RefreshToken`, `AuditLog` vb.

- **AuthServer.Identity.Application**  
  Use-case’ler / iş mantığı: MediatR command/handler yapıları, wrapper response tipi vb.

- **AuthServer.Identity.Infrastructure**  
  Token üretimi, audit servisi ve permission authorization handler gibi dış bağımlılıklar.

- **AuthServer.Identity.Persistence**  
  EF Core + IdentityDbContext (`AppDbContext`), migrations ve DB erişimi.

- **AuthServer.Dashboard**  
  Blazor tabanlı bir dashboard örneği (MudBlazor + LocalStorage vb. paketleri içerir).

---

## Özellikler

- ✅ **JWT Access Token** üretimi (Issuer/Audience/Secret üzerinden)
- ✅ **Refresh Token** üretimi + DB’de saklama
- ✅ **Refresh Token Rotation** (yenilemede eski token’ı revoke edip yenisini üretme)
- ✅ **Şüpheli token theft** senaryosunda tüm aktif token’ları revoke etme
- ✅ **Audit Log** (Login ve güvenlik aksiyonları için kayıt)
- ✅ **Role tabanlı** (roles claim) token üretimi
- ✅ **Permission** (role claim type = `permission`) kontrolü + **cache** desteği

---

## Gereksinimler

- **.NET SDK 10**
- **SQL Server / LocalDB**
  - Varsayılan bağlantı: `(localdb)\MSSQLLocalDB`
- (Opsiyonel) EF Core CLI:
  - `dotnet tool install --global dotnet-ef`

---

## Kurulum

1) Repoyu klonlayın:
```bash
git clone https://github.com/Serbaycam/AuthServer-Onion.git
cd AuthServer-Onion
```

2) API ayarlarını kontrol edin:
- `AuthServer.Identity.API/appsettings.json`
  - `ConnectionStrings:DefaultConnection`
  - `JwtSettings:*` (özellikle `Secret`)

3) Veritabanını oluşturun / migrations uygulayın:

```bash
dotnet ef database update ^
  --project AuthServer.Identity.Persistence ^
  --startup-project AuthServer.Identity.API
```

> Linux/macOS kullanıyorsanız `^` yerine `\` satır devamı veya tek satır kullanın.

---

## Çalıştırma

### Auth API

```bash
dotnet run --project AuthServer.Identity.API
```

Varsayılan (Development) URL:
- `https://localhost:7023`

### Dashboard

```bash
dotnet run --project AuthServer.Dashboard
```

Varsayılan (Development) URL:
- `https://localhost:5000`

---

## API Kullanımı

> Not: Bu repo sürümünde **register endpoint’i yok**. Login için DB’de bir kullanıcı bulunması gerekir.  
> İlk kullanıcıyı oluşturmak için genelde:
> - bir **seed** eklenir (Program.cs içine) veya
> - Dashboard üzerinden kullanıcı yönetimi sağlanır (uygulanmışsa) veya
> - geçici bir admin oluşturma komutu eklenir.

### 1) Login

**POST** `https://localhost:7023/api/auth/login`

Body:
```json
{
  "email": "user@example.com",
  "password": "YourPassword!"
}
```

Başarılı yanıt (örnek):
```json
{
  "succeeded": true,
  "message": "Giriş başarılı.",
  "data": {
    "accessToken": "eyJhbGciOi...",
    "accessTokenExpiration": "2026-02-13T12:34:56Z",
    "refreshToken": "base64...",
    "refreshTokenExpiration": "2026-02-20T12:34:56Z"
  }
}
```

### 2) Refresh Token

**POST** `https://localhost:7023/api/auth/refresh-token`

Body:
```json
{
  "accessToken": "EXPIRED_OR_VALID_ACCESS_TOKEN",
  "refreshToken": "REFRESH_TOKEN"
}
```

> Refresh sırasında **rotation** uygulanır: eski refresh token revoke edilir, yenisi üretilir.

### 3) Revoke Token

**POST** `https://localhost:7023/api/auth/revoke-token`

Body:
```json
{
  "token": "REFRESH_TOKEN"
}
```

---

## Güvenlik Notları

- 🔒 `JwtSettings:Secret` içeriği repoda **örnek** olarak bulunur; production’da mutlaka:
  - uzun ve güçlü bir secret kullanın,
  - secret’ı **user-secrets / env var / vault** gibi güvenli bir yerde saklayın.
- 🔐 Refresh token’ı istemci tarafında saklayacaksanız mümkünse **HttpOnly Cookie** kullanın.
- 🌐 Reverse proxy arkasında çalıştıracaksanız gerçek IP için `X-Forwarded-For` header’ını yapılandırın.
- ✅ HTTPS zorunlu tutun.

---

## Yetkilendirme (Permission) Yapısı

Projede örnek bir permission handler vardır:

- Permission’lar, **role claim** olarak tutulur:  
  - `Type = "permission"`
  - `Value = "Some.Permission.Name"`

- `SuperAdmin` rolü varsa permission kontrolü bypass edilir.

- Permission listesi kullanıcı bazında **MemoryCache** ile 30 dakika cache’lenir.

> Genişletme önerisi: Policy isimlendirme standardı (örn. `Permission:Catalog.Read`) kurup, dinamik policy provider ekleyerek daha esnek bir yapı kurabilirsiniz.

---

## Veritabanı

DB şeması:
- ASP.NET Identity tabloları (AspNetUsers, AspNetRoles, …)
- `RefreshTokens`
- `AuditLogs`

Varsayılan connection string (Development):
- `(localdb)\MSSQLLocalDB` / `AuthServerIdentityDb`

---

## Lisans

MIT — detay için `LICENSE` dosyasına bakın.
