# FactoryOS Enterprise — API container (multi-stage, production build)

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for layer caching: copy solution + project files.
COPY *.slnx global.json Directory.Build.props ./
COPY src/ ./src/
COPY tests/ ./tests/

RUN dotnet restore FactoryOS.slnx
RUN dotnet publish src/FactoryOS.Api/FactoryOS.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as a non-root user for production hardening.
RUN adduser --disabled-password --gecos "" --uid 5001 factoryos
USER factoryos

COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FactoryOS.Api.dll"]
