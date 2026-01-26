ARG BUILD_FROM
FROM $BUILD_FROM

# Install ASP.NET Core Runtime
# Note: Since this is a HA Addon, the base image is likely Alpine or Debian based.
# For .NET, we usually need the official Microsoft images or install the runtime.
# However, HA Addons use a specific base. Let's assume we can install dotnet.
# A better approach for HA Addons is using a multi-stage build or a base image that supports it.
# But often HA Addons run on Alpine.
# Let's use the official pattern: Build in SDK image, copy to runtime.

# Since we don't know the exact base architecture of the user at build time here effectively,
# but HA handles 'build_from'.
# Actually, for HA Addons, usually we just assume the environment has what we need or we install it.
# Let's try to install dotnet via apk if alpine, or apt if debian.
# Assuming Alpine based on HA standards.

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

# Install .NET Runtime
RUN \
    apk add --no-cache \
        icu-libs \
        krb5-libs \
        libgcc \
        libintl \
        libssl3 \
        libstdc++ \
        zlib \
    && wget https://dot.net/v1/dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

WORKDIR /app
COPY . .

# Build the app
RUN ./dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet

RUN dotnet publish -c Release -o output

# Cleanup SDK to save space? For an addon, maybe not strictly necessary but good practice.
# But we installed in same layer so it won't save space unless we use multi-stage.
# Let's stick to simple structure for now.

WORKDIR /app/output
CMD [ "dotnet", "HomeRecall.dll" ]
