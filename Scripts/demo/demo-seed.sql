/* Demo data for manual testing (SQL Server). Not run by the application.
   Before running: start Mapper once against this database (creates the usr/org/acl schemas, runs scripts 009–011,
   syncs the Acl resources of the "Mapper" application). Then run this script once in SSMS on the same database.
   Everything it creates carries a "DEMO-" key/code, a "demo." user name or a "دمو - " role name;
   Scripts/demo/demo-remove.sql deletes it again.

   Creates:
   - org: 6 unit types (company … team, with levels), 5 position types, the company "شرکت نمونه" with 6 units below it,
          25 positions (5 without a holder, one of them covered by an acting holder), 20 users each holding one position,
          1 delegation.
   - usr: 20 users demo.* with password Test1234.
   - acl: 5 roles for the Mapper application, bound to the chart (unit with sub-units, positions) and to one user,
          with an Acl data-scope rule "all" for the auditor.
   - dbo: 9 static data sources with items, 7 templates (6 published with allowed units, 1 draft), sample records. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- prerequisites ---------- */
IF OBJECT_ID(N'usr.Users') IS NULL OR OBJECT_ID(N'org.OrgUnits') IS NULL OR OBJECT_ID(N'acl.Roles') IS NULL
   OR OBJECT_ID(N'dbo.TemplateOrgUnit') IS NULL
    THROW 50001, N'Start Mapper once against this database first (schemas usr, org, acl and script 011 are missing).', 1;
IF NOT EXISTS (SELECT 1 FROM acl.Applications WHERE [Key] = N'Mapper')
   OR NOT EXISTS (SELECT 1 FROM acl.Resources r JOIN acl.Applications a ON a.Id = r.ApplicationId
                  WHERE a.[Key] = N'Mapper' AND r.[Key] = N'Mapper.DataRecords')
    THROW 50002, N'The Acl resources of Mapper are not synced yet: start Mapper once.', 1;
IF EXISTS (SELECT 1 FROM org.OrgUnits WHERE NormalizedKey = N'DEMO-HQ')
    THROW 50003, N'Demo data already exists. Run Scripts/demo/demo-remove.sql first to recreate it.', 1;
IF EXISTS (SELECT 1 FROM usr.Users WHERE NormalizedUserName LIKE N'DEMO.%')
    THROW 50004, N'Users named demo.* already exist. Run Scripts/demo/demo-remove.sql first.', 1;

BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();
DECLARE @today datetime2 = CAST(CAST(@now AS date) AS datetime2);
DECLARE @from datetime2 = DATEADD(DAY, -90, @today);   -- assignments started three months ago
DECLARE @actor nvarchar(450) = N'demo-seed';
-- password "Test1234" (ASP.NET Core Identity V3 hash: PBKDF2-HMACSHA512, 100000 iterations)
DECLARE @hash nvarchar(max) = N'AQAAAAIAAYagAAAAEALCqbIg7BOsgY2N6tc2J0fgflMvQIUMVvlpYnUlaJa/iMO9TtoNpAYndaEU7vQWbw==';

/* =====================================================================
   1. Org chart types
   ===================================================================== */
INSERT INTO org.OrgUnitTypes (CreatedAt, CreatedBy, [Key], NormalizedKey, Title, SortOrder, IsActive, CanBeRoot, [Level])
VALUES
 (@now, @actor, N'DEMO-UT-COMPANY', N'DEMO-UT-COMPANY', N'شرکت',    1, 1, 1, 1),
 (@now, @actor, N'DEMO-UT-DEPUTY',  N'DEMO-UT-DEPUTY',  N'معاونت',  2, 1, 0, 2),
 (@now, @actor, N'DEMO-UT-MGMT',    N'DEMO-UT-MGMT',    N'مدیریت',  3, 1, 0, 3),
 (@now, @actor, N'DEMO-UT-DEPT',    N'DEMO-UT-DEPT',    N'اداره',   4, 1, 0, 4),
 (@now, @actor, N'DEMO-UT-SECTION', N'DEMO-UT-SECTION', N'واحد',    5, 1, 0, 5),
 (@now, @actor, N'DEMO-UT-TEAM',    N'DEMO-UT-TEAM',    N'تیم',     6, 1, 0, 6);

INSERT INTO org.PositionTypes (CreatedAt, CreatedBy, [Key], NormalizedKey, Title, SortOrder, IsActive)
VALUES
 (@now, @actor, N'DEMO-PT-MGR',     N'DEMO-PT-MGR',     N'مدیریتی',         1, 1),
 (@now, @actor, N'DEMO-PT-SUP',     N'DEMO-PT-SUP',     N'سرپرستی',         2, 1),
 (@now, @actor, N'DEMO-PT-EXP',     N'DEMO-PT-EXP',     N'کارشناسی',        3, 1),
 (@now, @actor, N'DEMO-PT-OPS',     N'DEMO-PT-OPS',     N'عملیاتی',         4, 1),
 (@now, @actor, N'DEMO-PT-SUPPORT', N'DEMO-PT-SUPPORT', N'اداری و پشتیبانی', 5, 1);

/* =====================================================================
   2. Units: company → 2 deputies (each with 2 departments) + IT management
   ===================================================================== */
DECLARE @units TABLE (K nvarchar(256), Title nvarchar(256), TypeKey nvarchar(256), ParentKey nvarchar(256) NULL, Code nvarchar(64), Sort int, Depth int);
INSERT INTO @units VALUES
 (N'DEMO-HQ',   N'شرکت نمونه',              N'DEMO-UT-COMPANY', NULL,        N'DEMO-100', 1, 0),
 (N'DEMO-FIN',  N'معاونت مالی و اداری',     N'DEMO-UT-DEPUTY',  N'DEMO-HQ',  N'DEMO-200', 1, 1),
 (N'DEMO-OPS',  N'معاونت بهره‌برداری',       N'DEMO-UT-DEPUTY',  N'DEMO-HQ',  N'DEMO-300', 2, 1),
 (N'DEMO-IT',   N'مدیریت فناوری اطلاعات',    N'DEMO-UT-MGMT',    N'DEMO-HQ',  N'DEMO-400', 3, 1),
 (N'DEMO-ACC',  N'اداره حسابداری',           N'DEMO-UT-DEPT',    N'DEMO-FIN', N'DEMO-210', 1, 2),
 (N'DEMO-HR',   N'اداره منابع انسانی',       N'DEMO-UT-DEPT',    N'DEMO-FIN', N'DEMO-220', 2, 2),
 (N'DEMO-PROD', N'اداره تولید',              N'DEMO-UT-DEPT',    N'DEMO-OPS', N'DEMO-310', 1, 2),
 (N'DEMO-QC',   N'اداره کنترل کیفیت',        N'DEMO-UT-DEPT',    N'DEMO-OPS', N'DEMO-320', 2, 2);

DECLARE @depth int = 0;
WHILE @depth <= 2
BEGIN
    INSERT INTO org.OrgUnits (Code, TypeId, ParentId, ManagerPositionId, ValidFrom, ValidTo, CreatedAt, CreatedBy,
                              [Key], NormalizedKey, Title, SortOrder, IsActive)
    SELECT u.Code, t.Id, p.Id, NULL, NULL, NULL, @now, @actor, u.K, UPPER(u.K), u.Title, u.Sort, 1
    FROM @units u
    JOIN org.OrgUnitTypes t ON t.NormalizedKey = u.TypeKey
    LEFT JOIN org.OrgUnits p ON p.NormalizedKey = u.ParentKey
    WHERE u.Depth = @depth;
    SET @depth += 1;
END;

/* =====================================================================
   3. Positions (parent = superior inside the same unit; heads have no parent)
   ===================================================================== */
DECLARE @positions TABLE (K nvarchar(256), Title nvarchar(256), UnitKey nvarchar(256), TypeKey nvarchar(256),
                          IsManagerial bit, ParentKey nvarchar(256) NULL, Sort int, IsHead bit);
INSERT INTO @positions VALUES
 (N'DEMO-P-CEO',        N'مدیرعامل',                    N'DEMO-HQ',   N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-CEO-OFFICE', N'مسئول دفتر مدیرعامل',          N'DEMO-HQ',   N'DEMO-PT-SUPPORT', 0, NULL,                2, 0),
 (N'DEMO-P-FIN-VP',     N'معاون مالی و اداری',           N'DEMO-FIN',  N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-OPS-VP',     N'معاون بهره‌برداری',            N'DEMO-OPS',  N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-IT-HEAD',    N'مدیر فناوری اطلاعات',          N'DEMO-IT',   N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-IT-DEV',     N'کارشناس نرم‌افزار',            N'DEMO-IT',   N'DEMO-PT-EXP',     0, NULL,                2, 0),
 (N'DEMO-P-IT-NET',     N'کارشناس شبکه',                N'DEMO-IT',   N'DEMO-PT-EXP',     0, NULL,                3, 0),
 (N'DEMO-P-IT-DBA',     N'کارشناس پایگاه داده',          N'DEMO-IT',   N'DEMO-PT-EXP',     0, NULL,                4, 0),
 (N'DEMO-P-ACC-HEAD',   N'رئیس اداره حسابداری',          N'DEMO-ACC',  N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-ACC-SR',     N'حسابدار ارشد',                N'DEMO-ACC',  N'DEMO-PT-SUP',     0, NULL,                2, 0),
 (N'DEMO-P-ACC-ACCT',   N'حسابدار',                     N'DEMO-ACC',  N'DEMO-PT-EXP',     0, N'DEMO-P-ACC-SR',    3, 0),
 (N'DEMO-P-ACC-TRS',    N'کارشناس خزانه‌داری',           N'DEMO-ACC',  N'DEMO-PT-EXP',     0, NULL,                4, 0),
 (N'DEMO-P-ACC-BUDGET', N'کارشناس بودجه',               N'DEMO-ACC',  N'DEMO-PT-EXP',     0, N'DEMO-P-ACC-SR',    5, 0),
 (N'DEMO-P-HR-HEAD',    N'رئیس اداره منابع انسانی',      N'DEMO-HR',   N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-HR-PAY',     N'کارشناس حقوق و دستمزد',        N'DEMO-HR',   N'DEMO-PT-EXP',     0, NULL,                2, 0),
 (N'DEMO-P-HR-PERS',    N'کارشناس کارگزینی',            N'DEMO-HR',   N'DEMO-PT-EXP',     0, NULL,                3, 0),
 (N'DEMO-P-PROD-HEAD',  N'رئیس اداره تولید',             N'DEMO-PROD', N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-PROD-SHIFT', N'سرپرست شیفت',                 N'DEMO-PROD', N'DEMO-PT-SUP',     1, NULL,                2, 0),
 (N'DEMO-P-PROD-OP1',   N'اپراتور تولید ۱',              N'DEMO-PROD', N'DEMO-PT-OPS',     0, N'DEMO-P-PROD-SHIFT', 3, 0),
 (N'DEMO-P-PROD-OP2',   N'اپراتور تولید ۲',              N'DEMO-PROD', N'DEMO-PT-OPS',     0, N'DEMO-P-PROD-SHIFT', 4, 0),
 (N'DEMO-P-PROD-OP3',   N'اپراتور تولید ۳',              N'DEMO-PROD', N'DEMO-PT-OPS',     0, N'DEMO-P-PROD-SHIFT', 5, 0),
 (N'DEMO-P-QC-HEAD',    N'رئیس اداره کنترل کیفیت',       N'DEMO-QC',   N'DEMO-PT-MGR',     1, NULL,                1, 1),
 (N'DEMO-P-QC-LAB',     N'کارشناس آزمایشگاه',            N'DEMO-QC',   N'DEMO-PT-EXP',     0, NULL,                2, 0),
 (N'DEMO-P-QC-EXP',     N'کارشناس کنترل کیفیت',          N'DEMO-QC',   N'DEMO-PT-EXP',     0, NULL,                3, 0),
 (N'DEMO-P-QC-SAMPLER', N'نمونه‌بردار',                  N'DEMO-QC',   N'DEMO-PT-OPS',     0, N'DEMO-P-QC-EXP',    4, 0);

-- first the positions without a parent, then their subordinates
INSERT INTO org.Positions (Code, OrgUnitId, TypeId, IsManagerial, ValidFrom, ValidTo, CreatedAt, CreatedBy,
                           [Key], NormalizedKey, Title, SortOrder, IsActive, ParentPositionId)
SELECT NULL, u.Id, t.Id, p.IsManagerial, NULL, NULL, @now, @actor, p.K, UPPER(p.K), p.Title, p.Sort, 1, NULL
FROM @positions p
JOIN org.OrgUnits u ON u.NormalizedKey = p.UnitKey
JOIN org.PositionTypes t ON t.NormalizedKey = p.TypeKey
WHERE p.ParentKey IS NULL;

INSERT INTO org.Positions (Code, OrgUnitId, TypeId, IsManagerial, ValidFrom, ValidTo, CreatedAt, CreatedBy,
                           [Key], NormalizedKey, Title, SortOrder, IsActive, ParentPositionId)
SELECT NULL, u.Id, t.Id, p.IsManagerial, NULL, NULL, @now, @actor, p.K, UPPER(p.K), p.Title, p.Sort, 1, pp.Id
FROM @positions p
JOIN org.OrgUnits u ON u.NormalizedKey = p.UnitKey
JOIN org.PositionTypes t ON t.NormalizedKey = p.TypeKey
JOIN org.Positions pp ON pp.NormalizedKey = p.ParentKey
WHERE p.ParentKey IS NOT NULL;

-- unit heads
UPDATE u SET ManagerPositionId = pos.Id
FROM org.OrgUnits u
JOIN @positions p ON p.UnitKey = u.NormalizedKey AND p.IsHead = 1
JOIN org.Positions pos ON pos.NormalizedKey = p.K;

/* =====================================================================
   4. Users (usr) and their positions
   ===================================================================== */
DECLARE @people TABLE (UserName nvarchar(256), DisplayName nvarchar(128), PositionKey nvarchar(256));
INSERT INTO @people VALUES
 (N'demo.ceo',      N'علی احمدی',       N'DEMO-P-CEO'),
 (N'demo.office',   N'شیما رستمی',      N'DEMO-P-CEO-OFFICE'),
 (N'demo.fin',      N'مریم رضایی',      N'DEMO-P-FIN-VP'),
 (N'demo.ops',      N'سارا کاظمی',      N'DEMO-P-OPS-VP'),
 (N'demo.it',       N'نسرین فراهانی',   N'DEMO-P-IT-HEAD'),
 (N'demo.dev',      N'بهزاد طاهری',     N'DEMO-P-IT-DEV'),
 (N'demo.net',      N'پریسا ملکی',      N'DEMO-P-IT-NET'),
 (N'demo.acc',      N'حسین کریمی',      N'DEMO-P-ACC-HEAD'),
 (N'demo.acc1',     N'زهرا محمدی',      N'DEMO-P-ACC-SR'),
 (N'demo.acc2',     N'رضا حسینی',       N'DEMO-P-ACC-ACCT'),
 (N'demo.treasury', N'فاطمه موسوی',     N'DEMO-P-ACC-TRS'),
 (N'demo.hr',       N'محمد جعفری',      N'DEMO-P-HR-HEAD'),
 (N'demo.hr1',      N'نرگس صادقی',      N'DEMO-P-HR-PAY'),
 (N'demo.hr2',      N'امیر نوری',       N'DEMO-P-HR-PERS'),
 (N'demo.prod',     N'مهدی رحیمی',      N'DEMO-P-PROD-HEAD'),
 (N'demo.shift',    N'الهام قاسمی',     N'DEMO-P-PROD-SHIFT'),
 (N'demo.op1',      N'جواد مرادی',      N'DEMO-P-PROD-OP1'),
 (N'demo.op2',      N'لیلا اکبری',      N'DEMO-P-PROD-OP2'),
 (N'demo.qc',       N'سعید یوسفی',      N'DEMO-P-QC-HEAD'),
 (N'demo.lab',      N'مینا عباسی',      N'DEMO-P-QC-LAB');

INSERT INTO usr.Users (DisplayName, IsActive, IsAdministrator, CreatedAt, CreatedBy, UserName, NormalizedUserName,
                       Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                       PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount)
SELECT DisplayName, 1, 0, @now, NULL, UserName, UPPER(UserName),
       NULL, NULL, 0, @hash, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), CONVERT(nvarchar(36), NEWID()),
       NULL, 0, 0, NULL, 1, 0
FROM @people;

-- user id as the modules store it (string)
DECLARE @ids TABLE (UserName nvarchar(256) PRIMARY KEY, UserId bigint, Uid nvarchar(450));
INSERT INTO @ids
SELECT u.UserName, u.Id, CONVERT(nvarchar(450), u.Id)
FROM usr.Users u JOIN @people p ON p.UserName = u.UserName;

-- primary assignments
INSERT INTO org.Assignments (PositionId, UserId, Kind, ValidFrom, ValidTo, Note, CreatedAt, CreatedBy)
SELECT pos.Id, i.Uid, 1, @from, NULL, NULL, @now, @actor
FROM @people p
JOIN @ids i ON i.UserName = p.UserName
JOIN org.Positions pos ON pos.NormalizedKey = p.PositionKey;

-- acting (سرپرست): the software expert also covers the vacant database position
INSERT INTO org.Assignments (PositionId, UserId, Kind, ValidFrom, ValidTo, Note, CreatedAt, CreatedBy)
SELECT pos.Id, i.Uid, 2, @from, NULL, N'سرپرستی تا جذب کارشناس پایگاه داده', @now, @actor
FROM org.Positions pos, @ids i
WHERE pos.NormalizedKey = N'DEMO-P-IT-DBA' AND i.UserName = N'demo.dev';

-- delegation: the head of accounting hands their authority to the senior accountant for 30 days
INSERT INTO org.Delegations (Kind, PositionId, FromUserId, ToUserId, ToPositionId, Priority, IsFullScope,
                             ValidFrom, ValidTo, Note, CreatedAt, CreatedBy)
SELECT 1, pos.Id, f.Uid, t.Uid, NULL, 0, 1, @today, DATEADD(DAY, 30, @today), N'تفویض نمونه (مرخصی رئیس حسابداری)', @now, @actor
FROM org.Positions pos, @ids f, @ids t
WHERE pos.NormalizedKey = N'DEMO-P-ACC-HEAD' AND f.UserName = N'demo.acc' AND t.UserName = N'demo.acc1';

INSERT INTO org.AuditLogs (At, ActorUserId, Operation, EntityType, EntityId, ChangeJson)
VALUES (@now, @actor, N'DemoSeed', N'OrgUnit', N'DEMO-HQ', N'{"note":"Scripts/demo/demo-seed.sql"}');

-- every server rereads the chart
UPDATE org.ChartStamps SET Stamp = NEWID(), UpdatedAt = @now WHERE Id = 1;

/* =====================================================================
   5. Acl roles for the Mapper application
   ===================================================================== */
DECLARE @app int = (SELECT Id FROM acl.Applications WHERE [Key] = N'Mapper');

DECLARE @roles TABLE (Name nvarchar(256), Description nvarchar(1000));
INSERT INTO @roles VALUES
 (N'دمو - کاربر ثبت داده',        N'همهٔ دارندگان پست در شرکت نمونه: دیدن قالب‌ها، ثبت داده و ایمپورت.'),
 (N'دمو - مدیر واحد',             N'سرپرستان واحدها: خروجی داده، حذف ایمپورت، دیدن چارت.'),
 (N'دمو - طراح قالب',             N'فناوری اطلاعات: ساخت و ویرایش قالب‌ها، منابع داده و پروفایل‌ها.'),
 (N'دمو - مدیر چارت و کاربران',   N'منابع انسانی: ویرایش چارت سازمانی و حساب‌های کاربری.'),
 (N'دمو - حسابرس',                N'دیدن همهٔ داده‌ها و ایمپورت‌ها (محدودهٔ دادهٔ «همه»).');

INSERT INTO acl.Roles (ApplicationId, Name, Description, ParentRoleId, IsSystem, IsActive, CreatedAt, CreatedBy)
SELECT @app, Name, Description, NULL, 0, 1, @now, @actor FROM @roles;

DECLARE @grants TABLE (RoleName nvarchar(256), ResourceKey nvarchar(256), ActionKey nvarchar(256));
INSERT INTO @grants VALUES
 (N'دمو - کاربر ثبت داده', N'Mapper.Templates', N'View'),
 (N'دمو - کاربر ثبت داده', N'Mapper.DataSources', N'View'),
 (N'دمو - کاربر ثبت داده', N'Mapper.MappingProfiles', N'View'),
 (N'دمو - کاربر ثبت داده', N'Mapper.DataRecords', N'View'),
 (N'دمو - کاربر ثبت داده', N'Mapper.DataRecords', N'Create'),
 (N'دمو - کاربر ثبت داده', N'Mapper.DataRecords', N'Edit'),
 (N'دمو - کاربر ثبت داده', N'Mapper.DataRecords', N'Delete'),
 (N'دمو - کاربر ثبت داده', N'Mapper.Imports', N'View'),
 (N'دمو - کاربر ثبت داده', N'Mapper.Imports', N'Create'),
 (N'دمو - کاربر ثبت داده', N'Mapper.Imports', N'Edit'),       -- column mapping of an upload
 (N'دمو - کاربر ثبت داده', N'Mapper.Imports', N'Approve'),
 (N'دمو - مدیر واحد', N'Mapper.DataRecords', N'Export'),
 (N'دمو - مدیر واحد', N'Mapper.Imports', N'Delete'),
 (N'دمو - مدیر واحد', N'Mapper.Imports', N'Export'),
 (N'دمو - مدیر واحد', N'Mapper.OrgChart', N'View'),
 (N'دمو - حسابرس', N'Mapper.DataRecords', N'View'),
 (N'دمو - حسابرس', N'Mapper.DataRecords', N'Export'),
 (N'دمو - حسابرس', N'Mapper.Imports', N'View'),
 (N'دمو - حسابرس', N'Mapper.Imports', N'Export'),
 (N'دمو - مدیر چارت و کاربران', N'Mapper.OrgChart', N'View'),
 (N'دمو - مدیر چارت و کاربران', N'Mapper.OrgChart', N'Edit'),
 (N'دمو - مدیر چارت و کاربران', N'Mapper.Users', N'View'),
 (N'دمو - مدیر چارت و کاربران', N'Mapper.Users', N'Edit');
-- template designer: every action on the definition pages
INSERT INTO @grants
SELECT N'دمو - طراح قالب', r.ResourceKey, a.ActionKey
FROM (VALUES (N'Mapper.Templates'), (N'Mapper.DataSources'), (N'Mapper.MappingProfiles')) r(ResourceKey)
CROSS JOIN (VALUES (N'View'), (N'Create'), (N'Edit'), (N'Delete'), (N'Approve')) a(ActionKey);

-- only (resource, action) pairs valid for the resource type
DECLARE @pairs TABLE (RoleName nvarchar(256), ResourceId int, ActionId int);
INSERT INTO @pairs
SELECT g.RoleName, r.Id, act.Id
FROM @grants g
JOIN acl.Resources r ON r.ApplicationId = @app AND r.[Key] = g.ResourceKey
JOIN acl.Actions act ON act.[Key] = g.ActionKey
JOIN acl.ResourceTypeActions rta ON rta.ResourceType = r.[Type] AND rta.ActionId = act.Id;

INSERT INTO acl.Permissions (ResourceId, ActionId)
SELECT DISTINCT p.ResourceId, p.ActionId FROM @pairs p
WHERE NOT EXISTS (SELECT 1 FROM acl.Permissions x WHERE x.ResourceId = p.ResourceId AND x.ActionId = p.ActionId);

INSERT INTO acl.RolePermissions (RoleId, PermissionId, Effect, CreatedAt, CreatedBy)
SELECT DISTINCT ro.Id, pe.Id, 1, @now, @actor
FROM @pairs p
JOIN acl.Roles ro ON ro.ApplicationId = @app AND ro.Name = p.RoleName
JOIN acl.Permissions pe ON pe.ResourceId = p.ResourceId AND pe.ActionId = p.ActionId;

-- everyone holding a position anywhere in the company
INSERT INTO acl.OrgUnitRoles (OrgUnitKey, RoleId, IncludeSubUnits, ValidFrom, ValidTo, CreatedAt, CreatedBy)
SELECT N'DEMO-HQ', Id, 1, NULL, NULL, @now, @actor FROM acl.Roles WHERE ApplicationId = @app AND Name = N'دمو - کاربر ثبت داده';

-- unit heads
INSERT INTO acl.PositionRoles (PositionKey, RoleId, ValidFrom, ValidTo, CreatedAt, CreatedBy)
SELECT p.K, ro.Id, NULL, NULL, @now, @actor
FROM @positions p, acl.Roles ro
WHERE p.IsHead = 1 AND ro.ApplicationId = @app AND ro.Name = N'دمو - مدیر واحد';

INSERT INTO acl.PositionRoles (PositionKey, RoleId, ValidFrom, ValidTo, CreatedAt, CreatedBy)
SELECT x.K, ro.Id, NULL, NULL, @now, @actor
FROM (VALUES (N'DEMO-P-IT-HEAD', N'دمو - طراح قالب'), (N'DEMO-P-IT-DEV', N'دمو - طراح قالب'),
             (N'DEMO-P-HR-HEAD', N'دمو - مدیر چارت و کاربران')) x(K, RoleName)
JOIN acl.Roles ro ON ro.ApplicationId = @app AND ro.Name = x.RoleName;

-- direct role: the treasury expert audits everything
INSERT INTO acl.UserRoles (UserId, RoleId, OrgUnitKey, ValidFrom, ValidTo, CreatedAt, CreatedBy)
SELECT i.Uid, ro.Id, NULL, NULL, NULL, @now, @actor
FROM @ids i, acl.Roles ro
WHERE i.UserName = N'demo.treasury' AND ro.ApplicationId = @app AND ro.Name = N'دمو - حسابرس';

INSERT INTO acl.DataScopeRules (RoleId, UserId, ResourceId, ScopeType, Expression, CreatedAt, CreatedBy)
SELECT ro.Id, NULL, r.Id, 4, NULL, @now, @actor
FROM acl.Roles ro
JOIN acl.Resources r ON r.ApplicationId = @app AND r.[Key] IN (N'Mapper.DataRecords', N'Mapper.Imports')
WHERE ro.ApplicationId = @app AND ro.Name = N'دمو - حسابرس';

INSERT INTO acl.AuditLogs (At, ActorUserId, Operation, EntityType, EntityId, ChangeJson)
VALUES (@now, @actor, N'DemoSeed', N'Role', NULL, N'{"note":"Scripts/demo/demo-seed.sql"}');

-- invalidate cached access everywhere
IF EXISTS (SELECT 1 FROM acl.UserAccessStamps WHERE UserId = N'*')
    UPDATE acl.UserAccessStamps SET Stamp = NEWID(), UpdatedAt = @now WHERE UserId = N'*';
ELSE
    INSERT INTO acl.UserAccessStamps (UserId, Stamp, UpdatedAt) VALUES (N'*', NEWID(), @now);

/* =====================================================================
   6. Static data sources
   ===================================================================== */
DECLARE @sources TABLE (Code varchar(100), Name nvarchar(250));
INSERT INTO @sources VALUES
 ('DEMO-DS-PROVINCE', N'استان‌ها'),
 ('DEMO-DS-PRODUCT',  N'محصولات'),
 ('DEMO-DS-MEASURE',  N'واحد اندازه‌گیری'),
 ('DEMO-DS-COSTCTR',  N'مراکز هزینه'),
 ('DEMO-DS-EXPENSE',  N'نوع هزینه'),
 ('DEMO-DS-SHIFT',    N'شیفت کاری'),
 ('DEMO-DS-QCRESULT', N'نتیجهٔ آزمون کیفیت'),
 ('DEMO-DS-LEAVE',    N'نوع مرخصی'),
 ('DEMO-DS-PRIORITY', N'اولویت درخواست');

INSERT INTO dbo.DataSource (Code, Name, SourceType, ConfigJson, IsActive, OrgUnitKey, CreatedAt, CreatedBy, IsDeleted)
SELECT Code, Name, 1, NULL, 1, NULL, @now, NULL, 0 FROM @sources;

DECLARE @items TABLE (Code varchar(100), Value nvarchar(500), Label nvarchar(500), Sort int);
INSERT INTO @items VALUES
 ('DEMO-DS-PROVINCE', N'THR', N'تهران', 0), ('DEMO-DS-PROVINCE', N'ISF', N'اصفهان', 1),
 ('DEMO-DS-PROVINCE', N'KHZ', N'خوزستان', 2), ('DEMO-DS-PROVINCE', N'FRS', N'فارس', 3),
 ('DEMO-DS-PROVINCE', N'KRZ', N'خراسان رضوی', 4), ('DEMO-DS-PROVINCE', N'AZS', N'آذربایجان شرقی', 5),
 ('DEMO-DS-PROVINCE', N'BSH', N'بوشهر', 6),
 ('DEMO-DS-PRODUCT', N'P100', N'لوله فولادی ۲ اینچ', 0), ('DEMO-DS-PRODUCT', N'P110', N'لوله فولادی ۴ اینچ', 1),
 ('DEMO-DS-PRODUCT', N'P200', N'ورق گالوانیزه', 2), ('DEMO-DS-PRODUCT', N'P300', N'پروفیل ساختمانی', 3),
 ('DEMO-DS-PRODUCT', N'P400', N'میلگرد آجدار', 4),
 ('DEMO-DS-MEASURE', N'KG', N'کیلوگرم', 0), ('DEMO-DS-MEASURE', N'TON', N'تن', 1),
 ('DEMO-DS-MEASURE', N'PCS', N'عدد', 2), ('DEMO-DS-MEASURE', N'M', N'متر', 3),
 ('DEMO-DS-COSTCTR', N'CC100', N'دفتر مرکزی', 0), ('DEMO-DS-COSTCTR', N'CC200', N'خط تولید', 1),
 ('DEMO-DS-COSTCTR', N'CC300', N'آزمایشگاه', 2), ('DEMO-DS-COSTCTR', N'CC400', N'انبار', 3),
 ('DEMO-DS-COSTCTR', N'CC500', N'فناوری اطلاعات', 4),
 ('DEMO-DS-EXPENSE', N'EX01', N'تنخواه', 0), ('DEMO-DS-EXPENSE', N'EX02', N'خرید مواد اولیه', 1),
 ('DEMO-DS-EXPENSE', N'EX03', N'تعمیر و نگهداری', 2), ('DEMO-DS-EXPENSE', N'EX04', N'ایاب و ذهاب', 3),
 ('DEMO-DS-EXPENSE', N'EX05', N'آموزش', 4), ('DEMO-DS-EXPENSE', N'EX06', N'پذیرایی', 5),
 ('DEMO-DS-SHIFT', N'M', N'صبح', 0), ('DEMO-DS-SHIFT', N'E', N'عصر', 1), ('DEMO-DS-SHIFT', N'N', N'شب', 2),
 ('DEMO-DS-QCRESULT', N'PASS', N'قبول', 0), ('DEMO-DS-QCRESULT', N'FAIL', N'مردود', 1),
 ('DEMO-DS-QCRESULT', N'RETEST', N'نیاز به آزمون مجدد', 2),
 ('DEMO-DS-LEAVE', N'ANNUAL', N'استحقاقی', 0), ('DEMO-DS-LEAVE', N'SICK', N'استعلاجی', 1),
 ('DEMO-DS-LEAVE', N'UNPAID', N'بدون حقوق', 2), ('DEMO-DS-LEAVE', N'HOURLY', N'ساعتی', 3),
 ('DEMO-DS-PRIORITY', N'LOW', N'کم', 0), ('DEMO-DS-PRIORITY', N'MED', N'متوسط', 1),
 ('DEMO-DS-PRIORITY', N'HIGH', N'زیاد', 2), ('DEMO-DS-PRIORITY', N'URGENT', N'فوری', 3);

INSERT INTO dbo.DataSourceItem (DataSourceId, Value, Label, SortOrder, CreatedAt, IsDeleted)
SELECT ds.Id, i.Value, i.Label, i.Sort, @now, 0
FROM @items i JOIN dbo.DataSource ds ON ds.Code = i.Code AND ds.IsDeleted = 0;

/* =====================================================================
   7. Templates: version 1 published (the draft template keeps a draft version), fields, allowed units
   ===================================================================== */
DECLARE @templates TABLE (Code varchar(50), Name nvarchar(200), Description nvarchar(1000), Published bit);
INSERT INTO @templates VALUES
 ('DEMO-TPL-EXPENSE',    N'ثبت هزینه‌ها',              N'هزینه‌های روزانه به تفکیک مرکز هزینه — حسابداری و معاونت مالی.', 1),
 ('DEMO-TPL-SALES',      N'فروش استانی',               N'فروش ماهانهٔ محصولات به تفکیک استان — معاونت مالی و حسابداری.', 1),
 ('DEMO-TPL-PRODUCTION', N'گزارش تولید روزانه',         N'مقدار تولید هر شیفت — اداره تولید.', 1),
 ('DEMO-TPL-QC',         N'نتایج کنترل کیفیت',          N'نتیجهٔ آزمون هر بچ — کنترل کیفیت و تولید (قالب مشترک دو واحد).', 1),
 ('DEMO-TPL-LEAVE',      N'ثبت مرخصی',                 N'مرخصی کارکنان — منابع انسانی.', 1),
 ('DEMO-TPL-ITREQ',      N'درخواست پشتیبانی فناوری',    N'درخواست‌های پشتیبانی — همهٔ واحدها (قالب مشترک).', 1),
 ('DEMO-TPL-DRAFT',      N'موجودی انبار (پیش‌نویس)',    N'قالب پیش‌نویس بدون واحد مجاز؛ برای تست انتشار و تعیین واحد.', 0);

INSERT INTO dbo.Template (Code, Name, Description, Status, OrgUnitKey, CreatedAt, CreatedBy, IsDeleted)
SELECT Code, Name, Description, CASE WHEN Published = 1 THEN 2 ELSE 1 END, NULL, @now, NULL, 0 FROM @templates;

INSERT INTO dbo.TemplateVersion (TemplateId, VersionNo, Status, SchemaJson, IsPublished, PublishedAt, PublishedBy, CreatedAt, CreatedBy, IsDeleted)
SELECT t.Id, 1, CASE WHEN x.Published = 1 THEN 2 ELSE 1 END, NULL, x.Published,
       CASE WHEN x.Published = 1 THEN @now END, NULL, @now, NULL, 0
FROM @templates x JOIN dbo.Template t ON t.Code = x.Code AND t.IsDeleted = 0;

DECLARE @fields TABLE (TemplateCode varchar(50), FieldKey varchar(200), Label nvarchar(250), DataType varchar(30),
                       DbType varchar(30), IsRequired bit, Sort int, [Length] int NULL, [Precision] tinyint NULL,
                       Scale tinyint NULL, Regex nvarchar(1000) NULL, SourceCode varchar(100) NULL);
INSERT INTO @fields VALUES
 ('DEMO-TPL-EXPENSE', 'F001', N'تاریخ',        'date',   'DATE',     1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-EXPENSE', 'F002', N'مرکز هزینه',   'text',   'VARCHAR2', 1, 2, 20,   NULL, NULL, NULL, 'DEMO-DS-COSTCTR'),
 ('DEMO-TPL-EXPENSE', 'F003', N'نوع هزینه',    'text',   'VARCHAR2', 1, 3, 20,   NULL, NULL, NULL, 'DEMO-DS-EXPENSE'),
 ('DEMO-TPL-EXPENSE', 'F004', N'مبلغ (ریال)',  'number', 'NUMBER',   1, 4, NULL, 18,   0,    NULL, NULL),
 ('DEMO-TPL-EXPENSE', 'F005', N'شرح',          'text',   'NVARCHAR2', 0, 5, 500, NULL, NULL, NULL, NULL),

 ('DEMO-TPL-SALES', 'F001', N'تاریخ',          'date',   'DATE',     1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-SALES', 'F002', N'استان',          'text',   'VARCHAR2', 1, 2, 10,   NULL, NULL, NULL, 'DEMO-DS-PROVINCE'),
 ('DEMO-TPL-SALES', 'F003', N'محصول',          'text',   'VARCHAR2', 1, 3, 10,   NULL, NULL, NULL, 'DEMO-DS-PRODUCT'),
 ('DEMO-TPL-SALES', 'F004', N'تعداد',          'number', 'NUMBER',   1, 4, NULL, 12,   0,    NULL, NULL),
 ('DEMO-TPL-SALES', 'F005', N'مبلغ کل (ریال)', 'number', 'NUMBER',   1, 5, NULL, 18,   0,    NULL, NULL),

 ('DEMO-TPL-PRODUCTION', 'F001', N'تاریخ',       'date',   'DATE',     1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-PRODUCTION', 'F002', N'شیفت',        'text',   'VARCHAR2', 1, 2, 5,    NULL, NULL, NULL, 'DEMO-DS-SHIFT'),
 ('DEMO-TPL-PRODUCTION', 'F003', N'محصول',       'text',   'VARCHAR2', 1, 3, 10,   NULL, NULL, NULL, 'DEMO-DS-PRODUCT'),
 ('DEMO-TPL-PRODUCTION', 'F004', N'مقدار',       'number', 'NUMBER',   1, 4, NULL, 12,   2,    NULL, NULL),
 ('DEMO-TPL-PRODUCTION', 'F005', N'واحد',        'text',   'VARCHAR2', 1, 5, 5,    NULL, NULL, NULL, 'DEMO-DS-MEASURE'),
 ('DEMO-TPL-PRODUCTION', 'F006', N'توضیحات',     'text',   'NVARCHAR2', 0, 6, 500, NULL, NULL, NULL, NULL),

 ('DEMO-TPL-QC', 'F001', N'تاریخ آزمون',  'date', 'DATE',      1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-QC', 'F002', N'محصول',        'text', 'VARCHAR2',  1, 2, 10,   NULL, NULL, NULL, 'DEMO-DS-PRODUCT'),
 ('DEMO-TPL-QC', 'F003', N'شماره بچ',     'text', 'VARCHAR2',  1, 3, 12,   NULL, NULL, N'^B-\d{4}$', NULL),
 ('DEMO-TPL-QC', 'F004', N'نتیجه',        'text', 'VARCHAR2',  1, 4, 10,   NULL, NULL, NULL, 'DEMO-DS-QCRESULT'),
 ('DEMO-TPL-QC', 'F005', N'توضیح',        'text', 'NVARCHAR2', 0, 5, 500,  NULL, NULL, NULL, NULL),

 ('DEMO-TPL-LEAVE', 'F001', N'از تاریخ',    'date',   'DATE',      1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-LEAVE', 'F002', N'تا تاریخ',    'date',   'DATE',      1, 2, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-LEAVE', 'F003', N'نوع مرخصی',   'text',   'VARCHAR2',  1, 3, 10,   NULL, NULL, NULL, 'DEMO-DS-LEAVE'),
 ('DEMO-TPL-LEAVE', 'F004', N'تعداد روز',   'number', 'NUMBER',    1, 4, NULL, 4,    1,    NULL, NULL),
 ('DEMO-TPL-LEAVE', 'F005', N'توضیح',       'text',   'NVARCHAR2', 0, 5, 500,  NULL, NULL, NULL, NULL),

 ('DEMO-TPL-ITREQ', 'F001', N'تاریخ',      'date', 'DATE',      1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-ITREQ', 'F002', N'عنوان',      'text', 'NVARCHAR2', 1, 2, 200,  NULL, NULL, NULL, NULL),
 ('DEMO-TPL-ITREQ', 'F003', N'اولویت',     'text', 'VARCHAR2',  1, 3, 10,   NULL, NULL, NULL, 'DEMO-DS-PRIORITY'),
 ('DEMO-TPL-ITREQ', 'F004', N'شرح',        'text', 'NVARCHAR2', 0, 4, 1000, NULL, NULL, NULL, NULL),

 ('DEMO-TPL-DRAFT', 'F001', N'تاریخ',      'date',   'DATE',     1, 1, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-TPL-DRAFT', 'F002', N'کالا',       'text',   'VARCHAR2', 1, 2, 10,   NULL, NULL, NULL, 'DEMO-DS-PRODUCT'),
 ('DEMO-TPL-DRAFT', 'F003', N'موجودی',     'number', 'NUMBER',   1, 3, NULL, 12,   2,    NULL, NULL),
 ('DEMO-TPL-DRAFT', 'F004', N'واحد',       'text',   'VARCHAR2', 1, 4, 5,    NULL, NULL, NULL, 'DEMO-DS-MEASURE');

INSERT INTO dbo.TemplateField (TemplateVersionId, FieldKey, Label, DataType, DbType, [Length], [Precision], Scale,
                               IsRequired, Regex, DefaultValue, DataSourceId, SortOrder, ConfigJson, CreatedAt, CreatedBy, IsDeleted)
SELECT v.Id, f.FieldKey, f.Label, f.DataType, f.DbType, f.[Length], f.[Precision], f.Scale,
       f.IsRequired, f.Regex, NULL, ds.Id, f.Sort, NULL, @now, NULL, 0
FROM @fields f
JOIN dbo.Template t ON t.Code = f.TemplateCode AND t.IsDeleted = 0
JOIN dbo.TemplateVersion v ON v.TemplateId = t.Id AND v.VersionNo = 1 AND v.IsDeleted = 0
LEFT JOIN dbo.DataSource ds ON ds.Code = f.SourceCode AND ds.IsDeleted = 0;

-- allowed units for data entry and import
INSERT INTO dbo.TemplateOrgUnit (TemplateId, OrgUnitKey)
SELECT t.Id, x.UnitKey
FROM (VALUES
  ('DEMO-TPL-EXPENSE', N'DEMO-ACC'), ('DEMO-TPL-EXPENSE', N'DEMO-FIN'),
  ('DEMO-TPL-SALES', N'DEMO-FIN'), ('DEMO-TPL-SALES', N'DEMO-ACC'),
  ('DEMO-TPL-PRODUCTION', N'DEMO-PROD'),
  ('DEMO-TPL-QC', N'DEMO-QC'), ('DEMO-TPL-QC', N'DEMO-PROD'),
  ('DEMO-TPL-LEAVE', N'DEMO-HR'),
  ('DEMO-TPL-ITREQ', N'DEMO-HQ'), ('DEMO-TPL-ITREQ', N'DEMO-FIN'), ('DEMO-TPL-ITREQ', N'DEMO-OPS'),
  ('DEMO-TPL-ITREQ', N'DEMO-IT'), ('DEMO-TPL-ITREQ', N'DEMO-ACC'), ('DEMO-TPL-ITREQ', N'DEMO-HR'),
  ('DEMO-TPL-ITREQ', N'DEMO-PROD'), ('DEMO-TPL-ITREQ', N'DEMO-QC')
) x(TemplateCode, UnitKey)
JOIN dbo.Template t ON t.Code = x.TemplateCode AND t.IsDeleted = 0;

/* =====================================================================
   8. Sample records (manual entry), stamped with the author and the author's unit
      Values: dates yyyy-MM-dd (Gregorian), numbers as JSON numbers, list fields as the item value.
   ===================================================================== */
DECLARE @records TABLE (TemplateCode varchar(50), UserName nvarchar(256), UnitKey nvarchar(256), DaysAgo int, DataJson nvarchar(max));
INSERT INTO @records VALUES
 ('DEMO-TPL-EXPENSE', N'demo.acc2', N'DEMO-ACC', 12, N'{"F001":"{d}","F002":"CC200","F003":"EX03","F004":45000000,"F005":"تعمیر پمپ خط ۲"}'),
 ('DEMO-TPL-EXPENSE', N'demo.acc2', N'DEMO-ACC', 6,  N'{"F001":"{d}","F002":"CC100","F003":"EX06","F004":3800000,"F005":"پذیرایی جلسهٔ هیئت‌مدیره"}'),
 ('DEMO-TPL-EXPENSE', N'demo.acc1', N'DEMO-ACC', 4,  N'{"F001":"{d}","F002":"CC400","F003":"EX02","F004":250000000,"F005":"خرید ورق خام"}'),
 ('DEMO-TPL-EXPENSE', N'demo.fin',  N'DEMO-FIN', 2,  N'{"F001":"{d}","F002":"CC500","F003":"EX05","F004":18000000,"F005":"دورهٔ آموزش امنیت"}'),
 ('DEMO-TPL-SALES',   N'demo.acc1', N'DEMO-ACC', 20, N'{"F001":"{d}","F002":"THR","F003":"P100","F004":1200,"F005":5400000000}'),
 ('DEMO-TPL-SALES',   N'demo.fin',  N'DEMO-FIN', 15, N'{"F001":"{d}","F002":"ISF","F003":"P400","F004":800,"F005":3100000000}'),
 ('DEMO-TPL-SALES',   N'demo.treasury', N'DEMO-ACC', 3, N'{"F001":"{d}","F002":"KHZ","F003":"P200","F004":450,"F005":2250000000}'),
 ('DEMO-TPL-PRODUCTION', N'demo.op1',   N'DEMO-PROD', 5, N'{"F001":"{d}","F002":"M","F003":"P100","F004":12.5,"F005":"TON","F006":null}'),
 ('DEMO-TPL-PRODUCTION', N'demo.op1',   N'DEMO-PROD', 4, N'{"F001":"{d}","F002":"M","F003":"P110","F004":9.75,"F005":"TON","F006":"توقف ۴۵ دقیقه‌ای برای تعویض قالب"}'),
 ('DEMO-TPL-PRODUCTION', N'demo.op2',   N'DEMO-PROD', 4, N'{"F001":"{d}","F002":"E","F003":"P300","F004":3200,"F005":"M","F006":null}'),
 ('DEMO-TPL-PRODUCTION', N'demo.shift', N'DEMO-PROD', 1, N'{"F001":"{d}","F002":"N","F003":"P400","F004":15,"F005":"TON","F006":"شیفت شب با نیروی کمکی"}'),
 ('DEMO-TPL-QC', N'demo.lab', N'DEMO-QC',   5, N'{"F001":"{d}","F002":"P100","F003":"B-1021","F004":"PASS","F005":null}'),
 ('DEMO-TPL-QC', N'demo.lab', N'DEMO-QC',   3, N'{"F001":"{d}","F002":"P110","F003":"B-1022","F004":"RETEST","F005":"ضخامت دیواره خارج از تلرانس"}'),
 ('DEMO-TPL-QC', N'demo.shift', N'DEMO-PROD', 2, N'{"F001":"{d}","F002":"P300","F003":"B-1023","F004":"FAIL","F005":"ترک در جوش"}'),
 ('DEMO-TPL-LEAVE', N'demo.hr1', N'DEMO-HR', 10, N'{"F001":"{d}","F002":"{d}","F003":"ANNUAL","F004":1,"F005":null}'),
 ('DEMO-TPL-LEAVE', N'demo.hr2', N'DEMO-HR', 7,  N'{"F001":"{d}","F002":"{d}","F003":"SICK","F004":2,"F005":"گواهی پزشک ضمیمه شد"}'),
 ('DEMO-TPL-ITREQ', N'demo.hr2',   N'DEMO-HR',   6, N'{"F001":"{d}","F002":"خرابی چاپگر","F003":"MED","F004":"چاپگر طبقه دوم کاغذ گیر می‌کند"}'),
 ('DEMO-TPL-ITREQ', N'demo.op1',   N'DEMO-PROD', 3, N'{"F001":"{d}","F002":"دسترسی به سامانه تولید","F003":"HIGH","F004":null}'),
 ('DEMO-TPL-ITREQ', N'demo.acc2',  N'DEMO-ACC',  2, N'{"F001":"{d}","F002":"نصب نرم‌افزار حسابداری","F003":"LOW","F004":null}'),
 ('DEMO-TPL-ITREQ', N'demo.ceo',   N'DEMO-HQ',   1, N'{"F001":"{d}","F002":"راه‌اندازی جلسه آنلاین","F003":"URGENT","F004":"جلسه با سهامداران ساعت ۱۰"}');

INSERT INTO dbo.DataRecord (TemplateId, TemplateVersionId, DataJson, SourceType, ImportBatchId, ImportRowId, OrgUnitKey,
                            CreatedAt, CreatedBy, IsDeleted)
SELECT t.Id, v.Id,
       REPLACE(r.DataJson, N'{d}', CONVERT(nchar(10), DATEADD(DAY, -r.DaysAgo, @today), 23)),
       1, NULL, NULL, r.UnitKey, DATEADD(DAY, -r.DaysAgo, @now), i.UserId, 0
FROM @records r
JOIN dbo.Template t ON t.Code = r.TemplateCode AND t.IsDeleted = 0
JOIN dbo.TemplateVersion v ON v.TemplateId = t.Id AND v.VersionNo = 1 AND v.IsDeleted = 0
JOIN @ids i ON i.UserName = r.UserName;

COMMIT TRANSACTION;

/* summary */
SELECT N'کاربران' AS [بخش], COUNT(*) AS [تعداد] FROM usr.Users WHERE NormalizedUserName LIKE N'DEMO.%'
UNION ALL SELECT N'واحدها', COUNT(*) FROM org.OrgUnits WHERE NormalizedKey LIKE N'DEMO-%'
UNION ALL SELECT N'پست‌ها', COUNT(*) FROM org.Positions WHERE NormalizedKey LIKE N'DEMO-%'
UNION ALL SELECT N'انتصاب‌ها', COUNT(*) FROM org.Assignments a JOIN org.Positions p ON p.Id = a.PositionId WHERE p.NormalizedKey LIKE N'DEMO-%'
UNION ALL SELECT N'نقش‌های Acl', COUNT(*) FROM acl.Roles WHERE Name LIKE N'دمو - %'
UNION ALL SELECT N'منابع داده', COUNT(*) FROM dbo.DataSource WHERE Code LIKE 'DEMO-%' AND IsDeleted = 0
UNION ALL SELECT N'قالب‌ها', COUNT(*) FROM dbo.Template WHERE Code LIKE 'DEMO-%' AND IsDeleted = 0
UNION ALL SELECT N'رکوردها', COUNT(*) FROM dbo.DataRecord r JOIN dbo.Template t ON t.Id = r.TemplateId WHERE t.Code LIKE 'DEMO-%';
GO
