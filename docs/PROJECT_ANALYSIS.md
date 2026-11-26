# AuthServer-Onion teknik analiz raporu

**İnceleme tarihi:** 26 Kasım 2026. **Başlangıç sürümü:** `9f08e66b26308bfcc6707dec99bacfd7ef223374`. API, Application, Domain, Infrastructure, Persistence, panel, testler, migration'lar ve CI incelenmiştir. Çalışma zamanı kanıtı ile statik bulgu aşağıda ayrılır. Sonraki refactoring değişiklikleri aynı belgenin doğrulama kaydında ayrıca izlenir.

Kullanım/kurulum için [proje rehberini](PROJECT_GUIDE.md), başlangıç için [README](../README.md) okuyun. Bu rapor kodun ve yerel testin kapsamını açıklar; canlı sistem, gerçek kullanıcı verisi veya uzak CI'ın son çalışması incelenmemiştir.

## Genel değerlendirme

Proje kullanıcı/rol/izin ve iptal edilebilir session yönetimi için anlaşılır bir katmanlı temel sunuyor. JWT sid doğrulaması, refresh hash/rotation/replay, güncel rol kontrolü ve HttpOnly cookie/CSRF yaklaşımı belirgin korumalar. Refresh sonrası eskimiş satırdan logout/kill güncel ardılı kapatabiliyor.

Başlangıçta en önemli doğrulanmış tutarlılık açığı yönetim transaction kapsamının namespace'e bağlanmasıydı; revoke-all kapsam dışında, JWT login session/audit yazımları da ayrıydı. İlk refactoring dilimi bu iki sorunu giderir; F01/F02 aşağıda başlangıç kanıtı olarak korunur. B turu seed/audit/session tutarlılığını da ele alır. Ölçek, derleyici borcu, operasyon hazırlığı ve eksik kimlik yaşam döngüsü özellikleri sonraki ilerleme alanlarıdır.

Mevcut mimari saf framework bağımsız Onion değildir. Domain Identity'ye, Application Identity/EF'ye bağlıdır. Mevcut kullanıcı/migration devamlılığını korumak için küçük adımlı refactoring uygundur; identity sağlayıcısını aniden değiştirmek bu görevin zorunlu ilk adımı değildir.

## Uygulanmış korumalar

| Konu | Kaynak davranışı |
|---|---|
| Access/session bağı | Her access kendi sid satırına bağlı; başka açık login eski token'ı kurtarmaz |
| Refresh sırrı | CSPRNG 32 bayt; DB'de SHA-256 özeti; unique index |
| Rotation | Yeni sid; önceki kayıt iptal; tek save ve RevokedDate concurrency kontrolü |
| Replay | İptal edilmiş sırrın refresh'te kullanılması bütün kullanıcı session'larını kapatır |
| Eski sırdan logout | Replacement zinciri izlenir; bağımsız login korunur |
| Hesap kontrolü | Aktiflik/kilit/MFA/stamp/sid ve güncel roller korunan istekte kontrol edilir |
| Browser session | HttpOnly/Strict, Secure varsayılanı; mutlak ömür; DB'den iptal |
| CSRF | Cookie mutation'ları ve anonim panel login için antiforgery header |
| Yetki | Yönetim SuperAdmin; permission claim'leri güncel DB'den okunur |
| Yönetici koruması | Son aktif yönetici rol/durum; SuperAdmin/Basic isim ve silme koruması |
| Hata | Raw exception yerine genel mesaj/trace; concurrency için 409 |
| Test güvenliği | Zorunlu `_tests` adı; atılabilir PostgreSQL; migration/model kontrolü |

## Başlangıç sürümünün bulguları ve öncelikler

F01/F02/F03 A turunda; F04/F05/F07 B turunda ele alınmıştır; son doğrulama kaydı aşağıdadır. F06, F08–F12 plan kapsamındaki açık işlerdir; başlangıç kanıtları aşağıda tarihsel olarak korunur. Öncelikler bu projenin kodu/işletim etkisine göre verilmiştir; bağımlılık raporundaki npm severity ile aynı ölçek değildir. Statik risk senaryoları, gerçekten yeniden üretilmiş hatalardan ayrılır.

### F01 — Revoke-all yönetim transaction/audit dışında (yüksek)

Kanıt: [ManagementTransactionBehavior](../AuthServer.Identity.Persistence/ManagementTransactionBehavior.cs) başlangıç sürümünde request namespace'inde `.Management.` ve `.Commands.` arar. [RevokeAll komutu](../AuthServer.Identity.Application/Features/Auth/Commands/RevokeAll/RevokeAllTokenCommand.cs) Auth namespace'indedir. Handler'da ayrıca audit yoktur, RevokedByIp gerçek IP yerine `Admin` olur.

Etki: diğer yönetim işlemleriyle aynı audit/Serializable read→write garantisi yoktur. Tek SaveChanges yine atomiktir; “her satır ayrı kaydediliyor” denemez. Eşzamanlı yeni login/rotation ile kullanıcı genelinde anlık kapatma sınırı ayrıca vardır.

Öneri: açık management marker sözleşmesi; mevcut yönetim komutları ve revoke-all aynı pipeline'a alınmalı. Audit hatasında rollback ve JWT/cookie birlikte iptal regresyonları eklenmeli.

### F02 — JWT login session/audit atomik değil (yüksek)

Kanıt: [LoginCommandHandler](../AuthServer.Identity.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs) session SaveChanges'inden sonra AuditService çağırır. Cookie login açık transaction içinde çalışır; bu bulgu JWT yoluna özeldir.

Etki: audit hatasında 500 dönmesine rağmen session satırı kalabilir. İstemci başarılı token çifti alamazken kayıt/başarı görünümü tutarsızlaşır.

Öneri: credential/lockout kontrolünü rollback kapsamına almadan session+audit aynı unit of work ile kaydedilmeli. Tüm Login handler'ını başarısız sonuçta rollback eden transaction'a almak, hatalı parola sayımlarını geri alma riski taşır. Failing-audit regresyonuyla doğrulanmalı.

### F03 — Npm audit güncel bildirimler nedeniyle başarısız (yüksek)

26.11.2026 `npm audit --audit-level=low`: **12 bulgu; 7 moderate, 5 high; exit 1**. Etkilenen gruplar Vite, typescript-eslint/minimatch/brace-expansion, source-map-js, undici ve zod zincirleridir. İncelenen kayıtlar dev/test/build bağımlılıklarıdır; son Nginx imajı Node dev sunucusu çalıştırmaz. Bu sonuç bütün açıkların production tarayıcı paketinde sömürülebildiği kanıtı değildir.

Mevcut workflow audit'i zorunlu kıldığından bu tarihli veriyle audit adımı geçmez. Uzak son workflow sonucu sorgulanmadı. Eski `REFRESH_SESSION_ANALYSIS.md` içindeki sıfır audit sonucu kendi tarihine aittir.

Öneri: mevcut major ve package.json aralıkları içinde hedefli lock güncellemesi; force/override olmadan build/lint/test/audit tekrarları. Vite advisory'sinin özel middleware koşulları vardır; mevcut standart SPA config'ine otomatik exploit iddiası taşınmamalıdır. Birincil kayıtlar: [Vite](https://github.com/advisories/GHSA-9jrq-w75r-8gcw), [source-map-js](https://github.com/advisories/GHSA-68fv-2mgg-jv7q), [brace-expansion](https://github.com/advisories/GHSA-q2hr-2g5m-vwhr).

### F04 — Bootstrap kısmi oluşturma riski (orta)

[ContextSeed](../AuthServer.Identity.Persistence/Seeds/ContextSeed.cs) yeni hesabı ve rol atamasını ayrı Identity save'leriyle yapar; tamamını saran açık transaction yoktur. Rol ekleme başarısız olursa mevcut email kontrolü sonraki denemede hesabı atlayabilir. Bu koddan çıkarılan hata senaryosudur; bu incelemede üretimde yaşandığı gösterilmemiştir.

Öneri: migration ile seed sorumluluklarını ayırmak; seed unit of work/başarısızlık testi ve mevcut hesabı sessiz yükseltmeden tutarlılık raporu.

### F05 — Refresh/stamp ve tüm-session kapatma sınırı (orta)

Refresh kaydı başlangıç stamp'ini saklamaz; handler stamp karşılaştırmaz. Dış entegrasyon yalnız security stamp değiştirip session'ları kapatmazsa refresh güncel stamp'li token üretebilir. Mevcut yönetici parola akışı ayrıca session iptal ettiği için normal sıralı yol korunur.

Revoke-all/replay mevcut kayıtları sorgular; kullanıcı düzeyinde logout epoch/session version yoktur. Sonradan başlayan yeni bağımsız login'i engelleme garantisi verilmemelidir. JWT refresh her seferinde ömrü ileri alır; family mutlak ömür sınırı yoktur.

Öneri: ürün politikasıyla session version/family ve mutlak ömür tasarımı; yarış ve dış Identity değişimi regresyonları. Bu değişiklikler migration/istemci davranışı etkisi nedeniyle ayrı dilim olmalıdır.

### F06 — Son aktif yönetici, kullanılabilir yönetici demek değil (orta)

[AdministratorGuard](../AuthServer.Identity.Application/Security/AdministratorGuard.cs) diğer SuperAdmin'lerde yalnız IsActive sayar. Kilitli/MFA işaretli veya credential'ı erişilemeyen yöneticiler de sayılır. Guard tek başına erişilebilir kurtarma hesabı garanti etmez. MFA challenge akışı olmadığı için flag'in doğrudan açılması giriş imkanını kaldırır.

Öneri: kurtarma prosedürü ve MFA yaşam döngüsü; ürün politikası netleşince korumanın erişilebilirlik ölçütleri.

### F07 — Audit izlenebilirliği eşit değil (orta)

Generic audit EntityId boş ve yalnız komut adı taşır. AssignRoles/UpdateUserStatus gibi işlemlerin hedefi/önceki-yeni durumu buradan anlaşılamaz. Özel audit olan işlemler ayrıca generic kayıt üretir; dashboard'da çift satır olabilir. Logout/refresh/başarısız login ve bütün okumalar kapsamlı güvenlik olay akışı değildir.

Öneri: tek olay sözleşmesi, hedef/aktör/correlation/sanitized değişim alanları; secret redaction; audit arama ve saklama politikası. AuditService cancellation token taşımıyor; bu da gözden geçirilebilir.

### F08 — Liste ölçeği ve indeksleme (orta)

[GetUsersWithRoles](../AuthServer.Identity.Application/Features/Management/Users/Queries/GetUsersWithRolesQuery.cs) tüm kullanıcıları getirir, kişi başına rol okur. Session API tüm aktif kayıtları döner. Panel 20 satır görünümü sayfalar; ağ/DB yükünü azaltmaz. Aktif session ve audit tarih sorgularına özel ölçülmüş indeks stratejisi yoktur.

Öneri: sunucu pagination/filter/sort, rol projection/batched query, ölçüme dayalı indeks. Bu raporda yük benchmark'ı yapılmadı; belirli latency/kapasite değeri ileri sürülmez.

### F09 — Operasyon eksikleri (orta)

API readiness/liveness yok; Compose yalnız DB healthcheck kullanır. TLS dış proxy sorumluluğudur. X-Forwarded-For işlenmez, peer limiter havuzu paylaşılır. Limiter süreç içi; çok instance ortak kota yok. Key vault, metrik/uyarı, backup/restore otomasyonu ve retention uygulanmış değildir.

DB volume adı sabittir: yalnız Compose proje adını değiştirmek izolasyon sağlamaz. API image explicit non-root USER içermez; image digest/SBOM/signature akışı tanımlı değildir. Build context yerel node_modules gibi gereksiz öğeleri de alabilir. Bunlar dağıtım gözden geçirme alanlarıdır; çalışan production kurulumu görülmemiştir.

### F10 — Derleyici ve tip güvenliği borcu (orta/düşük)

İlk Release build **71 uyarı**, ağırlıkla nullable CS8618/CS8601/CS8604. RoleManagementController'da nullable reflection dönüşleri, testte xUnit2031 vardır. Panel TypeScript strict/strictNullChecks etkin değildir.

Öneri: DTO required/nullability ve response hata modeli; warning sayısını azaltma, sonrasında warning gate. Sadece uyarıları bastırmak çözüm sayılmamalıdır.

### F11 — Gerçek browser ve işletim doğrulaması eksik (orta)

Panel suite 16 testtir; native dialog stub/BroadcastChannel kapalıdır. CSS, focus/top-layer, gerçek cookie/HTTPS ve tam browser/API E2E bununla doğrulanmaz. Backend suite güçlü güvenlik senaryoları kapsar; ilk ortamda DB olmadığından execution henüz yapılmamıştı. Sonraki kapsamlı test kaydı aşağıda güncellenir.

### F12 — Eksik ürün yetenekleri (plan konusu)

Self-service e-posta doğrulama/parola kurtarma, MFA challenge/enrollment/recovery, tam audit ekranı, uygulama/tenant izin kataloğu, OIDC/OAuth/SSO, asimetrik key/JWKS, harici API iptal sözleşmesi yoktur. Bunlar mevcut endpoint bug'ı değil ürün kapsamı boşluğudur. Eklenmeleri tasarım, migration, operasyon ve tüketici uyumluluğu çalışması gerektirir.

<a id="verification"></a>
## Doğrulama kaydı

### İlk analiz turu

| Kontrol | Sonuç / sınır |
|---|---|
| .NET | SDK 10.0.402 |
| Release solution build | Başarılı; 0 hata, 71 uyarı |
| Backend test keşfi | 18 vaka; `--list-tests` başarılı, execution değildir |
| İlk backend execution | DB/Docker/psql bulunmadığı için yapılmadı; mevcut DB kullanılmadı |
| Panel ortamı | Node 26.13.0 / npm 11.20.0 |
| npm ci | Başarılı; lockfile değişmedi |
| Panel build | Başarılı; TypeScript + Vite; JS 297.45 kB, gzip 93.05 kB |
| Panel lint | Başarılı |
| Panel test | 16/16 geçti; 0 fail/skip |
| npm audit | Başarısız; 12 bulgu (7 moderate, 5 high) |
| Docker Compose/Nginx runtime | Docker yok; çalıştırılmadı |
| Gerçek browser/canlı production | Çalıştırılmadı |
| Son uzak CI | Sorgulanmadı |

### Kapsamlı test ve refactoring sonrası

Bağımsız PostgreSQL 16.16 cluster üzerinde başlangıçtaki 18 vaka çalıştırıldı: **18/18 başarılı, 0 başarısız, 0 atlanan**, süre 1 dakika 31 saniye. Runner exit 0; yalnız kendisine ait cluster kapatıldı. Mevcut DB kullanılmadı. Kanıt: `artifacts/test-runs/20261126T104119413Z-b784af4260cb4a18a38d519cf1599554/results/backend.trx`, `tests.log`, `summary.json`.

Ek kontrol: `dotnet list AuthServer.sln package --vulnerable --include-transitive --no-restore`, altı projede güncel kaynaklarda bilinen güvenlik açığı bildirilen paket bildirmedi (exit 0). Bu sonuç 26.11.2026 paket bildirimi verisiyle sınırlıdır. Paket düzeltmesi sonrası Vite 8.3.8/typescript-eslint 8.74.0 ve uyumlu transitif sürümlerle **npm audit 12→0**, panel build/lint ve **16/16 test** başarılıdır. Package.json aralıkları ve runtime React/Router sürümleri korunmuştur. Lock-only güncelleme force/override kullanmaz. Backend regresyon ve düzeltme sonrası sonuçlar aşağıda kayıtlıdır.

### İlk refactoring dilimi: değişiklik ve regresyon kanıtı

- **F01 düzeltildi:** Namespace taraması yerine `IManagementCommand`; mevcut 11 yönetim komutu, RevokeAll dahil aynı Serializable transaction/generic audit kapsamına girdi. RevokeAll gerçek aktör IP'sini kaydeder.
- **F02 düzeltildi:** Login session'ı stage eder; aynı scoped DbContext'i kullanan AuditService session+audit'i tek SaveChanges ile yazar. Credential/lockout kontrolü yönetim transaction'ına alınmadı.
- **F03 düzeltildi:** Yalnız package-lock güncellendi. Runtime React 19.3.0/Router 7.18.3 ve package.json aralıkları korundu; değişen/eklenen 47 paket kaydının tamamı dev bağımlılığıdır.
- **Test kapsamı:** Sekiz yeni backend testi, toplam 26 vaka. Audit failure rollback, başarılı JWT/audit, JWT+cookie revoke-all/bağımsız aktör, rol rollback, kullanıcı/rol/izin/parola yaşam döngüsü, 17 yönetim endpoint'inde anonim 401/Basic 403 ve 31. auth isteğinde 429.
- **Test altyapısı:** Windows runner yeni atılabilir PG 16 cluster kullanır. ASCII 8.3 yol/Windows process ayrıntıları ele alındı; timeout'ta yalnız kendi kontrol process ağacı durdurulur. Bağımsız review ve kısa parent/child diagnostic ile timeout cleanup doğrulandı.

| Aşama | Sonuç | Kanıt |
|---|---|---|
| Başlangıç backend | 18/18 geçti; 0 fail/skip | `artifacts/test-runs/20261126T104119413Z-b784af4260cb4a18a38d519cf1599554/results/backend.trx` |
| Düzeltme öncesi iki regresyon | **2/2 beklenen başarısızlık**: revoke-all 500 yerine 200; JWT audit hatasında fazladan session | `artifacts/test-runs/20261126T104600219Z-07daacae8d0e49b18d00e0bfd9ee1382/results/backend.trx` |
| Düzeltme sonrası aynı iki regresyon | **2/2 geçti**, 31 sn; runner exit 0, cleanup uyarısı yok | `artifacts/test-runs/20261126T105047357Z-15991c753160494ab66f0513fba57afd/results/backend.trx` |
| Panel son kontrol | Build/lint ve 16/16 test geçti; npm audit 0 | Node 26.13.0/npm 11.20.0; 26.11.2026 |

A turunun son tam koşusu **26/26 backend testi başarılı, 0 başarısız/0 atlanan**, süre 1 dakika 45 saniye; runner exit 0 ve pg_ctl status 0/stop 0. Kanıt: `artifacts/test-runs/20261126T105241034Z-bc2339bc8ee943139e4230a36e415420/results/backend.trx`. A turunda panelin 16 testiyle birlikte toplam **42/42 test** başarılıydı; güncel B turu sonuçları sonraki bölümde tutulur. Runner'ın occupied-port guard'ı da gerçek çalıştırmada doğrulandı: 55432 kendi test listener'ıyla doluyken exit 1 ve beklenen hata, yeni run dizini 0; listener bozulmadı, sonra kapatıldı. Son durumda portta listener, postmaster.pid veya geçici parola dosyası yoktur. Kanıt: `artifacts/runner-guards/occupied-port.json`. Bütün testler geçse de F06 ve F08–F12'deki açık ürün/işletim alanları tamamlanmış sayılmaz; [refactoring planında](REFACTORING_PLAN.md) ayrı kabul kriterleriyle sıralanmıştır.

Geçici dosya temizliği: Yalnız bu çalışmada üretilen PostgreSQL ZIP/partial extraction ve kapalı test PGDATA dizinleri için mutlak yol, symlink/junction ve postmaster.pid preflight'ı yapıldı. Toplu işlem ve ardından tek sabit LiteralPath ile daraltılmış recursive silme, otomatik onay incelemesinde `blocked by policy` yanıtıyla reddedildi; ek gerekçe verilmedi. Silme zorlanmadı, dosyalar Git dışında `artifacts/` altında korundu. Test sunucusu kapalıdır; TRX/log/summary ve binary provenance kayıtları mevcuttur. Kalan gereksiz geçici dosya toplamı 1.070.302.605 bayttır (yaklaşık 1,07 GB): partial extraction 396,89 MiB, ZIP 360,56 MiB ve beş kapalı PGDATA toplam 263,28 MiB. Kesin envanter: `artifacts/runner-guards/cleanup-remaining.json`.


### İkinci refactoring turu — B1–B4

Kullanıcı devam talebiyle B aşaması uygulandı; JWT mutlak üst süresi için açıkça **30 gün** seçildi. [Tasarım kaydı](SESSION_SECURITY_DESIGN.md) güvenlik sürümü, kilit sırası, deadline ve migration kararını açıklar.

| Konu | Son davranış |
|---|---|
| F04 bootstrap | Migration ayrı; tek SeedAsync, eksik roller+yeni hesap/üyelik Serializable transaction'da. Var olan SuperAdmin değişmez; non-admin email eşleşmesi açık hata ve rollback. |
| F05 güvenlik sürümü | Refresh kaydı issuance stamp'ini taşır; dış Identity stamp değişimi eski access/cookie/refresh'i reddeder. Tüm-session iptali/replay/deactivation epoch döndürür. |
| Giriş/refresh yarışları | User row FOR UPDATE, birden çok kullanıcıda Guid sırası, kilit sonrası taze user reload. Başarılı parola kontrolündeki stamp kilit sonrasına yükseltilmez. |
| B3 aile sınırı | FamilyId/FamilyCreatedAt/AbsoluteExpiresAt rotation'da sabit; access/refresh normal ömrü bu bitişe clamp. Varsayılan 30 gün; cookie 12 saat korunur. |
| F07 audit | Her başarılı/no-op yönetim işleminde tek hedefli event; schemaVersion1/server correlation/outcome/changes, alan whitelist'i ve recursive secret redaction, gerçek request cancellation. Tarihsel loglar değiştirilmez. |
| Migration | SessionSecurityPolicy eski bütün session'ları kapatır/hash'leri siler; kullanıcı/rol/audit verisi korunur. Eski credential yeni girişe replay uygulayamaz. Down da yeniden giriş gerektirir. |

Önce düşürülen testler:

- B1+B2: **7 beklenen başarısız + 2 başarılı**, 41 saniye. Bootstrap kısmi kayıt/eksik credential, mevcut non-admin email ve stamp/login yarışları yeniden üretildi. Kanıt: `artifacts/test-runs/20261126T111047646Z-ffeefa379b15482793dd475b7c03eede/results/backend.trx`.
- B4: **2/2 beklenen başarısızlık**; create iki audit, AssignRoles boş hedef. Kanıt: `artifacts/test-runs/20261126T111235938Z-141b8ba2559147f8b8639c8ceccf38b2/results/backend.trx`.
- Düzeltme sonrası aynı kritik kapı: **11/11 başarılı**, 45 saniye. Kanıt: `artifacts/test-runs/20261126T112327308Z-210fe8b216ae4d51a3e413cefac0ec56/results/backend.trx`.

İlk kapsamlı B koşusu **66/68 geçti**, iki kontrollü refresh/iptal yarışında PostgreSQL 40001'in 500 dönmesi yakalandı. Gerçek TRX exception zinciri `InvalidOperationException → DbUpdateException → PostgresException(40001)` idi; middleware yalnız tek InnerException'a bakıyordu. Bütün zinciri denetleyen dar sınıflama eklendi; diğer audit hataları 500 kaldı. Kanıt: `artifacts/test-runs/20261126T112535219Z-c6730ccd9a9d44db83ac0f3451d60270/results/backend.trx`.

Bu düzeltme sonrası iki refresh yarış testi **2/2 başarılı**, 16 saniye. 409 dalı başarısız işlem ve açık kullanıcı retry'ı olarak sınanır; üretimde otomatik retry yoktur. Kanıt: `artifacts/test-runs/20261126T113104820Z-5a6a4e45b6fe41cd83e732c6d0f45dc5/results/backend.trx`.

Panel build/lint ve **16/16 test** geçti; npm audit **0**, NuGet altı proje vulnerable taraması **0 bildirim**. Uygulama paket sürümleri B turunda değiştirilmedi. Panel kaydı 26.11.2026 14:24 (İstanbul); NuGet çıktısı `artifacts/nuget-audit-b-stage.log`.

Son tam B koşusu **68/68 başarılı, 0 başarısız/0 atlanan**, süre **3 dakika 48 saniye**, runner exit **0**, pg_ctl status/stop **0**. Panelin 16 testiyle toplam **84/84 test** başarılıdır. Son runtime kaynakları bu koşudan sonra değiştirilmedi. Kanıt: `artifacts/test-runs/20261126T113213485Z-a1ebe6b3fbed4becae03f4a2aa0a30b4/results/backend.trx`; aynı run'da `tests.log`, `summary.json`, PostgreSQL logları ve `verification.json` korunur. Son kontrol: cleanupWarnings boş; postmaster.pid/geçici parola dosyası yok; 55432 listener sayısı 0. Kalan nullable derleyici borcu C3'tedir; Docker/Nginx runtime ve gerçek browser E2E bu yerel doğrulama kapsamında çalıştırılmamıştır. B turunda önceki geçici dosya temizliği politika engeli yeniden denenmemiştir; yeni test sonuçları da Git dışında artifacts altında tutulur.

## Önceki belgeler nasıl okunmalı?

`REFACTORING.md`, `ADMIN_PANEL.md` ve `REFRESH_SESSION_ANALYSIS.md` kendi commit/tarihindeki çalışmaların tarihsel kanıtlarıdır. Önceki “yerelde SDK yoktu”, “0 npm audit” veya eski test sayıları güncel ortam sonucu değildir. Güncel kullanım için PROJECT_GUIDE, güncel kanıt/bulgu için bu rapor ve sonraki refactoring planı esas alınmalıdır.
