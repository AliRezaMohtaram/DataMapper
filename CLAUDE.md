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
Lost admin password: `Users:Bootstrap:ResetPassword: true` (+ `Bootstrap:Password`) resets it at startup; turn it off afterwards.

## OrgChart and Acl modules

From github.com/AliRezaMohtaram/organization (OrgChart 0.2.2) and /Acl (0.5.2) as NuGet packages **committed in this
repository** (`modules-feed/`, user's choice: a pull and a build must be enough on their machine). `nuget.config` maps
`OrgChart.*`/`Acl.*` to that folder only — nuget.org has unrelated packages with the same names (e.g. Acl.Core 3.x).
To ship a module change: bump its version in its `Directory.Build.props` (NuGet caches by version), run
`tools/update-modules.sh` (packs both clones, replaces `modules-feed/*.nupkg`), update the versions in
`Mapper/Borc.DataMapper.Web.csproj`, commit. Cloud container: the clones are /home/user/acl (symlink /home/user/Acl) and
/home/user/organization.

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

## Who enters and who sees what (user decisions, replacing the earlier "data scope by unit" for definitions)

- Definitions (templates, data sources, mapping profiles) have no unit and no "public" state: anyone with View on the page
  sees them (their `OrgUnitKey` column is unused). The template/data source/profile/import forms no longer ask for a unit.
- Each template has allowed units (`TemplateOrgUnit`, `Scripts/011_TemplateUnits.sql` — seeded from the old
  `Template.OrgUnitKey`; set in the template form with the searchable multi-select `_UnitsPicker`,
  `SetTemplateUnitsCommand`). `ITemplateEntryPolicy` (Application): only holders of a position (primary/acting, now) in one
  of them may enter records or import; the record/import gets that unit. Data scope "all" on `Mapper.DataRecords` (Acl)
  may enter for every template. `GetImportLookupsQuery(ForEntry)` lists only the allowed templates (picker, upload form).
- Records and imports are seen by (`DataVisibility`, Mapper/Modules — the `IDataScopeProvider` and `ICurrentUserUnits`):
  the user who entered them (always), unit heads for their unit and all sub-units (org chart `ManagerPositionKey`;
  `Mapper:Visibility:ManagersSeeSubordinates`, default true), and the units an Acl data-scope rule gives. Rows without a
  unit are only seen by their author (or scope "all"). Loaded per request by a middleware after `UseAccessControl`.
  "Only my data" toggle (`_ViewMode`, cookie `mapper.view=mine`, `ViewModeController`) on the dashboard, records and
  imports — shown only to users who could see more than their own rows. Lists show who entered each row (`UserNames`).
- An Acl role bound to a unit "with sub-units" does reach people in the sub-units (test in DataScopeTests); it gives
  permissions — which rows are visible is the rule above.
- `AccessLinksTests` crawls every page a restricted user reaches and fails on any link to "access denied" (user: a user
  must never see what they cannot open).
- Known limit: lookups follow the scope too (a field's data source in another unit gives no options) — no longer applies to
  data sources (unfiltered); records used as a "template" data source follow the record visibility.

## No typed keys (user decision: nothing is keyed in by hand)

- Codes are generated (`Application/Common/Codes/GeneratedCodes`): templates `TPL-0001`, data sources `DS-0001`, template
  fields `F001` (numbered across all versions of the template, deleted rows count, so a key is never reused). The commands
  still accept an explicit code (blank = generated); the forms no longer send one. OrgChart 0.2.0 does the same for units,
  positions and types (`UNIT-0001`, `POS-0001`, `UTYPE-`, `PTYPE-`).
- Still typed: the JSON property names of an external API data source (they belong to the remote service).

## Records, imports, data sources

- `/DataRecords` (Index) is master/detail (user: never one row per record here): `ListRecordsOverviewQuery` returns one
  `RecordVersionSummaryDto` per template version with visible records — count, imported count, units, last change and who
  made it (search/source filters narrow the counted records). A row click (`tr[data-row-href]`) opens the detail
  `/DataRecords/Version?templateVersionId=` — the per-version table where records are added, edited, copied and deleted.
  `importBatchId` links redirect to Version.
- Last editor: record detail shows "آخرین ویرایش‌کننده" (`DataRecordDetailDto.UpdatedBy`), the version table shows
  "ویرایش: …" under the author when someone else edited (e.g. a unit head editing a subordinate's record).
- `ListDataRecordsQuery` (Version page) returns one template version as a table (`Columns` = fields, `Values` per record, option titles for
  static/file sources); without a version: the filtered import's, else the latest record's. Partial `_RecordGrid` on
  `/DataRecords` and, compact, in `_RecentRecords` under the entry form (`DataRecords/Recent?versionId=`, refreshed by
  record-form.js after "save and new"). Dates/numbers shown via `ToRecordDisplay` (Jalali, Persian digits).
  The record detail page is a horizontal table too (user: a vertical field list is not what users are used to).
- Imports (user request: large files, progress): upload stays a request (rows saved in chunks of 1000, saved rows
  detached). Mapping (`ApplyImportMappingCommand`) only stores `ImportBatch.MappingJson` and sets Validating; commit sets
  Committing (new status 6); both enqueue the batch on `IImportJobQueue` → `ImportJobWorker` (Mapper/Background,
  BackgroundService + Channel, one batch at a time, own DI scope) sends `RunImportJobCommand`. Validation checks list fields
  against their data source (`DataSourceOptionService.GetValueSetAsync`, loaded once per file). Commit: each chunk's
  records + row marks + `ProcessedRows` saved together, so a stopped commit resumes (worker re-queues Validating/Committing
  batches at start; Reopen/Delete refused once a row is imported). Imported records: unit and author = the batch's
  (uploader). Failures → `LastError`, back to the previous state. Page: progress card + `js/import-progress.js` polling
  `Imports/Progress/{id}`. Script `012_ImportJobs.sql`. Measured on SQL Server 2022, 20,000 rows: upload ~13 s,
  validation ~4 s, commit ~8 s; killed mid-commit and restarted → resumed, no duplicates. Tests: `ImportJobTests`
  (the Mapper test DB is now a temp SQLite file in WAL mode, because the worker runs alongside the tests).
- Import files are kept in `dbo.ImportBatchFile` (one row per batch, varbinary; `Scripts/010_ImportFiles.sql`, also run at
  startup); download: `Imports/Download/{id}` (only for batches the user can see). Content type from the extension.
- Data source pages (index, detail, map file, preview) are on MX; `vx.css` was removed (no page uses it any more).
  `TemplateLayouts/*` still use old class names.
- Record form = MX component `record-form` (`wwwroot/js/record-form.js`) on the partial `_RecordForm` (root `.modal`):
  opened in a modal from the grid (row click via `tr[data-row-modal]`, edit/copy icons), the detail page and the list
  toolbar; `Form.cshtml` shows the same partial as a page with the recent records. It loads its dependencies itself when
  a page lacks them (`data-rf-deps`; ui.js, jalali-picker.js, combobox.js, layout-designer.js) — `_RecordFormScripts`
  preloads them. Keys: Enter = next field, Ctrl+S = save, Ctrl+Enter = save and new. In a modal, saving reloads the page
  behind it (filters kept); "save and new" (`SaveRecordRequest.Stay`) keeps the form and sets no TempData message.
  `Create?copyFrom=` (copy, published versions only), `Create` without a version always shows the template picker (the
  automatic "last template" was removed at the user's request). Detail: previous/next in list order
  (`RecordNeighborsDto`).
- Navigation: `_Layout` renders a breadcrumb (`.crumbs`, in mx.css) on every page below a menu section's home: menu
  section › `ViewData["Crumbs"]` (`Crumb(Label, Url)`) › title. `wwwroot/js/nav.js` remembers each list's URL (search,
  filters, page) per browser tab and points plain links to that list back to it (not the sidebar); a page marked
  `[data-list-page="templateVersionId=5"]` is remembered under path + that parameter, so links with exactly that query
  are restored too. It opens `tr[data-row-modal]` rows in a modal and follows `tr[data-row-href]`.

- Demo data (user request, for manual tests): `Scripts/demo/demo-seed.sql` / `demo-remove.sql` (SQL Server, run by hand after
  one start of Mapper; everything marked `DEMO-` / `demo.` / «دمو - »; users' password Test1234, Identity V3 hash in the
  script), sample import files `Scripts/demo/*.xlsx`, guide `docs/demo-data.md`. Verified on SQL Server 2022 (docker) with
  the app running: seed → sign-ins → visibility per user → upload → remove → seed again.

- Reporting = module MReporter (github.com/AliRezaMohtaram/MReporter, clone /home/user/mreporter; packages MReporter.Core,
  .EFCore, .Sql 0.1.0 in `modules-feed/`, also packed by `tools/update-modules.sh`; schema `rpt` migrated at startup by
  `ModuleDatabaseMigrator`). Mapper side: `Mapper/Modules/Reports` — `MapperReportSources` (IReportSourceCatalog: one source
  per template "mapper.records:{code}" with its fields by key across versions + system columns `sys.version/unit/source/
  author/createdAt/editor/updatedAt`; default audience = template's allowed units; "mapper.imports"), `MapperReportData`
  (IReportDataProvider: raw SQL via MReporter.Sql, JSON_VALUE/TRY_CONVERT on SQL Server, json_extract on SQLite; applies
  IsDeleted and the scope itself because raw SQL skips EF filters — Viewer = `IDataScopeProvider` like the lists, Units =
  granted units + sub-units from the chart, All; list titles: data source items preloaded before the reader opens — one
  connection, no MARS; units from the chart, users via `UserNames`), `MapperReportUser`. Tests: `ReportDataTests`.
  `MAPPER_TEST_SQLSERVER` (connection string with "{0}" for a per-class database) runs Mapper's DB in the tests on SQL
  Server instead of SQLite — use it for SQL-dialect changes (it caught the MARS issue).
- Reporting decisions (user): a separate reusable module (like OrgChart/Acl), hosts register data sources;
  admin-made reports with an audience (units with/without sub-units, positions, persons; default = the template's allowed
  units); a report MAY grant data scope beyond the runner's own visibility (audited); advanced filter replaces the simple
  search, then Excel export; later a Stimulsoft-like banded layout designer. PDF = the user's browser ("Print → Save as
  PDF", Chrome/Edge 131+; CSS @page margin boxes give header/footer and Persian page numbers with the page's web font).
  Server constraints: nothing may be installed on the server, no commercial dependencies, no internet — so no server
  Chromium and no QuestPDF; server-side PDF (scheduled/archived) not needed for now (if ever: own engine on SkiaSharp +
  HarfBuzzSharp, MIT). Prototype and comparison: `prototypes/ReportPdf`, `docs/prototypes/pdf-engines.md`.

- Tests: `Mapper.Tests` (WebApplicationFactory, every DbContext on its own SQLite connection, startup migrations off;
  `DataScopeTests` for the unit filter).
  Manual checklist: `docs/modules-test-checklist.md`.
