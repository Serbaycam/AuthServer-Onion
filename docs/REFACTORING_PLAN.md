# AuthServer-Onion refactoring ve geliştirme planı

**Tarih:** 26 Kasım 2026. **Başlangıç:** `9f08e66`. Bu plan kaynak analizi, ilk build/panel testleri ve izole PostgreSQL doğrulaması için hazırlanan test altyapısına dayanır. Gerçek sonuçlar [teknik analizde](PROJECT_ANALYSIS.md#verification), kullanım bilgisi [rehberde](PROJECT_GUIDE.md) tutulur.

Amaç önce ölçülmüş hataları düzeltmek, sonra veri/oturum tutarlılığı ve gözlemlenebilirliği güçlendirmek, en son ürün kapsamını kontrollü genişletmektir. Aşağıdaki ileri özellikler tamamlanmış değildir. İlk uygulama turu A aşamasını tamamladı; ikinci tur B1–B4'ü uygular; büyük protokol/veri modeli dönüşümlerini tek değişikliğe yığmaz.

## Başlangıç kanıtları

- Solution Release build başarılı; 0 hata / 71 uyarı.
- Panel build ve lint başarılı, 16/16 test geçti.
- Npm audit 12 bildirim verdi: 7 moderate / 5 high; mevcut CI audit eşiği geçmiyor.
- Revoke-all namespace seçimi nedeniyle ortak management transaction/audit dışında.
- JWT login session ile audit'i ayrı kaydediyor.
- Backend başlangıç suite'indeki 18 vaka bağımsız PostgreSQL 16 cluster üzerinde geçti; iki kırmızı regresyon kanıtlandı, düzeltme sonrası 2/2 ve son tam koşuda 26/26 geçti.

## A — İlk uygulama turu: kanıtlanmış sorunlar

### A1. Tekrarlanabilir ve izole backend testi

**Sorun:** Yerelde Docker/PG servisi yok; testler verilen DB'yi silip oluşturuyor. Normal Compose'da sabit volume adı nedeniyle yalnız proje adı değiştirmek izolasyon sağlamıyor.

**İş:** Windows PowerShell 7 runner; verilen PostgreSQL 16 binary'leriyle yalnız repo `artifacts/test-runs` altında yeni cluster, rastgele test parolası, 127.0.0.1:55432, benzersiz `_tests` DB. Global servis/PATH kurulumu yapmadan test ve TRX/log üretme. Try/finally ile yalnız kendi cluster'ını durdurma, geçici credential dosyasını kaldırma, test exit kodunu koruma.

**Kabul:** İlk 18 backend testi gerçekten çalışmış; port/cluster kimliği doğrulanmış; test sonrasında süreç kapalı; mevcut DB kullanılmamış. Yanlış/occupied portta başka DB'ye düşmeden hata. Binary hash'i resmi yayımla eşleştirilmiş. Runner kaynak kontrolünde, binary/data/log Git dışında.

### A2. Yönetim transaction kapsamını açık sözleşmeye taşıma

**Sorun:** Davranış namespace metnine bağlı; revoke-all bu nedenle dışarıda.

**İş:** Application'a `IManagementCommand` marker ekle. Mevcut 10 management command ve Auth altındaki RevokeAll bu sözleşmeyi uygulasın. Behavior request tipini bu sözleşmeyle seçsin; Serializable/Succeeded/audit/rollback akışı korunsun. URL, JSON body, response ve DB şeması değişmesin. RevokeAll gerçek actor IP'sini kullansın.

**Önce düşecek test:** Failing audit ile revoke-all çağrısı 500 beklerken başlangıç kodu 200 dönüyor; rollback sonrası session'ın geçerli kalması gerekir.

**Kabul:** Audit hatasında iptal rollback; başarılı işlem JWT ve cookie hedeflerini kapatır, başka kullanıcı session'ı korunur ve doğru actor audit'i vardır. Mevcut role/user/kill transaction testleri geçer. Login/refresh/query'ler yanlışlıkla management transaction'a girmez.

### A3. JWT login session/audit atomikliği

**Sorun:** Audit hata verdiğinde session önceden kaydedilmiş olabilir.

**İş:** Credential/lockout kontrolünden sonra staged session ve audit aynı scoped DbContext save'inde kalıcı olsun. Mevcut AuditService'nin paylaşılan context'i save ettiği sözleşme açık yorumla belirtilsin. Hatalı parola lockout yazımlarını rollback eden genel Login transaction'ı eklenmesin.

**Önce düşecek test:** Failing audit sonrası 500 alınırken session sayısının artmaması. Başlangıç kodunda yeni satır kaldığı gösterilmeli.

**Kabul:** Başarısız audit'te session yok; başarılı login'de hem doğru hash/sid hem audit var; eski bağımsız login çalışır; beş hatalı parola kilitlemeye devam eder. Yeni provider/IAuditService değiştirilirse unit-of-work sözleşmesi korunmalı.

### A4. Uyumlu bağımlılık güvenlik güncellemesi

**Sorun:** Dev/test/build zincirinde audit bulguları.

**İş:** Mevcut package.json major/aralıkları içinde lock güncelle. Vite, typescript-eslint zinciri ve etkilenen transitif paketleri düzelt; `--force`, rastgele override veya React/router major değişimi kullanma. Runtime/development etki ayrımını kaydet.

**Kabul:** npm ci, TypeScript/Vite build, lint ve16 test başarılı; `npm audit --audit-level=low` sıfır bildirim. Lock diff'inde runtime paketleri/engine/peer değişimleri incelenmiş. Advisory sonucu tarihli; gelecekte sıfır kalacağı garanti edilmez.

### A5. Kanıt ve belgeyi son davranışa eşitleme

README ve rehberde başlangıç bulgusu ile düzeltilmiş davranış ayrılmalı. Test sayısı, yeni komut, paket sürümleri ve kalan sınırlar güncellenmeli. Refactoring sonrasında gerekli testler bir kez tam çalıştırılmalı; yeni değişiklik/failure yokken gereksiz tekrar yapılmamalı.

## B — Tutarlılık ve güvenlik modeli

### B1. Seed/bootstrap atomikliği ve onarım raporu

**İş:** Bootstrap hesabı+rolü tek atomik birim; başarısız role assignment testi. Migration, role seed ve admin seed sorumluluklarını açıklaştır. Mevcut hesabı sessiz yükseltme/parola değiştirmeme ilkesi korunsun.

**Kabul:** Enjekte role/store hatasında yarım yeni admin kalmaz. Aynı bootstrap ikinci kez çalışınca duplicate/izin geri yükleme yok. Mevcut non-admin email eşleşmesi açık sonuç verir; yetki otomatik yükselmez.

**Bağımlılık:** A1 test altyapısı. **Uyumluluk:** Bootstrap davranış değişiklikleri dağıtım rehberine yazılmalı.

### B2. Kullanıcı genelinde session sürümü ve yarışlar

**İş:** Logout epoch/security version veya eşdeğer model için tasarım. Şifre değişimi, force logout, pasifleştirme ve dış Identity stamp değişimiyle refresh invalidation tek sözleşme olsun. Session create/refresh ile all-session revoke yarışlarının hedef an semantiği tanımlansın.

**Kabul:** Kontrollü eşzamanlı login/refresh/password/revoke testleri; stamp değişince eski refresh yeni yetki üretemiyor. Bağımsız yeni login'in hangi andan sonra geçerli olduğu açık. DB migration ve eski token geçişi tanımlı.

**Bağımlılık:** A2/A3. **Karar konusu:** Session version modeli ve tüketici uyumu, uygulamadan önce tasarım kaydı gerektirir.

### B3. Refresh family ve mutlak oturum sınırı

**İş:** Family id, login başlangıcı, mutlak süre ve replay kapsamını modelle. Mevcut kullanıcı genelinde replay kapatma politikasını daraltmak ürün kararıdır; otomatik değiştirilmemeli.

**Kabul:** Rotation sabit mutlak üst sınırı aşamaz; expired/revoked/replay ayrımı testli; stale-chain logout ve kill korunur; migration+istemci yenileme kılavuzu var.

### B4. Tek audit olay sözleşmesi

**İş:** Actor/target/correlation/action/result ve sanitize edilmiş önceki/yeni değerler. Generic/özel çift olay stratejisini birleştir. AssignRoles/status/revoke-all hedefi görünür olsun. Token/parola redaction ve cancellation davranışı açık olsun.

**Kabul:** Her yönetim mutasyonu için hedef/aktör izlenebilir; başarısız audit transaction'ı bozduğu testli; sır içermediği testli; dashboard aynı eylemi yanlışlıkla iki kez saymaz. Veri saklama kararıyla uyumlu.

## C — API ve kod kalitesi

### C1. Sunucu pagination, filtre ve N+1 kaldırma

**İş:** Kullanıcı/session/rol/audit sorgularına sınırlandırılmış page/pageSize/sort/filter sözleşmesi. Rol projection/batched query, deterministic sıralama. Panel pagination'ını sunucu yanıtına geçir.

**Kabul:** Büyük fixture'da sorgu sayısı kullanıcı sayısıyla doğrusal büyümüyor; maksimum pageSize zorunlu; toplam/boş/son sayfa sınırları; Türkçe arama beklentisi açık. Uygun indeksler EXPLAIN/ölçümle seçilmiş. API sözleşme geçişi belgeli.

### C2. Validation ve hata sözleşmesi

**İş:** Guid.Empty, liste elemanı/uzunluk, normalize email/rol adı ve bounds tutarlılığı. ValidationProblem/ServiceResponse/challenge/rate-limit farkları için tek belgeli hata modeli, kararlı code ve trace.

**Kabul:** Tüm23 uç için yetkisiz/yetkisiz rol/geçersiz input/iş kuralı matrisi; frontend alan hatası eşlemesi; exception ayrıntısı sızmıyor. Public contract değişimi compatibility testleriyle yönetiliyor.

### C3. Nullability ve compiler gates

**İş:** 71 başlangıç uyarısının kaynağını DTO/entity/claim/reflection başlıklarında çöz. Uygun required/nullable/init/default kullan; null-forgiving ile toplu bastırma yapma. Panel strict mode'u bağımsız dilimde aç.

**Kabul:** Backend temiz rebuild uyarı sayısı düşer ve hedef sıfırdır; yeni warning gate. Panel strict build ve bütün regresyonlar geçer. Nullable alanların HTTP/DB anlamı korunur.

### C4. Mimari sınırları sadeleştirme

**İş:** Önce audit/unit-of-work ve session policy arayüzlerini belirginleştir; kullanılan framework bağımlılıklarını belgelemeyi sürdür. Kanıtlanmış ihtiyaç yokken bütün Identity/EF modelini yeniden yazma. Eski yorumlar, kullanılmayan using/folder/DTO'lar küçük dilimlerle temizlenebilir.

**Kabul:** Bağımlılık yönü açık, yaşam döngüsü testli; katmanlar arası sahte soyutlama eklenmemiş. Kullanıcı hash'leri ve migration sürekliliği korunmuş.

## D — Eksik ürün özellikleri

| İş | Kapsam | Kabul kriteri / bağımlılık |
|---|---|---|
| D1 Parola kurtarma | Self-service request/reset, e-posta gönderimi, süreli tek kullanımlı token, genel yanıt | Hesap enumeration önlemi, rate limit, token replay/expiry, bütün session invalidation; B2 |
| D2 E-posta doğrulama | Confirm/resend, değişen e-postada doğrulama politikası | Tek kullanımlı token ve süre, eski/yeni adres yarışları, yönetici override audit'i |
| D3 MFA yaşam döngüsü | Enrollment, challenge, recovery, reset ve güvenilen cihaz politikası | Tek faktör bypass yok; recovery tek kullanımlı; mevcut session etkisi; kilitlenmeden kurtarma |
| D4 Audit ekranı | Arama, hedef/aktör/tarih/sonuç filtresi, sayfalama, kontrollü export | B4/C1; permission ve veri erişim sınırı; büyük export asenkron/sınırlı |
| D5 Session görünürlüğü | Browser/JWT türü, family, son kullanım, cihaz etiketi | Ham token/hash sızmıyor; privacy/retention; yanlış cihaz güveni oluşturmayan açıklama |
| D6 Permission kataloğu | Uygulama/modül kaydı, uygulama bazlı rol/izin sınırı | Mevcut laboratuvar sabitlerinin migration'ı ve tüketici uyumu; tenant ihtiyacı ayrı kararla |
| D7 Yönetici kurtarma | Break-glass prosedürü ve kullanılabilir yönetici kontrolü | Kilit/MFA ve rol düşürme senaryoları; ayrı yetki ve denetlenebilir kurtarma |
| D8 Panel kullanım | Kaydedilmemiş izin uyarısı, alan hataları, erişilebilirlik, browser uyumu | Gerçek browser klavye/focus/dialog/sekme testleri; tanımlı destek matrisi |

Kullanıcı silme/anonimleştirme de veri saklama ve audit ilişkileriyle birlikte tasarlanmalıdır; mevcut kullanıcı pasifleştirmesiyle aynı işlem değildir. Sosyal login için tabloların varlığı hazır entegrasyon anlamına gelmez.

## E — Protokol ve dış uygulama entegrasyonu

OIDC/OAuth2/SSO'yu mevcut `/api/Auth` uçlarının adına dayanarak eklenmiş saymayın. İhtiyaç netleşince standards-compliant sağlayıcı/kitaplık seçimi, client registry, redirect URI/PKCE/consent/scope, discovery/JWKS, asimetrik key rotation ve token revocation/introspection birlikte tasarlanmalı.

**Kabul:** Bağımsız istemciyle authorization code+PKCE; yanlış redirect/client/scope, replay ve key rotation testleri; mevcut kullanıcı/parola hash/migration geçiş planı. HMAC sırrını tüketicilere dağıtmak token üretme yetkisini de paylaştırır. AuthServer dışındaki API'ler için anlık iptal ve rol değişim sözleşmesi ayrıca test edilmeli.

Bu aşama büyük ürün/mimari kararıdır; A turunun parçası olarak sessizce başlatılmayacaktır.

## F — İşletim ve teslim güvenceleri

| İş | Çıktı ve kabul |
|---|---|
| F1 Readiness/liveness | DB/key/migration durumunu uygun gizlilikle ayıran endpoint; Compose/proxy probe; startup/migration yarış testi |
| F2 Dağıtık limiter/proxy | Güvenilen forwarded chain ve gerçek istemci kimliği politikası; çok instance ortak kota; spoof testi |
| F3 Secrets/key lifecycle | Vault veya eşdeğer, non-root/least privilege, shared Data Protection keyring, rotation/restore tatbikatı |
| F4 Gözlemleme | Yapılandırılmış log, metric/trace, 401/403/409/429/500 ve replay uyarıları; hassas veri redaction |
| F5 Backup/retention | DB+key+config tutarlı yedek; ayrı hedef restore kanıtı; session zincirini bozmayan cleanup, audit saklama |
| F6 Supply chain | .NET SDK/tool pinning, image digest, dependency/image scan, SBOM/signature, CI artifact ve deployment gate |
| F7 Gerçek browser E2E | Atılabilir DB/API/panel ile HTTPS login-F5-mutation-logout, demotion/revoke, iki sekme, ağ hatası ve erişilebilirlik |
| F8 Performans | Hedef kullanıcı/session yükü tanımla; ölçülmüş query/latency/memory bütçesi; C1 indeksi/limiti bu sonuca göre |

## Uygulama ve geri dönüş kuralları

1. Her dilimde önce hatayı yakalayan anlamlı regresyon; mevcut davranışı ayna gibi sayan test değil.
2. Başlangıç başarısızlığı ve düzeltme sonrası sonucu kaydet.
3. İlgili testler geçince bir tam uygun suite; yeni sorun yokken tekrar döngüsü kurma.
4. Migration/endpoint/kimlik modeli değişikliklerini ayrı gözden geçirilebilir dilime ayır.
5. Eski refresh sırlarını geri yükleyen rollback yapma. Gerektiğinde tekrar login gereksinimini kabul et.
6. Belge/plan/test raporu son davranışa eşitlensin. Commit/push/deploy bu yerel çalışmanın otomatik sonucu değildir.

## İlk tur uygulama kaydı

- [x] Kaynak incelemesi, README, ayrıntılı rehber ve teknik analiz.
- [x] Başlangıç panel build/lint/16 test ve tarihli audit.
- [x] A1 izole PostgreSQL 16 baseline: 18/18 geçti; runner exit 0 ve cluster kapalı; gerçek occupied-port guard kontrolü de geçti.
- [x] A2/A3 iki regresyon önce beklenen biçimde düştü; marker/atomik login düzeltmesi sonrası 2/2 geçti. Sekiz ek testle toplam 26 vaka hazır.
- [x] A4 uyumlu lock güncellemesi; audit 12→0, build/lint ve 16/16 test başarılı.
- [x] A5 son backend 26/26, panel 16/16; build/lint, npm audit 0, NuGet taraması temiz; belgeler güncel. Docker/Nginx runtime ve gerçek browser E2E çalıştırılmadı, F7 açık.

## İkinci tur — B uygulama kaydı

- [x] B1: tek seed giriş noktası; eksik roller+yeni yönetici Serializable transaction'ı; mevcut non-admin email'de açık hata; 6 test.
- [x] B2: kullanıcı row lock sırası, refresh stamp snapshot/epoch, login credential-stamp yarış koruması; [tasarım kaydı](SESSION_SECURITY_DESIGN.md).
- [x] B3: kullanıcının seçtiği **30 günlük** değişmez JWT aile sınırı; normal access/refresh ömrü bu sınırı aşamaz. Cookie süresi korunur, replay kullanıcı-geneli kalır.
- [x] B4: 11 yönetim mutasyonunda tek hedefli/correlation/değişim bilgili audit; secret redaction; request cancellation ve audit rollback; 25 test.
- [x] `20261126112131_SessionSecurityPolicy`: eski bütün JWT/cookie oturumlarını güvenli kapatma; kullanıcı/rol/audit verisi korunur; Up/Down eski sırları geri getirmez.
- [x] İlave hata düzeltmesi: Npgsql'nin iç içe sarılmış 40001/40P01 zinciri 409 olarak eşlenir. Gerçek refresh yarışında 500 yeniden üretildi, düzeltme sonrası 2/2 geçti.
- [x] Son tam backend **68/68**, panel **16/16**; toplam **84/84**. Build/lint, npm audit 0 ve NuGet taraması başarılı; doküman ve test kanıtları güncel.

B turu tamamlandı. Sıradaki aşama **C1–C4**: sunucu sayfalaması/N+1, doğrulama ve hata sözleşmesi, nullability/strict derleme ve mimari sınırların sadeleştirilmesi. D–F ürün/işletim işleri ayrıca açık kalır.
