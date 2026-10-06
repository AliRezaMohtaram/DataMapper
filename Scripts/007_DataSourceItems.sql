/* منبع داده: گزینه‌ها (فهرست دستی و فایل Excel/CSV) + نوع‌های جدید */

BEGIN TRANSACTION;

/* مقدار 2 قبلاً SqlQuery بود (بدون پیاده‌سازی) و حالا Template است.
   اگر منبع داده‌ای با نوع 2 دارید، باید پیش از ادامه بررسی شود. */
IF EXISTS (SELECT 1 FROM dbo.DataSource WHERE SourceType = 2 AND IsDeleted = 0)
BEGIN
    RAISERROR(N'منبع داده‌ای با نوع 2 (SqlQuery قدیمی) وجود دارد؛ ابتدا آن را بررسی یا حذف کنید.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

IF OBJECT_ID('dbo.DataSourceItem', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataSourceItem
    (
        Id           BIGINT IDENTITY(1,1) NOT NULL,
        DataSourceId BIGINT NOT NULL,

        [Value]      NVARCHAR(500) NOT NULL,
        [Label]      NVARCHAR(500) NOT NULL,
        SortOrder    INT NOT NULL CONSTRAINT DF_DataSourceItem_SortOrder DEFAULT (0),

        CreatedAt    DATETIME2(3) NOT NULL CONSTRAINT DF_DataSourceItem_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy    BIGINT NULL,
        UpdatedAt    DATETIME2(3) NULL,
        UpdatedBy    BIGINT NULL,
        DeletedAt    DATETIME2(3) NULL,
        DeletedBy    BIGINT NULL,
        IsDeleted    BIT NOT NULL CONSTRAINT DF_DataSourceItem_IsDeleted DEFAULT (0),

        CONSTRAINT PK_DataSourceItem PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_DataSourceItem_DataSource FOREIGN KEY (DataSourceId) REFERENCES dbo.DataSource (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_DataSourceItem_Source_Value
        ON dbo.DataSourceItem (DataSourceId, [Value])
        WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_DataSourceItem_Source_Sort
        ON dbo.DataSourceItem (DataSourceId, SortOrder);
END;

COMMIT TRANSACTION;
