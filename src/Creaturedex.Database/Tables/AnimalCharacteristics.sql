CREATE TABLE [dbo].[AnimalCharacteristics] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [AnimalId]            UNIQUEIDENTIFIER NOT NULL,
    [CharacteristicName]  NVARCHAR(100)    NOT NULL,
    [CharacteristicValue] NVARCHAR(300)    NOT NULL,
    [SortOrder]           INT              NOT NULL DEFAULT 0,
    CONSTRAINT [PK_AnimalCharacteristics] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AnimalCharacteristics_Animals] FOREIGN KEY ([AnimalId]) REFERENCES [dbo].[Animals]([Id]) ON DELETE CASCADE
);
