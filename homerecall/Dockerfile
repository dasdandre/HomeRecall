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
RUN dotnet publish -c Release -o /app/publish

# STAGE 2: Das eigentliche Add-on
FROM $BUILD_FROM

# S6-Overlay Setup
# Wenn das Base-Image bereits s6 verwendet (was bei HA Addons der Fall ist), 
# müssen wir sicherstellen, dass wir es korrekt nutzen oder nicht stören.
# Der Fehler "s6-overlay-suexec: fatal: can only run as pid 1" deutet darauf hin, 
# dass s6 versucht zu starten, aber nicht PID 1 ist, oder wir versuchen Befehle falsch auszuführen.
# HA Addons starten standardmäßig via s6 init.

# Umgebungsvariablen für .NET
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    ASPNETCORE_URLS=http://+:8080 \
    persist_path="/data" \
    backup_path="/data/backups"

# Runtime-Abhängigkeiten installieren
RUN \
    apk add --no-cache \
        icu-libs \
        libgcc \
        libintl \
        libssl3 \
        libstdc++ \
        zlib \
        wget \
        bash

# Install .NET Runtime
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

WORKDIR /app
COPY --from=build-env /app/publish .

# S6 Service Skript erstellen
# Home Assistant Addons nutzen S6 Overlay zur Prozessverwaltung.
# Wir müssen einen Service erstellen, damit S6 dotnet startet.
COPY run.sh /
RUN chmod a+x /run.sh

CMD [ "/run.sh" ]