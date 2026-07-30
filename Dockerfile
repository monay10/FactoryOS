# FactoryOS Enterprise — API container (multi-stage, production build)

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for layer caching. Central package management and the NuGet feed config are required for
# restore; the API is built project-scoped, so only its reference graph is needed: src/ plus the workflow
# plugin it composes the platform engines from (added when the host began composing them).
COPY global.json Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/ ./src/
COPY plugins/ ./plugins/

RUN dotnet restore src/FactoryOS.Api/FactoryOS.Api.csproj
RUN dotnet publish src/FactoryOS.Api/FactoryOS.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as the non-root user the .NET base image already provides (UID via $APP_UID). The base aspnet image has
# no `adduser`, so create nothing — this is the hardening Microsoft's images ship for exactly this purpose.
USER $APP_UID

ENTRYPOINT ["dotnet", "FactoryOS.Api.dll"]
