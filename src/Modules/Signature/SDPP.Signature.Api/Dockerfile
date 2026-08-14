FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

# See SDPP.Documents.Api/Dockerfile — restore is cached separately from the full-source layer.
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/SDPP.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/SDPP.BuildingBlocks.Contracts.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/
COPY src/Modules/Signature/SDPP.Signature.Domain/SDPP.Signature.Domain.csproj src/Modules/Signature/SDPP.Signature.Domain/
COPY src/Modules/Signature/SDPP.Signature.Application/SDPP.Signature.Application.csproj src/Modules/Signature/SDPP.Signature.Application/
COPY src/Modules/Signature/SDPP.Signature.Infrastructure/SDPP.Signature.Infrastructure.csproj src/Modules/Signature/SDPP.Signature.Infrastructure/
COPY src/Modules/Signature/SDPP.Signature.Api/SDPP.Signature.Api.csproj src/Modules/Signature/SDPP.Signature.Api/
RUN dotnet restore src/Modules/Signature/SDPP.Signature.Api/SDPP.Signature.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Modules/Signature/ src/Modules/Signature/
RUN dotnet publish src/Modules/Signature/SDPP.Signature.Api/SDPP.Signature.Api.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# See SDPP.Documents.Api/Dockerfile — ICU is required for Microsoft.Data.SqlClient on Alpine.
# ttf-dejavu is required by PdfSharpFontResolver (PdfSharp has no GDI+/system font access on
# Linux) — unlike the Conversion Worker, this is a plain API image with no LibreOffice dependency
# to pull the font in transitively, so it must be installed explicitly here.
# tzdata is required for TimeZoneInfo.FindSystemTimeZoneById (America/Bogota) when rendering the
# completion certificate's timestamps — Alpine has no IANA tz database by default.
RUN apk add --no-cache icu-libs ttf-dejavu tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN addgroup -S sdpp && adduser -S sdpp -G sdpp
USER sdpp

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SDPP.Signature.Api.dll"]
