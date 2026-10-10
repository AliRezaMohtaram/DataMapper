/* record-form.js — فرم ورود/ویرایش دستی داده
   فرم با همان موتور رندر طراح Layout ساخته می‌شود (LayoutEngine.renderRuntime)؛
   اگر نسخه Layout نداشته باشد، چیدمان خودکار از فیلدهای قالب استفاده می‌شود.
   داده‌ها از <script id="rfData" type="application/json"> خوانده می‌شود.
   وابستگی: ui.js، jalali-picker.js، layout-designer.js (به همین ترتیب لود شوند). */
(() => {
    "use strict";

    const dataEl = document.getElementById("rfData");
    const host = document.getElementById("rfHost");
    if (!dataEl || !host || !window.LayoutEngine) return;

    const PAGE = JSON.parse(dataEl.textContent);
    const L = window.LayoutEngine;

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

    const saveBtn = document.getElementById("rfSave");
    const saveNewBtn = document.getElementById("rfSaveNew");
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
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
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
            values: L.collect(host)
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
            render({});
            refreshRecent();
            window.scrollTo({ top: 0, behavior: "smooth" });
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
})();
