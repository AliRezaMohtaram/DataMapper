/* datasource-preview.js — دکمهٔ «نمایش نمونه» در صفحهٔ جزئیات منبع داده.
   <button id="dsPreviewBtn" data-url="/DataSources/Options?id=5"> و محل نتیجه #dsPreviewOut */
(() => {
    "use strict";

    const btn = document.getElementById("dsPreviewBtn");
    const out = document.getElementById("dsPreviewOut");
    if (!btn || !out) return;

    const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[c]));

    btn.addEventListener("click", async () => {
        btn.disabled = true;
        out.innerHTML = '<div class="hint">در حال دریافت…</div>';

        try {
            const res = await fetch(btn.dataset.url + "&take=20", { headers: { Accept: "application/json" } });
            const data = await res.json();

            if (data.error) {
                out.innerHTML = `<div class="status error show">${esc(data.error)}</div>`;
            } else if (!data.items.length) {
                out.innerHTML = '<div class="status error show">هیچ گزینه‌ای پیدا نشد.</div>';
            } else {
                out.innerHTML =
                    `<div class="hint">${data.total} گزینه — ${data.items.length} مورد اول:</div>` +
                    '<div class="tscroll results-table-container"><table class="results-table"><thead><tr><th>مقدار واقعی</th><th>عنوان نمایشی</th></tr></thead><tbody>' +
                    data.items.map(i => `<tr><td dir="ltr">${esc(i.value)}</td><td>${esc(i.label)}</td></tr>`).join("") +
                    "</tbody></table></div>";
            }
        } catch (_) {
            out.innerHTML = '<div class="status error show">ارتباط با سرور برقرار نشد.</div>';
        } finally {
            btn.disabled = false;
        }
    });
})();
