/* field-form.js — فرم فیلد قالب (partial «_FieldForm»)
   - نمایش «طول» برای متن و «Precision/Scale» برای عدد بر اساس نوع داده
   - انتخاب Regex آماده و قرار دادن الگوی آن در ورودی Regex
   به‌صورت کامپوننت MX ثبت می‌شود تا هم در صفحهٔ کامل و هم در مودال اجرا شود. */
MX.component("field-form", root => {
    "use strict";

    const dataType = root.querySelector("[data-field-type]");

    function sync() {
        if (!dataType) return;
        root.querySelectorAll("[data-show-for]").forEach(el => {
            el.hidden = !el.dataset.showFor.split(" ").includes(dataType.value);
        });
    }

    dataType?.addEventListener("change", sync);
    sync();

    // تب «نام‌های مستعار» فرم‌های خودش را دارد و بلافاصله ذخیره می‌شود؛ دکمهٔ ذخیرهٔ فیلد آنجا معنا ندارد
    const save = root.querySelector('.modal-foot [type="submit"][form="fieldForm"]');
    const aliasPane = root.querySelector("#fldPaneAlias");
    const syncSave = () => { if (save && aliasPane) save.hidden = !aliasPane.hidden; };
    root.querySelectorAll("[data-tab]").forEach(tab => tab.addEventListener("click", () => setTimeout(syncSave)));
    syncSave();

    const picker = root.querySelector("[data-regex-picker]");
    const regexInput = root.querySelector("[data-regex-input]");

    picker?.addEventListener("change", () => {
        const opt = picker.selectedOptions[0];
        if (opt?.dataset.pattern && regexInput) {
            regexInput.value = opt.dataset.pattern;
            regexInput.focus();
        }
        picker.value = "";
    });
});
