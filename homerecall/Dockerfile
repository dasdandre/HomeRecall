# ARG muss ganz nach oben, damit es global verfügbar ist
ARG BUILD_FROM
# STAGE 1: Build der App
# Wir nutzen --platform=$BUILDPLATFORM damit der Build auf dem Host (amd64) läuft und Cross-Compilieren kann (schneller als QEMU)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-env
ARG TARGETARCH
WORKDIR /src
# Projektdaten kopieren und Restore
COPY *.csproj ./
RUN dotnet restore
# Rest kopieren und bauen
COPY . ./
RUN case "${TARGETARCH}" in \
      "amd64") RID="linux-musl-x64" ;; \
      "arm64") RID="linux-musl-arm64" ;; \
      "arm")   RID="linux-musl-arm" ;; \
      *)       echo "Unsupported arch: ${TARGETARCH}"; exit 1 ;; \
    esac && \
    echo "Building for RID: $RID" && \
    dotnet publish -c Release -o /app/publish -r $RID --self-contained true -p:PublishTrimmed=true

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
# KEIN .NET mehr installieren, da Self-Contained!
RUN \
apk add --no-cache \
icu-libs \
libgcc \
libintl \
libssl3 \
libstdc++ \
zlib \
bash

WORKDIR /app
COPY --from=build-env /app/publish .
# S6 Service Skript erstellen
# Home Assistant Addons nutzen S6 Overlay zur Prozessverwaltung.
# Wir müssen einen Service erstellen, damit S6 dotnet startet.
COPY run.sh /
RUN chmod a+x /run.sh
CMD [ "/run.sh" ]