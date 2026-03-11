CREATE TABLE [dbo].[ReferenceDietTypes] (
    [Id]        INT           IDENTITY(1,1)  NOT NULL,
    [Code]      NVARCHAR(50)  NOT NULL,
    [Name]      NVARCHAR(50)  NOT NULL,
    [SortOrder] INT           NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ReferenceDietTypes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceDietTypes_Code] UNIQUE ([Code])
);
