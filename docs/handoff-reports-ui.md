# Handoff: reports UI overhaul (local session)

> **راهنمای استفاده (برای کاربر):** این فایل را در یک سشن تازهٔ Claude Code روی سیستم خودتان، داخل پوشهٔ DataMapper، بدهید:
> «فایل `docs/handoff-reports-ui.md` را بخوان و طبق آن کار کن». سشن جدید برنامه را روی سیستم شما اجرا می‌کند، صفحه‌ها را
> در مرورگر می‌بیند، اول از شما می‌پرسد چه چیزهایی بهم ریخته و سخت است، بعد طراحی تازه را پیشنهاد و پیاده می‌کند.

The rest of this file is for Claude.

---

## 1. The task

The user tested the reports module (MReporter) inside Mapper: **it works correctly, but the UI is messy and not easy to
use** ("ui بهم ریخته است و کاربری آسانی نداره"). This session runs locally on the user's machine so you can run the app,
look at the pages in a real browser and fix the UI/UX.

Do this, in order:

1. Read §2–§7 and the three `CLAUDE.md` files (DataMapper, MReporter, organization). They hold the conventions and every
   decision made so far.
2. Get the app running locally with the demo data (§4). Confirm with the user which database to use before writing to it.
3. **Ask the user first** what exactly bothers them (which pages, which steps feel hard). Show them the list in §6 as a
   starting point and let them pick and add. Do not redesign on guesses alone.
4. Propose a concrete redesign per page (short, in Persian, with a sketch or a screenshot of a prototype if useful) and get
   agreement before larger changes. Small obvious fixes (spacing, alignment, wording) can go straight in.
5. Implement in MReporter (and Mapper where the host is involved), ship through `modules-feed/` (§5), check every changed page
   in the browser (§7), run the automated tests, update the checklist and the `CLAUDE.md` status sections, commit and push
   when the user agrees.

Talk to the user in **Persian**; code, identifiers, comments and commit messages in **English**. Desktop only — mobile /
narrow widths are not required (user decision). Commit only when the user asks (or as they have agreed).

## 2. The repositories

All four are clones **side by side in the same parent folder** (the scripts and `nuget.config` files use relative paths):

| Folder | GitHub | Branch | What |
|---|---|---|---|
| `DataMapper` | AliRezaMohtaram/DataMapper | `master` | the host app "Mapper" (+ Users module). `modules-feed/` holds the module packages |
| `MReporter` | AliRezaMohtaram/MReporter | `main` | reporting module — **most of this task is here** |
| `organization` | AliRezaMohtaram/organization | `main` | OrgChart module (the old branch `claude/elegant-bohr-qnybnq` was merged into main) |
| `Acl` | AliRezaMohtaram/Acl | `master` | access-control module 0.5.2 |

`tools/update-modules.sh` (in DataMapper) looks for `../Acl`, `../organization` and `../MReporter` (or `../mreporter`).
MReporter's `nuget.config` reads OrgChart packages from `../organization/artifacts/packages` (needed by the
`MReporter.OrgChart` bridge), so `organization` must be packed before MReporter builds — the script does that.

Stack: .NET 9 (packages stay on 9.x), EF Core 9 + SQL Server, Razor Pages / MVC, plain CSS/JS (no SPA, no CDN — the
server has no internet), Persian RTL UI, Jalali dates, texts in `.resx`.

## 3. What exists (reports)

Module MReporter 0.1.3 (stages 1–5 done; details in `MReporter/CLAUDE.md` and `MReporter/docs/design.md`):

- **Advanced filter** `MrFilter` (view component + `wwwroot/js/mr-filter.js`, `css/mr-filter.css`) — replaces the simple
  search on Mapper's `/DataRecords/Version` and `/Imports`; quick text search, conditions per column type, Jalali dates,
  searchable list options, chips. Options `embedded` (inside the report form; applying does not submit) and `askable`
  ("پرسیده شود" checkbox per condition).
- **Excel export** — streamed, nothing stored (`ReportXlsxWriter`, `ReportExcelResult`). Buttons on the two list pages and
  on the report run page.
- **Reports pages** `/Reports` (area "Reports" in `MReporter.Razor`), rendered inside Mapper's `_Layout`:
  - `Index` — "گزارش‌های آماده" (shared reports that reach the user; managers see all) and "گزارش‌های من" (personal).
  - `New` — modal: choose the data source (a template's records, or imports).
  - `Edit` — one long page of cards: basics (title, kind personal/shared, description, active), columns (checkbox tiles
    with up/down buttons) + two sort keys, conditions (embedded MrFilter with "پرسیده شود"), audience (source's units
    toggle, unit picker with "with sub-units", position picker, user search), data scope (Viewer / units / all).
  - `Run` — title, fixed conditions as text, the asked conditions as editable defaults in the filter, 50 rows per page,
    Excel and Print buttons.
  - `Print` — standalone HTML for the browser's print / "Save as PDF" (A4 landscape, `@page` header/footer with Persian
    page numbers, max 1000 rows).
- Permissions in Mapper: Acl page `Mapper.Reports` — View = menu, run, personal reports; Export = Excel; Edit = shared
  reports (audience + data scope). Menu section "گزارش" in `_Layout`.

Files you will most likely change:

```
MReporter/src/MReporter.Razor/
  Areas/Reports/Pages/            Index, New (+_NewForm), Edit, Run, Print (.cshtml + .cshtml.cs)
  Areas/Reports/Pages/Shared/     _ReportPage (assets, flash), _Picker, _Results, _FormErrors, _ReportIcons
  Pages/                          ReportPageModel, RunPageModel, ReportUiOptions (+ReportPolicies), ReportFormat, PickerModel
  Components/MrFilterViewComponent.cs, Views/Shared/Components/MrFilter/Default.cshtml
  wwwroot/js/mr-reports.js, mr-filter.js      wwwroot/css/mr-reports.css, mr-filter.css
  Resources/ReportText.resx (+ ReportText.cs)  — every UI text; tests check that used keys exist
DataMapper/Mapper/
  Views/Shared/_Layout.cshtml     menu, topbar (shows ViewData["Title"] — module pages repeat it in their own header)
  Views/DataRecords/Version.cshtml, Views/Imports/Index.cshtml   filter + Excel button on the list pages
  Program.cs                      AddMReporter()...AddOrgChart().AddUi(ui => policies, PrintFontUrl)
```

## 4. Running it locally

Prerequisites: .NET 9 SDK, SQL Server (the user's own instance; `Mapper/appsettings.json` → `ConnectionStrings:BorcDataMapper`),
SSMS or `sqlcmd`, Git Bash (for `tools/update-modules.sh` on Windows), Node + Playwright or the Chrome extension for
screenshots (§7).

1. `dotnet build Mapper.sln` in DataMapper (packages come from `modules-feed/`).
2. Run Mapper once against the database (`dotnet run --project Mapper`, Development environment). Startup migrates the
   module schemas (`usr`, `org`, `acl`, `rpt`) and runs `Scripts/009–012`. Mapper's own `dbo` tables must already exist
   (the user's database has them).
3. Demo data: in SSMS run `Scripts/demo/demo-remove.sql`, then `Scripts/demo/demo-seed.sql` on the same database
   (guide: `docs/demo-data.md`). Everything demo is marked `DEMO-` / `demo.` / «دمو - ». **Ask before running these on a
   database that holds real data.**
4. Sign in at `/Account/Login`. Demo users: password `Test1234` — useful ones:
   - `demo.acc` (head of accounting; runs «دمو - هزینه‌های ۳۰ روز اخیر», has Excel),
   - `demo.acc2` (plain user; personal reports only, sees only own rows),
   - `demo.it` / `demo.dev` (template designers = report managers: shared reports, audience, scope),
   - `demo.net` (IT; runs «دمو - همهٔ درخواست‌های پشتیبانی», granted scope "all").
   The admin account is the user's own (`Users:Bootstrap`).

## 5. Changing a module and shipping it to Mapper

Mapper uses the modules **only as NuGet packages** from `DataMapper/modules-feed/` (user's choice: a pull and a build must
be enough). After changing MReporter (or another module):

1. Bump `<Version>` in that module's `Directory.Build.props` (now MReporter 0.1.3 → 0.1.4; NuGet caches by version).
2. `tools/update-modules.sh` (Git Bash) — packs all modules and replaces `modules-feed/*.nupkg`.
3. Update the versions in `Mapper/Borc.DataMapper.Web.csproj` (all `MReporter.*` references, including `MReporter.OrgChart`).
4. `dotnet build Mapper.sln`, restart the app.

While iterating you may keep the same version and instead delete the cached packages before each build
(`%USERPROFILE%\.nuget\packages\mreporter.*`), then bump once before committing. Repacking Acl/OrgChart without changes
alters their `.nupkg` bytes — restore those files with `git checkout -- modules-feed/Acl.* modules-feed/OrgChart.*`
so the commit only carries real changes.

## 6. Known UI weak spots (starting list — confirm with the user)

- **Duplicate titles**: Mapper's topbar shows the page title and every reports page repeats it in its own `.mr-head`.
- **Report form (`Edit`) is one long scroll** of five cards. Ideas: steps/tabs (data & columns → conditions → who sees it),
  or a two-pane layout with a live preview of the first rows; sticky save bar.
- **Columns**: checkbox tiles with up/down arrow buttons in a grid — ordering is clumsy and the order is hard to read in a
  multi-column grid. Consider a single ordered list of chosen columns (drag or move buttons) next to a list of available ones.
- **"پرسیده شود" (asked conditions)** is not self-explanatory; the run page shows them as ordinary filter chips. Consider a
  clear "parameters" area on the run page (labelled inputs) instead of the generic filter.
- **Run page**: card padding/spacing around the conditions line, the filter toolbar and the hint is uneven; the fixed
  conditions read as plain text; actions (Edit / Print / Excel) compete with the title.
- **Audience and scope pickers**: long checkbox lists; "with sub-units" per row; user search with tags — functional but
  heavy. A summary ("۳ واحد، ۱ سمت، ۲ نفر") with a modal/drawer to edit may read better.
- **New report modal**: a search box above a `<select>` (two controls for one choice). A searchable list of sources
  (grouped by template) would be simpler.
- **Index**: row actions are faded until hover; delete uses the browser's `confirm()` instead of an MX confirmation modal.
- **Advanced filter on the list pages** (`mr-filter`): panel rows are dense; check it with the user too.
- Print view is plain (fine for printing; only change if the user asks).

## 7. UI rules and how to check

- **MX design system only** (`DataMapper/Mapper/wwwroot/css/mx.css`, `js/mx.js`): tokens (`var(--primary)`, `--surface`,
  `--line`, `--muted`, …), never hard-coded colors; components `.card*`, `.btn*`, `.tbl*`, `.field/.label/.input/.select/.hint`,
  `.badge` + `.st-*`, `.modal*`, `.mx-drawer`, `.toolbar`, `.empty`, `.seg`, `.toggle`, `.tag`, `.callout`, `.fchips`.
  Behaviours from `mx.js` hooks: `data-modal` (server form in a modal, `X-MX-Modal: 1`), `data-open/data-close`,
  `data-tabs`/`data-tab`, `data-wizard`, `data-dd`, `data-mx-init` (+ `MX.component(name, fn)`), `MX.toast`.
  Do not build on `theme.css` / `mx-legacy.css`. Good examples of the house style: Mapper's Templates and DataRecords pages,
  `_UnitsPicker`, the OrgChart module's pages.
- Module pages must work inside any host layout that loads MX: keep their own icons (`_ReportIcons`, ids `mr-i-*`) and
  assets via `_ReportPage`; no `@section`s.
- All texts in `ReportText.resx`; `tests/MReporter.Tests/Razor/TextResourceTests` fails on a missing key.
- A user must never see a link they cannot open (Mapper's `AccessLinksTests` crawls for this).
- After each change: look at the page in a browser at desktop width, dark and light theme (`localStorage["app-theme"]`
  = `{"mode":"dark"}` / `{"mode":"light"}`), as a plain user and as a manager. MX cards with `.rise` appear only when scrolled
  into view — for full-page screenshots use a tall viewport. A small Playwright helper (`npm i -D playwright`, then
  `node shot.js <user> <outPrefix> <path>...`; `H=2000` for a tall viewport):

```js
// shot.js — sign in to Mapper and screenshot pages
const { chromium } = require('playwright');
(async () => {
  const [user, out, ...paths] = process.argv.slice(2);
  const base = process.env.BASE || 'http://localhost:5000';
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: +(process.env.H || 900) } });
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push(String(e)));
  await page.addInitScript(t => localStorage.setItem('app-theme', JSON.stringify({ mode: t })), process.env.THEME || 'dark');
  await page.goto(base + '/Account/Login');
  await page.fill('input[name=Login]', user);
  await page.fill('input[name=Password]', process.env.PASS || 'Test1234');
  await Promise.all([page.waitForNavigation(), page.click('button[type=submit]')]);
  let i = 0;
  for (const p of paths) {
    if (p.startsWith('click:')) { await page.click(p.slice(6)); await page.waitForTimeout(800); }
    else await page.goto(base + p, { waitUntil: 'networkidle' });
    await page.screenshot({ path: `${out}-${i++}.png`, fullPage: true });
  }
  console.log(JSON.stringify({ errors }));
  await browser.close();
})();
```

  Example: `node shot.js demo.it shots/edit "/Reports" "click:a[href='/Reports/New']" "/Reports/Edit?id=1" "/Reports/Run?id=1"`.

## 8. Tests and finishing

- MReporter: `dotnet test` (73 tests). Mapper: `dotnet test Mapper.Tests` (39 tests; `ReportPagesTests` and
  `ReportDataTests` cover the reports pages — update their HTML assertions if markup or texts change on purpose).
- Update `docs/modules-test-checklist.md` (section «گزارش‌ها»، items 77–85) for changed behaviour, and the status sections
  of `MReporter/CLAUDE.md` and `DataMapper/CLAUDE.md`.
- End with a short Persian summary for the user: what changed per page, what to test, how to pull and build.
