CREATE TABLE [dbo].[Taxonomy] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [Kingdom]     NVARCHAR(100)    NOT NULL DEFAULT 'Animalia',
    [Phylum]      NVARCHAR(100)    NULL,
    [Class]       NVARCHAR(100)    NULL,
    [TaxOrder]    NVARCHAR(100)    NULL,
    [Family]      NVARCHAR(100)    NULL,
    [Genus]       NVARCHAR(100)    NULL,
    [Species]     NVARCHAR(100)    NULL,
    [Subspecies]  NVARCHAR(100)    NULL,
    [ColTaxonId]  NVARCHAR(200)    NULL,
    [Authorship]  NVARCHAR(500)    NULL,
    [Synonyms]    NVARCHAR(MAX)    NULL,
    CONSTRAINT [PK_Taxonomy] PRIMARY KEY CLUSTERED ([Id])
);
