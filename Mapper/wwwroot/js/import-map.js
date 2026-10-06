/* import-map.js — تطبیق ستون‌های ایمپورت (Imports/Detail در وضعیت «آپلودشده»)
   - شمارش ستون‌های نگاشت‌شده
   - هشدار وقتی یک فیلد به بیش از یک ستون وصل شده (سرور فقط یکی را می‌پذیرد) و جلوگیری از ارسال
   - وضعیت فیلدهای الزامی: نگاشت‌شده (سبز) / بدون ستون (زرد) */
MX.component("import-map", root => {
    "use strict";

    const fa = MX.fa;
    const rows = Array.from(root.querySelectorAll("[data-map-row]"));
    const selects = rows.map(r => r.querySelector("[data-map-select]"));
    const required = Array.from(root.querySelectorAll("[data-required]"));
    const requiredHint = root.querySelector("[data-required-hint]");
    const summary = root.querySelector("[data-map-summary]");
    const status = root.querySelector("[data-map-status]");
    const submit = root.querySelector("[data-map-submit]");

    function refresh() {
        const used = {};
        selects.forEach(s => { if (s.value) used[s.value] = (used[s.value] || 0) + 1; });

        let duplicates = 0;
        rows.forEach((row, i) => {
            const value = selects[i].value;
            const dup = !!value && used[value] > 1;
            row.classList.toggle("is-dup", dup);
            row.classList.toggle("is-skipped", !value);
            row.querySelector("[data-map-error]").textContent = dup ? "این فیلد به ستون دیگری هم وصل شده است." : "";
            if (dup) duplicates++;
        });

        const mapped = selects.filter(s => s.value).length;
        if (summary) summary.textContent = `${fa(mapped)} از ${fa(selects.length)} ستون به فیلد وصل شده`;

        let missing = 0;
        required.forEach(tag => {
            const ok = !!used[tag.dataset.required];
            tag.classList.toggle("tone-success", ok);
            tag.classList.toggle("tone-warning", !ok);
            if (!ok) missing++;
        });
        if (requiredHint) {
            requiredHint.textContent = missing === 0
                ? "همهٔ فیلدهای الزامی ستون دارند."
                : `${fa(missing)} فیلد الزامی ستونی ندارد؛ سطرهای فایل برای این فیلدها نامعتبر می‌شوند.`;
        }

        if (status) {
            status.textContent = duplicates
                ? `${fa(duplicates)} ستون فیلد تکراری دارند — قبل از ادامه اصلاح کنید.`
                : `${fa(selects.length - mapped)} ستون نادیده گرفته می‌شود.`;
            status.style.color = duplicates ? "var(--danger)" : "";
        }
        if (submit) submit.disabled = duplicates > 0;
    }

    selects.forEach(s => s.addEventListener("change", refresh));
    root.addEventListener("submit", e => {
        if (submit?.disabled) e.preventDefault();
        else submit?.setAttribute("aria-busy", "true");
    });
    refresh();
});
