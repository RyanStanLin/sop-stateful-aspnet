FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /source
COPY global.json Directory.Build.props App.slnx ./
COPY src/App/App.csproj src/App/packages.lock.json ./src/App/
RUN dotnet restore src/App/App.csproj --locked-mode
COPY src/ ./src/
RUN dotnet publish src/App/App.csproj -c Release --no-restore -o /out /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0 DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["dotnet", "App.dll"]
