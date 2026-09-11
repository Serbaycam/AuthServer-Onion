# AuthServer yönetim paneli

Panel, sayfa yenilendiğinde `/api/admin-session` ile sunucu oturumunu geri yükler. Kontrol tamamlanmadan login sayfasına yönlendirme yapmaz. Kimlik cookie’si HttpOnly’dir; access/refresh token tarayıcı JavaScript’ine veya localStorage’a verilmez. Varsayılan oturum süresi 12 saattir.

Kurulum için [ana README](../README.md), bulgular, oturum sözleşmesi, HTTPS ve anahtar kalıcılığı için [panel belgesi](../docs/ADMIN_PANEL.md).

```bash
npm ci
npm run dev
npm run build
npm run lint
npm test
```

Geliştirme `/api` isteklerini `http://localhost:8080` hedefine proxy eder. Yalnızca yerel HTTP geliştirmede API’ye `AdminSession__RequireHttps=false` verin. Docker’da Nginx aynı origin altında API’ye yönlendirir. Panel ve API birlikte güncellenmelidir.

`npm test` gerçek API istemcisi ile React ekranlarının regresyon testlerini çalıştırır; DOM testleri gerçek tarayıcı veya CSS görsel testi değildir. Manuel ekran kontrolü için `npm run build` ardından `node tests/preview-server.mjs` kullanılabilir. Bu sunucu yalnızca loopback üzerinde, atılabilir ve önceden oturum açılmış örnek veri sunar; gerçek hesap veya API kullanmaz ve production paketine dahil edilmez.
