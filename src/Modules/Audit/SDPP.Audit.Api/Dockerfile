FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

# See SDPP.Documents.Api/Dockerfile — restore is cached separately from the full-source layer.
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/SDPP.BuildingBlocks.Contracts.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/
COPY src/Modules/Audit/SDPP.Audit.Domain/SDPP.Audit.Domain.csproj src/Modules/Audit/SDPP.Audit.Domain/
COPY src/Modules/Audit/SDPP.Audit.Application/SDPP.Audit.Application.csproj src/Modules/Audit/SDPP.Audit.Application/
COPY src/Modules/Audit/SDPP.Audit.Infrastructure/SDPP.Audit.Infrastructure.csproj src/Modules/Audit/SDPP.Audit.Infrastructure/
COPY src/Modules/Audit/SDPP.Audit.Api/SDPP.Audit.Api.csproj src/Modules/Audit/SDPP.Audit.Api/
RUN dotnet restore src/Modules/Audit/SDPP.Audit.Api/SDPP.Audit.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Modules/Audit/ src/Modules/Audit/
RUN dotnet publish src/Modules/Audit/SDPP.Audit.Api/SDPP.Audit.Api.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# See SDPP.Documents.Api/Dockerfile — ICU is required for Microsoft.Data.SqlClient on Alpine.
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN addgroup -S sdpp && adduser -S sdpp -G sdpp
USER sdpp

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SDPP.Audit.Api.dll"]
