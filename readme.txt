================================================================================
DATABASE UPDATE AND RUN INSTRUCTIONS / YONETIM DETAYLARI
================================================================================

[EN] ENGLISH INSTRUCTIONS
--------------------------------------------------------------------------------
Docker Usage:

1. To run the API Server:
docker build -t authserver-api .
docker run -d --name authserver-api -p 8080:8080 authserver-api

2. To run the new Frontend Admin Panel (Web Application):
cd AuthServer-AdminPanel
docker build -t authadmin-panel .
docker run -d --name authadmin-panel -p 3000:80 authadmin-panel

NOTE FOR MIGRATIONS:
If you are running the application via Docker, you DO NOT need to run any update database commands! The database migration is automatically applied on container startup via Program.cs.

--------------------------------------------------------------------------------

[TR] TURKCE TALIMATLAR
--------------------------------------------------------------------------------
Docker Kullanim:

1. API Sunucusunu Calistirmak İcin:
docker build -t authserver-api .
docker run -d --name authserver-api -p 8080:8080 authserver-api

2. Yeni Yonetim Panelini (Frontend) Calistirmak İcin:
cd AuthServer-AdminPanel
docker build -t authadmin-panel .
docker run -d --name authadmin-panel -p 3000:80 authadmin-panel

MIGRATION (VERITABANI) NOTU:
Eger uygulamayi Docker uzerinden calistiriyorsaniz, komut satirindan guncelleme calistirmaniza GEREK YOKTUR! Veritabani guncellemesi konteyner ayaga kalkarken otomatik olarak yapilmaktadir.

================================================================================
