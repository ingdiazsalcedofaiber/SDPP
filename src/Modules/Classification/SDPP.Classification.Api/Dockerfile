FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

# See SDPP.Documents.Api/Dockerfile — restore is cached separately from the full-source layer.
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/SDPP.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/
COPY src/Modules/Classification/SDPP.Classification.Domain/SDPP.Classification.Domain.csproj src/Modules/Classification/SDPP.Classification.Domain/
COPY src/Modules/Classification/SDPP.Classification.Application/SDPP.Classification.Application.csproj src/Modules/Classification/SDPP.Classification.Application/
COPY src/Modules/Classification/SDPP.Classification.Infrastructure/SDPP.Classification.Infrastructure.csproj src/Modules/Classification/SDPP.Classification.Infrastructure/
COPY src/Modules/Classification/SDPP.Classification.Api/SDPP.Classification.Api.csproj src/Modules/Classification/SDPP.Classification.Api/
RUN dotnet restore src/Modules/Classification/SDPP.Classification.Api/SDPP.Classification.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Modules/Classification/ src/Modules/Classification/
RUN dotnet publish src/Modules/Classification/SDPP.Classification.Api/SDPP.Classification.Api.csproj -c Release -o /app --no-self-contained

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

ENTRYPOINT ["dotnet", "SDPP.Classification.Api.dll"]
