# Cok asamali derleme: son imajda SDK degil yalnizca calisma zamani bulunur.
# Baglam olarak depo koku kullanilir (bkz. docker-compose.yml -> build.context),
# cunku Directory.Packages.props ve katman projeleri kokten cozulur.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Once yalnizca proje dosyalari kopyalanir: kaynak kod degistiginde
# restore katmani onbellekten gelir, her derlemede paket indirilmez.
# .editorconfig da kopyalanmali: analyzer kurallari ve gerekceli bastirmalar
# orada tanimli. Atlanirsa imaj icindeki derleme yereldekinden farkli davranir
# ve TreatWarningsAsErrors nedeniyle yalnizca Docker'da kirilir.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Learnier.Domain/Learnier.Domain.csproj src/Learnier.Domain/
COPY src/Learnier.Application/Learnier.Application.csproj src/Learnier.Application/
COPY src/Learnier.Infrastructure/Learnier.Infrastructure.csproj src/Learnier.Infrastructure/
COPY src/Learnier.WebApi/Learnier.WebApi.csproj src/Learnier.WebApi/
RUN dotnet restore src/Learnier.WebApi/Learnier.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/Learnier.WebApi/Learnier.WebApi.csproj \
    -c Release \
    -o /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Saglik kontrolu icin curl gerekiyor: aspnet calisma zamani imajinda ne curl
# ne de wget bulunuyor, bu yuzden HEALTHCHECK komutu sessizce hep basarisiz olur
# ve container sonsuza kadar unhealthy kalir.
# Kurulum root gerektirdigi icin ayricaliksiz kullaniciya gecmeden once yapilir.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Root olarak calistirma: imajdaki hazir ayricaliksiz kullanici kullanilir.
USER $APP_UID

COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Learnier.WebApi.dll"]
