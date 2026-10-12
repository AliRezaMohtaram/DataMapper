/* Import validation and commit run in the background (ImportJobWorker): the batch keeps the column mapping to apply,
   how many rows the running step has processed (progress) and the last error.
   Idempotent; also run at startup (ModuleDatabaseMigrator). */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.ImportBatch', 'MappingJson') IS NULL
    ALTER TABLE dbo.ImportBatch ADD MappingJson NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH('dbo.ImportBatch', 'ProcessedRows') IS NULL
    ALTER TABLE dbo.ImportBatch ADD ProcessedRows INT NOT NULL CONSTRAINT DF_ImportBatch_ProcessedRows DEFAULT (0);
GO

IF COL_LENGTH('dbo.ImportBatch', 'LastError') IS NULL
    ALTER TABLE dbo.ImportBatch ADD LastError NVARCHAR(1000) NULL;
GO
