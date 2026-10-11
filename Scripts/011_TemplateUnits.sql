/* واحدهای مجاز هر قالب برای ثبت داده و ایمپورت (چند واحد برای یک قالب).
   قالب‌هایی که قبلاً واحد داشتند همان واحد را به‌عنوان واحد مجاز می‌گیرند.
   Idempotent: می‌توان دوباره اجرا کرد؛ در شروع برنامه هم اجرا می‌شود (ModuleDatabaseMigrator). */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.TemplateOrgUnit', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TemplateOrgUnit
    (
        TemplateId BIGINT        NOT NULL,
        OrgUnitKey NVARCHAR(256) NOT NULL,
        CONSTRAINT PK_TemplateOrgUnit PRIMARY KEY (TemplateId, OrgUnitKey),
        CONSTRAINT FK_TemplateOrgUnit_Template FOREIGN KEY (TemplateId) REFERENCES dbo.Template (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_TemplateOrgUnit_OrgUnitKey ON dbo.TemplateOrgUnit (OrgUnitKey);

    INSERT INTO dbo.TemplateOrgUnit (TemplateId, OrgUnitKey)
    SELECT Id, OrgUnitKey FROM dbo.Template WHERE OrgUnitKey IS NOT NULL;
END
GO
