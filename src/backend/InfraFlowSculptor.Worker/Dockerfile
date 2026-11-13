FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "src/backend/InfraFlowSculptor.Worker/InfraFlowSculptor.Worker.csproj"
WORKDIR "/src/src/backend/InfraFlowSculptor.Worker"
RUN dotnet publish "InfraFlowSculptor.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG BICEP_VERSION=0.47.16
ARG POWERSHELL_VERSION=7.5.0
ARG PSSCRIPTANALYZER_VERSION=1.25.0
ARG POWERSHELL_YAML_VERSION=0.4.12
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl tar \
    && rm -rf /var/lib/apt/lists/*
RUN curl --fail --location --silent --show-error \
        "https://github.com/PowerShell/PowerShell/releases/download/v${POWERSHELL_VERSION}/powershell-${POWERSHELL_VERSION}-linux-x64.tar.gz" \
        --output /tmp/powershell.tar.gz \
    && mkdir -p /opt/microsoft/powershell/7 \
    && tar -xzf /tmp/powershell.tar.gz -C /opt/microsoft/powershell/7 \
    && chmod +x /opt/microsoft/powershell/7/pwsh \
    && ln -s /opt/microsoft/powershell/7/pwsh /usr/local/bin/pwsh \
    && rm /tmp/powershell.tar.gz
RUN curl --fail --location --silent --show-error \
        "https://github.com/Azure/bicep/releases/download/v${BICEP_VERSION}/bicep-linux-x64" \
        --output /usr/local/bin/bicep \
    && chmod +x /usr/local/bin/bicep
RUN pwsh -NoLogo -NoProfile -NonInteractive -Command \
    "Set-PSRepository -Name PSGallery -InstallationPolicy Trusted; Install-Module -Name PSScriptAnalyzer -RequiredVersion '${PSSCRIPTANALYZER_VERSION}' -Scope AllUsers -Force; Install-Module -Name powershell-yaml -RequiredVersion '${POWERSHELL_YAML_VERSION}' -Scope AllUsers -Force"
ENV HOME=/home/app
RUN mkdir -p /home/app/.bicep && chown -R $APP_UID:$APP_UID /home/app
USER $APP_UID
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InfraFlowSculptor.Worker.dll"]
