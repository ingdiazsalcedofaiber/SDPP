-- Least-privilege application login used by all 5 SDPP APIs instead of `sa`.
-- Scope: db_owner WITHIN each SDPP_* database only — no sysadmin, no access to master/msdb, no
-- visibility into other tenants on the same instance. db_owner (not just datareader/datawriter)
-- is required because every API self-migrates at startup (context.Database.MigrateAsync()), so
-- the login needs schema rights inside its own database. Runs once via the sql-init compose
-- service, after each module's own migration has created its database (see docker-compose.yml —
-- sql-init depends on the APIs being healthy, not just sql-server, for exactly this reason).
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'sdpp_app')
BEGIN
    CREATE LOGIN sdpp_app WITH PASSWORD = 'rDEv929WW1I6NuEbRFC8pJvWsrRDAa1!', CHECK_POLICY = ON;
END
GO

-- dbcreator (not sysadmin) so a truly fresh instance — e.g. after `docker compose down -v` wipes
-- the sql-data volume — can still self-bootstrap: each API's own Database.MigrateAsync() creates
-- its database on first boot, which requires CREATE DATABASE rights. SQL Server automatically
-- makes the creating login db_owner of whatever database it creates, so this single grant is
-- sufficient for both the fresh-install path and, combined with the per-database loop below,
-- the "databases already exist" path this script also has to handle idempotently.
ALTER SERVER ROLE dbcreator ADD MEMBER sdpp_app;
GO

DECLARE @db sysname;
DECLARE db_cursor CURSOR FOR
    SELECT name FROM sys.databases
    WHERE name IN ('SDPP_Documents', 'SDPP_Classification', 'SDPP_Audit', 'SDPP_Signature', 'SDPP_Identity');

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @sql nvarchar(max) = N'
        USE ' + QUOTENAME(@db) + N';
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ''sdpp_app'')
        BEGIN
            CREATE USER sdpp_app FOR LOGIN sdpp_app;
        END
        ALTER ROLE db_owner ADD MEMBER sdpp_app;
    ';
    EXEC sp_executesql @sql;
    PRINT 'Configured sdpp_app in ' + @db;
    FETCH NEXT FROM db_cursor INTO @db;
END
CLOSE db_cursor;
DEALLOCATE db_cursor;
GO
