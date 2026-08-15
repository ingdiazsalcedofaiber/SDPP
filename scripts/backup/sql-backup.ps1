<#
.SYNOPSIS
    Full backup of every SDPP_* database, meant to run on a schedule (Windows Task Scheduler) on
    the server hosting SQL Server — works identically whether SQL Server runs natively on that
    server or inside the sql-server Docker container (same TCP endpoint either way).

.DESCRIPTION
    - One .bak per database, timestamped, written under -BackupRoot.
    - Enforces retention: anything older than -RetentionDays is deleted AFTER a successful new
      backup, never before — a failed backup run must never leave zero backups on disk.
    - Never receives the SQL password as a literal argument on the command line (visible in
      process listings / Task Scheduler history) — read it from the SDPP_BACKUP_SQL_PASSWORD
      environment variable, set as part of the scheduled task's own (protected) configuration.
    - This script alone does NOT make a backup "validated" — see sql-restore-test.ps1, which must
      run against every backup this script produces before it's trusted for a real restore.

.EXAMPLE
    $env:SDPP_BACKUP_SQL_PASSWORD = "..."
    .\sql-backup.ps1 -SqlServerHost "localhost" -SqlServerPort 14330 -BackupRoot "D:\SDPP\backups"
#>
param(
    [string]$SqlServerHost = "localhost",
    [int]$SqlServerPort = 1433,
    [string]$SqlUser = "sdpp_app",
    [string]$BackupRoot = "D:\SDPP\backups",
    [int]$RetentionDays = 14,
    [string[]]$Databases = @("SDPP_Documents", "SDPP_Classification", "SDPP_Audit", "SDPP_Signature", "SDPP_Identity")
)

$ErrorActionPreference = "Stop"

$password = $env:SDPP_BACKUP_SQL_PASSWORD
if ([string]::IsNullOrEmpty($password)) {
    throw "SDPP_BACKUP_SQL_PASSWORD no está definida. No se aceptan contraseñas por línea de comandos."
}

# sdpp_app is scoped to db_owner within its own 5 databases (see deploy/compose/sql-init) —
# db_owner includes BACKUP DATABASE rights on that database, so no sysadmin/sa account is needed
# here either, consistent with the rest of the platform's least-privilege posture.
$server = "$SqlServerHost,$SqlServerPort"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dateDir = Join-Path $BackupRoot (Get-Date -Format "yyyy-MM-dd")

New-Item -ItemType Directory -Force -Path $dateDir | Out-Null

$failures = @()

foreach ($db in $Databases) {
    $backupFile = Join-Path $dateDir "$db-$timestamp.bak"
    Write-Host "Respaldando $db -> $backupFile"

    $query = "BACKUP DATABASE [$db] TO DISK = N'$backupFile' WITH COMPRESSION, CHECKSUM, STATS = 10;"

    try {
        sqlcmd -S $server -U $SqlUser -P $password -C -Q $query
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd salió con código $LASTEXITCODE para $db" }
        Write-Host "OK: $db"
    }
    catch {
        Write-Warning "FALLÓ el backup de $db : $_"
        $failures += $db
    }
}

if ($failures.Count -gt 0) {
    Write-Warning "Retención NO aplicada — al menos un backup falló ($($failures -join ', ')), no se borra nada."
    exit 1
}

# Retention only runs after every database above backed up successfully.
Write-Host "Aplicando retención de $RetentionDays días en $BackupRoot"
Get-ChildItem -Path $BackupRoot -Recurse -Filter "*.bak" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } |
    ForEach-Object {
        Write-Host "Eliminando backup expirado: $($_.FullName)"
        Remove-Item $_.FullName -Force
    }

Write-Host "Backup completo."
