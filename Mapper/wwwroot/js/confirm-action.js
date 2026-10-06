/* مودال تأیید عمومی — با هر عنصری که data-confirm-target داشته باشد کار می‌کند:
<button type="button" data-confirm-target="formId" data-confirm-message="متن پیام">
   باز/بسته شدن، Esc، کلیک بیرون و نگه‌داشتن فوکوس را mx.js انجام می‌دهد. */
(() => {
    "use strict";

    const overlay = document.getElementById("confirmOverlay");
    if (!overlay || !window.MX) return;

    const title = document.getElementById("confirmTitle");
    const message = document.getElementById("confirmMessage");
    const okBtn = document.getElementById("confirmOkBtn");

    let pendingForm = null;

    document.addEventListener("click", (e) => {
        const trigger = e.target.closest("[data-confirm-target]");
        if (!trigger) return;

        const form = document.getElementById(trigger.dataset.confirmTarget);
        if (!form) return;

        pendingForm = form;
        title.textContent = trigger.dataset.confirmTitle || "تأیید عملیات";
        message.textContent = trigger.dataset.confirmMessage || "آیا مطمئن هستید؟";
        okBtn.textContent = "تأیید";
        okBtn.className = "btn btn-danger";
        MX.open(overlay);
    });

    okBtn.addEventListener("click", () => {
        const form = pendingForm;
        pendingForm = null;
        if (!form) return;          // باز شده از showConfirm (ui.js) — آنجا رسیدگی می‌شود
        MX.close(overlay);
        form.submit();
    });

    overlay.addEventListener("mx:close", () => { pendingForm = null; });
})();
