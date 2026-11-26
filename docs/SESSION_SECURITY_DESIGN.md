# Oturum güvenliği ve tutarlılık tasarımı — B2/B3

26 Kasım 2026. Önceki A turunun üzerine uygulanır. Kullanıcı JWT girişlerinin mutlak üst süresini **30 gün** olarak seçti; ayar `JwtSettings:AbsoluteSessionLifetimeDays` olarak uygulanır. Replay kapsamı mevcut kullanıcı-geneli politikayı korur.

## Güvenlik sürümü

Identity'nin `SecurityStamp` değeri kullanıcı-geneli session epoch olarak kullanılır. Yeni bir paralel kullanıcı sürümü kolonu eklenmez. Her refresh kaydı üretildiği stamp'i saklar. Access/cookie doğrulaması ve refresh hem kullanıcı stamp'ini hem kayıt stamp'ini eşleştirir.

Force logout/replay/deactivation bu stamp'i değiştirir ve mevcut açık kayıtları kapatır. Parola reseti Identity'nin stamp değişimini kullanır ve aynı kilit kapsamında oturumları kapatır. Dış `UserManager.UpdateSecurityStampAsync` da mevcut refresh'i geçersiz kılar.

## Kilit ve sıralama

Session üretimi, refresh, tüm oturumları kapatma ve güvenlik değişiklikleri aynı kullanıcı satırında PostgreSQL `SELECT ... FOR UPDATE` kullanır. Bir işlem birden fazla kullanıcıyı etkiliyorsa Guid sırasıyla bütün kullanıcı satırlarını önce kilitler; cookie ile A hesabından B'ye giriş de buna dahildir. Sonra oturum satırları okunur/değiştirilir.

Kilit helper'ı mevcut yönetim transaction'ına katılır; yoksa ReadCommitted transaction açar. EF'nin önceden izlediği kullanıcılar kilit sonrası yeniden yüklenir. Serializable snapshot çakışmaları 409 ile fail-closed olur; otomatik tekrarlanmaz.

Başarısız parola sayımlarının kalıcı olması için girişin ilk parola kontrolü transaction dışında kalır. Başarılı kontroldeki stamp yakalanır; kilit sonrasında taze hesap stamp'i farklıysa token verilmez. Böylece arada parola reseti/force logout olursa eski credential yeni stamp'le yükseltilemez. Yeni giriş kilit/epoch değişiminden sonra doğrulanmışsa bağımsız yeni oturum açabilir.

## Refresh ailesi ve süre

Yeni JWT login yeni FamilyId, FamilyCreatedAt ve AbsoluteExpiresAt üretir. Her rotation yeni sid üretir fakat aile kimliği, başlangıcı ve mutlak bitişi aynen taşır. Access ve refresh bitişi yapılandırılmış normal ömür ile aile bitişinin küçük olanıdır. Deadline geçtiğinde access/refresh kullanılamaz; yeniden parola gerekir. Cookie'nin mevcut 12 saatlik mutlak ömrü korunur, JWT'nin 30 günü cookie'yi uzatmaz.

Replay aynı kullanıcıdaki bütün açık kayıtları kapatır ve stamp'i döndürür. Tek logout/kill yalnız hedef replacement zincirini kapatır; bağımsız aileleri kapatmaz.

## Migration

Eski kayıtlardan güvenilir aile başlangıcı/stamp çıkarılamaz. Yeni migration kullanıcı/rol/audit verisini korur, bütün eski JWT/cookie oturumlarını iptal eder ve eski refresh hash'lerini geri kullanılamayan yer tutucularla değiştirir. Böylece yükseltme öncesi sırlar sonraki yeni girişlere replay uygulayamaz. Geri migration da açık oturumları kapatır; eski sırları geri getirmez.

## Test sınırları

Gerçek PostgreSQL üzerinde: dış stamp değişimi; login/refresh ile revoke/password yarışlarının bariyerle kontrollü sıraları; bağımsız yeni login'in korunması; aile deadline clamp ve expiry; eski migration'dan veri korunarak geçiş; eski sırın yeni login'i kapatmaması; cookie hesap değiştirmede sıralı kilit; mevcut stale-chain logout, CSRF, lockout ve audit rollback regresyonları.

## Hata ve yeniden deneme sözleşmesi

PostgreSQL serialization/deadlock hata zinciri Npgsql tarafından `InvalidOperationException → DbUpdateException → PostgresException(40001/40P01)` biçiminde sarılabilir. Middleware bütün InnerException zincirini denetler ve bu çakışmaları 409 döndürür. Diğer audit/işletim hataları 500 kalır. 409 başarısız işlemdir; otomatik refresh/mutasyon retry eklenmez. Yarış testleri 409 sonrası bir yeni yönetim isteğini açıkça yeniden göndererek başarı sonrası iptali doğrular.

Dayanaklar: [PostgreSQL izolasyonu](https://www.postgresql.org/docs/16/transaction-iso.html), [satır kilitleri](https://www.postgresql.org/docs/16/explicit-locking.html), [EF tracking](https://learn.microsoft.com/en-us/ef/core/querying/tracking), [Npgsql 10.0.1 execution strategy](https://raw.githubusercontent.com/npgsql/efcore.pg/v10.0.1/src/EFCore.PG/Storage/Internal/NpgsqlExecutionStrategy.cs). Uygulama garantileri ayrıca gerçek PostgreSQL entegrasyon testleriyle sınanır.
