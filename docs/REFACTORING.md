# İnceleme ve refactoring

İncelenen başlangıç commit'i: `94a52ff143f8c35ed84565f8636d788962dd6c76`.

## Bulgular ve uygulanan değişiklikler

| Öncelik | Bulgu | Değişiklik |
|---|---|---|
| Kritik | Bir kullanıcının herhangi bir açık refresh kaydı bütün access token'larını geçerli kılıyordu; tek oturum iptali etkisiz kalabiliyordu. | Access token'da `sid`, kullanıcı/security stamp/son kullanma/iptal kontrolü. Her token kendi oturumuyla doğrulanır. |
| Kritik | Refresh sırları ve replacement sırları veritabanında açık metindi. | SHA-256 özet saklama, eski sırları silen migration ve yeniden giriş. |
| Yüksek | Eşzamanlı refresh aynı token'dan birden çok yeni oturum üretebiliyordu. | RevokedDate concurrency token, benzersiz token indeksi; başarısız yarışta token döndürülmez. |
| Yüksek | Pasif/kilitli hesap refresh üzerinden yeni token alabiliyordu. | Yenilemede hesap durumu kontrolü; JWT doğrulamasında da güncel durum kontrolü. |
| Kritik | JWT anahtarı, DB şifresi, yönetici şifresi ve örnek gerçek JWT depodaydı. | Ortam yapılandırması, doğrulanan bootstrap, HTTP örneğinin temizlenmesi. Mevcut sırların rotasyonu işletim adımıdır. |
| Yüksek | Login başarısız denemeleri lockout'a saymıyordu. | Lockout açık, ortak başarısız giriş mesajı, istek boyutu/required/email doğrulama ve rate limiter. |
| Yüksek | JWT içindeki eski SuperAdmin rolü ve 30 dakikalık süreç içi izin cache'i yetki kaldırılmasını geciktiriyordu. | Roller ve izinler güncel veritabanından okunur; genel cache temizleme kaldırıldı. |
| Yüksek | Rol/claim sonuçları göz ardı ediliyordu; kısmi kullanıcı/rol değişiklikleri mümkün oluyordu. | IdentityResult kontrolü, yönetim komutlarında serializable transaction ve audit'in aynı transaction'a katılması. |
| Yüksek | Son yöneticinin kapatılması ve sistem rolünün yeniden adlandırılması mümkündü. | Son aktif yönetici ve sistem rol adı koruması. |
| Orta | Exception mesajları istemciye ham olarak dönüyordu. | Genel JSON hata, trace id, sunucu logu, concurrency için 409 ve istek iptali desteği. |
| Orta | İstemcinin X-Forwarded-For başlığı audit IP'si olarak kabul ediliyordu. | Varsayılan olarak yalnızca bağlantının uzak IP'si kullanılır. |
| Yüksek | Panel localStorage token'ı, paralel refresh yarışı ve logout sırasında token geri gelmesi riski taşıyordu. | Bellek deposu, tek ortak refresh promise, yeni oturumun logout yarışında ayrıca iptali, React state senkronizasyonu. |
| Orta | Kullanıcı oluşturma yanlış endpoint'e gidiyordu; force logout sahte düğmeydi. | Endpoint düzeltildi, gerçek revoke-all çağrısı eklendi. |
| Orta | Current session hiçbir zaman doldurulmuyor, otomatik yenileme çalışmıyordu. | Sunucuda sid üzerinden current session, panelde temizlenen polling/abort yaşam döngüsü. |
| Orta | Başlangıç migration/seed hatası yutuluyordu. | Açık initialize seçeneği ve hatada durma. Seed mevcut kullanıcıyı yükseltmez, izinleri her açılışta geri eklemez. |
| Orta | DB/pgAdmin ağda açık, pgAdmin sabit parolalıydı. | pgAdmin varsayılan stack'ten çıkarıldı; DB/API internal, panel loopback; sırlar env üzerinden. |
| Düşük | Kullanılmayan AutoMapper/FluentValidation paketleri, başlangıç görselleri ve çelişen kurulum belgesi vardı. | Kullanılmayan öğeler kaldırıldı, `.cs.cs` dosyası düzeltildi, README birleştirildi. |

## Tasarım kararları

Mevcut Identity/EF Core modelini tümüyle farklı bir identity sağlayıcısıyla değiştirmek kullanıcı, şifre hash'leri ve migration sürekliliğini bozacağı için mevcut katmanları koruyan bir güvenlik refactoring'i yapıldı. Laboratuvar rol/izin adları başka tüketicileri kırmamak için korundu; yeni kurulumda otomatik izin verilmez. Ayrı uygulama/tenant izin kataloğu henüz yoktur.

Refresh rotation yeni sid üretir. Bu bilinçli olarak önceki access token'ı hemen geçersiz kılar; istemciler refresh'i seri çalıştırmalı ve yeni çiftin tamamını kullanmalıdır. Replay durumunda mevcut davranış korunarak kullanıcının bütün açık oturumları iptal edilir; aile başına iptal için ayrı token-family modeli gerekir.

Güncel rol kontrolü, yetki kaldırılmasını hemen uygular; bunun bedeli her kimliği doğrulanmış istekte veritabanı sorgularıdır. İleride cache gerekirse çok instance arasında tutarlı revocation/version invalidation tasarlanmalıdır.

Yönetim uçları SuperAdmin ile sınırlı kalır. Mevcut laboratuvar permission handler'ı diğer policy tüketicileri içindir; sırf bir laboratuvar izni atanması kimlik yönetim paneline erişim vermez.

## Doğrulama

- Panel production build ve TypeScript kontrolü yerelde başarılı.
- Panel ESLint yerelde başarılı.
- Gerçek API istemci modülünü kullanan 3 Node testi başarılı: paralel 401 tek refresh, logout/refresh yarışı, başarısız refresh'te sonlanma.
- PostgreSQL üzerinde .NET entegrasyon testleri ve GitHub Actions workflow eklendi. Bu çalışma ortamında .NET SDK olmadığı için yerel backend derlemesi/testi çalıştırılamadı. CI sonucu ayrıca kontrol edilmelidir; dosyanın varlığı testlerin geçtiği anlamına gelmez.

## Açık sınırlar ve sonraki işler

1. OIDC/OAuth2 authorization code + PKCE, JWKS/asimetrik anahtar yönetimi, istemci kaydı ve SSO uygulanmış değildir. Harici uygulamalara ortak HMAC sırrı dağıtmak token üretme yetkisi de verir; bu mimari standart merkezi SSO olarak dağıtılmamalıdır.
2. MFA enrollment/challenge/recovery, self-service parola sıfırlama ve e-posta doğrulama akışı yoktur. MFA işaretli hesaplar tek faktör endpoint'inden giriş alamaz; desteklenen MFA akışı eklenmelidir.
3. Kullanıcı/oturum listeleri henüz sayfalı değildir; kullanıcı listesinde rol okuma N+1 kalır. Büyük veri için sorgu projeksiyonu ve UI pagination ayrı bir uyumlu sözleşme değişikliğidir.
4. Proxy güven zinciri, HTTPS sertifikası, ortak rate limiter, key vault, izleme/uyarılar, yedek geri yükleme ve veri saklama/temizleme politikaları dağıtım ortamında kurulmalıdır. Varsayılan proxy tek rate-limit havuzunu paylaşır.
5. LocalStorage bırakıldı; ancak XSS aynı açık sayfada bellekteki token'lara erişebilir. CSP savunmayı güçlendirir, XSS'i tek başına ortadan kaldırmaz. Uzun süreli browser session için BFF/HttpOnly cookie ve CSRF tasarımı gerekir.
6. Kaynak geçmişindeki eski sırlar silinmedi. Mevcut yönetici ve DB parolaları otomatik değiştirilmedi. Sistemin production'a hazır olduğu iddia edilmez; CI ve dağıtım geçişi tamamlanmalıdır.
