# Dockerfiles

Cada servicio tiene su propio `Dockerfile` junto a su `.csproj` (p. ej.
`src/Modules/Documents/SDPP.Documents.Api/Dockerfile`), no centralizados en esta carpeta —
mantiene el contexto de build cerca del código que empaqueta y evita un directorio con quince
Dockerfiles casi idénticos difíciles de distinguir.

Esta carpeta queda como punto de referencia para artefactos de build transversales (por ejemplo,
una imagen base común si en el futuro se decide estandarizar el hardening compartido entre
servicios — ver `docs/07-operations/kubernetes-architecture.md §1`).
