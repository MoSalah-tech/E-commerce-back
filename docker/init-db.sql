-- Creates the least-privilege application login and its database.
--
-- The mssql/server image only auto-creates MSSQL_USER/MSSQL_DATABASE in some
-- configurations, so this is done explicitly instead. The script is idempotent
-- and safe to run on every `docker compose up`.
--
-- Values are supplied by `sqlcmd -v` from docker/sql-init (see docker-compose.yml).

USE [master];
GO

IF DB_ID(N'$(AppDbName)') IS NULL
BEGIN
    CREATE DATABASE [$(AppDbName)];
END
GO

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(AppDbUser)')
BEGIN
    CREATE LOGIN [$(AppDbUser)] WITH PASSWORD = N'$(AppDbPassword)', CHECK_POLICY = OFF;
END
GO

USE [$(AppDbName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppDbUser)')
BEGIN
    CREATE USER [$(AppDbUser)] FOR LOGIN [$(AppDbUser)];
    ALTER ROLE db_owner ADD MEMBER [$(AppDbUser)];
END
GO

PRINT N'Database and application login are ready.';
GO