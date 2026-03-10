-- Animals: GBIF taxon key and canonical name
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'GbifTaxonKey')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [GbifTaxonKey] INT NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'GbifCanonicalName')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [GbifCanonicalName] NVARCHAR(300) NULL;
END

-- Animals: Map tile and observation data
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'MapTileUrlTemplate')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [MapTileUrlTemplate] NVARCHAR(2000) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'MapObservationCount')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [MapObservationCount] INT NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'MapMinLat')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [MapMinLat] FLOAT NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'MapMaxLat')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [MapMaxLat] FLOAT NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'MapMinLng')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [MapMinLng] FLOAT NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'MapMaxLng')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [MapMaxLng] FLOAT NULL;
END

-- Animals: Image attribution
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'ImageLicense')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [ImageLicense] NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'ImageRightsHolder')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [ImageRightsHolder] NVARCHAR(500) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'ImageSource')
BEGIN
    ALTER TABLE [dbo].[Animals] ADD [ImageSource] NVARCHAR(500) NULL;
END

-- Taxonomy: Catalogue of Life taxon ID
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Taxonomy') AND name = 'ColTaxonId')
BEGIN
    ALTER TABLE [dbo].[Taxonomy] ADD [ColTaxonId] NVARCHAR(200) NULL;
END

-- Taxonomy: Authorship citation
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Taxonomy') AND name = 'Authorship')
BEGIN
    ALTER TABLE [dbo].[Taxonomy] ADD [Authorship] NVARCHAR(500) NULL;
END

-- Taxonomy: Synonyms (JSON array or semicolon-delimited)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Taxonomy') AND name = 'Synonyms')
BEGIN
    ALTER TABLE [dbo].[Taxonomy] ADD [Synonyms] NVARCHAR(MAX) NULL;
END
