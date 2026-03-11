CREATE TABLE [dbo].[ReferenceTags] (
    [Id]        INT            IDENTITY(1,1)  NOT NULL,
    [Code]      NVARCHAR(50)   NOT NULL,
    [Name]      NVARCHAR(100)  NOT NULL,
    [TagGroup]  NVARCHAR(50)   NOT NULL,
    [SortOrder] INT            NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ReferenceTags] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceTags_Code] UNIQUE ([Code])
);
