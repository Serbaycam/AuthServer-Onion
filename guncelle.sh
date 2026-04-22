docker rm -f authserver-api
docker build -t authserver-api .
docker run -d --name authserver-api -p 8080:8080 --network my-postgres-env_default authserver-api
#dotnet ef migrations add YeniTabloEklemesi --project AuthServer.Identity.Persistence --startup-project AuthServer.Identity.API
#dotnet ef database update --project AuthServer.Identity.Persistence --startup-project AuthServer.Identity.API