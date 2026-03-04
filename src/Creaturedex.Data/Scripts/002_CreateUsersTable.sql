IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Username]     NVARCHAR(50)     NOT NULL,
        [PasswordHash] NVARCHAR(255)    NOT NULL,
        [DisplayName]  NVARCHAR(100)    NOT NULL,
        [Role]         NVARCHAR(50)     NOT NULL DEFAULT 'Admin',
        [CreatedAt]    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Users_Username] UNIQUE ([Username])
    );
END
