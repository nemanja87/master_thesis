FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/ResultsService/ResultsService.csproj -c Release -o /app/publish \
    && dotnet publish src/BenchRunner/BenchRunner.csproj -c Release -o /app/benchrunner

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/benchrunner ./benchrunner
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates curl tar; \
    rm -rf /var/lib/apt/lists/*; \
    K6_VERSION=1.3.0; \
    curl -L -o /tmp/k6.tar.gz "https://github.com/grafana/k6/releases/download/v${K6_VERSION}/k6-v${K6_VERSION}-linux-arm64.tar.gz"; \
    tar -xzf /tmp/k6.tar.gz -C /tmp; \
    mv /tmp/k6-v${K6_VERSION}-linux-arm64/k6 /usr/local/bin/k6; \
    GHZ_VERSION=0.120.0; \
    curl -L -o /tmp/ghz.tar.gz "https://github.com/bojand/ghz/releases/download/v${GHZ_VERSION}/ghz-linux-arm64.tar.gz"; \
    tar -xzf /tmp/ghz.tar.gz -C /tmp; \
    mv /tmp/ghz /usr/local/bin/ghz; \
    chmod +x /usr/local/bin/k6 /usr/local/bin/ghz; \
    rm -rf /tmp/*;
ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000
ENTRYPOINT ["dotnet", "ResultsService.dll"]
