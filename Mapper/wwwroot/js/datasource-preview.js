/* datasource-preview.js — دکمهٔ «نمایش نمونه» در صفحهٔ جزئیات منبع داده.
   <button id="dsPreviewBtn" data-url="/DataSources/Options?id=5"> و محل نتیجه #dsPreviewOut */
(() => {
    "use strict";

    const btn = document.getElementById("dsPreviewBtn");
    const out = document.getElementById("dsPreviewOut");
    if (!btn || !out) return;

    const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[c]));

    const callout = msg =>
        `<div class="card-body"><div class="callout tone-danger"><svg class="ico"><use href="#i-alert" /></svg><div>${esc(msg)}</div></div></div>`;
    const fa = n => String(n).replace(/\d/g, d => "۰۱۲۳۴۵۶۷۸۹"[d]);

    btn.addEventListener("click", async () => {
        btn.disabled = true;
        out.innerHTML = '<div class="empty">در حال دریافت…</div>';

        try {
            const res = await fetch(btn.dataset.url + "&take=20", { headers: { Accept: "application/json" } });
            const data = await res.json();

            if (data.error) {
                out.innerHTML = callout(data.error);
            } else if (!data.items.length) {
                out.innerHTML = callout("هیچ گزینه‌ای پیدا نشد.");
            } else {
                out.innerHTML =
                    '<div class="tbl-wrap"><table class="tbl"><thead><tr><th>مقدار واقعی</th><th>عنوان نمایشی</th></tr></thead><tbody>' +
                    data.items.map(i => `<tr><td><span class="mono">${esc(i.value)}</span></td><td>${esc(i.label)}</td></tr>`).join("") +
                    "</tbody></table></div>" +
                    `<footer class="card-foot"><span>${fa(data.items.length)} مورد اول از ${fa(data.total)} گزینه</span></footer>`;
            }
        } catch (_) {
            out.innerHTML = callout("ارتباط با سرور برقرار نشد.");
        } finally {
            btn.disabled = false;
        }
    });
})();
