CREATE TABLE [dbo].[ReferenceDomesticationStatuses] (
    [Id]        INT           IDENTITY(1,1)  NOT NULL,
    [Code]      NVARCHAR(50)  NOT NULL,
    [Name]      NVARCHAR(50)  NOT NULL,
    [IsPet]     BIT           NOT NULL DEFAULT 0,
    [SortOrder] INT           NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ReferenceDomesticationStatuses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceDomesticationStatuses_Code] UNIQUE ([Code])
);
