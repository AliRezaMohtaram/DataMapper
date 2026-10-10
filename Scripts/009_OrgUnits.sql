/* واحد سازمانی برای داده‌های Mapper (محدودهٔ داده بر اساس چارت سازمانی).
   OrgUnitKey = کلید پایدار واحد در چارت (org.OrgUnits.[Key])؛ NULL = عمومی (همه می‌بینند).
   Idempotent: می‌توان دوباره اجرا کرد. */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Template', 'OrgUnitKey') IS NULL
    ALTER TABLE dbo.Template ADD OrgUnitKey NVARCHAR(256) NULL;

IF COL_LENGTH('dbo.DataSource', 'OrgUnitKey') IS NULL
    ALTER TABLE dbo.DataSource ADD OrgUnitKey NVARCHAR(256) NULL;

IF COL_LENGTH('dbo.MappingProfile', 'OrgUnitKey') IS NULL
    ALTER TABLE dbo.MappingProfile ADD OrgUnitKey NVARCHAR(256) NULL;

IF COL_LENGTH('dbo.ImportBatch', 'OrgUnitKey') IS NULL
    ALTER TABLE dbo.ImportBatch ADD OrgUnitKey NVARCHAR(256) NULL;

IF COL_LENGTH('dbo.DataRecord', 'OrgUnitKey') IS NULL
    ALTER TABLE dbo.DataRecord ADD OrgUnitKey NVARCHAR(256) NULL;

COMMIT TRANSACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Template_OrgUnitKey' AND object_id = OBJECT_ID('dbo.Template'))
    CREATE INDEX IX_Template_OrgUnitKey ON dbo.Template (OrgUnitKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataSource_OrgUnitKey' AND object_id = OBJECT_ID('dbo.DataSource'))
    CREATE INDEX IX_DataSource_OrgUnitKey ON dbo.DataSource (OrgUnitKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MappingProfile_OrgUnitKey' AND object_id = OBJECT_ID('dbo.MappingProfile'))
    CREATE INDEX IX_MappingProfile_OrgUnitKey ON dbo.MappingProfile (OrgUnitKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImportBatch_OrgUnitKey' AND object_id = OBJECT_ID('dbo.ImportBatch'))
    CREATE INDEX IX_ImportBatch_OrgUnitKey ON dbo.ImportBatch (OrgUnitKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataRecord_OrgUnitKey' AND object_id = OBJECT_ID('dbo.DataRecord'))
    CREATE INDEX IX_DataRecord_OrgUnitKey ON dbo.DataRecord (OrgUnitKey);
GO
