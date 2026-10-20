/* ui.js — ابزارهای مشترک رابط کاربری (برگرفته از script.js نمونه)
   توابع سراسری: $, escapeHtml, showToast, showConfirm, showAlertModal
   نیازمند partial «_ConfirmModal» برای مودال تأیید. */
const $byId = id => document.getElementById(id);

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[ch]));
}

function showToast(message, type = "info") {
    // MX toast (mx.js is on every page)
    const tone = { error: "danger", danger: "danger", success: "success", warning: "warning" }[type] || "info";
    if (window.MX) MX.toast(message, tone);
    else window.alert(message);
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
