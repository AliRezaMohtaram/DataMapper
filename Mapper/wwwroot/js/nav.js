/* nav.js — جابه‌جایی راحت‌تر در صفحه‌های Mapper
   · بازگشت با حفظ فیلترها: آدرس هر صفحهٔ فهرست (با جستجو، فیلتر و شمارهٔ صفحه) در sessionStorage همین زبانه
     نگه داشته می‌شود و لینک‌های بدون پارامتر به همان فهرست (مسیر صفحه، دکمه‌های «بازگشت»، …) به آن برمی‌گردند.
     منوی کناری عمداً همیشه فهرست را تازه باز می‌کند.
   · ردیف‌های data-row-modal: کلیک روی ردیف (نه روی لینک یا دکمه) فرم آن را در مودال باز می‌کند.
   فهرست‌ها: data-lists روی تگ همین اسکریپت (مسیرها با «,» جدا). */
(() => {
    "use strict";

    const script = document.currentScript;
    const lists = (script?.dataset.lists || "").split(",").map(p => p.trim().toLowerCase()).filter(Boolean);
    const KEY = "mx-list:";
    const storage = (() => { try { return window.sessionStorage; } catch { return null; } })();

    const here = location.pathname.toLowerCase().replace(/\/index$/, "");
    if (storage && lists.includes(here)) {
        try { storage.setItem(KEY + here, location.pathname + location.search); } catch { /* حافظهٔ مرورگر در دسترس نیست */ }
    }

    function restore(scope) {
        if (!storage) return;
        scope.querySelectorAll("a[href]").forEach(a => {
            if (a.closest(".sidebar")) return;
            const url = new URL(a.href, location.href);
            if (url.origin !== location.origin || url.search || url.hash) return;
            const path = url.pathname.toLowerCase().replace(/\/index$/, "");
            if (!lists.includes(path)) return;
            let saved = null;
            try { saved = storage.getItem(KEY + path); } catch { }
            if (saved && saved !== url.pathname) a.setAttribute("href", saved);
        });
    }

    restore(document);

    document.addEventListener("click", e => {
        const row = e.target.closest("tr[data-row-modal]");
        if (!row || !row.dataset.rowModal || !window.MX) return;
        if (e.target.closest("a, button, input, select, textarea, label")) return;
        if (window.getSelection && String(window.getSelection()).length) return;   // متن انتخاب می‌شود، نه کلیک
        MX.openRemote(row.dataset.rowModal);
    });
})();
