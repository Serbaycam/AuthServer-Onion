# 1. Aşama: Çalışma Zamanı (Runtime) İmajının Ayarlanması
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# 2. Aşama: Derleme (Build) ve Bağımlılıkların Yüklenmesi
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Proje dosyalarını (.csproj) kopyalıyoruz
COPY ["AuthServer.Identity.API/AuthServer.Identity.API.csproj", "AuthServer.Identity.API/"]
COPY ["AuthServer.Identity.Application/AuthServer.Identity.Application.csproj", "AuthServer.Identity.Application/"]
COPY ["AuthServer.Identity.Persistence/AuthServer.Identity.Persistence.csproj", "AuthServer.Identity.Persistence/"]
COPY ["AuthServer.Identity.Infrastructure/AuthServer.Identity.Infrastructure.csproj", "AuthServer.Identity.Infrastructure/"]
COPY ["AuthServer.Identity.Domain/AuthServer.Identity.Domain.csproj", "AuthServer.Identity.Domain/"]

# Bağımlılıkları (paketleri) indiriyoruz (restore işlemi)
RUN dotnet restore "AuthServer.Identity.API/AuthServer.Identity.API.csproj"

# Kalan tüm dosyaları ve kaynak kodları kopyalıyoruz
COPY . .

# Uygulamayı derlemek için API projesinin bulunduğu klasöre geçiş
WORKDIR "/src/AuthServer.Identity.API"

# Uygulamayı Release modunda derliyoruz
RUN dotnet build "AuthServer.Identity.API.csproj" -c Release -o /app/build

# 3. Aşama: Yayınlama (Publish) İşlemi
FROM build AS publish
RUN dotnet publish "AuthServer.Identity.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Aşama: Son İmajın Oluşturulması (Sadece Çalıştırılabilir Dosyalar)
FROM base AS final
WORKDIR /app

# Konteynerin sadece HTTP üzerinden 8080 portunu dinlemesini zorunlu kılıyoruz
# Bu sayede HTTPS sertifikası hatası (dev-certs) almayı önlüyoruz
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Yayınlanan (.dll vb.) çıktıları publish aşamasından son imaja kopyalıyoruz
COPY --from=publish /app/publish .

# Uygulama başladığında hangi komutun çalışacağını belirliyoruz
ENTRYPOINT ["dotnet", "AuthServer.Identity.API.dll"]
