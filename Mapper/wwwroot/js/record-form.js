/* record-form.js — فرم ورود/ویرایش دستی داده (کامپوننت MX «record-form»)
   فرم با همان موتور رندر طراح Layout ساخته می‌شود (LayoutEngine.renderRuntime)؛
   اگر نسخه Layout نداشته باشد، چیدمان خودکار از فیلدهای قالب استفاده می‌شود.
   ریشه: partial «_RecordForm» (data-mx-init="record-form")؛ هم در مودال (از فهرست و جزئیات رکورد) و هم در صفحهٔ کامل.
   داده‌ها از <script type="application/json" data-rf-data> داخل ریشه خوانده می‌شود.
   صفحه‌کلید: Enter = فیلد بعدی، Ctrl+S = ذخیره، Ctrl+Enter = ذخیره و رکورد جدید.
   وابستگی: ui.js، jalali-picker.js، combobox.js، layout-designer.js (پیش از این فایل لود شوند). */
(() => {
    "use strict";

    if (!window.MX) return;

    // صفحه‌ای که اسکریپت‌های فرم را از قبل ندارد (مثلاً داشبورد): وابستگی‌ها به ترتیب و فقط یک بار بار می‌شوند.
    // data-rf-deps روی ریشه: [{ "src": "/js/ui.js?v=…", "global": "showToast" }, …]
    const loading = {};
    function load(src) {
        return loading[src] ??= new Promise((resolve, reject) => {
            const el = document.createElement("script");
            el.src = src;
            el.onload = resolve;
            el.onerror = () => reject(new Error("Could not load " + src));
            document.head.appendChild(el);
        });
    }

    async function ensureDeps(root) {
        let deps = [];
        try { deps = JSON.parse(root.dataset.rfDeps || "[]"); } catch { }
        for (const d of deps) {
            if (typeof window[d.global] === "undefined") await load(d.src);
        }
    }

    MX.component("record-form", async root => {
        try { await ensureDeps(root); } catch (err) { console.error(err); }

        const dataEl = root.querySelector("[data-rf-data]");
        const host = root.querySelector("[data-rf-host]");
        if (!dataEl || !host || !window.LayoutEngine) return;

        const PAGE = JSON.parse(dataEl.textContent);
        const L = window.LayoutEngine;
        const modal = root.closest(".modal-overlay");   // null = صفحهٔ کامل
        const fields = (PAGE.fields || []).map(f => ({
            key: f.key,
            label: f.label,
            type: f.type,
            required: f.required,
            dbType: f.dbType,
            length: f.length,
            precision: f.precision,
            scale: f.scale,
            regex: f.regex,
            // منبع کوچک: گزینه‌ها همراه فرم (لیست کشویی)؛ منبع بزرگ/API/قالب: Combobox با جستجوی سمت سرور
            dataOptions: Array.isArray(f.options) ? f.options : null,
            dataSource: f.dataSourceId
                ? { id: f.dataSourceId, url: (PAGE.urls || {}).options, labels: (PAGE.labels || {})[f.key] ? { [PAGE.values[f.key]]: PAGE.labels[f.key] } : {} }
                : null
        }));

        const hasBlocks = l => !!(l && Array.isArray(l.blocks) && l.blocks.length);
        const layout = hasBlocks(PAGE.layout) ? PAGE.layout : L.autoLayout(fields);
        const tpl = { id: PAGE.versionId, name: PAGE.templateName, fields, layout };

        const saveBtn = root.querySelector("[data-rf-save]");
        const saveNewBtn = root.querySelector("[data-rf-save-new]");
        const submitText = (layout.settings && layout.settings.submitText) || "ذخیره رکورد";
        if (saveBtn) saveBtn.textContent = submitText;

        function normalize(v, field) {
            if (v === null || v === undefined) return v;
            const s = String(v);
            // تاریخ‌وساعت ذخیره‌شده: yyyy-MM-ddTHH:mm:ss → yyyy-MM-ddTHH:mm برای کنترل
            if (field && field.type === "datetime") return s.replace(/^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}).*$/, "$1");
            return s;
        }

        function fill(values) {
            const out = {};
            fields.forEach(f => { if (values[f.key] !== undefined) out[f.key] = normalize(values[f.key], f); });
            L.setValues(host, out);
        }

        function render(values) {
            host.innerHTML = "";
            L.renderRuntime(tpl, host);
            fill(values || {});
        }

        render(PAGE.values);

        async function post(url, body) {
            const token = root.querySelector('input[name="__RequestVerificationToken"]') || document.querySelector('input[name="__RequestVerificationToken"]');
            try {
                const res = await fetch(url, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token ? token.value : ""
                    },
                    body: JSON.stringify(body)
                });
                const data = await res.json().catch(() => null);
                if (!res.ok || !data) return { success: false, message: "پاسخ نامعتبر از سرور (" + res.status + ")." };
                return data;
            } catch (_) {
                return { success: false, message: "ارتباط با سرور برقرار نشد." };
            }
        }

        let busy = false;

        async function save(andNew) {
            if (busy) return;

            if (!L.validate(host, tpl)) {
                showToast("فیلدهای فرم را تکمیل یا اصلاح کنید.", "error");
                return;
            }

            busy = true;
            [saveBtn, saveNewBtn].forEach(b => { if (b) b.disabled = true; });

            const res = await post(PAGE.urls.save, {
                versionId: PAGE.versionId,
                recordId: PAGE.recordId || null,
                values: L.collect(host),
                stay: andNew
            });

            busy = false;
            [saveBtn, saveNewBtn].forEach(b => { if (b) b.disabled = false; });

            if (res.fieldErrors && Object.keys(res.fieldErrors).length) {
                L.setErrors(host, res.fieldErrors);
                showToast(res.message || "برخی فیلدها نامعتبرند.", "error");
                return;
            }

            if (!res.success) {
                showToast(res.message || "ذخیره انجام نشد.", "error");
                return;
            }

            if (andNew) {
                showToast(res.message || "رکورد ثبت شد.", "success");
                changed = true;
                render({});
                refreshRecent();
                (modal ? root.querySelector(".modal-body") : window).scrollTo({ top: 0, behavior: "smooth" });
                focusFirst();
                return;
            }

            if (modal) {
                // در مودال: فهرست/جزئیاتِ پشت آن با همان فیلترها دوباره بارگذاری می‌شود (پیام از TempData)
                changed = false;
                MX.close(modal);
                window.location.reload();
                return;
            }

            window.location.href = res.redirect || PAGE.urls.index;
        }

        // فهرست رکوردهای ثبت‌شدهٔ همین نسخه زیر فرم (partial _RecentRecords)
        async function refreshRecent() {
            const box = document.getElementById("rfRecent");
            if (!box || !PAGE.urls.recent) return;
            try {
                const response = await fetch(PAGE.urls.recent, { headers: { "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" });
                if (response.ok) box.innerHTML = await response.text();
            } catch {
                // فهرست قدیمی می‌ماند؛ رکورد ذخیره شده است
            }
        }

        if (saveBtn) saveBtn.addEventListener("click", () => save(false));
        if (saveNewBtn) saveNewBtn.addEventListener("click", () => save(true));

        // رکوردهایی که با «ذخیره و رکورد جدید» در مودال ثبت شدند: با بستن مودال، صفحهٔ پشت آن تازه شود
        let changed = false;
        if (modal) modal.addEventListener("mx:close", () => { if (changed) window.location.reload(); }, { once: true });

        const FIELD = "input:not([type=hidden]):not([disabled]):not([readonly]), select:not([disabled]), textarea:not([disabled])";
        const visibleFields = () => Array.from(host.querySelectorAll(FIELD)).filter(el => el.offsetParent !== null);

        function focusFirst() {
            setTimeout(() => visibleFields()[0]?.focus({ preventScroll: true }), 60);
        }

        root.addEventListener("keydown", e => {
            const key = e.key;
            if ((e.ctrlKey || e.metaKey) && (key === "s" || key === "S" || key === "س")) {
                e.preventDefault();
                save(false);
                return;
            }
            if ((e.ctrlKey || e.metaKey) && key === "Enter") {
                e.preventDefault();
                save(!!saveNewBtn);
                return;
            }
            // Enter در یک فیلد (نه چندخطی و نه وقتی فهرست کشویی/تقویم آن را مصرف کرده) → فیلد بعدی
            if (key === "Enter" && !e.shiftKey && !e.altKey && !e.defaultPrevented) {
                const t = e.target;
                if (!host.contains(t) || t.tagName === "TEXTAREA" || t.tagName === "BUTTON" || t.type === "checkbox" || t.type === "radio") return;
                const list = visibleFields();
                const i = list.indexOf(t);
                if (i === -1) return;
                e.preventDefault();
                if (i < list.length - 1) list[i + 1].focus();
                else (saveBtn || saveNewBtn)?.focus();
            }
        });

        focusFirst();
    });
})();
