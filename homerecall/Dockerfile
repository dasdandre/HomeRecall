# STAGE 1: Build der App mit dem .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-env
WORKDIR /src

# Kopiere die Projektdatei und restore die Abhängigkeiten
# (Dies nutzt den Docker-Cache optimal aus)
COPY *.csproj ./
RUN dotnet restore

# Kopiere den Rest und erstelle das Release
COPY . ./
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# STAGE 2: Erstellung des eigentlichen Add-ons
ARG BUILD_FROM
FROM $BUILD_FROM

# Umgebungsvariablen für .NET
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    ASPNETCORE_URLS=http://+:8080

# Installiere die Laufzeit-Abhängigkeiten für .NET auf Alpine
# 'icu-libs' wird für korrekte Globalisierung benötigt
RUN \
    apk add --no-cache \
        icu-libs \
        libgcc \
        libintl \
        libssl3 \
        libstdc++ \
        zlib

# Erstelle das App-Verzeichnis und kopiere die gebaute App aus Stage 1
WORKDIR /app
COPY --from=build-env /app/publish .

# Home Assistant Add-on benötigt meistens keine speziellen Entrypoints,
# CMD reicht hier völlig aus.
CMD [ "dotnet", "HomeRecall.dll" ]