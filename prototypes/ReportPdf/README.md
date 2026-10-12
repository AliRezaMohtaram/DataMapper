# ReportPdf — PDF engine prototype

The same Persian report (expenses grouped by cost center, subtotals, grand total, repeated table header, page numbers
"صفحهٔ ۲ از ۶", Jalali dates, Persian digits, mixed Latin text, signature block) rendered by two engines:

- **Chromium** (`HtmlEngine.cs`): HTML/CSS → PDF with headless Chromium (Microsoft.Playwright).
- **QuestPDF** (`QuestEngine.cs`): laid out directly in .NET.

Run (writes `output/`):

```
dotnet run -- 150 output
```

Arguments: rows (default 150), output folder, optional Chromium executable (Playwright's own browser when omitted:
`pwsh bin/Debug/net9.0/playwright.ps1 install chromium`).

The Chromium engine's page header/footer need the fonts installed on the machine (`fonts/Vazir.ttf`, `fonts/VazirFD.ttf`:
Chromium's header/footer templates cannot load web fonts). `VazirFD.ttf` is Vazir with Persian glyphs on 0-9, for the
page numbers Chromium fills in. Fonts: Vazir by Saber Rastikerdar (free license).

Results (cloud container, Linux): see `docs/prototypes/pdf-engines.md`.
