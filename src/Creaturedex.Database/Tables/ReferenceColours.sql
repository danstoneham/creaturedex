CREATE TABLE [dbo].[ReferenceColours] (
    [Id]        INT           IDENTITY(1,1)  NOT NULL,
    [Code]      NVARCHAR(50)  NOT NULL,
    [Name]      NVARCHAR(50)  NOT NULL,
    [HexValue]  NVARCHAR(7)   NULL,
    [SortOrder] INT           NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ReferenceColours] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceColours_Code] UNIQUE ([Code])
);
