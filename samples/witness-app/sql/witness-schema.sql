IF OBJECT_ID(N'dbo.witness_checks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.witness_checks
    (
        id uniqueidentifier NOT NULL CONSTRAINT PK_witness_checks PRIMARY KEY,
        written_at datetime2 NOT NULL,
        value nvarchar(100) NOT NULL
    );
END;
