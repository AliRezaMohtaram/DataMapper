/* nav.js — جابه‌جایی راحت‌تر در صفحه‌های Mapper
   · بازگشت با حفظ فیلترها: آدرس هر صفحهٔ فهرست (با جستجو، فیلتر و شمارهٔ صفحه) در sessionStorage همین زبانه
     نگه داشته می‌شود و لینک‌های بدون پارامتر به همان فهرست (مسیر صفحه، دکمه‌های «بازگشت»، …) به آن برمی‌گردند.
     منوی کناری عمداً همیشه فهرست را تازه باز می‌کند.
   صفحه‌ای که [data-list-page] دارد (مثلاً رکوردهای یک نسخه) هم فهرست حساب می‌شود.
   · ردیف‌های data-row-modal: کلیک روی ردیف (نه روی لینک یا دکمه) فرم آن را در مودال باز می‌کند؛
     data-row-href: کلیک روی ردیف به آن آدرس می‌رود.
   فهرست‌ها: data-lists روی تگ همین اسکریپت (مسیرها با «,» جدا). */
(() => {
    "use strict";

    const script = document.currentScript;
    const lists = (script?.dataset.lists || "").split(",").map(p => p.trim().toLowerCase()).filter(Boolean);
    const KEY = "mx-list:";
    const storage = (() => { try { return window.sessionStorage; } catch { return null; } })();

    // کلید هر فهرست = مسیر، یا برای صفحه‌های [data-list-page="templateVersionId=5"] مسیر + همان پارامتر هویت
    const norm = p => p.toLowerCase().replace(/\/index$/, "");
    const here = norm(location.pathname);
    const marker = document.querySelector("[data-list-page]");
    const hereKey = marker ? here + "|" + (marker.dataset.listPage || "").toLowerCase() : here;
    if (storage && (marker || lists.includes(here))) {
        try { storage.setItem(KEY + hereKey, location.pathname + location.search); } catch { /* حافظهٔ مرورگر در دسترس نیست */ }
    }

    function restore(scope) {
        if (!storage) return;
        scope.querySelectorAll("a[href]").forEach(a => {
            if (a.closest(".sidebar")) return;
            const url = new URL(a.href, location.href);
            if (url.origin !== location.origin || url.hash) return;
            const path = norm(url.pathname);
            const key = url.search ? path + "|" + url.search.slice(1).toLowerCase() : path;
            if (!url.search && !lists.includes(path)) return;
            let saved = null;
            try { saved = storage.getItem(KEY + key); } catch { }
            if (saved && saved !== url.pathname + url.search) a.setAttribute("href", saved);
        });
    }

    restore(document);

    document.addEventListener("click", e => {
        const row = e.target.closest("tr[data-row-modal], tr[data-row-href]");
        if (!row) return;
        if (e.target.closest("a, button, input, select, textarea, label")) return;
        if (window.getSelection && String(window.getSelection()).length) return;   // متن انتخاب می‌شود، نه کلیک
        if (row.dataset.rowHref) {
            if (e.ctrlKey || e.metaKey) window.open(row.dataset.rowHref, "_blank");
            else location.href = row.dataset.rowHref;
        } else if (row.dataset.rowModal && window.MX) {
            MX.openRemote(row.dataset.rowModal);
        }
    });
})();
