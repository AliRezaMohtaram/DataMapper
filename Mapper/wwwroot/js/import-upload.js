/* import-upload.js — فرم آپلود ایمپورت (partial «_UploadForm»)
   فقط پروفایل‌های نگاشتِ همان قالب/نسخهٔ انتخاب‌شده نمایش داده می‌شوند. */
MX.component("import-upload", root => {
    "use strict";

    const version = root.querySelector("[data-import-version]");
    const profile = root.querySelector("[data-import-profile]");
    const hint = root.querySelector("[data-profile-hint]");
    if (!version || !profile) return;

    function sync() {
        let available = 0;
        Array.from(profile.options).forEach(o => {
            if (!o.value) return;
            const show = o.dataset.version === version.value;
            o.hidden = !show;
            o.disabled = !show;
            if (show) available++;
        });
        if (profile.selectedOptions[0]?.disabled) profile.value = "";
        profile.disabled = !version.value || available === 0;
        if (hint) {
            hint.textContent = !version.value
                ? "ابتدا قالب و نسخه را انتخاب کنید."
                : available === 0
                    ? "برای این نسخه پروفایلی ذخیره نشده؛ ستون‌ها خودکار تطبیق داده می‌شوند."
                    : "ستون‌ها مطابق نگاشت ذخیره‌شده تطبیق داده می‌شوند.";
        }
    }

    version.addEventListener("change", sync);
    sync();
});
