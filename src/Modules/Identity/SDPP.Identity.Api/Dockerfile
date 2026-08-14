FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/SDPP.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/SDPP.BuildingBlocks.Contracts.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/
COPY src/Modules/Identity/SDPP.Identity.Domain/SDPP.Identity.Domain.csproj src/Modules/Identity/SDPP.Identity.Domain/
COPY src/Modules/Identity/SDPP.Identity.Application/SDPP.Identity.Application.csproj src/Modules/Identity/SDPP.Identity.Application/
COPY src/Modules/Identity/SDPP.Identity.Infrastructure/SDPP.Identity.Infrastructure.csproj src/Modules/Identity/SDPP.Identity.Infrastructure/
COPY src/Modules/Identity/SDPP.Identity.Api/SDPP.Identity.Api.csproj src/Modules/Identity/SDPP.Identity.Api/
RUN dotnet restore src/Modules/Identity/SDPP.Identity.Api/SDPP.Identity.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Modules/Identity/ src/Modules/Identity/
RUN dotnet publish src/Modules/Identity/SDPP.Identity.Api/SDPP.Identity.Api.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN addgroup -S sdpp && adduser -S sdpp -G sdpp
USER sdpp

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SDPP.Identity.Api.dll"]
