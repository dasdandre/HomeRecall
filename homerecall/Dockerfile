# ARG muss ganz nach oben, damit es global verfügbar ist
ARG BUILD_FROM

# STAGE 1: Build der App
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-env
WORKDIR /src

# Projektdaten kopieren und Restore
COPY *.csproj ./
RUN dotnet restore

# Rest kopieren und bauen
COPY . ./
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# STAGE 2: Das eigentliche Add-on
FROM $BUILD_FROM

# Umgebungsvariablen für .NET
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    ASPNETCORE_URLS=http://+:8080

# Runtime-Abhängigkeiten installieren
RUN \
    apk add --no-cache \
        icu-libs \
        libgcc \
        libintl \
        libssl3 \
        libstdc++ \
        zlib

WORKDIR /app
COPY --from=build-env /app/publish .

CMD [ "dotnet", "HomeRecall.dll" ]