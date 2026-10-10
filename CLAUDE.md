# DataMapper (Mapper)

- Talk to the user in Persian; code, identifiers, comments and commit messages in English.
- .NET 9, EF Core 9 (SQL Server). Mapper's own schema changes are hand-written scripts in `Scripts/`.
- UI: MX design system (`Mapper/wwwroot/css/mx.css`, `js/mx.js`); modal-first forms (`data-modal`, `X-MX-Modal`,
  `ModalResults`). Do not build on `theme.css` / `mx-legacy.css`.
- The user runs browser tests; Claude runs automated tests and writes manual checklists under `docs/`.
- Build: `dotnet build Mapper.sln`. Tests: `dotnet test Borc.Users.Tests` (cloud container: `DOTNET_ROLL_FORWARD=Major`).

## Users module (`Borc.Users`, `Borc.Users.Web`)

Sign-in and account management, kept independent of the Mapper projects so it can be extracted later; see
`Borc.Users/README.md`. Wiring in Mapper: `Program.cs` (`AddBorcUsers`, `AddBorcUsersUi`, `UseAuthentication`,
`MapRazorPages`), sign-in required everywhere, `_Layout` (user chip, sign-out, change password, "کاربران" via Acl),
`HttpCurrentUser` → `ICurrentUser` (Application) → `AuditStampInterceptor` fills `CreatedBy/UpdatedBy/DeletedBy`.
User ids are `long`; other modules (OrgChart, Acl) store them as strings. Manual checklist: `docs/users-test-checklist.md`.

## OrgChart and Acl modules

From github.com/AliRezaMohtaram/organization (OrgChart 0.2.0) and /Acl (0.5.0) as NuGet packages from local folders,
configured in `nuget.config`: `../organization/artifacts/packages` and `../Acl/artifacts/packages` (pack Acl first, then
OrgChart). Package source mapping pins `OrgChart.*`/`Acl.*` to those folders — nuget.org has unrelated packages with the
same names (e.g. Acl.Core 3.x). Bump the module version when repacking (NuGet caches by version).
Cloud container: the clones are /home/user/acl and /home/user/organization; a symlink /home/user/Acl → acl makes the paths work.

- `Program.cs`: `AddOrgChart().AddSqlServerStore().AddHttpContextUser().AddUserDirectory<MapperUserDirectory>().AddAcl()
  .AddAdminUi(view/edit policies = Acl permission on Mapper.OrgChart)`, `AddAccessControl(ApplicationKey "Mapper",
  SuperAdminUserIds from Acl:SuperAdminUserIds).AddSqlServerStore().AddUserDirectory<MapperUserDirectory>().AddAdminUi()`,
  `UseAuthentication → UseAccessControl → UseAuthorization`.
- `Modules/`: `MapperUserDirectory` (both modules' IUserDirectory over `IUserLookup`), `ModuleDatabaseMigrator` (org/acl
  migrations at startup, `Modules:MigrateOnStartup`, registered before AddAccessControl), `EndOrgChartRolesOnDeactivation`
  (`IUserStatusListener`: ends/removes the user's assignments and delegations), `MapperResources` (`[Resource]` Mapper,
  Mapper.OrgChart).
- `_Layout`: section "سازمان" (chart via `acl-res`, my delegations, Acl via `acl-res="Acl.Admin"`); controller links carry
  `asp-area=""` so they work from area pages. Module pages render in this layout (no `@section`s).
- No shared menu contract (decided to keep modules independent): the host lists module pages in its layout.

## Access control of Mapper's own pages

- Acl resources (`Modules/MapperResources.cs`): module `Mapper` with pages `Mapper.Templates` (templates, versions, fields,
  layouts), `Mapper.DataSources`, `Mapper.MappingProfiles`, `Mapper.Imports`, `Mapper.DataRecords`, and module
  `Mapper.OrgChart`, `Mapper.Users` (user pages: View = list, Edit = every change; `AddBorcUsersUi` policies, the
  account's administrator flag is hidden and unused in Mapper). A grant on `Mapper` is inherited by everything below it.
- Controllers: `[RequirePermission]` — class = View; Create/Edit/Delete actions as named; Approve = publish/archive a
  template version, commit an import; template fields/layouts and new versions = Edit on Templates; DataSources has no
  class-level check so `Options` (used by record forms) only needs sign-in; `DataRecords.Save` checks Create or Edit in code.
  The dashboard is open to every signed-in user (landing page).
- Views: links/forms to protected actions carry `acl-res`/`acl-act` (hidden when denied); `_Layout` filters the menu
  with `IAccessService` (UI hiding is cosmetic; the controllers enforce).
- `MapperAccessBootstrapper` (first run only): role "مدیر Mapper" with every action on `Mapper`, given to
  `Acl:SuperAdminUserIds`; left alone once it exists.

## Data scope by org unit (user decisions: all five kinds; default unit + choice; rows without a unit are public)

- `Template`, `DataSource`, `MappingProfile`, `ImportBatch`, `DataRecord` implement `IOrgUnitOwned` (`OrgUnitKey`
  nvarchar(256) = the chart's stable unit key, null = public). Script `Scripts/009_OrgUnits.sql` (idempotent; also run at
  startup by `ModuleDatabaseMigrator` as an embedded resource).
- Row filter in `BorcDataMapperDbContext` (soft delete AND (unit null OR scope)) over `IDataScopeProvider` (Application);
  Mapper's `AclDataScopeProvider` reads Acl's data scope per resource (`Mapper.Templates`, …): All, Own (`CreatedBy`),
  OrgUnit / OrgUnitAndChildren. No HTTP request → everything; request without loaded access → public rows only.
  No data-scope rule for a resource → only public rows (Acl fails closed). Rules are set per role in Acl
  ("محدودهٔ داده"); a rule on `Mapper` is inherited by every page.
- New rows: `AuditStampInterceptor` stamps the unit from `IOrgUnitSelection` (`OrgUnitChoices`): the form's validated
  choice (`[OrgUnitField(resource, allowPublic)]` on create actions + `_OrgUnitField` partial in the create forms of
  templates, data sources, mapping profiles, imports), else the user's current primary (else acting) position's unit.
  Choices = units of positions held now ∪ units of the data scope (all active units with "all"); "public" only for
  definitions and only with scope "all". Import records take the import's unit (`DataRecord.CreateFromImport`).
- Existing rows: `_OrgUnitBadge` on the detail pages + `OrgUnitsController.Change` (modal; Edit on the resource, row
  visible, new unit among the choices). Application: `OrgUnits/OrgUnitOwnership.cs`.
- `MapperAccessBootstrapper`: the "مدیر Mapper" role gets data scope All on `Mapper` (also added once to an existing role
  that has no data-scope rule).
- Acl 0.4.0: role assignments pick users by name (search over `MapperUserDirectory`); unknown user ids are rejected.
- DI gotcha: `UserAdministration` resolves `IUserStatusListener`s lazily — the org chart listener depends (via Acl) on
  `IUserDirectory` → `IUserLookup`, and constructor injection made a cycle that crashed startup (stack overflow).
- Known limit: lookups follow the scope too (a field's data source in another unit gives no options).

## No typed keys (user decision: nothing is keyed in by hand)

- Codes are generated (`Application/Common/Codes/GeneratedCodes`): templates `TPL-0001`, data sources `DS-0001`, template
  fields `F001` (numbered across all versions of the template, deleted rows count, so a key is never reused). The commands
  still accept an explicit code (blank = generated); the forms no longer send one. OrgChart 0.2.0 does the same for units,
  positions and types (`UNIT-0001`, `POS-0001`, `UTYPE-`, `PTYPE-`).
- Still typed: the JSON property names of an external API data source (they belong to the remote service).

## Records, imports, data sources

- `ListDataRecordsQuery` returns one template version as a table (`Columns` = fields, `Values` per record, option titles for
  static/file sources); without a version: the filtered import's, else the latest record's. Partial `_RecordGrid` on
  `/DataRecords` and, compact, in `_RecentRecords` under the entry form (`DataRecords/Recent?versionId=`, refreshed by
  record-form.js after "save and new"). Dates/numbers shown via `ToRecordDisplay` (Jalali, Persian digits).
- Import files are kept in `dbo.ImportBatchFile` (one row per batch, varbinary; `Scripts/010_ImportFiles.sql`, also run at
  startup); download: `Imports/Download/{id}` (only for batches the user can see). Content type from the extension.
- Data source pages (index, detail, map file, preview) are on MX; `vx.css` was removed (it was never loaded: `_Layout` has no
  Styles section). `TemplateLayouts/*` still use old class names.

- Tests: `Mapper.Tests` (WebApplicationFactory, every DbContext on its own SQLite connection, startup migrations off;
  `DataScopeTests` for the unit filter).
  Manual checklist: `docs/modules-test-checklist.md`.
