/* مودال تأیید حذف — با هر عنصری که data-delete-id داشته باشد کار می‌کند.
   <button type="button" data-delete-id="5" data-delete-name="نام رکورد">
   باز/بسته شدن، Esc، کلیک بیرون و نگه‌داشتن فوکوس را mx.js انجام می‌دهد. */
(() => {
    "use strict";

    const overlay = document.getElementById("deleteOverlay");
    if (!overlay || !window.MX) return;

    const message = document.getElementById("deleteMessage");
    const idInput = document.getElementById("deleteId");

    document.addEventListener("click", (e) => {
        const trigger = e.target.closest("[data-delete-id]");
        if (!trigger) return;

        const name = trigger.dataset.deleteName;
        idInput.value = trigger.dataset.deleteId;
        message.textContent = name
            ? `آیا از حذف «${name}» مطمئن هستید؟\nاین عملیات قابل بازگشت نیست.`
            : "آیا از حذف این مورد مطمئن هستید؟";
        MX.open(overlay);
    });

    overlay.addEventListener("mx:close", () => { idInput.value = ""; });
})();
