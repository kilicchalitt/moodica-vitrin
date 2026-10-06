# Moodica web uygulaması (ASP.NET Core, .NET 10).
# Derleme:  docker build -t moodica-web .
# Yerelde uygulama + veritabanı:  docker compose --profile app up -d --build

# ---- Derleme aşaması ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyası kopyalanır: paketler değişmedikçe restore katmanı önbellekten gelir.
COPY bitirme/bitirme/bitirme.csproj bitirme/bitirme/
RUN dotnet restore bitirme/bitirme/bitirme.csproj

COPY bitirme/bitirme/ bitirme/bitirme/
RUN dotnet publish bitirme/bitirme/bitirme.csproj --configuration Release --no-restore --output /app

# ---- Çalışma aşaması (yalnızca çalışma zamanı, SDK yok) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
# Yüklenen dosyalar için klasör; compose'da kalıcı birim bağlanır. Uygulama kullanıcısı yazabilmeli.
RUN mkdir -p /app/App_Data && chown $APP_UID /app/App_Data
# Uygulama root yerine imajın hazır, yetkisiz kullanıcısıyla çalışır.
USER $APP_UID
ENTRYPOINT ["dotnet", "bitirme.dll"]
