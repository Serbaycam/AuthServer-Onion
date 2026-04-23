================================================================================
DATABASE UPDATE AND RUN INSTRUCTIONS / YONETIM DETAYLARI
================================================================================

[EN] ENGLISH INSTRUCTIONS
--------------------------------------------------------------------------------
Docker Usage (Recommended via Docker Compose):

To start the Database, API Server, and Admin Panel together:
docker compose up -d --build

To stop the services:
docker compose down

* The database will be available at localhost:5432
* pgAdmin will be available at http://localhost:5050 (admin@admin.com / admin)
* The API Server will be available at http://localhost:8080
* The Admin Panel will be available at http://localhost:3000

NOTE FOR MIGRATIONS:
If you are running the application via Docker Compose, you DO NOT need to run any update database commands! The database migration is automatically applied on container startup via Program.cs.

--------------------------------------------------------------------------------

[TR] TURKCE TALIMATLAR
--------------------------------------------------------------------------------
Docker Kullanim (Onerilen Yontem: Docker Compose):

Veritabani, API Sunucusu ve Yonetim Panelini tek seferde baslatmak icin:
docker compose up -d --build

Servisleri durdurmak icin:
docker compose down

* Veritabani localhost:5432 adresinde calisacaktir.
* pgAdmin http://localhost:5050 adresinde calisacaktir (admin@admin.com / admin).
* API Sunucusu http://localhost:8080 adresinde calisacaktir.
* Yonetim Paneli http://localhost:3000 adresinde calisacaktir.

MIGRATION (VERITABANI) NOTU:
Eger uygulamayi Docker Compose uzerinden calistiriyorsaniz, komut satirindan guncelleme calistirmaniza GEREK YOKTUR! Veritabani guncellemesi konteyner ayaga kalkarken otomatik olarak yapilmaktadir.

================================================================================
