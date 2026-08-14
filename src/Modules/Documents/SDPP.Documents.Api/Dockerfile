FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

# Restore is cached across rebuilds as long as no .csproj changes — full source (needed only for
# publish) is copied in a separate layer below, so editing a .cs file no longer forces a re-restore.
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/SDPP.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/SDPP.BuildingBlocks.Contracts.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/
COPY src/Modules/Documents/SDPP.Documents.Domain/SDPP.Documents.Domain.csproj src/Modules/Documents/SDPP.Documents.Domain/
COPY src/Modules/Documents/SDPP.Documents.Application/SDPP.Documents.Application.csproj src/Modules/Documents/SDPP.Documents.Application/
COPY src/Modules/Documents/SDPP.Documents.Infrastructure/SDPP.Documents.Infrastructure.csproj src/Modules/Documents/SDPP.Documents.Infrastructure/
COPY src/Modules/Documents/SDPP.Documents.Api/SDPP.Documents.Api.csproj src/Modules/Documents/SDPP.Documents.Api/
RUN dotnet restore src/Modules/Documents/SDPP.Documents.Api/SDPP.Documents.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Modules/Documents/ src/Modules/Documents/
RUN dotnet publish src/Modules/Documents/SDPP.Documents.Api/SDPP.Documents.Api.csproj -c Release -o /app --no-self-contained

# Hardened runtime image: non-root user, read-only filesystem except /tmp (see
# docs/07-operations/kubernetes-architecture.md §1 and §4).
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Alpine ships without ICU by default, which breaks Microsoft.Data.SqlClient's locale handling
# (CultureNotFoundException on every connection attempt) unless globalization stays invariant —
# but invariant mode is too restrictive for user-facing string formatting elsewhere in the app.
# poppler-utils is PopplerTextExtractor's actual runtime dependency (`pdftotext`) — the
# service-to-service /extracted-text endpoint Classification.Api calls runs inside this
# container, not the worker, so it needs its own copy of the binary.
RUN apk add --no-cache icu-libs poppler-utils
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN addgroup -S sdpp && adduser -S sdpp -G sdpp
USER sdpp

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SDPP.Documents.Api.dll"]
