/* datasource-form.js — فرم ایجاد/ویرایش منبع داده (partial «_DataSourceForm»)
   - با انتخاب «نوع منبع» فقط بخش همان نوع نمایش داده می‌شود و کنترل‌های بقیه غیرفعال می‌شوند.
   - نوع «قالب داخلی»: فیلدهای قالب انتخاب‌شده در «فیلد مقدار» و «فیلد نمایشی» ریخته می‌شود.
   به‌صورت کامپوننت MX ثبت می‌شود تا هم در صفحهٔ کامل و هم در مودال (پس از تزریق HTML) اجرا شود.
   داده‌ها از <script type="application/json" data-ds-lookups> داخل همان فرم می‌آید. */
MX.component("ds-form", root => {
    "use strict";

    const radios = root.querySelectorAll('input[type="radio"][name="SourceType"]');
    const sections = root.querySelectorAll("section[data-ds-type]");
    const lookupsEl = root.querySelector("[data-ds-lookups]");
    const tplSelect = root.querySelector("[data-ds-template]");
    const valueSelect = root.querySelector("[data-ds-value]");
    const displaySelect = root.querySelector("[data-ds-display]");
    const submit = root.querySelector("[data-ds-submit]");

    const lookups = lookupsEl ? JSON.parse(lookupsEl.textContent) : { templates: [] };

    // در ویرایش نوع ثابت است و رادیویی وجود ندارد
    const currentType = () => root.querySelector('input[name="SourceType"]:checked')?.value || root.dataset.dsCurrent;

    function showSection() {
        const type = currentType();
        sections.forEach(s => {
            const active = s.dataset.dsType === type;
            s.hidden = !active;
            // کنترل‌های بخش پنهان ارسال نمی‌شوند تا مقدار قدیمی ذخیره نشود
            s.querySelectorAll("input, select, textarea").forEach(c => { c.disabled = !active; });
        });

        const label = submit?.querySelector("[data-label]");
        if (label && radios.length) {
            label.textContent = type === "File" ? submit.dataset.labelFile : submit.dataset.labelDefault;
        }
    }

    function fillFields() {
        if (!tplSelect || !valueSelect || !displaySelect) return;

        const tpl = lookups.templates.find(t => String(t.id) === tplSelect.value);
        const selValue = valueSelect.dataset.selected || valueSelect.value;
        const selDisplay = displaySelect.dataset.selected || displaySelect.value;

        [valueSelect, displaySelect].forEach(sel => {
            sel.innerHTML = `<option value="">${tpl ? "— انتخاب کنید —" : "— ابتدا قالب —"}</option>`;
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

    function fillTemplates() {
        if (!tplSelect) return;
        const selected = tplSelect.dataset.selected || tplSelect.value;
        lookups.templates.forEach(t => {
            if (tplSelect.querySelector(`option[value="${t.id}"]`)) return;
            const o = document.createElement("option");
            o.value = t.id;
            o.textContent = t.name;
            tplSelect.appendChild(o);
        });
        if (selected) tplSelect.value = selected;
        tplSelect.dataset.selected = "";
    }

    radios.forEach(r => r.addEventListener("change", showSection));

    if (tplSelect) {
        tplSelect.addEventListener("change", () => {
            valueSelect.dataset.selected = "";
            displaySelect.dataset.selected = "";
            fillFields();
        });
        fillTemplates();
        fillFields();
    }

    showSection();
});
