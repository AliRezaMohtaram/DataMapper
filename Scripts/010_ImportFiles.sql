/* فایل اصلیِ هر ایمپورت (قبلاً فقط نام فایل نگه داشته می‌شد).
   Idempotent: می‌توان دوباره اجرا کرد؛ در شروع برنامه هم اجرا می‌شود (ModuleDatabaseMigrator). */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.ImportBatchFile', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportBatchFile
    (
        ImportBatchId BIGINT          NOT NULL,
        ContentType   VARCHAR(200)    NOT NULL,
        Content       VARBINARY(MAX)  NOT NULL,
        CreatedAt     DATETIME2(3)    NOT NULL,
        CONSTRAINT PK_ImportBatchFile PRIMARY KEY (ImportBatchId),
        CONSTRAINT FK_ImportBatchFile_ImportBatch FOREIGN KEY (ImportBatchId) REFERENCES dbo.ImportBatch (Id)
    );
END
GO
