# AuthServer-Onion (TR | EN)

Onion/Clean Architecture yaklaşımıyla tasarlanmış **JWT tabanlı bir Identity/Auth API** ve onu kullanan **Admin Web Panel (AuthServer.Identity.WebPanel)** örneği.

- API: ASP.NET Core + Identity + EF Core + MediatR (CQRS) + JWT Access Token + Refresh Token (Rotation) + Audit Log ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs))  
- WebPanel: ASP.NET Core MVC + Cookie Auth (server-side ticket store) + otomatik token refresh ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))  

---

## İçindekiler

- [Özet](#özet)
- [Mimari](#mimari)
- [Çözüm / Proje Yapısı](#çözüm--proje-yapısı)
- [Öne Çıkan Özellikler](#öne-çıkan-özellikler)
- [Gereksinimler](#gereksinimler)
- [Kurulum](#kurulum)
- [Çalıştırma](#çalıştırma)
- [Konfigürasyon](#konfigürasyon)
- [API Endpointleri](#api-endpointleri)
- [WebPanel](#webpanel)
- [Güvenlik Notları](#güvenlik-notları)
- [Veritabanı](#veritabanı)
- [Lisans](#lisans)

---

## Özet

Bu repo; kimlik doğrulama (login) ve token yönetimi (refresh/revoke) yapan bir Auth API + yönetim işlemleri (kullanıcı/rol/izin) + bunları kullanan bir WebPanel örneği içerir.

> Not: Proje .NET `net10.0` hedefli. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/AuthServer.Identity.API.csproj))

---

## Mimari

Onion/Clean yaklaşımında bağımlılıklar dıştan içe akar:

```
┌────────────────────────────────────────┐
│ Presentation                            │
│  - AuthServer.Identity.API              │
│  - AuthServer.Identity.WebPanel         │
└───────────────────────▲────────────────┘
                        │
┌───────────────────────┴────────────────┐
│ Application                             │
│  - CQRS / MediatR                        │
│  - DTO / Wrappers (ServiceResponse<T>)  │
└───────────────────────▲────────────────┘
                        │
┌───────────────────────┴────────────────┐
│ Domain                                  │
│  - Entities (AppUser, RefreshToken, …)  │
│  - Constants (Permissions)              │
└───────────────────────▲────────────────┘
                        │
┌───────────────────────┴────────────────┐
│ Infrastructure & Persistence            │
│  - JWT TokenService                     │
│  - AuditService                         │
│  - EF Core + IdentityDbContext          │
└────────────────────────────────────────┘
```

---

## Çözüm / Proje Yapısı

Solution içeriği: ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.sln))

- **AuthServer.Identity.API**  
  REST API. Auth + yönetim endpointleri. (Controllers) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/AuthController.cs))

- **AuthServer.Identity.Application**  
  Use-case’ler (MediatR commands/queries), wrapper response, DTO’lar. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/Login/LoginCommand.cs))

- **AuthServer.Identity.Domain**  
  Temel entity ve constants. (AppUser, RefreshToken, AuditLog, Permissions) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Domain/Entities/AppUser.cs))

- **AuthServer.Identity.Infrastructure**  
  Token üretimi, audit servisleri vb. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Infrastructure/Services/TokenService.cs))

- **AuthServer.Identity.Persistence**  
  EF Core + IdentityDbContext, RefreshTokens/AuditLogs tabloları. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Persistence/Context/AppDbContext.cs))

- **AuthServer.Identity.WebPanel**  
  Admin panel (ASP.NET Core MVC). Cookie auth + server-side ticket store + otomatik refresh. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))

---

## Öne Çıkan Özellikler

### Auth / Token
- ✅ JWT Access Token üretimi (sub/email/jti/fullName + roles claim) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Infrastructure/Services/TokenService.cs))  
- ✅ Refresh Token üretimi ve DB’de saklama ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs))  
- ✅ **Refresh Token Rotation**: refresh edildiğinde eski token revoke edilir, yeni token üretilir ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs))  
- ✅ **Reuse Detection (Token Theft)**: revoke edilmiş refresh token tekrar kullanılırsa ilgili kullanıcının aktif oturumları kapatılır ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs))  

### Yönetim
- ✅ SuperAdmin’e özel dashboard stats ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/DashboardController.cs))  
- ✅ User management: kullanıcı oluşturma/güncelleme/rol atama/aktif-pasif/force logout ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/UserManagementController.cs))  
- ✅ Role management: rol CRUD + role-permission yönetimi (claim type = `permission`) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/RoleManagementController.cs))  
- ✅ Audit Log: login + admin aksiyonları loglanır ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs))  

### WebPanel
- ✅ Cookie güvenliği: `__Host-` prefix, HttpOnly, Secure, Sliding Expiration ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))  
- ✅ Token’ları cookie’ye gömmek yerine **server-side ticket store** (MemoryCache) kullanır ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))  
- ✅ Access token süresi dolmaya yaklaşınca otomatik refresh + session bazlı lock (paralel refresh çakışmasını engeller) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))  

---

## Gereksinimler

- .NET SDK (`net10.0`) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/AuthServer.Identity.API.csproj))  
- SQL Server / LocalDB (varsayılan connection string LocalDB) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/appsettings.json))  
- (Opsiyonel) EF Core CLI: `dotnet tool install --global dotnet-ef`

---

## Kurulum

1) Repoyu klonlayın:
```bash
git clone https://github.com/Serbaycam/AuthServer-Onion.git
cd AuthServer-Onion
```

2) API config kontrol edin:
- `AuthServer.Identity.API/appsettings.json`  
  - `ConnectionStrings:DefaultConnection`  
  - `JwtSettings:*` (özellikle `Secret`) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/appsettings.json))  

3) DB migrate:
```bash
dotnet ef database update \
  --project AuthServer.Identity.Persistence \
  --startup-project AuthServer.Identity.API
```
> Windows PowerShell için `\` yerine `^` kullanabilirsiniz.

---

## Çalıştırma

### Auth API
```bash
dotnet run --project AuthServer.Identity.API
```
Dev URL: `https://localhost:7023` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Properties/launchSettings.json))

### WebPanel
```bash
dotnet run --project AuthServer.Identity.WebPanel
```
Dev URL: `https://localhost:5000` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Properties/launchSettings.json))

---

## Konfigürasyon

### API (AuthServer.Identity.API/appsettings.json)
Örnek içerik: ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/appsettings.json))

- `ConnectionStrings:DefaultConnection`
- `JwtSettings:Secret`
- `JwtSettings:Issuer`
- `JwtSettings:Audience`
- `JwtSettings:AccessTokenExpirationMinutes`
- `JwtSettings:RefreshTokenExpirationDays`

> Production için `JwtSettings:Secret` değerini repo içinde tutmayın (user-secrets / env var / vault).

### WebPanel (AuthServer.Identity.WebPanel/appsettings.json)
WebPanel, API endpointlerini buradan okur: ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/appsettings.json))  
- `AuthApi:BaseUrl` (default: `https://localhost:7023/`)
- `AuthApi:*Path` (login/refresh/revoke + dashboard + user/role/permission endpointleri)

---

## API Endpointleri

> Tüm response’lar genelde `ServiceResponse<T>` wrapper’ı döner:  
> `succeeded`, `message`, `data`, `errors` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Wrappers/ServiceResponse.cs))

### Auth
Base route: `/api/auth` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/AuthController.cs))

#### 1) Login
`POST /api/auth/login`

Body:
```json
{ "email": "user@example.com", "password": "YourPassword!" }
```

#### 2) Refresh Token
`POST /api/auth/refresh-token`

Body:
```json
{ "accessToken": "EXPIRED_OR_VALID_ACCESS_TOKEN", "refreshToken": "REFRESH_TOKEN" }
```

- Rotation uygulanır, eski refresh token revoke edilir. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs))

#### 3) Revoke Token
`POST /api/auth/revoke-token`

Body:
```json
{ "token": "REFRESH_TOKEN" }
```
Controller command’e IP adresini de ekler. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/AuthController.cs))

---

### Dashboard (SuperAdmin)
Base route: `/api/dashboard` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/DashboardController.cs))

- `GET /api/dashboard/stats`

> `[Authorize(Roles="SuperAdmin")]` ile korunur. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/DashboardController.cs))

---

### User Management (SuperAdmin)
Base route: `/api/usermanagement` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/UserManagementController.cs))

- `GET /api/usermanagement/all-users`
- `POST /api/usermanagement/create-user`
- `PUT /api/usermanagement/update-user`
- `POST /api/usermanagement/change-password`
- `POST /api/usermanagement/assign-roles`
- `POST /api/usermanagement/update-status`
- `POST /api/usermanagement/revoke-all`

> Hepsi SuperAdmin role gerektirir. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/UserManagementController.cs))

---

### Role & Permission Management (SuperAdmin)
Base route: `/api/rolemanagement` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/RoleManagementController.cs))

- `GET /api/rolemanagement/roles`
- `POST /api/rolemanagement/role`
- `PUT /api/rolemanagement/role`
- `DELETE /api/rolemanagement/role/{id}`
- `GET /api/rolemanagement/permissions` (static permission listesi reflection ile okunur) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/RoleManagementController.cs))
- `POST /api/rolemanagement/permissions` (role’a permission claim set eder) ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/RoleManagementController.cs))
- `GET /api/rolemanagement/role-permissions/{id}` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/RoleManagementController.cs))

**Permission modeli**
- Permission’lar role claim olarak tutulur: `Type = "permission"` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Management/Roles/Commands/UpdateRolePermissions/UpdateRolePermissionsHandler.cs))  
- Permission örnekleri: `Permissions.Laboratories.View` vb. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Domain/Constants/Permissions.cs))  

---

## WebPanel

WebPanel; kullanıcı girişini API üzerinden yapar ve JWT içinden role’leri okuyup cookie claims olarak ekler. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Controllers/AccountController.cs))

### Neden cookie + ticket store?
- Cookie “küçülür”; access/refresh token cookie içine gömülmez
- Tokenlar server-side MemoryCache ticket store’da saklanır ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))

### Otomatik Refresh Mantığı
- Access token bitimine 2 dk kala refresh dener
- Refresh token bitmişse oturumu düşürür
- Aynı session’da paralel refresh çağrılarını lock ile engeller ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))

---

## Güvenlik Notları

- JWT secret’ı production’da güvenli saklayın (env/vault/user-secrets). ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/appsettings.json))  
- HTTPS zorunlu tutun (WebPanel cookie `SecurePolicy.Always`). ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))  
- Refresh token theft senaryosu için reuse detection mevcut. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs))  
- Reverse proxy arkasında IP için `X-Forwarded-For` değerlendiriliyor. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Controllers/AuthController.cs))  

---

## Veritabanı

`AppDbContext` içerisinde:
- ASP.NET Identity tabloları (AspNetUsers, AspNetRoles, …)
- `RefreshTokens`
- `AuditLogs` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Persistence/Context/AppDbContext.cs))

RefreshToken mapping örneği: tablo adı `RefreshTokens` ve ilişki `User -> RefreshTokens`. ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Persistence/Configurations/RefreshTokenConfiguration.cs))  

---

## Lisans

MIT. (Detay için `LICENSE`) ([github.com](https://github.com/Serbaycam/AuthServer-Onion))

---

# EN (Short)

JWT-based Identity/Auth API + Admin WebPanel built with Onion/Clean Architecture.

- API: ASP.NET Core, Identity, EF Core, MediatR CQRS, JWT + Refresh Token Rotation + Audit Log ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs))  
- WebPanel: ASP.NET Core MVC, Cookie Auth with server-side ticket store, automatic refresh ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Program.cs))  

Default URLs:
- API: `https://localhost:7023` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.API/Properties/launchSettings.json))
- WebPanel: `https://localhost:5000` ([raw.githubusercontent.com](https://raw.githubusercontent.com/Serbaycam/AuthServer-Onion/master/AuthServer.Identity.WebPanel/Properties/launchSettings.json))
