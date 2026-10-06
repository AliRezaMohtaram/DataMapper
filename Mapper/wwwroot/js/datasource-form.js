/* datasource-form.js — فرم منبع داده
   - در صفحهٔ ایجاد: با تغییر «نوع منبع» فقط بخش همان نوع نمایش داده می‌شود.
   - نوع «قالب داخلی»: فهرست فیلدهای قالب انتخاب‌شده در دو لیست «فیلد مقدار» و «فیلد نمایشی» ریخته می‌شود.
   داده‌ها از <script id="dsLookups" type="application/json"> می‌آید. */
(() => {
    "use strict";

    const typeSelect = document.getElementById("SourceType");
    const sections = document.querySelectorAll("[data-ds-type]");
    const lookupsEl = document.getElementById("dsLookups");
    const tplSelect = document.getElementById("TemplateId");
    const valueSelect = document.getElementById("ValueKey");
    const displaySelect = document.getElementById("DisplayKey");

    const lookups = lookupsEl ? JSON.parse(lookupsEl.textContent) : { templates: [] };

    function currentType() {
        const form = document.getElementById("dsForm");
        return typeSelect ? typeSelect.value : (form ? form.dataset.dsType : "");
    }

    function showSection() {
        const t = currentType();
        sections.forEach(s => {
            const active = s.dataset.dsType === t;
            s.hidden = !active;
            // کنترل‌های بخش پنهان ارسال نمی‌شوند تا مقدار قدیمی ذخیره نشود
            s.querySelectorAll("input, select, textarea").forEach(c => { c.disabled = !active; });
        });
    }

    function fillFields() {
        if (!tplSelect || !valueSelect || !displaySelect) return;

        const tpl = lookups.templates.find(t => String(t.id) === tplSelect.value);
        const selValue = valueSelect.dataset.selected || valueSelect.value;
        const selDisplay = displaySelect.dataset.selected || displaySelect.value;

        [valueSelect, displaySelect].forEach(sel => {
            sel.innerHTML = '<option value="">— انتخاب کنید —</option>';
            (tpl ? tpl.fields : []).forEach(f => {
                const o = document.createElement("option");
                o.value = f.key;
                o.textContent = `${f.label} (${f.key})`;
                sel.appendChild(o);
            });
        });

        if (selValue) valueSelect.value = selValue;
        if (selDisplay) displaySelect.value = selDisplay;

        valueSelect.dataset.selected = "";
        displaySelect.dataset.selected = "";
    }

    if (typeSelect) typeSelect.addEventListener("change", showSection);

    if (tplSelect) {
        tplSelect.addEventListener("change", () => {
            valueSelect.dataset.selected = "";
            displaySelect.dataset.selected = "";
            fillFields();
        });
        fillFields();
    }

    showSection();
})();
