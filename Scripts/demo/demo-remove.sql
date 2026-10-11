/* Removes the demo data of Scripts/demo/demo-seed.sql (SQL Server), including what was entered or imported on the
   demo templates while testing. Rows outside the demo set are left alone: a demo data source still used by another
   template stays, and so does a demo unit/position type still used by other units or positions. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

/* ---------- Mapper (dbo) ---------- */
DECLARE @tpl TABLE (Id bigint PRIMARY KEY);
INSERT INTO @tpl SELECT Id FROM dbo.Template WHERE Code LIKE 'DEMO-TPL-%';
DECLARE @ver TABLE (Id bigint PRIMARY KEY);
INSERT INTO @ver SELECT v.Id FROM dbo.TemplateVersion v JOIN @tpl t ON t.Id = v.TemplateId;
DECLARE @batch TABLE (Id bigint PRIMARY KEY);
INSERT INTO @batch SELECT b.Id FROM dbo.ImportBatch b JOIN @ver v ON v.Id = b.TemplateVersionId;
DECLARE @profile TABLE (Id bigint PRIMARY KEY);
INSERT INTO @profile SELECT p.Id FROM dbo.MappingProfile p JOIN @ver v ON v.Id = p.TemplateVersionId;

DELETE r FROM dbo.DataRecord r WHERE r.TemplateId IN (SELECT Id FROM @tpl) OR r.ImportBatchId IN (SELECT Id FROM @batch);
DELETE x FROM dbo.ImportRow x JOIN @batch b ON b.Id = x.ImportBatchId;
DELETE x FROM dbo.ImportBatchFile x JOIN @batch b ON b.Id = x.ImportBatchId;
DELETE x FROM dbo.ImportBatch x JOIN @batch b ON b.Id = x.Id;
DELETE x FROM dbo.MappingRule x JOIN @profile p ON p.Id = x.MappingProfileId;
DELETE x FROM dbo.MappingProfile x JOIN @profile p ON p.Id = x.Id;
DELETE a FROM dbo.TemplateFieldAlias a JOIN dbo.TemplateField f ON f.Id = a.TemplateFieldId JOIN @ver v ON v.Id = f.TemplateVersionId;
DELETE x FROM dbo.TemplateLayout x JOIN @ver v ON v.Id = x.TemplateVersionId;
DELETE x FROM dbo.TemplateField x JOIN @ver v ON v.Id = x.TemplateVersionId;
DELETE x FROM dbo.TemplateOrgUnit x JOIN @tpl t ON t.Id = x.TemplateId;
DELETE x FROM dbo.TemplateVersion x JOIN @ver v ON v.Id = x.Id;
DELETE x FROM dbo.Template x JOIN @tpl t ON t.Id = x.Id;

DECLARE @ds TABLE (Id bigint PRIMARY KEY);
INSERT INTO @ds SELECT d.Id FROM dbo.DataSource d
WHERE d.Code LIKE 'DEMO-DS-%' AND NOT EXISTS (SELECT 1 FROM dbo.TemplateField f WHERE f.DataSourceId = d.Id);
DELETE x FROM dbo.DataSourceItem x JOIN @ds d ON d.Id = x.DataSourceId;
DELETE x FROM dbo.DataSource x JOIN @ds d ON d.Id = x.Id;

/* ---------- users ---------- */
DECLARE @users TABLE (Uid nvarchar(450) PRIMARY KEY);
INSERT INTO @users SELECT CONVERT(nvarchar(450), Id) FROM usr.Users WHERE NormalizedUserName LIKE N'DEMO.%';

/* ---------- Acl ---------- */
DECLARE @roles TABLE (Id int PRIMARY KEY);
INSERT INTO @roles SELECT Id FROM acl.Roles WHERE Name LIKE N'دمو - %';

DELETE x FROM acl.DataScopeRules x WHERE x.RoleId IN (SELECT Id FROM @roles) OR x.UserId IN (SELECT Uid FROM @users);
DELETE x FROM acl.RolePermissions x JOIN @roles r ON r.Id = x.RoleId;
DELETE x FROM acl.UserRoles x WHERE x.RoleId IN (SELECT Id FROM @roles) OR x.UserId IN (SELECT Uid FROM @users);
DELETE x FROM acl.UserPermissions x JOIN @users u ON u.Uid = x.UserId;
DELETE x FROM acl.PositionRoles x WHERE x.RoleId IN (SELECT Id FROM @roles) OR x.PositionKey LIKE N'DEMO-%';
DELETE x FROM acl.OrgUnitRoles x WHERE x.RoleId IN (SELECT Id FROM @roles) OR x.OrgUnitKey LIKE N'DEMO-%';
UPDATE acl.Roles SET ParentRoleId = NULL WHERE ParentRoleId IN (SELECT Id FROM @roles);
DELETE x FROM acl.Roles x JOIN @roles r ON r.Id = x.Id;
DELETE x FROM acl.UserAccessStamps x JOIN @users u ON u.Uid = x.UserId;
UPDATE acl.UserAccessStamps SET Stamp = NEWID(), UpdatedAt = @now WHERE UserId = N'*';

/* ---------- org chart ---------- */
DECLARE @pos TABLE (Id int PRIMARY KEY);
INSERT INTO @pos SELECT Id FROM org.Positions WHERE NormalizedKey LIKE N'DEMO-%';
DECLARE @unit TABLE (Id int PRIMARY KEY);
INSERT INTO @unit SELECT Id FROM org.OrgUnits WHERE NormalizedKey LIKE N'DEMO-%';

DELETE s FROM org.DelegationScopes s JOIN org.Delegations d ON d.Id = s.DelegationId
WHERE d.PositionId IN (SELECT Id FROM @pos) OR d.ToPositionId IN (SELECT Id FROM @pos)
   OR d.FromUserId IN (SELECT Uid FROM @users) OR d.ToUserId IN (SELECT Uid FROM @users);
DELETE d FROM org.Delegations d
WHERE d.PositionId IN (SELECT Id FROM @pos) OR d.ToPositionId IN (SELECT Id FROM @pos)
   OR d.FromUserId IN (SELECT Uid FROM @users) OR d.ToUserId IN (SELECT Uid FROM @users);
DELETE a FROM org.Assignments a WHERE a.PositionId IN (SELECT Id FROM @pos) OR a.UserId IN (SELECT Uid FROM @users);
UPDATE org.OrgUnits SET ManagerPositionId = NULL WHERE ManagerPositionId IN (SELECT Id FROM @pos);
UPDATE org.Positions SET ParentPositionId = NULL WHERE ParentPositionId IN (SELECT Id FROM @pos);
DELETE x FROM org.Positions x JOIN @pos p ON p.Id = x.Id;
UPDATE org.OrgUnits SET ParentId = NULL WHERE ParentId IN (SELECT Id FROM @unit);
DELETE x FROM org.OrgUnits x JOIN @unit u ON u.Id = x.Id;
DELETE t FROM org.OrgUnitTypes t
WHERE t.NormalizedKey LIKE N'DEMO-%' AND NOT EXISTS (SELECT 1 FROM org.OrgUnits u WHERE u.TypeId = t.Id);
DELETE t FROM org.PositionTypes t
WHERE t.NormalizedKey LIKE N'DEMO-%' AND NOT EXISTS (SELECT 1 FROM org.Positions p WHERE p.TypeId = t.Id);
UPDATE org.ChartStamps SET Stamp = NEWID(), UpdatedAt = @now WHERE Id = 1;

DELETE FROM usr.Users WHERE NormalizedUserName LIKE N'DEMO.%';   -- claims, logins and tokens cascade

COMMIT TRANSACTION;

SELECT N'داده‌های نمونه حذف شد.' AS [نتیجه];
