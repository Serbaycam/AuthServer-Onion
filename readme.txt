================================================================================
DATABASE UPDATE INSTRUCTIONS / VERITABANI GUNCELLEME TALIMATLARI
================================================================================

[EN] ENGLISH INSTRUCTIONS
--------------------------------------------------------------------------------
To apply the latest Entity Framework migrations and update the databases, follow these steps:

1. Open your terminal or command prompt.
2. Navigate to the root directory of the solution (the folder containing 'AuthServer.sln').
3. Run the following commands to update each microservice's database:

# For Identity API (Users, Roles, Permissions):
dotnet ef database update --project AuthServer.Identity.API

NOTE FOR DOCKER USERS: 
If you are running the application via Docker, you DO NOT need to run these commands! The database migration is automatically applied on container startup via Program.cs.

--------------------------------------------------------------------------------

[TR] TURKCE TALIMATLAR
--------------------------------------------------------------------------------
En son Entity Framework migrasyonlarini uygulamak ve veritabanlarini guncellemek icin su adimlari izleyin:

1. Terminalinizi veya komut satirinizi acin.
2. Solution dosyasinin ('AuthServer.sln') bulundugu ana dizine gidin.
3. Her bir mikroservisin veritabanini guncellemek icin asagidaki komutlari calistirin:

# Identity API icin (Kullanicilar, Roller, Yetkiler):
dotnet ef database update --project AuthServer.Identity.API

DOCKER KULLANICILARI ICIN NOT:
Eger uygulamayi Docker uzerinden calistiriyorsaniz, bu komutlari calistirmaniza GEREK YOKTUR! Veritabani guncellemesi (migration) konteyner ayaga kalkarken Program.cs uzerinden otomatik olarak yapilmaktadir.

================================================================================
NOTE / NOT:
If you receive a "command not found" error, ensure that the EF Core tool is installed:
"Komut bulunamadi" hatasi alirsaniz, EF Core aracinin yuklu oldugundan emin olun:

dotnet tool install --global dotnet-ef
================================================================================
