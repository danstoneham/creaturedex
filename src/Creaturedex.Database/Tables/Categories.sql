CREATE TABLE [dbo].[Categories] (
    [Id]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [Name]             NVARCHAR(100)    NOT NULL,
    [Slug]             NVARCHAR(100)    NOT NULL,
    [Description]      NVARCHAR(MAX)    NULL,
    [IconName]         NVARCHAR(100)    NULL,
    [ParentCategoryId] UNIQUEIDENTIFIER NULL,
    [SortOrder]        INT              NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentCategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [UQ_Categories_Slug] UNIQUE ([Slug])
);
