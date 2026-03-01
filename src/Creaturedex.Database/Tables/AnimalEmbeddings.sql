CREATE TABLE [dbo].[AnimalEmbeddings] (
    [Id]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [AnimalId]   UNIQUEIDENTIFIER NOT NULL,
    [Embedding]  VARBINARY(MAX)   NOT NULL,
    [Dimensions] INT              NOT NULL,
    [ModelUsed]  NVARCHAR(100)    NOT NULL,
    [CreatedAt]  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_AnimalEmbeddings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AnimalEmbeddings_Animals] FOREIGN KEY ([AnimalId]) REFERENCES [dbo].[Animals]([Id]) ON DELETE CASCADE
);
