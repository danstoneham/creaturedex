CREATE TABLE [dbo].[ReferenceConservationStatuses] (
    [Id]          INT            IDENTITY(1,1)  NOT NULL,
    [Code]        NVARCHAR(5)    NOT NULL,
    [Name]        NVARCHAR(50)   NOT NULL,
    [Description] NVARCHAR(200)  NOT NULL,
    [Severity]    INT            NOT NULL,
    [Colour]      NVARCHAR(7)    NOT NULL,
    CONSTRAINT [PK_ReferenceConservationStatuses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ReferenceConservationStatuses_Code] UNIQUE ([Code])
);
