/* Users module (Borc.Users): schema usr. Generated, idempotent:
   dotnet ef migrations script -p Borc.Users -s Borc.Users --idempotent
   Not needed when Users:MigrateOnStartup is true (the default). */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[usr].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'usr') IS NULL EXEC(N'CREATE SCHEMA [usr];');
    CREATE TABLE [usr].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    IF SCHEMA_ID(N'usr') IS NULL EXEC(N'CREATE SCHEMA [usr];');
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    CREATE TABLE [usr].[Users] (
        [Id] bigint NOT NULL IDENTITY,
        [DisplayName] nvarchar(128) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsAdministrator] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [CreatedBy] bigint NULL,
        [UpdatedAt] datetime2(3) NULL,
        [UpdatedBy] bigint NULL,
        [LastSignInAt] datetime2(3) NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset(3) NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    CREATE TABLE [usr].[UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [usr].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    CREATE TABLE [usr].[UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] bigint NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [usr].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    CREATE TABLE [usr].[UserTokens] (
        [UserId] bigint NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [usr].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [usr].[UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [usr].[UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [usr].[Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Users_NormalizedEmail] ON [usr].[Users] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009062211_InitialUsers'
)
BEGIN
    INSERT INTO [usr].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009062211_InitialUsers', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    ALTER TABLE [usr].[UserTokens] DROP CONSTRAINT [PK_UserTokens];
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    ALTER TABLE [usr].[UserLogins] DROP CONSTRAINT [PK_UserLogins];
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[usr].[UserTokens]') AND [c].[name] = N'Name');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [usr].[UserTokens] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [usr].[UserTokens] ALTER COLUMN [Name] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[usr].[UserTokens]') AND [c].[name] = N'LoginProvider');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [usr].[UserTokens] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [usr].[UserTokens] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[usr].[UserLogins]') AND [c].[name] = N'ProviderKey');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [usr].[UserLogins] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [usr].[UserLogins] ALTER COLUMN [ProviderKey] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[usr].[UserLogins]') AND [c].[name] = N'LoginProvider');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [usr].[UserLogins] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [usr].[UserLogins] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    ALTER TABLE [usr].[UserLogins] ADD CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    ALTER TABLE [usr].[UserTokens] ADD CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [usr].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063953_IdentityKeyLengths'
)
BEGIN
    INSERT INTO [usr].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009063953_IdentityKeyLengths', N'9.0.0');
END;

COMMIT;
GO

