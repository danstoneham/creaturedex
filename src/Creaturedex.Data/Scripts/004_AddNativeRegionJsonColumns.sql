-- Add separate JSON columns for native continents and countries
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'NativeContinentsJson')
    ALTER TABLE [dbo].[Animals] ADD [NativeContinentsJson] NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Animals') AND name = 'NativeCountriesJson')
    ALTER TABLE [dbo].[Animals] ADD [NativeCountriesJson] NVARCHAR(MAX) NULL;
