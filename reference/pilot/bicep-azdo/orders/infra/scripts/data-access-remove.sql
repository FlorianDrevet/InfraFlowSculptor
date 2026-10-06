-- Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
-- Paramètres sqlcmd requis : PrincipalName, ClientId (GUID).

SET XACT_ABORT ON;

DECLARE @PrincipalName sysname = N'$(PrincipalName)';
DECLARE @ClientId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(ClientId)');
DECLARE @PrincipalId int = USER_ID(@PrincipalName);
DECLARE @ExpectedSid varbinary(16);
DECLARE @ManagedValue nvarchar(100);

IF @PrincipalName = N'' OR @PrincipalName LIKE N'%[^A-Za-z0-9._-]%'
    THROW 50011, 'PrincipalName must contain only letters, digits, dot, underscore, or hyphen.', 1;

IF @ClientId IS NULL
    THROW 50012, 'ClientId must be a valid GUID.', 1;

IF @PrincipalId IS NULL
    RETURN;

SET @ExpectedSid = CONVERT(varbinary(16), @ClientId);

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE principal_id = @PrincipalId
      AND sid = @ExpectedSid
)
    THROW 50013, 'The database principal client ID does not match; no changes were made.', 1;

SELECT @ManagedValue = CONVERT(nvarchar(100), value)
FROM sys.extended_properties
WHERE class = 4
  AND major_id = @PrincipalId
  AND name = N'ifs-managed';

IF ISNULL(@ManagedValue, N'') <> N'true'
    THROW 50014, 'The database principal is not marked as managed by IFS; it was not removed.', 1;

IF EXISTS (SELECT 1 FROM sys.schemas WHERE principal_id = @PrincipalId)
    THROW 50015, 'The IFS user owns a schema. Transfer each schema with ALTER AUTHORIZATION ON SCHEMA::<schema> TO dbo, then retry.', 1;

IF EXISTS (SELECT 1 FROM sys.objects WHERE principal_id = @PrincipalId)
    THROW 50016, 'The IFS user owns database objects. Transfer ownership to dbo, then retry.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @RoleName sysname;
    DECLARE remove_role CURSOR LOCAL FAST_FORWARD FOR
        SELECT role_principal.name
        FROM sys.database_role_members drm
        JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
        WHERE drm.member_principal_id = @PrincipalId
          AND role_principal.name IN (N'db_datareader', N'db_datawriter', N'db_ddladmin');

    OPEN remove_role;
    FETCH NEXT FROM remove_role INTO @RoleName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @DropRoleSql nvarchar(max) =
            N'ALTER ROLE ' + QUOTENAME(@RoleName) + N' DROP MEMBER ' + QUOTENAME(@PrincipalName) + N';';
        EXEC sys.sp_executesql @DropRoleSql;
        FETCH NEXT FROM remove_role INTO @RoleName;
    END;
    CLOSE remove_role;
    DEALLOCATE remove_role;

    DECLARE @DropUserSql nvarchar(max) = N'DROP USER ' + QUOTENAME(@PrincipalName) + N';';
    EXEC sys.sp_executesql @DropUserSql;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'remove_role') >= 0
        CLOSE remove_role;
    IF CURSOR_STATUS('local', 'remove_role') > -3
        DEALLOCATE remove_role;
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
