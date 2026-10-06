/* ui.js — ابزارهای مشترک رابط کاربری (برگرفته از script.js نمونه)
   توابع سراسری: $, escapeHtml, showToast, showConfirm, showAlertModal
   نیازمند partial «_ConfirmModal» برای مودال تأیید. */
const $byId = id => document.getElementById(id);

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[ch]));
}

function showToast(message, type = "info", duration = 3800) {
    // MX toast (mx.js) when the new layout is present
    if (window.MX) {
        const tone = { error: "danger", danger: "danger", success: "success", warning: "warning" }[type] || "info";
        MX.toast(message, tone);
        return;
    }
    let container = $byId("toastContainer");
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

/* مودال تأیید/اطلاع روی partial «_ConfirmModal». Promise<boolean> برمی‌گرداند؛
   بستن با Esc، کلیک بیرون یا انصراف = false. */
function showModal({ title = "", message = "", okText = "تأیید", cancelText = "انصراف", danger = false }) {
    const overlay = $byId("confirmOverlay");
    if (!overlay || !window.MX) return Promise.resolve(window.confirm(message));

    return new Promise(resolve => {
        const okBtn = $byId("confirmOkBtn");
        const cancelBtn = $byId("confirmCancelBtn");

        $byId("confirmTitle").textContent = title;
        $byId("confirmMessage").textContent = message;
        okBtn.textContent = okText;
        okBtn.className = "btn " + (danger ? "btn-danger" : "btn-primary");
        if (cancelText) { cancelBtn.hidden = false; cancelBtn.textContent = cancelText; }
        else { cancelBtn.hidden = true; }

        let result = false;
        const onOk = () => { result = true; MX.close(overlay); };
        okBtn.addEventListener("click", onOk);
        overlay.addEventListener("mx:close", () => {
            okBtn.removeEventListener("click", onOk);
            cancelBtn.hidden = false;
            resolve(result);
        }, { once: true });

        MX.open(overlay);
    });
}

function showConfirm(title, message, danger = false) {
    return showModal({ title, message, okText: "تأیید", cancelText: "انصراف", danger });
}

function showAlertModal(title, message, danger = false) {
    return showModal({ title, message, okText: "متوجه شدم", cancelText: null, danger });
}
