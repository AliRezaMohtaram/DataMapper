# Borc.Users — user accounts and sign-in

Self-contained module (no reference to the DataMapper projects), planned to move to its own repository/package once a
second host needs it.

- `Borc.Users` — ASP.NET Core Identity with `long` ids, schema `usr` in the host database (tables `Users`, `UserClaims`,
  `UserLogins`, `UserTokens`; no roles — authorization comes from the host or the Acl module), own migrations
  (history table `usr.__EFMigrationsHistory`). Services: `IUserAdministration` (list/create/update/(de)activate/
  reset password), `IUserLookup` (search and names for pickers and other modules), `UserSignIn` (user name or e-mail),
  `IUserStatusListener` (hook on (de)activation).
- `Borc.Users.Web` — Razor Pages on the MX design system inside the host's `_Layout`: `/Account/Login` (own minimal shell),
  `/Account/Logout` (POST), `/Account/Password`, `/Account/AccessDenied`, `/Users` (+ `Edit`, `Password`, `Status`),
  modal-first like the host (`X-MX-Modal`, JSON `{redirect}`, toast via `TempData["Success"]`).
- `Borc.Users.Tests` — TestServer + SQLite.

## Host setup

```csharp
builder.Services.AddBorcUsers(connectionString, o => builder.Configuration.GetSection("Users").Bind(o));
builder.Services.AddBorcUsersUi();           // cookie auth, pages, policy "Users.Administrator", sign-in required everywhere
// ...
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
```

Configuration (`Users` section): `MigrateOnStartup` (default true; otherwise run `Scripts/008_Users.sql`),
`Bootstrap:UserName/Password/DisplayName` (first administrator, created only while there is no account),
`PasswordMinLength` (8, plus a digit), `MaxFailedSignIns` (5), `LockoutDuration` (10 min).

## Rules

- Sign-in by user name or e-mail (e-mail optional, unique when set). No public registration: administrators create accounts.
- Accounts are never deleted. Deactivating blocks sign-in and ends open sessions within a minute (security stamp,
  validated every minute); the same applies to password resets. Lockout after 5 failures for 10 minutes; a reset unlocks.
- Administrators (`IsAdministrator`, claim `usr:admin`) manage accounts. You cannot deactivate yourself or remove your
  own administrator flag, and at least one active administrator must remain.
- Claims: `NameIdentifier` = id (long as string — also the `UserId` other modules store), `Name` = user name,
  `usr:display_name`.

## Migrations

`dotnet tool restore`, then (in the cloud container prefix `DOTNET_ROLL_FORWARD=Major`):

```
dotnet ef migrations add <Name> -p Borc.Users -s Borc.Users -o Persistence/Migrations
dotnet ef migrations script -p Borc.Users -s Borc.Users --idempotent   # → Scripts/008_Users.sql (keep the SET header)
```
