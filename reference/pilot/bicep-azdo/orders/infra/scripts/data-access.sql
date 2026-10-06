-- Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
-- Paramètres sqlcmd requis : PrincipalName, ClientId (GUID), Roles (liste séparée par des virgules).

SET XACT_ABORT ON;

DECLARE @PrincipalName sysname = N'$(PrincipalName)';
DECLARE @ClientId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(ClientId)');
DECLARE @RolesCsv nvarchar(4000) = N'$(Roles)';
DECLARE @PrincipalId int;
DECLARE @ExpectedSid varbinary(16);
DECLARE @ManagedValue nvarchar(100);

IF @PrincipalName = N'' OR @PrincipalName LIKE N'%[^A-Za-z0-9._-]%'
    THROW 50001, 'PrincipalName must contain only letters, digits, dot, underscore, or hyphen.', 1;

IF @ClientId IS NULL
    THROW 50002, 'ClientId must be a valid GUID.', 1;

SET @ExpectedSid = CONVERT(varbinary(16), @ClientId);

DECLARE @DesiredRoles TABLE (role_name sysname NOT NULL PRIMARY KEY);
INSERT INTO @DesiredRoles (role_name)
SELECT DISTINCT TRIM(value)
FROM STRING_SPLIT(@RolesCsv, N',')
WHERE TRIM(value) <> N'';

IF EXISTS (
    SELECT 1
    FROM @DesiredRoles
    WHERE role_name NOT IN (N'db_datareader', N'db_datawriter', N'db_ddladmin')
)
    THROW 50003, 'Roles contains a role outside the supported IFS SQL role set.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    SET @PrincipalId = USER_ID(@PrincipalName);

    IF @PrincipalId IS NULL
    BEGIN
        DECLARE @CreateUserSql nvarchar(max) =
            N'CREATE USER ' + QUOTENAME(@PrincipalName) +
            N' WITH SID = ' + sys.fn_varbintohexstr(@ExpectedSid) + N', TYPE = E;';
        EXEC sys.sp_executesql @CreateUserSql;
        SET @PrincipalId = USER_ID(@PrincipalName);

        EXEC sys.sp_addextendedproperty
            @name = N'ifs-managed',
            @value = N'true',
            @level0type = N'USER',
            @level0name = @PrincipalName;
    END;
    ELSE
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.database_principals
            WHERE principal_id = @PrincipalId
              AND sid = @ExpectedSid
        )
            THROW 50004, 'An existing database principal has a different client ID; no changes were made.', 1;

        SELECT @ManagedValue = CONVERT(nvarchar(100), value)
        FROM sys.extended_properties
        WHERE class = 4
          AND major_id = @PrincipalId
          AND name = N'ifs-managed';

        IF ISNULL(@ManagedValue, N'') <> N'true'
            THROW 50005, 'The existing database principal is not marked as managed by IFS; no changes were made.', 1;
    END;

    DECLARE @RoleName sysname;
    DECLARE add_role CURSOR LOCAL FAST_FORWARD FOR
        SELECT role_name FROM @DesiredRoles;

    OPEN add_role;
    FETCH NEXT FROM add_role INTO @RoleName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.database_role_members drm
            JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
            WHERE drm.member_principal_id = @PrincipalId
              AND role_principal.name = @RoleName
        )
        BEGIN
            DECLARE @AddRoleSql nvarchar(max) =
                N'ALTER ROLE ' + QUOTENAME(@RoleName) + N' ADD MEMBER ' + QUOTENAME(@PrincipalName) + N';';
            EXEC sys.sp_executesql @AddRoleSql;
        END;
        FETCH NEXT FROM add_role INTO @RoleName;
    END;
    CLOSE add_role;
    DEALLOCATE add_role;

    DECLARE remove_role CURSOR LOCAL FAST_FORWARD FOR
        SELECT role_principal.name
        FROM sys.database_role_members drm
        JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
        WHERE drm.member_principal_id = @PrincipalId
          AND role_principal.name IN (N'db_datareader', N'db_datawriter', N'db_ddladmin')
          AND NOT EXISTS (SELECT 1 FROM @DesiredRoles desired WHERE desired.role_name = role_principal.name);

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

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'add_role') >= 0
        CLOSE add_role;
    IF CURSOR_STATUS('local', 'add_role') > -3
        DEALLOCATE add_role;
    IF CURSOR_STATUS('local', 'remove_role') >= 0
        CLOSE remove_role;
    IF CURSOR_STATUS('local', 'remove_role') > -3
        DEALLOCATE remove_role;
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
