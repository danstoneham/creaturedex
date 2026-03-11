CREATE TABLE [dbo].[ReferenceHabitatTypes] (
    [Id]        INT            IDENTITY(1,1)  NOT NULL,
    [Code]      NVARCHAR(50)   NOT NULL,
    [Name]      NVARCHAR(100)  NOT NULL,
    [SortOrder] INT            NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ReferenceHabitatTypes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceHabitatTypes_Code] UNIQUE ([Code])
);
