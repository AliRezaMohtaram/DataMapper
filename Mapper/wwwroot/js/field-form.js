/* فرم فیلد: نمایش طول/دقت بر اساس نوع داده + انتخاب Regex آماده */
(() => {
    "use strict";

    const dataType = document.getElementById("DataType");
    const lengthGroup = document.getElementById("lengthGroup");
    const numberGroup = document.getElementById("numberGroup");

    function sync() {
        if (!dataType) return;
        const v = dataType.value;
        if (lengthGroup) lengthGroup.style.display = v === "Text" ? "" : "none";
        if (numberGroup) numberGroup.style.display = v === "Number" ? "" : "none";
    }

    if (dataType) {
        dataType.addEventListener("change", sync);
        sync();
    }

    const picker = document.getElementById("regexPicker");
    const regexInput = document.getElementById("Regex");

    if (picker && regexInput) {
        picker.addEventListener("change", () => {
            const opt = picker.selectedOptions[0];
            if (opt && opt.dataset.pattern) regexInput.value = opt.dataset.pattern;
            picker.value = "";
        });
    }
})();
