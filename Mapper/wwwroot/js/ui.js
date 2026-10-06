/* ui.js — ابزارهای مشترک رابط کاربری (برگرفته از script.js نمونه)
   توابع سراسری: $, escapeHtml, showToast, showConfirm, showAlertModal
   نیازمند partial «_ConfirmModal» برای مودال تأیید. */
const $ = id => document.getElementById(id);

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[ch]));
}

function showToast(message, type = "info", duration = 3800) {
    let container = $("toastContainer");
    if (!container) {
        container = document.createElement("div");
        container.id = "toastContainer";
        container.className = "toast-container";
        container.setAttribute("aria-live", "polite");
        document.body.appendChild(container);
    }
    const toast = document.createElement("div");
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    container.appendChild(toast);
    requestAnimationFrame(() => toast.classList.add("show"));
    setTimeout(() => {
        toast.classList.remove("show");
        setTimeout(() => toast.remove(), 250);
    }, duration);
}

function showModal({ title = "", message = "", okText = "تأیید", cancelText = "انصراف", danger = false }) {
    return new Promise(resolve => {
        const overlay = $("confirmOverlay");
        const okBtn = $("confirmOkBtn");
        const cancelBtn = $("confirmCancelBtn");

        $("confirmTitle").textContent = title;
        $("confirmMessage").textContent = message;
        okBtn.textContent = okText;
        okBtn.className = "btn " + (danger ? "danger" : "primary");
        if (cancelText) { cancelBtn.style.display = ""; cancelBtn.textContent = cancelText; }
        else { cancelBtn.style.display = "none"; }

        overlay.classList.remove("hidden");
        requestAnimationFrame(() => overlay.classList.add("in"));

        function cleanup(result) {
            overlay.classList.remove("in");
            setTimeout(() => overlay.classList.add("hidden"), 180);
            okBtn.removeEventListener("click", onOk);
            cancelBtn.removeEventListener("click", onCancel);
            overlay.removeEventListener("click", onOverlay);
            document.removeEventListener("keydown", onKey);
            resolve(result);
        }
        function onOk() { cleanup(true); }
        function onCancel() { cleanup(false); }
        function onOverlay(e) { if (e.target === overlay) cleanup(false); }
        function onKey(e) { if (e.key === "Escape") cleanup(false); }

        okBtn.addEventListener("click", onOk);
        cancelBtn.addEventListener("click", onCancel);
        overlay.addEventListener("click", onOverlay);
        document.addEventListener("keydown", onKey);
    });
}

function showConfirm(title, message, danger = false) {
    return showModal({ title, message, okText: "تأیید", cancelText: "انصراف", danger });
}

function showAlertModal(title, message, danger = false) {
    return showModal({ title, message, okText: "متوجه شدم", cancelText: null, danger });
}
