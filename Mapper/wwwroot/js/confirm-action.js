
/* مودال تأیید عمومی — با هر عنصری که data-confirm-target داشته باشد کار می‌کند:
<button type="button" data-confirm-target="formId" data-confirm-message="متن پیام"> */
(() => {
    "use strict";

    const overlay = document.getElementById("confirmOverlay");
    if (!overlay) return;

    const message = document.getElementById("confirmMessage");
    const okBtn = document.getElementById("confirmOkBtn");
    const cancelBtn = document.getElementById("confirmCancelBtn");

    let pendingForm = null;
    let lastFocused = null;

    function open(form, text) {
        lastFocused = document.activeElement;
        pendingForm = form;
        message.textContent = text || "آیا مطمئن هستید؟";

        overlay.classList.remove("hidden");
        requestAnimationFrame(() => overlay.classList.add("in"));
        cancelBtn.focus(); // پیش‌فرض ایمن: Enter عملیات را انجام نمی‌دهد
    }

    function close() {
        overlay.classList.remove("in");
        setTimeout(() => overlay.classList.add("hidden"), 180);
        pendingForm = null;
        if (lastFocused) lastFocused.focus();
    }

    document.addEventListener("click", (e) => {
        const trigger = e.target.closest("[data-confirm-target]");
        if (!trigger) return;

        const form = document.getElementById(trigger.dataset.confirmTarget);
        if (form) open(form, trigger.dataset.confirmMessage);
    });

    okBtn.addEventListener("click", () => {
        const form = pendingForm;
        close();
        if (form) form.submit();
    });

    cancelBtn.addEventListener("click", close);

    overlay.addEventListener("click", (e) => {
        if (e.target === overlay) close();
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && !overlay.classList.contains("hidden")) close();
    });
})();