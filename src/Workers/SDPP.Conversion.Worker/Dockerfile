FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

# See SDPP.Documents.Api/Dockerfile — restore is cached separately from the full-source layer.
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Domain/SDPP.BuildingBlocks.Domain.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Domain/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Application/SDPP.BuildingBlocks.Application.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Application/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/SDPP.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/SDPP.BuildingBlocks.Contracts.csproj src/BuildingBlocks/SDPP.BuildingBlocks.Contracts/
COPY src/Modules/Documents/SDPP.Documents.Domain/SDPP.Documents.Domain.csproj src/Modules/Documents/SDPP.Documents.Domain/
COPY src/Modules/Documents/SDPP.Documents.Application/SDPP.Documents.Application.csproj src/Modules/Documents/SDPP.Documents.Application/
COPY src/Modules/Documents/SDPP.Documents.Infrastructure/SDPP.Documents.Infrastructure.csproj src/Modules/Documents/SDPP.Documents.Infrastructure/
COPY src/Modules/Classification/SDPP.Classification.Domain/SDPP.Classification.Domain.csproj src/Modules/Classification/SDPP.Classification.Domain/
COPY src/Modules/Classification/SDPP.Classification.Application/SDPP.Classification.Application.csproj src/Modules/Classification/SDPP.Classification.Application/
COPY src/Modules/Classification/SDPP.Classification.Infrastructure/SDPP.Classification.Infrastructure.csproj src/Modules/Classification/SDPP.Classification.Infrastructure/
COPY src/Workers/SDPP.Conversion.Worker/SDPP.Conversion.Worker.csproj src/Workers/SDPP.Conversion.Worker/
RUN dotnet restore src/Workers/SDPP.Conversion.Worker/SDPP.Conversion.Worker.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Modules/Documents/ src/Modules/Documents/
COPY src/Modules/Classification/ src/Modules/Classification/
COPY src/Workers/SDPP.Conversion.Worker/ src/Workers/SDPP.Conversion.Worker/
RUN dotnet publish src/Workers/SDPP.Conversion.Worker/SDPP.Conversion.Worker.csproj -c Release -o /app --no-self-contained

# Debian-based (not alpine) runtime because LibreOffice/Ghostscript/Poppler/Tesseract are far
# better supported there — see docs/01-architecture/technology-stack.md §4. This is the one image
# in the platform that is intentionally large; it never runs anywhere but the isolated
# sdpp-workers namespace with no network egress (docs/07-operations/kubernetes-architecture.md §2).
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libreoffice-writer libreoffice-calc libreoffice-impress \
    ghostscript poppler-utils tesseract-ocr tesseract-ocr-spa qpdf \
    && rm -rf /var/lib/apt/lists/*

# LibreOffice needs a real, writable HOME to create its profile (~/.config, ~/.cache/dconf) —
# without -m/-d it gets a system account with no home directory and fails with "User installation
# could not be completed" the moment it tries to launch.
RUN groupadd -r sdpp && useradd -r -g sdpp -m -d /home/sdpp sdpp \
    && chown -R sdpp:sdpp /home/sdpp
ENV HOME=/home/sdpp
USER sdpp

COPY --from=build /app .

ENTRYPOINT ["dotnet", "SDPP.Conversion.Worker.dll"]
