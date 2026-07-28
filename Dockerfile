# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so `dotnet restore` is cached independently of source changes.
COPY ZISK/ZISK.sln ZISK/ZISK.sln
COPY ZISK/ZISK/ZISK.csproj ZISK/ZISK/ZISK.csproj
COPY ZISK/ZISK.Client/ZISK.Client.csproj ZISK/ZISK.Client/ZISK.Client.csproj
COPY ZISK/ZISK.Shared/ZISK.Shared.csproj ZISK/ZISK.Shared/ZISK.Shared.csproj
COPY ZISK/ZISK.Tests/ZISK.Tests.csproj ZISK/ZISK.Tests/ZISK.Tests.csproj
RUN dotnet restore ZISK/ZISK/ZISK.csproj

# Copy the rest of the source and publish the server project (it references
# ZISK.Client and ZISK.Shared, so this also builds and bundles the WASM client).
COPY ZISK/ ZISK/
RUN dotnet publish ZISK/ZISK/ZISK.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Non-root user — the ASP.NET runtime image runs as root by default.
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ZISK.dll"]
