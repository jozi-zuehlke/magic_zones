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
    ./dotnet-install.sh --install-dir /usr/local/lib/dotnet --version "10.0.201" --no-path && \
    rm dotnet-install.sh

RUN ln -s /usr/local/lib/dotnet/dotnet /usr/local/bin/dotnet

RUN echo "validating dotnet10 installation..." && \
    /usr/local/lib/dotnet/dotnet --list-sdks

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=true DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
WORKDIR /fancy_zones
COPY Directory.Build.props .
COPY src/FancyZonesPortable/*.csproj src/FancyZonesPortable/
COPY src/FancyZonesPortable.Core/*.csproj src/FancyZonesPortable.Core/

RUN dotnet restore src/FancyZonesPortable

COPY src src
RUN dotnet publish src/FancyZonesPortable -r win-x64 --self-contained true -p:PublishSingleFile=true

FROM scratch
COPY --from=builder /fancy_zones/src/FancyZonesPortable/bin/Release/net10.0-windows/win-x64/publish/FancyZonesPortable.exe /fancy_zones/
