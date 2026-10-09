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
`MapRazorPages`), sign-in required everywhere, `_Layout` (user chip, sign-out, change password, "کاربران" for admins),
`HttpCurrentUser` → `ICurrentUser` (Application) → `AuditStampInterceptor` fills `CreatedBy/UpdatedBy/DeletedBy`.
User ids are `long`; other modules (OrgChart, Acl) store them as strings. Manual checklist: `docs/users-test-checklist.md`.

## OrgChart and Acl modules

From github.com/AliRezaMohtaram/organization (OrgChart 0.1.0) and /Acl (0.3.0) as NuGet packages from local folders,
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
  `Mapper.OrgChart`. A grant on `Mapper` is inherited by everything below it.
- Controllers: `[RequirePermission]` — class = View; Create/Edit/Delete actions as named; Approve = publish/archive a
  template version, commit an import; template fields/layouts and new versions = Edit on Templates; DataSources has no
  class-level check so `Options` (used by record forms) only needs sign-in; `DataRecords.Save` checks Create or Edit in code.
  The dashboard is open to every signed-in user (landing page).
- Views: links/forms to protected actions carry `acl-res`/`acl-act` (hidden when denied); `_Layout` filters the menu
  with `IAccessService` (UI hiding is cosmetic; the controllers enforce).
- `MapperAccessBootstrapper` (first run only): role "مدیر Mapper" with every action on `Mapper`, given to
  `Acl:SuperAdminUserIds`; left alone once it exists.
- Tests: `Mapper.Tests` (WebApplicationFactory, every DbContext on its own SQLite connection, startup migrations off).
  Manual checklist: `docs/modules-test-checklist.md`.
