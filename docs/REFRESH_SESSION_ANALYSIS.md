# Refresh ve eski oturum incelemesi

İncelenen master: `e39242fcb6058e469a4f740c2f77f2e67c929494` (AdminPanel PR #2 sonrası).

## Sonuç

Normal refresh eski kaydı açık bırakmıyor. RefreshTokens tablosundaki eski kayıt silinmiyor; `RevokedDate` dolduruluyor ve `ReplacedByToken` yeni refresh sırrının özetini gösteriyor. Bu tarihçe replay denetimi için gerekli. Aktif oturum sorgusu yalnızca iptal edilmemiş ve süresi dolmamış kayıtları getiriyor. AuthServer API, access token içindeki `sid` üzerinden tam o kaydı denetlediği için eski access token'ın kriptografik süresi dolmamış olsa bile erişimi reddediyor.

Ancak refresh sonrasındaki **oturum kapatma** yollarında iki gerçek hata vardı. Ayrıca MFA kontrolü refresh yolunda eksikti.

| Senaryo | Değişiklik öncesi kanıt | Düzeltme |
|---|---|---|
| Süresi dolmuş access token ile geçerli refresh kullanımı | Test başarılı: yeni token alınır; eski access token 401; eski kayıt aktif listede yok. Ayrı bir login açık kalır. | Doğru davranış korunuyor. |
| Panel satırı yüklendikten sonra token bir/iki kez yenileniyor; yönetici eski satırı kapatıyor | Kill isteği 200 döndü, yeni access token hâlâ 200 aldı. | İstenen kayıttan replacement zincirinin sonuna gidilir; kullanılabilir ardılı da iptal edilir. |
| JWT istemcisinin logout isteği refresh öncesindeki sırla geliyor | Logout 200 döndü, yeni access token hâlâ 200 aldı. | Aynı zincir iptali logout'a uygulanır; bağımsız girişlere dokunulmaz. |
| MFA girişten sonra etkinleştiriliyor | Refresh beklenen 400 yerine 200 döndü. | MFA gerektiren kullanıcıda tek faktör refresh ve JWT erişimi reddedilir. |

İlk regresyon çalıştırması gerçek PostgreSQL üzerinde **15 başarılı, bu üç açık nedeniyle 3 başarısız** test verdi. Bu yalnızca kaynak okumasına dayalı bir tahmin değildir: açıklar HTTP yanıtlarıyla yeniden üretildi. Düzeltme sonrası sonuçlar PR'ın son CI kontrolünden izlenir.

## Neden kapatma başarısız oluyordu?

1. İstemci/panel A kaydını biliyor.
2. Refresh, A'yı iptal edip B'yi üretiyor. Bir sonraki refresh B'yi iptal edip C'yi üretebilir.
3. Eski kod A'nın iptal edilmiş olduğunu görünce hemen başarı döndürüyordu; B/C incelenmiyordu.
4. Kullanıcı “oturum kapandı” mesajını alırken aynı girişin güncel token'ı çalışmaya devam ediyordu.

Ortak `RefreshTokenRevocation.RevokeChainAsync` bu erken başarı dönüşünü kaldırır. İptal işlemi sadece aynı kullanıcıya ait replacement bağlantılarını takip eder. Döngülü/eksik bir zinciri başarı saymaz. Değişiklikler tamamı bir kez `SaveChangesAsync` ile kaydedilir; mevcut `RevokedDate` concurrency denetimi, aynı anda yapılan yeni refresh'in iptal işlemini sessizce geçersiz kılmasını engeller. Yönetici komutunun mevcut serializable transaction ve audit davranışı korunur. Çakışma varsa başarılı logout taklidi yapmak yerine 409 alınabilir; istemci işlemi tekrar denemelidir.

Tekrar kapatma idempotenttir. Eski refresh token'ı **yenilemek için yeniden kullanma** ise farklıdır: mevcut güvenlik politikası gereği kullanıcının bütün açık oturumlarını iptal eder. Logout bu replay yolu üzerinden çalışmaz; yalnızca hedef girişin ardıllarını kapatır.

## Panelde “açık kalmış” görünmesinin diğer nedenleri

- Paneldeki aktif liste 30 saniyede bir güncellenir; başarılı refresh sonrası eski satır bir sonraki sorguya kadar görünebilir. Yenile düğmesi anlık sorgu yapar. Yeni test sunucunun döndürdüğü listeyi de kontrol eder.
- Aynı hesabın ayrı login çağrıları bağımsız oturum üretir. Bir login'in refresh'i diğer cihazı/girişi kapatmaz. Kullanıcının tüm oturumlarını kapatmak için revoke-all gerekir.
- Access token süresinin dolması, refresh hakkının da sona ermesi değildir. Access süresi dolsa bile geçerli refresh kaydı aktif oturum sayılır; bu, yeniden şifre sormadan yenilemeyi mümkün kılar.
- PR #2 sonrasında yönetim paneli `/api/admin-session` cookie akışını kullanır; JWT refresh yapmaz. Aynı hesabın panel cookie oturumu ile başka uygulamanın JWT oturumu birlikte listelenebilir. Önceki sürümde oluşturulmuş bağımsız login kayıtları da süreleri dolana/iptal edilene kadar kalır.
- AuthServer dışındaki bir API yalnızca JWT imzası ve `exp` doğruluyorsa, bu sunucudaki iptali öğrenemez; eski token kendi süresine kadar orada geçerli kalabilir. Bu repo dışındaki tüketicilerin kodu ve canlı veritabanı bu incelemeye dahil değildir. Anında ortak iptal için o API'lerin de oturum durumunu doğrulaması veya ortak, tutarlı revocation mekanizması kullanması gerekir.

## Dağıtım ve doğrulama

Yeni tablo/migration yoktur. Var olan SHA-256 replacement zincirleri kullanılır; eski kayıtlar topluca silinmez. API'nin güncellenmesi kapatma/MFA düzeltmesini etkinleştirir. Anahtar veya şifre rotasyonu bu değişikliğin gereği değildir.

Backend testleri normal/expired refresh, iki ardışık rotation sonrası eski panel satırını kapatma, eski sırla logout, idempotent retry, bağımsız oturumun korunması ve MFA'yi kapsar. Önceki JWT/cookie/CSRF/migration/concurrency testleri de çalışır.

Önceki panel CI'ında saptanan 10 npm bağımlılık uyarısı da uyumlu sürüm güncellemeleriyle giderildi; yerel audit sonucu 0'dır. React Router 7.18.3, Vite 8.3.0 ve ilgili bağımlılıklar lockfile'da sabitlenir. Build/lint ve 16 panel testi güncellemeden sonra başarılıdır. CI'a yeni güvenlik bildirimlerini yakalayan `npm audit --audit-level=low` kontrolü eklendi. Audit sonucu çalıştırıldığı tarihteki bilinen bildirimlerle sınırlıdır.
