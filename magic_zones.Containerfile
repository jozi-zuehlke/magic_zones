FROM docker.io/library/ubuntu:24.04 AS builder

RUN apt-get update && \
    apt-get install -y \
    curl \
    libicu74 \
    && \
    rm -rf /var/lib/apt/lists/* && \
    rm -rf /var/cache/debconf/* && \
    rm -rf /var/cache/swcatalog/* && \
    rm -rf /var/log/* && \
    rm -rf /var/lib/command-not-found

RUN curl -fsSL "https://dot.net/v1/dotnet-install.sh" -o dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --install-dir /usr/local/lib/dotnet --channel "8.0" --quality "GA" --no-path && \
    rm dotnet-install.sh

RUN ln -s /usr/local/lib/dotnet/dotnet /usr/local/bin/dotnet

RUN echo "validating dotnet8 installation..." && \
    /usr/local/lib/dotnet/dotnet --list-sdks

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=true DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
WORKDIR /magic_zones
COPY Directory.Build.props .
COPY src/MagicZonesPortable/*.csproj src/MagicZonesPortable/
COPY src/MagicZonesPortable.Core/*.csproj src/MagicZonesPortable.Core/

RUN dotnet restore src/MagicZonesPortable

COPY src src
RUN dotnet publish src/MagicZonesPortable -r win-x64 --self-contained false -p:PublishSingleFile=true

FROM scratch
COPY --from=builder /magic_zones/src/MagicZonesPortable/bin/Release/net8.0-windows/win-x64/publish/MagicZonesPortable.exe /magic_zones/
