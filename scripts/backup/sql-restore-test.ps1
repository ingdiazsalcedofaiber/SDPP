<#
.SYNOPSIS
    Restores a .bak into a throwaway database and verifies it, so a backup is only ever called
    "validated" after it has actually been restored — a .bak nobody has restored is not a backup,
    it's an unverified file (see docs/07-operations/backup-recovery-plan.md).

.DESCRIPTION
    - RESTORE DATABASE into a "<OriginalName>_RestoreTest" database (never overwrites the real one).
    - Runs DBCC CHECKDB against the restored copy — catches page-level corruption a plain RESTORE
      wouldn't surface on its own.
    - Drops the throwaway database afterwards regardless of outcome, success or failure, so repeated
      runs never accumulate leftover *_RestoreTest databases.
    - Meant to run on a schedule (weekly is reasonable) against the MOST RECENT backup of each
      database, and its exit code is what should actually gate "this backup is trustworthy" — not
      just "BACKUP DATABASE returned success" at write time.

.EXAMPLE
    $env:SDPP_BACKUP_SQL_PASSWORD = "..."
    .\sql-restore-test.ps1 -BackupFile "D:\SDPP\backups\2026-08-15\SDPP_Signature-20260815-020000.bak" -DatabaseName "SDPP_Signature"
#>
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [Parameter(Mandatory = $true)][string]$DatabaseName,
    [string]$SqlServerHost = "localhost",
    [int]$SqlServerPort = 1433,
    [string]$SqlUser = "sa"  # RESTORE DATABASE + moving files needs more than sdpp_app's scoped
                              # db_owner grant — a dedicated restore-test account with dbcreator
                              # (same as sdpp_app's own bootstrap grant, see sql-init) is the right
                              # production choice; sa is the pragmatic default for this script alone.
)

$ErrorActionPreference = "Stop"

$password = $env:SDPP_BACKUP_SQL_PASSWORD
if ([string]::IsNullOrEmpty($password)) {
    throw "SDPP_BACKUP_SQL_PASSWORD no está definida."
}
if (-not (Test-Path $BackupFile)) {
    throw "No existe el archivo de backup: $BackupFile"
}

$server = "$SqlServerHost,$SqlServerPort"
$testDbName = "${DatabaseName}_RestoreTest"

function Invoke-Sql([string]$Query) {
    sqlcmd -S $server -U $SqlUser -P $password -C -Q $Query
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd falló (código $LASTEXITCODE) ejecutando: $Query" }
}

# Always attempt cleanup first — a database left over from a previous failed run would otherwise
# make this run fail on "database already exists" instead of on the thing actually being tested.
try { Invoke-Sql "IF DB_ID('$testDbName') IS NOT NULL DROP DATABASE [$testDbName];" } catch {}

try {
    Write-Host "Restaurando $BackupFile como $testDbName..."

    # MOVE clauses discovered from the backup's own file list rather than hardcoded — the original
    # database's logical file names are whatever they were when the backup was taken, and guessing
    # wrong here is the single most common reason a RESTORE ... WITH MOVE fails.
    $fileListRaw = sqlcmd -S $server -U $SqlUser -P $password -C -Q `
        "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$BackupFile';" -s "|" -W
    $dataDir = "C:\SDPP\sql-restore-test\data"
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

    $moveClauses = @()
    foreach ($line in ($fileListRaw -split "`n")) {
        $cols = $line -split '\|'
        if ($cols.Count -lt 2) { continue }
        $logicalName = $cols[0].Trim()
        if ($logicalName -eq "" -or $logicalName -eq "LogicalName") { continue }
        $extension = if ($cols[0] -match "_log$") { "ldf" } else { "mdf" }
        $moveClauses += "MOVE N'$logicalName' TO N'$dataDir\$testDbName`_$logicalName.$extension'"
    }
    $moveSql = ($moveClauses -join ", ")

    Invoke-Sql "RESTORE DATABASE [$testDbName] FROM DISK = N'$BackupFile' WITH $moveSql, CHECKSUM, STATS = 10;"
    Write-Host "Restauración OK. Ejecutando DBCC CHECKDB..."

    Invoke-Sql "DBCC CHECKDB([$testDbName]) WITH NO_INFOMSGS;"
    Write-Host "DBCC CHECKDB OK — el backup está validado."
    $exitCode = 0
}
catch {
    Write-Warning "El backup NO pasó la validación de restauración: $_"
    $exitCode = 1
}
finally {
    try { Invoke-Sql "IF DB_ID('$testDbName') IS NOT NULL DROP DATABASE [$testDbName];" } catch {
        Write-Warning "No se pudo limpiar $testDbName — revisar manualmente."
    }
}

exit $exitCode
