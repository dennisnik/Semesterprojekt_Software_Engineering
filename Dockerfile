# TN-Watch application image (ADR-08). Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (layer cache): only build settings + project files
COPY Directory.Build.props .editorconfig ./
COPY src/TNWatch.Domain/TNWatch.Domain.csproj src/TNWatch.Domain/
COPY src/TNWatch.TelemetryIngestion/TNWatch.TelemetryIngestion.csproj src/TNWatch.TelemetryIngestion/
COPY src/TNWatch.Persistence/TNWatch.Persistence.csproj src/TNWatch.Persistence/
COPY src/TNWatch.Alerting/TNWatch.Alerting.csproj src/TNWatch.Alerting/
COPY src/TNWatch.WebUI/TNWatch.WebUI.csproj src/TNWatch.WebUI/
RUN dotnet restore src/TNWatch.WebUI/TNWatch.WebUI.csproj

COPY src/ src/
RUN dotnet publish src/TNWatch.WebUI/TNWatch.WebUI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "TNWatch.WebUI.dll"]
