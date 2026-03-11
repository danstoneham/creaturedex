CREATE TABLE [dbo].[ReferenceActivityPatterns] (
    [Id]        INT           IDENTITY(1,1)  NOT NULL,
    [Code]      NVARCHAR(50)  NOT NULL,
    [Name]      NVARCHAR(50)  NOT NULL,
    [SortOrder] INT           NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ReferenceActivityPatterns] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceActivityPatterns_Code] UNIQUE ([Code])
);
