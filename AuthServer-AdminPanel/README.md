# AuthServer yönetim paneli

Kurulum, API yapılandırması ve geçiş talimatları için [ana README](../README.md) dosyasına bakın.

```bash
npm ci
npm run dev
npm run build
npm run lint
npm test
```

Geliştirme istekleri `/api` üzerinden `http://localhost:8080` hedefine proxy edilir. Docker'da Nginx aynı origin altında API'ye yönlendirir. Token'lar yalnızca bellekte tutulur; sayfa yeniden yüklendiğinde yeniden giriş gerekir.
