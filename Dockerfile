FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NuGetMcpServer.csproj", "./"]
RUN dotnet restore "NuGetMcpServer.csproj"

COPY . .
RUN dotnet publish "NuGetMcpServer.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --self-contained false \
    -p:PublishAot=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl \
    && rm -rf /var/lib/apt/lists/*

RUN if ! getent group app > /dev/null; then groupadd --system app; fi \
    && if ! id -u app > /dev/null 2>&1; then useradd --system --gid app --no-create-home app; fi

COPY --from=build /app/publish .
COPY appsettings.Docker.json ./appsettings.json

ENV ASPNETCORE_URLS=http://+:18081
EXPOSE 18081

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail --silent http://localhost:18081/api/health || exit 1

USER app
ENTRYPOINT ["dotnet", "NuGetMcpServer.dll"]
