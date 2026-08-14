FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

# Restore is cached across rebuilds as long as no .csproj changes — the Gateway references
# BuildingBlocks.Infrastructure now (shared cookie-JWT auth, see
# BuildingBlocks.Infrastructure/Security/CookieJwtBearerExtensions.cs), same layering as every
# module Api's Dockerfile.
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/SDPP.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/
COPY src/Gateway/SDPP.Gateway/SDPP.Gateway.csproj src/Gateway/SDPP.Gateway/
RUN dotnet restore src/Gateway/SDPP.Gateway/SDPP.Gateway.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Gateway/ src/Gateway/
RUN dotnet publish src/Gateway/SDPP.Gateway/SDPP.Gateway.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
RUN addgroup -S sdpp && adduser -S sdpp -G sdpp
USER sdpp

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SDPP.Gateway.dll"]
