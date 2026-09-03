# 1. Aþama: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Proje dosyasýný kopyala ve baðýmlýlýklarý geri yükle (restore)
COPY ["SmartAssistant.API.csproj", "./"]
RUN dotnet restore "./SmartAssistant.API.csproj"

# Kalan tüm kaynak kodlarý kopyala ve Release modunda derle
COPY . .
RUN dotnet publish "./SmartAssistant.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Aþama: Runtime & Final Image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartAssistant.API.dll"]