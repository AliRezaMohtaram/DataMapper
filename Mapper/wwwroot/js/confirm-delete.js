/* مودال تأیید حذف — با هر عنصری که data-delete-id داشته باشد کار می‌کند.
   <button type="button" data-delete-id="5" data-delete-name="نام رکورد"> */
(() => {
    "use strict";

    const overlay = document.getElementById("deleteOverlay");
    if (!overlay) return;

    const message = document.getElementById("deleteMessage");
    const idInput = document.getElementById("deleteId");
    const cancelBtn = document.getElementById("deleteCancelBtn");

    let lastFocused = null;

    function open(id, name) {
        lastFocused = document.activeElement;
        idInput.value = id;
        message.textContent = name
            ? `آیا از حذف «${name}» مطمئن هستید؟`
            : "آیا از حذف این مورد مطمئن هستید؟";

        overlay.classList.remove("hidden");
        requestAnimationFrame(() => overlay.classList.add("in"));
        cancelBtn.focus(); // پیش‌فرض ایمن: Enter حذف نمی‌کند
    }

    function close() {
        overlay.classList.remove("in");
        setTimeout(() => overlay.classList.add("hidden"), 180);
        idInput.value = "";
        if (lastFocused) lastFocused.focus();
    }

    document.addEventListener("click", (e) => {
        const trigger = e.target.closest("[data-delete-id]");
        if (trigger) {
            open(trigger.dataset.deleteId, trigger.dataset.deleteName);
        }
    });

    cancelBtn.addEventListener("click", close);

    overlay.addEventListener("click", (e) => {
        if (e.target === overlay) close();
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && !overlay.classList.contains("hidden")) close();
    });
})();
