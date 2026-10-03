// ================================================================
// Advanced ERP Data Mapper - Logic (Ultimate Edition v2.2)
// ================================================================

const STORAGE_KEY = "erp_templates_advanced_v2";
const ENTRY_STORAGE_KEY = "erp_manual_entries_v1"; // مدل داده: { [templateId]: [ {_id, _createdAt, <key>: value, ...} ] }

const DEFAULT_TEMPLATES = {
    site: {
        id: 1, name: "اطلاعات پایه سایت (Site)",
        fields: [
            { label: "نام سایت", key: "Site Name", required: true, regex: /^(site\s*name|نام\s*سایت)$/i },
            { label: "نام انگلیسی سایت", key: "English Site Name", required: true, regex: /^(english\s*site\s*name|نام\s*انگلیسی)$/i },
            { label: "نوع سایت", key: "Site Type", required: true, regex: /^(site\s*type|نوع\s*سایت)$/i },
            { label: "آیا برای اقلام محصولی است؟", key: "is production site", required: false, regex: /^(is\s*production\s*site|محصولی)$/i },
            { label: "آیا برای اقلام دارایی است؟", key: "is asset type", required: false, regex: /^(is\s*asset\s*type|دارایی)$/i },
            { label: "آیا سایت یابی کار است؟", key: "Is Container", required: false, regex: /^(is\s*container|پایی\s*کار|یابی\s*کار)$/i },
            { label: "نام واحد سازمانی مرتبط", key: "organization unit", required: false, regex: /^(organization\s*unit|واحد\s*سازمانی)$/i }
        ]
    },
    warehouse: {
        id: 2, name: "اطلاعات پایه انبار (Warehouse)",
        fields: [
            { label: "نام انبار", key: "Warehouse Name", required: true, regex: /^(warehouse\s*name|نام\s*انبار)$/i },
            { label: "نام انگلیسی انبار", key: "English Warehouse Name", required: true, regex: /^(english\s*warehouse\s*name|نام\s*انگلیسی\s*انبار)$/i },
            { label: "وضعیت تراکنش انبار", key: "Transaction Status", required: true, regex: /^(transaction\s*status|وضعیت\s*تراکنش)$/i },
            { label: "نوع انبار", key: "Warehouse Type", required: true, regex: /^(warehouse\s*type|نوع\s*انبار)$/i },
            { label: "نام انبار موقت مرتبط", key: "Related Temporary Warehouse", required: false, regex: /^(related\s*temporary|انبار\s*موقت)$/i },
            { label: "آیا انبار یابی کار است؟", key: "Is Container?", required: false, regex: /^(is\s*container|پایی\s*کار|یابی\s*کار)$/i },
            { label: "نام واحد سازمانی مرتبط", key: "organization unit code", required: false, regex: /^(organization\s*unit|واحد\s*سازمانی)$/i }
        ]
    },
    ps: {
        id: 3, name: "اطلاعات پایه محل برنامه‌ریزی (PS)",
        fields: [
            { label: "نام سایت", key: "Site Name", required: true, regex: /^(site\s*name|نام\s*سایت)$/i },
            { label: "نام محل برنامه‌ریزی", key: "PS Name", required: true, regex: /^(ps\s*name|نام\s*محل\s*برنامه)$/i },
            { label: "وضعیت ورود و خروج", key: "Status", required: true, regex: /^(status|وضعیت)$/i }
        ]
    },
    psItem: {
        id: 4, name: "اطلاعات اقلام در محل برنامه‌ریزی (PS Item)",
        fields: [
            { label: "نام محل برنامه‌ریزی", key: "PS name", required: true, regex: /^(ps\s*name|نام\s*محل\s*برنامه)$/i },
            { label: "کد قلم", key: "Item code", required: true, regex: /^(item\s*code|کد\s*قلم)$/i },
            { label: "روش خروج", key: "exit method", required: true, regex: /^(exit\s*method|روش\s*خروج)$/i },
            { label: "روش بسته‌بندی", key: "parcelling method", required: true, regex: /^(parcelling\s*method|روش\s*بسته)$/i },
            { label: "روش سفارش‌گذاری", key: "ordering method", required: true, regex: /^(ordering\s*method|روش\s*سفارش)$/i },
            { label: "مقدار سفارش", key: "order quantity", required: false, regex: /^(order\s*quantity|مقدار\s*سفارش)$/i },
            { label: "بازه زمانی سفارش", key: "ordering time", required: false, regex: /^(ordering\s*time|بازه\s*زمانی)$/i },
            { label: "ذخیره اطمینان", key: "safety stock", required: false, regex: /^(safety\s*stock|ذخیره\s*اطمینان)$/i },
            { label: "حداکثر زمان رزرو", key: "max reserve time-day", required: false, regex: /^(max\s*reserve|حداکثر\s*زمان)$/i },
            { label: "روش تأمین", key: "supply method", required: false, regex: /^(supply\s*method|روش\s*تأمین)$/i },
            { label: "نام سایت تأمین‌کننده", key: "supplier site name", required: false, regex: /^(supplier\s*site|سایت\s*تأمین)$/i },
            { label: "نام انبار تأمین‌کننده", key: "supplier warehouse name", required: false, regex: /^(supplier\s*warehouse|انبار\s*تأمین)$/i },
            { label: "زمان تحویل", key: "lead time", required: false, regex: /^(lead\s*time|زمان\s*تحویل)$/i },
            { label: "وضعیت", key: "Status", required: false, regex: /^(status|وضعیت)$/i },
            { label: "نیاز به کنترل کیفیت", key: "QC need", required: false, regex: /^(qc\s*need|کنترل\s*کیفیت)$/i },
            { label: "تاریخ انقضا", key: "need register expire date", required: false, regex: /^(expire\s*date|تاریخ\s*انقضا)$/i }
        ]
    },
    psLinking: {
        id: 5, name: "اتصالات محل برنامه‌ریزی (PS Linking)",
        fields: [
            { label: "نام محل برنامه‌ریزی", key: "PS Name", required: true, regex: /^(ps\s*name|نام\s*محل\s*برنامه)$/i },
            { label: "نام انبار", key: "Warehouse Name", required: false, regex: /^(warehouse\s*name|نام\s*انبار)$/i },
            { label: "کد واحد سازمانی", key: "OU Name", required: false, regex: /^(ou\s*name|واحد\s*سازمانی)$/i }
        ]
    }
};

let savedTemplates = JSON.parse(localStorage.getItem(STORAGE_KEY) || "[]");
let currentTemplate = null;
let uploadedExcelData = [];
let uploadedColumns = [];
let validRows = [];
let invalidRows = [];
let currentPage = 1;
let rowsPerPage = 10;
let currentView = 'valid'; // 'valid' or 'invalid'

// --- تخته Drag & Drop معادل‌سازی (تب ۲) ---
let mappingAssignments = {}; // { [sourceColumn]: fieldKey }
let selectedPoolColumn = null; // برای حالت کلیک-محور در موبایل/لمسی

// --- فرم ورود دستی داده (تب ۳) ---
let currentEntryTemplate = null;
let entryRecords = [];
let entryCurrentPage = 1;
const entryRowsPerPage = 10;

const $ = id => document.getElementById(id);

const PREDEFINED_REGEXES = {
    "email": { label: "ایمیل", pattern: "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$" },
    "mobile": { label: "موبایل (ایران)", pattern: "^09[0-9]{9}$" },
    "national_id": { label: "کد ملی", pattern: "^\\d{10}$" },
    "postal_code": { label: "کد پستی", pattern: "^\\d{10}$" },
    "date_ymd": { label: "تاریخ (YYYY-MM-DD)", pattern: "^\\d{4}-\\d{2}-\\d{2}$" }
};

// ================================================================
// 0. اعلان‌های سبک (Toast) و مودال تأیید — جایگزین alert()/confirm() مرورگر
// ================================================================
function showToast(message, type = "info", duration = 3800) {
    const container = $("toastContainer");
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
        function onKey(e) { if (e.key === "Escape") cleanup(false); if (e.key === "Enter") cleanup(true); }

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

// نمایان‌سازی نرم پنل‌هایی که با تعامل کاربر ظاهر می‌شوند (به‌جای پرش ناگهانی display:none → block)
function revealSection(el) {
    if (!el) return;
    if (el.classList.contains("hidden")) {
        el.classList.remove("hidden");
        el.classList.add("section-in");
    }
}
function hideSection(el) {
    if (!el) return;
    el.classList.add("hidden");
    el.classList.remove("section-in");
}

// ================================================================
// 1. Initialization & Default Templates
// ================================================================
function initDefaultTemplates() {
    const defaultTemplatesArray = Object.values(DEFAULT_TEMPLATES);
    let updated = false;
    defaultTemplatesArray.forEach(defTemplate => {
        const exists = savedTemplates.some(t => t.id === defTemplate.id);
        if (!exists) {
            const formattedFields = defTemplate.fields.map(f => ({
                label: f.label, key: f.key, aliases: f.aliases || [],
                required: !!f.required, type: f.type || 'text',
                regex: f.regex instanceof RegExp ? f.regex.source : (f.regex || "")
            }));
            savedTemplates.push({ id: defTemplate.id, name: defTemplate.name, fields: formattedFields });
            updated = true;
        }
    });
    if (updated) localStorage.setItem(STORAGE_KEY, JSON.stringify(savedTemplates));
}

// ================================================================
// 2. Tabs Navigation
// ================================================================
document.querySelectorAll(".chip").forEach(btn => {
    btn.addEventListener("click", () => {
        const target = $(btn.dataset.tab);
        if (target.classList.contains("active")) return;
        document.querySelectorAll(".chip").forEach(x => x.classList.remove("active"));
        document.querySelectorAll(".tab-content").forEach(x => x.classList.remove("active"));
        btn.classList.add("active");
        target.classList.add("active");
    });
});

// ================================================================
// 3. Helper Functions
// ================================================================
function normalize(value) {
    return String(value ?? "").trim().toLowerCase()
        .replace(/[يى]/g, "ی").replace(/ك/g, "ک").replace(/[ۀة]/g, "ه")
        .replace(/[\u064B-\u065F\u0670]/g, "").replace(/[ـ]/g, "")
        .replace(/[_\-./\\|]+/g, " ").replace(/[\(\)\[\]\{\}:؛،,]+/g, " ")
        .replace(/\s+/g, " ").trim();
}
function compact(value) { return normalize(value).replace(/[^a-z0-9\u0600-\u06ff]/gi, ""); }
function tokens(value) { return normalize(value).split(/\s+/).filter(Boolean); }
function escapeHtml(value) { return String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[char])); }
function levenshtein(a, b) {
    a = compact(a); b = compact(b);
    if (a === b) return 0; if (!a) return b.length; if (!b) return a.length;
    const prev = Array.from({ length: b.length + 1 }, (_, i) => i);
    for (let i = 1; i <= a.length; i++) {
        let current = [i];
        for (let j = 1; j <= b.length; j++) current[j] = Math.min(current[j - 1] + 1, prev[j] + 1, prev[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
        for (let j = 0; j <= b.length; j++) prev[j] = current[j];
    }
    return prev[b.length];
}
function similarity(a, b) { const aa = compact(a); const bb = compact(b); if (!aa || !bb) return 0; return Math.round((1 - levenshtein(aa, bb) / Math.max(aa.length, bb.length)) * 100); }
function tokenSimilarity(a, b) { const A = new Set(tokens(a)); const B = new Set(tokens(b)); if (!A.size || !B.size) return 0; let common = 0; A.forEach(x => { if (B.has(x)) common++; }); return Math.round((2 * common) / (A.size + B.size) * 100); }
function detectType(values) {
    const sample = values.filter(v => v !== null && v !== undefined && String(v).trim() !== "").slice(0, 20);
    if (!sample.length) return "text";
    const dateHits = sample.filter(v => !isNaN(Date.parse(String(v))) && /[-/]/.test(String(v))).length;
    const numberHits = sample.filter(v => !isNaN(Number(String(v).replace(/,/g, "")))).length;
    if (numberHits / sample.length > 0.8) return "number";
    if (dateHits / sample.length > 0.7) return "date";
    return "text";
}
// بونوس/جریمهٔ امتیاز بر اساس تطابق نوع فیلد قالب با نوع دادهٔ واقعی ستون اکسل.
// این تنها معیار اعتبارسنجی برای فیلدهای number/date/datetime است — نه Regex.
function typeCompatibility(fieldType, sourceType) {
    if (!fieldType || fieldType === "text" || fieldType === "any") return 0;
    if (fieldType === sourceType) return 7;
    if (fieldType === "datetime" && sourceType === "date") return 4; // تاریخ ساده هم تا حدی با فیلد datetime سازگار است
    return -4;
}

// ---- راهنمای انسانی برای الگوهای Regex آماده، تا پیام خطا فقط نگوید «نامعتبر» بلکه دقیقاً بگوید فرمت درست چیست ----
function regexHint(pattern) {
    const knownHints = {
        "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$": "باید یک ایمیل معتبر باشد. مثال درست: name@example.com",
        "^09[0-9]{9}$": "باید شماره موبایل ایران و ۱۱ رقمی باشد و با 09 شروع شود. مثال درست: 09121234567",
        "^\\d{10}$": "باید دقیقاً ۱۰ رقم باشد، بدون خط‌تیره، فاصله یا حرف.",
        "^\\d{4}-\\d{2}-\\d{2}$": "فرمت باید YYYY-MM-DD باشد. مثال درست: 2026-09-28"
    };
    return knownHints[pattern] || `باید با الگوی تعریف‌شده برای این فیلد مطابقت داشته باشد: ${pattern}`;
}

/* ================================================================
   setFieldErrorState — نمایش/پاک‌کردن خطا + کلاس .has-error روی کنترل
   روی همهٔ کنترل‌ها کار می‌کند: input, select, textarea, jdate-wrap, combo-wrap
   ================================================================ */
function setFieldErrorState(control, message) {
    if (!control) return;
    const wrap = control.closest("[data-field-wrap]") || control.closest(".lf-field");
    const errEl = wrap ? wrap.querySelector(".field-error") : null;
    if (errEl) errEl.textContent = message || "";
    control.classList.toggle("has-error", !!message);
}

/* پاک‌کردن خودکار خطا در لحظهٔ تایپ/تغییر کاربر — یک بار در سطح document */
document.addEventListener("input", e => {
    const ctl = e.target.closest && e.target.closest("[data-key]");
    if (ctl) setFieldErrorState(ctl, "");
}, true);
document.addEventListener("change", e => {
    const ctl = e.target.closest && e.target.closest("[data-key]");
    if (ctl) setFieldErrorState(ctl, "");
}, true);

// ---- اعتبارسنجی مشترک یک مقدار برای یک فیلد قالب (هم در آپلود اکسل و هم در فرم ورود دستی استفاده می‌شود) ----
// قانون کلیدی: الگوی Regex فقط وقتی نوع فیلد "text" است اعمال می‌شود.
// برای number/date/datetime، تشخیص معتبر بودن صرفاً بر پایهٔ نوع داده انجام می‌شود، نه Regex.
// هر پیام خطا علاوه بر «چه چیزی اشتباه است»، «چطور درستش کنیم» را هم توضیح می‌دهد.
function validateFieldValue(field, value) {
    const isEmpty = value === undefined || value === null || String(value).trim() === "";
    if (field.required && isEmpty) {
        //field.style.borderColor ="red";

        return `این فیلد اجباری است`;
    }
    if (isEmpty) return null;

    if (field.type === 'text' && field.regex) {
        try {
            const regex = new RegExp(field.regex, "iu");
            if (!regex.test(String(value).trim())) {
                return `مقدار «${value}» برای «${field.label}» با فرمت مورد انتظار مطابقت ندارد. ${regexHint(field.regex)}`;
            }
        } catch (_) { /* الگوی نامعتبر نادیده گرفته می‌شود */ }
    }
    if (field.type === 'number' && isNaN(Number(String(value).replace(/,/g, '')))) {
        return `مقدار «${value}» برای «${field.label}» باید یک عدد باشد (فقط رقم، می‌تواند اعشاری باشد). مثال درست: 1250 یا 45.5 — کاراکتر حرفی، فاصلهٔ اضافی یا علامت غیرمجاز را حذف کنید.`;
    }
    if (field.type === 'date' && isNaN(Date.parse(String(value))) && !/^\d{4}-\d{2}-\d{2}$/.test(String(value))) {
        return `مقدار «${value}» برای «${field.label}» یک تاریخ معتبر نیست. فرمت پیشنهادی: YYYY-MM-DD — مثال درست: 2026-09-28`;
    }
    if (field.type === 'datetime' && isNaN(Date.parse(String(value)))) {
        return `مقدار «${value}» برای «${field.label}» یک تاریخ و ساعت معتبر نیست. فرمت پیشنهادی: YYYY-MM-DDTHH:mm — مثال درست: 2026-09-28T14:30`;
    }
    // بررسی‌های مخصوص نوع Oracle (طول، عدد صحیح، دقت/مقیاس)
    const db = dbTypeOf(field), s = String(value).trim();
    if (["VARCHAR2", "NVARCHAR2", "CHAR"].includes(db) && field.length && String(value).length > field.length) {
        return `مقدار «${value}» برای «${field.label}» ${String(value).length} کاراکتر است ولی ستون ${db}(${field.length}) بیش از ${field.length} کاراکتر نمی‌پذیرد. مقدار را کوتاه کنید یا طول ستون را در قالب افزایش دهید.`;
    }
    if (db === "INTEGER" && !/^[+-]?\d+$/.test(s.replace(/,/g, ""))) {
        return `مقدار «${value}» برای «${field.label}» باید عدد صحیح باشد (بدون اعشار). مثال درست: 25`;
    }
    if (db === "NUMBER" && field.precision) {
        const sc = field.scale || 0, maxInt = field.precision - sc;
        const [ip, fp = ""] = s.replace(/,/g, "").replace(/^[+-]/, "").split(".");
        if (ip.replace(/^0+/, "").length > maxInt || fp.length > sc) {
            return `مقدار «${value}» برای «${field.label}» با NUMBER(${field.precision},${sc}) سازگار نیست: حداکثر ${maxInt} رقم صحیح و ${sc} رقم اعشار مجاز است.`;
        }
    }
    return null;
}

// ---- نوع‌های داده Oracle: base همان دستهٔ داخلی (text/number/date/datetime/any) است که بقیهٔ کد (اعتبارسنجی، فرم، Layout) با آن کار می‌کند ----
const ORACLE_TYPES = {
    VARCHAR2:  { base: "text",     size: "length",    def: 255, label: "VARCHAR2 · متن" },
    NVARCHAR2: { base: "text",     size: "length",    def: 255, label: "NVARCHAR2 · متن یونیکد" },
    CHAR:      { base: "text",     size: "length",    def: 1,   label: "CHAR · متن ثابت" },
    CLOB:      { base: "text",     size: "none",                label: "CLOB · متن بلند" },
    NUMBER:    { base: "number",   size: "precision",           label: "NUMBER · عدد (p,s)" },
    INTEGER:   { base: "number",   size: "none",                label: "INTEGER · عدد صحیح" },
    FLOAT:     { base: "number",   size: "none",                label: "FLOAT · اعشاری" },
    DATE:      { base: "date",     size: "none",                label: "DATE · تاریخ" },
    TIMESTAMP: { base: "datetime", size: "none",                label: "TIMESTAMP · تاریخ‌وساعت" },
    ANY:       { base: "any",      size: "none",                label: "ANY · آزاد" }
};
const LEGACY_DB = { text: "VARCHAR2", number: "NUMBER", date: "DATE", datetime: "TIMESTAMP", any: "ANY" };
const dbTypeOf = f => (f.dbType && ORACLE_TYPES[f.dbType]) ? f.dbType : (LEGACY_DB[f.type] || "VARCHAR2"); // قالب‌های قدیمی بدون dbType هم کار می‌کنند

function parseSize(dbType, raw) {
    const kind = ORACLE_TYPES[dbType].size, t = String(raw || "").trim();
    if (kind === "length") {
        const n = parseInt(t, 10);
        if (t && !(n >= 1 && n <= 4000)) throw new Error(`طول ${dbType} باید عددی بین 1 و 4000 باشد.`);
        return t ? { length: n } : {};
    }
    if (kind === "precision") {
        if (!t) return {};
        const m = t.match(/^(\d{1,2})(?:\s*,\s*(\d{1,2}))?$/);
        const p = m && +m[1], sc = m && (m[2] === undefined ? 0 : +m[2]);
        if (!m || p < 1 || p > 38 || sc > p) throw new Error("دقت NUMBER باید به شکل p یا p,s باشد (p بین 1 تا 38 و s ≤ p). مثال: 10,2");
        return { precision: p, scale: sc };
    }
    return {};
}

// ================================================================
// 4. Template Builder Logic
// ================================================================
function addFieldRow(data = {}) {
    const tr = document.createElement("tr");
    const curDb = dbTypeOf(data);
    const typeOptions = Object.entries(ORACLE_TYPES).map(([k, v]) =>
        `<option value="${k}" ${k === curDb ? "selected" : ""}>${v.label}</option>`).join("");
    const sizeVal = data.length ?? (data.precision != null ? (data.scale ? `${data.precision},${data.scale}` : data.precision) : "");
    let regexOptions = `<option value="">-- انتخاب Regex آماده --</option>`;
    for (const [key, val] of Object.entries(PREDEFINED_REGEXES))
        regexOptions += `<option value="${val.pattern}">${val.label}</option>`;

    // گزینه‌های قالب‌های منبع (به‌جز خود قالب جاری) برای Dynamic Data
    const currentTplId = Number($("editTemplateId").value || 0);
    const sourceTemplates = savedTemplates.filter(t => t.id !== currentTplId);
    const dsTemplateOptions = sourceTemplates.map(t =>
        `<option value="${t.id}" ${data.dataSource && String(data.dataSource.templateId) === String(t.id) ? "selected" : ""}>${escapeHtml(t.name)}</option>`
    ).join("");

    tr.innerHTML = `
        <td><input type="text" class="field-label" value="${escapeHtml(data.label || "")}" placeholder="مثلاً نام شرکت"></td>
        <td><input type="text" class="field-key" value="${escapeHtml(data.key || "")}" placeholder="CompanyName"></td>
        <td><input type="text" class="field-aliases" value="${escapeHtml((data.aliases || []).join(", "))}" placeholder="نام کمپانی, شرکت"></td>
        <td>
            <label class="switch" title="اجباری">
                <input type="checkbox" class="field-required" ${data.required ? "checked" : ""}>
                <span class="slider"></span>
            </label>
        </td>
        <td>
            <select class="field-type">${typeOptions}</select>
            <input type="text" class="field-size" value="${escapeHtml(sizeVal)}">
        </td>
        <td>
            <div class="regex-container" style="display:flex;gap:5px;flex-direction:column;">
                <select class="predefined-regex-select" style="height:30px;font-size:11px;">${regexOptions}</select>
                <input type="text" class="field-regex" value="${escapeHtml(data.regex || "")}" placeholder="مثلاً ^شماره\\s*ملی$" style="height:30px;font-size:11px;">
            </div>
        </td>
        <td>
            <div class="ds-config">
                <select class="ds-template" title="اتصال به دادهٔ قالب دیگر (لیست کشویی جستجوپذیر)">
                    <option value="">— بدون منبع داده —</option>
                    ${dsTemplateOptions}
                </select>
                <select class="ds-display" title="فیلد نمایشی"></select>
            </div>
        </td>
        <td><button class="remove-btn" type="button">حذف</button></td>
    `;

    const typeSelect = tr.querySelector(".field-type");
    const regexContainer = tr.querySelector(".regex-container");
    const regexInput = tr.querySelector(".field-regex");
    const predefinedSelect = tr.querySelector(".predefined-regex-select");

    function toggleRegex() {
        const isText = ORACLE_TYPES[typeSelect.value].base === "text";
        regexContainer.style.opacity = isText ? "1" : "0.3";
        regexContainer.style.pointerEvents = isText ? "auto" : "none";
        if (!isText) { regexInput.value = ""; predefinedSelect.value = ""; }
    }

    const sizeInput = tr.querySelector(".field-size");
    function toggleSize(fill) {
        const t = ORACLE_TYPES[typeSelect.value];
        sizeInput.style.display = t.size === "none" ? "none" : "";
        sizeInput.placeholder = t.size === "length" ? "طول (مثلاً 100)" : "p,s (مثلاً 10,2)";
        if (t.size === "none") sizeInput.value = ""; else if (fill && t.def) sizeInput.value = t.def;
    }
    typeSelect.addEventListener("change", () => { sizeInput.value = ""; toggleRegex(); toggleSize(true); });
    toggleRegex(); toggleSize(false);

    predefinedSelect.addEventListener("change", e => { if (e.target.value) regexInput.value = e.target.value; });

    /* --- Dynamic Data Source: پر کردن فیلد نمایشی بر اساس قالب انتخابی --- */
    const dsTemplateSel = tr.querySelector(".ds-template");
    const dsDisplaySel = tr.querySelector(".ds-display");
    function refreshDsFields() {
        const tid = dsTemplateSel.value;
        if (!tid) {
            dsDisplaySel.style.display = "none";
            dsDisplaySel.innerHTML = "";
            return;
        }
        const tpl = savedTemplates.find(t => String(t.id) === tid);
        if (!tpl) { dsDisplaySel.style.display = "none"; return; }
        const cur = data.dataSource && String(data.dataSource.templateId) === String(tid) ? data.dataSource.displayKey : "";
        dsDisplaySel.style.display = "";
        dsDisplaySel.innerHTML = `<option value="">— فیلد نمایشی —</option>` +
            tpl.fields.map(f => `<option value="${escapeHtml(f.key)}" ${cur === f.key ? "selected" : ""}>${escapeHtml(f.label)}</option>`).join("");
    }
    dsTemplateSel.addEventListener("change", refreshDsFields);
    refreshDsFields();

    tr.querySelector(".remove-btn").onclick = () => tr.remove();
    $("fieldsContainer").appendChild(tr);
}

function clearBuilder() {
    $("editTemplateId").value = ""; $("templateName").value = "";
    $("fieldsContainer").innerHTML = ""; addFieldRow();
}

function collectFields() {
    const fields = [];
    for (const row of $("fieldsContainer").querySelectorAll("tr")) {
        const label = row.querySelector(".field-label").value.trim();
        const key = row.querySelector(".field-key").value.trim();
        if (!label || !key) throw new Error("عنوان فارسی و Key همه فیلدها باید تکمیل شود.");
        const dbType = row.querySelector(".field-type").value;
        const type = ORACLE_TYPES[dbType].base;

        // --- خواندن منبع دادهٔ داینامیک (در صورت وجود) ---
        const dsTemplateVal = row.querySelector(".ds-template")?.value || "";
        let dataSource = null;
        if (dsTemplateVal) {
            const displayKey = row.querySelector(".ds-display")?.value || "";
            if (!displayKey) throw new Error(`برای فیلد «${label}» که منبع داده دارد، فیلد نمایشی را انتخاب کنید.`);
            dataSource = { templateId: Number(dsTemplateVal), displayKey, valueKey: displayKey };
        }

        fields.push({
            label, key, dbType,
            ...parseSize(dbType, row.querySelector(".field-size").value),
            aliases: row.querySelector(".field-aliases").value.split(",").map(x => x.trim()).filter(Boolean),
            required: row.querySelector(".field-required").checked,
            type,
            regex: type === "text" ? row.querySelector(".field-regex").value.trim() : "",
            dataSource
        });
    }
    if (!fields.length) throw new Error("حداقل یک فیلد لازم است.");
    return fields;
}

//$("addFieldBtn").onclick = () => addFieldRow();
$("clearBuilderBtn").onclick = () => clearBuilder();
$("saveTemplateBtn").onclick = () => {
    try {
        const name = $("templateName").value.trim();
        if (!name) throw new Error("نام قالب را وارد کنید.");
        const fields = collectFields();
        const editId = $("editTemplateId").value;
        const previous = editId ? savedTemplates.find(x => x.id === Number(editId)) : null;
        const template = { id: editId ? Number(editId) : Date.now(), name, fields };
        if (previous && previous.layout) template.layout = previous.layout; // Layout اختصاص‌یافته با ویرایش قالب از بین نمی‌رود

        if (editId) {
            const index = savedTemplates.findIndex(x => x.id === Number(editId));
            if (index >= 0) savedTemplates[index] = template;
        } else savedTemplates.push(template);

        localStorage.setItem(STORAGE_KEY, JSON.stringify(savedTemplates));
        renderTemplates(); updateSelector(); updateEntryTemplateSelector(); clearBuilder();
        showToast("قالب با موفقیت ذخیره شد.", "success");
    } catch (e) { showToast(e.message, "error"); }
};

// ورود دستی JSON
$("importJsonBtn").onclick = () => {
    try {
        const jsonStr = $("jsonInput").value.trim();
        if (!jsonStr) throw new Error("لطفاً JSON را وارد کنید.");
        const parsed = JSON.parse(jsonStr);
        const fields = Array.isArray(parsed) ? parsed : (parsed.fields || []);
        if (!Array.isArray(fields)) throw new Error("ساختار JSON باید یک آرایه از فیلدها یا یک آبجکت با کلید 'fields' باشد.");

        $("fieldsContainer").innerHTML = "";
        fields.forEach(f => addFieldRow(f));
        showToast("قالب از JSON بارگذاری شد. اکنون می‌توانید آن را ویرایش و ذخیره کنید.", "success");
    } catch (e) { showToast("خطا در خواندن JSON: " + e.message, "error"); }
};

function renderTemplates() {
    const box = $("savedTemplatesList");
    if (!savedTemplates.length) { box.innerHTML = "<p style='color:var(--muted);'>هنوز قالبی ذخیره نشده است.</p>"; return; }
    box.innerHTML = "";
    savedTemplates.forEach(template => {
        const div = document.createElement("div");
        div.className = "template-item";
        div.innerHTML = `
            <div class="template-info">
                <strong>${escapeHtml(template.name)}</strong>
                <small>${template.fields.length} فیلد | ${template.fields.filter(x => x.required).length} اجباری${template.layout ? ' | دارای Layout' : ''}</small>
            </div>
            <div class="template-actions">
                <button class="edit-btn">ویرایش</button>
                <button class="delete-btn">حذف</button>
            </div>`;
        div.querySelector(".edit-btn").onclick = () => {
            $("editTemplateId").value = template.id; $("templateName").value = template.name;
            $("fieldsContainer").innerHTML = ""; template.fields.forEach(addFieldRow);
            window.scrollTo({ top: 0, behavior: "smooth" });
        };
        div.querySelector(".delete-btn").onclick = async () => {
            const ok = await showConfirm("حذف قالب", `آیا از حذف قالب «${template.name}» مطمئن هستید؟\nاین عملیات قابل بازگشت نیست.`, true);
            if (!ok) return;
            savedTemplates = savedTemplates.filter(x => x.id !== template.id);
            localStorage.setItem(STORAGE_KEY, JSON.stringify(savedTemplates));
            renderTemplates(); updateSelector(); updateEntryTemplateSelector();
            showToast("قالب حذف شد.", "success");
        };
        box.appendChild(div);
    });
}

function updateSelector() {
    $("templateSelector").innerHTML = `<option value="">-- تشخیص خودکار قالب --</option>`;
    savedTemplates.forEach(template => {
        const option = document.createElement("option");
        option.value = template.id; option.textContent = template.name;
        $("templateSelector").appendChild(option);
    });
}

// ================================================================
// 5. Excel Upload & Mapping
// ================================================================
$("excelUpload").addEventListener("change", event => {
    const file = event.target.files[0]; if (!file) return;
    const reader = new FileReader();
    reader.onload = e => {
        try {
            const workbook = XLSX.read(new Uint8Array(e.target.result), { type: "array", cellDates: true });
            const sheet = workbook.Sheets[workbook.SheetNames[0]];
            const matrix = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: "" });
            if (matrix.length < 2) throw new Error("فایل خالی یا فاقد Header معتبر است.");
            uploadedColumns = matrix[0].map((x, i) => String(x || `Column_${i + 1}`).trim());
            uploadedExcelData = matrix.slice(1).map(row => {
                const obj = {}; uploadedColumns.forEach((column, index) => { obj[column] = row[index] ?? ""; }); return obj;
            });
            showStatus(`فایل «${file.name}» با ${uploadedExcelData.length} ردیف و ${uploadedColumns.length} ستون خوانده شد.`, "success");
            autoDetectTemplate();
        } catch (error) { showStatus(error.message, "error"); }
    };
    reader.readAsArrayBuffer(file);
});

function showStatus(text, type) { const el = $("uploadStatus"); el.textContent = text; el.className = `status show ${type}`; }

function scoreField(sourceColumn, field) {
    const source = normalize(sourceColumn); const sourceCompact = compact(sourceColumn);
    let best = 0; let method = "";
    const candidates = [
        { value: field.key, score: 100, method: "Exact Key" },
        { value: field.label, score: 98, method: "Exact Label" },
        ...(field.aliases || []).map(alias => ({ value: alias, score: 94, method: "Alias" }))
    ];
    for (const candidate of candidates) {
        if (source === normalize(candidate.value)) { best = Math.max(best, candidate.score); method = candidate.method; continue; }
        if (sourceCompact === compact(candidate.value)) { best = Math.max(best, candidate.score - 2); method = candidate.method + " / normalized"; }
        const sim = similarity(source, candidate.value); const tokenSim = tokenSimilarity(source, candidate.value);
        const fuzzyScore = Math.max(sim, tokenSim);
        if (fuzzyScore >= 82) { const score = Math.min(candidate.score - 8, fuzzyScore); if (score > best) { best = score; method = candidate.method + " / fuzzy"; } }
        if (source.includes(normalize(candidate.value)) || normalize(candidate.value).includes(source)) {
            const score = Math.min(candidate.score - 18, 78); if (score > best) { best = score; method = candidate.method + " / contains"; }
        }
    }
    // اعمال Regex فقط اگر نوع فیلد text باشد (برای بقیه انواع، امتیازدهی صرفاً بر پایهٔ تطابق نام + بونوس typeCompatibility است)
    if (field.type === 'text' && field.regex) {
        try {
            const regex = new RegExp(field.regex, "iu");
            if (regex.test(sourceColumn) && 96 > best) { best = 96; method = "REGEX"; }
        } catch (_) {}
    }
    return { score: Math.max(0, Math.min(100, best)), method };
}

function getSourceType(column) { return detectType(uploadedExcelData.slice(0, 30).map(row => row[column])); }
function analyzeColumn(column) {
    const sourceType = getSourceType(column);
    return currentTemplate.fields.map(field => {
        const base = scoreField(column, field);
        const bonus = typeCompatibility(field.type, sourceType);
        return { ...field, score: Math.max(0, Math.min(100, base.score + bonus)), method: base.method };
    }).sort((a, b) => b.score - a.score);
}
function computeScore(column, field) {
    const base = scoreField(column, field);
    const bonus = typeCompatibility(field.type, getSourceType(column));
    return { score: Math.max(0, Math.min(100, base.score + bonus)), method: base.method };
}
function buildSuggestions() {
    const result = {}; const used = new Set(); const all = [];
    uploadedColumns.forEach(column => { all.push({ col: column, ranked: analyzeColumn(column) }); });
    all.sort((a, b) => (b.ranked[0]?.score || 0) - (a.ranked[0]?.score || 0));
    for (const item of all) {
        const chosen = item.ranked.find(field => !used.has(field.key) && field.score >= 60);
        if (chosen) { used.add(chosen.key); result[item.col] = { key: chosen.key, score: chosen.score, method: chosen.method }; }
        else result[item.col] = { key: "", score: 0, method: "No confident match" };
    }
    return result;
}
function templateScore(template) {
    const previous = currentTemplate; currentTemplate = template;
    const scores = uploadedColumns.map(column => analyzeColumn(column)[0]?.score || 0);
    currentTemplate = previous; const matched = scores.filter(score => score >= 70).length;
    return scores.length ? Math.round(matched / scores.length * 100) : 0;
}
function autoDetectTemplate() {
    if (!savedTemplates.length) { $("uploadStatus").textContent = "قالبی ذخیره نشده است؛ ابتدا از تب «طراحی قالب» قالب بسازید."; return; }
    const ranked = savedTemplates.map(template => ({ template, score: templateScore(template) })).sort((a, b) => b.score - a.score);
    const best = ranked[0]; currentTemplate = best.template; $("templateSelector").value = best.template.id; renderMapping();
}

$("templateSelector").addEventListener("change", () => {
    currentTemplate = savedTemplates.find(t => t.id === Number($("templateSelector").value)) || null;
    if (currentTemplate) renderMapping(); else hideSection($("mappingSection"));
});

function confidenceClass(score) { if (score >= 85) return "conf-high"; if (score >= 70) return "conf-mid"; return "conf-low"; }

// ---- راه‌اندازی اولیهٔ Mapping بر اساس بهترین حدس خودکار (قابل جابه‌جایی با Drag & Drop در ادامه) ----
function initMappingAssignments() {
    mappingAssignments = {};
    const suggestions = buildSuggestions();
    Object.entries(suggestions).forEach(([column, sug]) => {
        if (sug.key) mappingAssignments[column] = sug.key;
    });
}

function renderMapping() {
    if (!currentTemplate || !uploadedColumns.length) return;
    initMappingAssignments();
    selectedPoolColumn = null;
    revealSection($("mappingSection"));
    renderMappingBoard();
}

// ---- تخته Drag & Drop: پول ستون‌های بدون Mapping + اسلات‌های فیلد قالب ----
function renderMappingBoard() {
    const pool = $("mappingPool");
    const slots = $("mappingSlots");
    pool.innerHTML = ""; slots.innerHTML = "";

    const assignedColumns = new Set(Object.keys(mappingAssignments));
    const unassigned = uploadedColumns.filter(c => !assignedColumns.has(c));
    $("poolCount").textContent = unassigned.length;

    if (!unassigned.length) {
        pool.innerHTML = `<div class="pool-empty">همهٔ ستون‌ها Mapping شده‌اند ✓</div>`;
    }
    unassigned.forEach(column => {
        const chip = document.createElement("div");
        chip.className = "source-chip";
        chip.draggable = true;
        chip.dataset.column = column;
        const sample = uploadedExcelData.slice(0, 2).map(row => row[column]).filter(v => v !== "" && v != null).map(String).join(" | ") || "—";
        chip.innerHTML = `<strong>${escapeHtml(column)}</strong><span class="chip-sample">${escapeHtml(sample)}</span>`;

        chip.addEventListener("dragstart", e => {
            e.dataTransfer.setData("text/plain", column);
            e.dataTransfer.effectAllowed = "move";
            chip.classList.add("dragging");
        });
        chip.addEventListener("dragend", () => chip.classList.remove("dragging"));
        chip.addEventListener("click", () => {
            if (selectedPoolColumn === column) { selectedPoolColumn = null; chip.classList.remove("selected"); return; }
            pool.querySelectorAll(".source-chip").forEach(c => c.classList.remove("selected"));
            selectedPoolColumn = column;
            chip.classList.add("selected");
        });
        pool.appendChild(chip);
    });

    let high = 0, mid = 0, low = 0;

    currentTemplate.fields.forEach(field => {
        const column = Object.keys(mappingAssignments).find(c => mappingAssignments[c] === field.key) || "";
        const slot = document.createElement("div");
        slot.className = "field-slot";
        slot.dataset.field = field.key;

        let scoreHtml = "";
        if (column) {
            const { score, method } = computeScore(column, field);
            if (score >= 85) high++; else if (score >= 70) mid++; else low++;
            scoreHtml = `<span class="confidence ${confidenceClass(score)}">${score}%</span><span class="method">${escapeHtml(method)}</span>`;
        } else if (field.required) {
            low++;
        }

        slot.innerHTML = `
            <div class="slot-head">
                <strong>${escapeHtml(field.label)}${field.required ? ' <span class="req-mark">*</span>' : ''}</strong>
                <code>${escapeHtml(field.key)}</code>
            </div>
            <div class="slot-body ${column ? 'filled' : 'empty'}">
                ${column
                    ? `<div class="assigned-chip"><span>${escapeHtml(column)}</span><button type="button" class="chip-remove" title="حذف Mapping">×</button></div><div class="score-container">${scoreHtml}</div>`
                    : `<div class="drop-hint">ستون را اینجا رها کنید یا از منوی زیر انتخاب کنید</div>`}
            </div>
            <select class="slot-select">
                <option value="">-- بدون Mapping --</option>
                ${uploadedColumns.map(c => `<option value="${escapeHtml(c)}" ${c === column ? "selected" : ""}>${escapeHtml(c)}</option>`).join("")}
            </select>
        `;

        slot.addEventListener("dragover", e => { e.preventDefault(); slot.classList.add("drag-over"); });
        slot.addEventListener("dragleave", () => slot.classList.remove("drag-over"));
        slot.addEventListener("drop", e => {
            e.preventDefault(); slot.classList.remove("drag-over");
            const draggedColumn = e.dataTransfer.getData("text/plain");
            if (draggedColumn) assignMapping(draggedColumn, field.key);
        });
        slot.addEventListener("click", (e) => {
            if (e.target.closest(".chip-remove") || e.target.closest("select")) return;
            if (selectedPoolColumn) { assignMapping(selectedPoolColumn, field.key); selectedPoolColumn = null; }
        });
        const removeBtn = slot.querySelector(".chip-remove");
        if (removeBtn) removeBtn.addEventListener("click", (e) => { e.stopPropagation(); unassignField(field.key); });
        slot.querySelector(".slot-select").addEventListener("change", (e) => {
            e.stopPropagation();
            if (e.target.value) assignMapping(e.target.value, field.key); else unassignField(field.key);
        });

        slots.appendChild(slot);
    });

    $("mappingSummary").innerHTML = `
        <span><b>${uploadedColumns.length}</b> ستون</span>
        <span style="color:var(--success);">● <b>${high}</b> اطمینان بالا</span>
        <span style="color:var(--primary);">● <b>${mid}</b> نیازمند بررسی</span>
        <span style="color:var(--danger);">● <b>${low}</b> دستی / بدون Mapping</span>`;
}

function assignMapping(column, fieldKey) {
    // هر فیلد فقط یک ستون می‌پذیرد: اگر فیلد مقصد قبلاً ستون دیگری داشت، آزادش کن
    Object.keys(mappingAssignments).forEach(c => { if (mappingAssignments[c] === fieldKey) delete mappingAssignments[c]; });
    // هر ستون هم فقط به یک فیلد وصل می‌شود (بازنویسی خودکار به‌خاطر کلید بودن column)
    mappingAssignments[column] = fieldKey;
    renderMappingBoard();
}
function unassignField(fieldKey) {
    Object.keys(mappingAssignments).forEach(c => { if (mappingAssignments[c] === fieldKey) delete mappingAssignments[c]; });
    renderMappingBoard();
}

// ================================================================
// 6. Validation & Pagination
// ================================================================
$("generateBtn").onclick = async () => {
    if (!currentTemplate || !uploadedExcelData.length) { showToast("قالب و فایل Excel را انتخاب کنید.", "error"); return; }

    const mapping = { ...mappingAssignments }; // { column: fieldKey }
    const requiredFields = currentTemplate.fields.filter(f => f.required).map(f => f.key);
    const mappedFieldKeys = new Set(Object.values(mapping));
    const missingRequired = requiredFields.filter(f => !mappedFieldKeys.has(f));
    if (missingRequired.length) {
        const labels = missingRequired.map(key => currentTemplate.fields.find(f => f.key === key)?.label || key);
        await showAlertModal(
            "فیلدهای اجباری Mapping نشده‌اند",
            `فیلدهای زیر اجباری هستند اما هنوز به هیچ ستونی وصل نشده‌اند:\n\n${labels.map(l => "• " + l).join("\n")}\n\nاز تختهٔ Mapping، ستون متناظر هر یک را به آن بکشید یا از منوی کشویی انتخاب کنید.`,
            true
        );
        return;
    }

    revealSection($("validationSection"));
    $("progressContainer").classList.remove("hidden");
    $("progressBar").style.width = "0%"; $("progressText").textContent = "شروع اعتبارسنجی...";

    validRows = []; invalidRows = [];
    const chunkSize = 1000; const totalRows = uploadedExcelData.length;

    for (let i = 0; i < totalRows; i += chunkSize) {
        const chunk = uploadedExcelData.slice(i, i + chunkSize);
        chunk.forEach((row, index) => {
            const globalIndex = i + index + 2; const targetRow = {}; const errors = [];
            currentTemplate.fields.forEach(field => {
                const sourceCol = Object.keys(mapping).find(key => mapping[key] === field.key);
                const value = sourceCol ? row[sourceCol] : ""; targetRow[field.key] = value;
                const err = validateFieldValue(field, value);
                if (err) errors.push(err);

                
            });

            if (errors.length > 0) { targetRow._errors = errors.join(" ؛ "); targetRow._row_number = globalIndex; invalidRows.push(targetRow); }
            else validRows.push(targetRow);
        });
        const progress = Math.round(((i + chunkSize) / totalRows) * 100);
        $("progressBar").style.width = `${Math.min(progress, 100)}%`;
        $("progressText").textContent = `پردازش ${Math.min(i + chunkSize, totalRows)} از ${totalRows} ردیف...`;
        await new Promise(resolve => setTimeout(resolve, 0));
    }

    $("progressText").textContent = "اعتبارسنجی کامل شد.";
    $("validationSummary").innerHTML = `<div>کل ردیف‌ها: <b>${totalRows}</b></div><div class="success">سالم: <b>${validRows.length}</b></div><div class="error">معیوب: <b>${invalidRows.length}</b></div>`;
    setTimeout(() => { $("progressContainer").classList.add("hidden"); }, 1800);

    currentPage = 1; currentView = 'valid';
    document.querySelectorAll(".v-tab").forEach(t => t.classList.remove("active"));
    document.querySelector('.v-tab[data-view="valid"]').classList.add("active");
    renderResultsTable();

    $("downloadValidBtn").disabled = validRows.length === 0;
    $("downloadInvalidBtn").disabled = invalidRows.length === 0;
    $("forceDownloadBtn").disabled = validRows.length === 0;

    showToast(`اعتبارسنجی تمام شد — ${validRows.length} سالم، ${invalidRows.length} معیوب.`, invalidRows.length ? "info" : "success");
};

document.querySelectorAll(".v-tab").forEach(tab => {
    tab.onclick = () => {
        document.querySelectorAll(".v-tab").forEach(t => t.classList.remove("active"));
        tab.classList.add("active");
        currentView = tab.dataset.view;
        currentPage = 1;
        renderResultsTable();
    };
});

$("rowsPerPageSelect").addEventListener("change", (e) => {
    rowsPerPage = Number(e.target.value) || 10;
    currentPage = 1;
    renderResultsTable();
});

// جدول نتایج با صفحه‌بندی — کل لیست (سالم/معیوب) همیشه از طریق صفحه‌بندی قابل مرور است، هرگز بریده نمی‌شود
function renderResultsTable() {
    const data = currentView === 'valid' ? validRows : invalidRows;
    const thead = $("resultsTableHead");
    const tbody = $("resultsTableBody");
    const pageInfo = $("pageInfo");

    if (!data.length) {
        thead.innerHTML = ""; tbody.innerHTML = `<tr><td colspan="100%" style="text-align:center;padding:20px;color:var(--muted);">داده‌ای برای نمایش وجود ندارد.</td></tr>`;
        pageInfo.textContent = "صفحه ۰ از ۰";
        $("prevPageBtn").disabled = true; $("nextPageBtn").disabled = true;
        return;
    }

    const totalPages = Math.ceil(data.length / rowsPerPage);
    if (currentPage > totalPages) currentPage = totalPages;
    const start = (currentPage - 1) * rowsPerPage;
    const end = start + rowsPerPage;
    const pageData = data.slice(start, end);

    let headerHtml = `<th>ردیف</th>`;
    currentTemplate.fields.forEach(f => { headerHtml += `<th>${escapeHtml(f.label)}</th>`; });
    if (currentView === 'invalid') headerHtml += `<th>خطاها و راهنمای رفع</th>`;
    thead.innerHTML = `<tr>${headerHtml}</tr>`;

    tbody.innerHTML = pageData.map((row, index) => {
        let rowHtml = `<td class="_row_number">${row._row_number || (start + index + 1)}</td>`;
        currentTemplate.fields.forEach(f => {
            const val = row[f.key] !== undefined ? String(row[f.key]) : "";
            rowHtml += `<td>${escapeHtml(val)}</td>`;
        });
        if (currentView === 'invalid') {
            const errList = String(row._errors || "").split(" ؛ ").filter(Boolean);
            const chipsHtml = errList.map(msg => `<div class="err-chip">${escapeHtml(msg)}</div>`).join("");
            rowHtml += `<td class="_errors">${chipsHtml}</td>`;
        }
        return `<tr>${rowHtml}</tr>`;
    }).join("");

    pageInfo.textContent = `صفحه ${currentPage} از ${totalPages} (مجموع ${data.length} ردیف)`;
    $("prevPageBtn").disabled = currentPage === 1;
    $("nextPageBtn").disabled = currentPage === totalPages;
}

$("prevPageBtn").onclick = () => { if (currentPage > 1) { currentPage--; renderResultsTable(); } };
$("nextPageBtn").onclick = () => { const data = currentView === 'valid' ? validRows : invalidRows; if (currentPage < Math.ceil(data.length / rowsPerPage)) { currentPage++; renderResultsTable(); } };

// ================================================================
// 7. Download Output
// ================================================================
function downloadExcel(data, fileName, sheetName = "Data") {
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, sheetName);
    XLSX.writeFile(wb, fileName);
}

$("downloadValidBtn").onclick = () => downloadExcel(validRows, `Valid_${currentTemplate.name}.xlsx`, "Valid Data");
$("downloadInvalidBtn").onclick = () => downloadExcel(invalidRows, `Invalid_${currentTemplate.name}.xlsx`, "Invalid Data");
$("forceDownloadBtn").onclick = async () => {
    const ok = await showConfirm("ادامه بدون رکوردهای معیوب", `${invalidRows.length} ردیف معیوب از خروجی نهایی حذف خواهد شد و فقط ${validRows.length} ردیف سالم دانلود می‌شود. ادامه می‌دهید؟`, true);
    if (ok) downloadExcel(validRows, `Mapped_${currentTemplate.name}.xlsx`, "Mapped Data");
};

// ================================================================
// 8. تب ۳ — تبدیل قالب به فرم گرافیکی ورود داده
// ================================================================

// --- لایهٔ داده (Data Model) ---
function loadEntryRecords(templateId) {
    const all = JSON.parse(localStorage.getItem(ENTRY_STORAGE_KEY) || "{}");
    return all[templateId] || [];
}
function persistEntryRecords(templateId, records) {
    const all = JSON.parse(localStorage.getItem(ENTRY_STORAGE_KEY) || "{}");
    all[templateId] = records;
    localStorage.setItem(ENTRY_STORAGE_KEY, JSON.stringify(all));
}

function updateEntryTemplateSelector() {
    const sel = $("entryTemplateSelector");
    if (!sel) return;
    const prevValue = sel.value;
    sel.innerHTML = `<option value="">-- انتخاب قالب --</option>`;
    savedTemplates.forEach(template => {
        const option = document.createElement("option");
        option.value = template.id; option.textContent = template.name;
        sel.appendChild(option);
    });
    if (prevValue && savedTemplates.some(t => String(t.id) === String(prevValue))) {
        sel.value = prevValue;
    } else {
        renderEntryForm(null);
    }
    if (window.LayoutEngine) LayoutEngine.syncSelector();
}

// --- لایهٔ UI: تولید خودکار المان HTML مناسب بر اساس نوع دادهٔ فیلد ---
function buildEntryInput(field) {
    // ۱) فیلد با منبع دادهٔ داینامیک → Combobox جستجوپذیر
    if (field.dataSource && field.dataSource.templateId) {
        return buildCombobox(field, { required: field.required });
    }
    // ۲) فیلد تاریخ/تاریخ‌وساعت → تقویم جلالی (ذخیرهٔ ISO میلادی)
    if (field.type === "date") {
        return buildJalaliPicker(field, { required: field.required, withTime: false });
    }
    if (field.type === "datetime") {
        return buildJalaliPicker(field, { required: field.required, withTime: true });
    }
    // ۳) حالت‌های دیگر — بدون تغییر
    let input = document.createElement("input");
    if (field.type === "number") {
        input.type = "number";
        input.step = dbTypeOf(field) === "INTEGER" ? "1" : "any";
    } else if (field.type === "any") {
        input.type = "text";
    } else {
        input.type = "text";
    }
    input.dataset.key = field.key;
    if (field.required) input.required = true;
    return input;
}

function renderEntryForm(template) {
    currentEntryTemplate = template;
    const container = $("entryFormContainer");
    if (!template) {
        container.innerHTML = "";
        hideSection($("entryFormSection"));
        hideSection($("entryListSection"));
        return;
    }
    container.innerHTML = "";
    // اگر برای این قالب Layout اختصاص داده شده باشد، همان Layout بارگذاری می‌شود و فرم داینامیک تولید نمی‌شود
    const useLayout = !!(template.layout && window.LayoutEngine);
    container.className = useLayout ? "lf-host" : "entry-form-grid";
    $("entryModeBadge").textContent = useLayout ? "Layout سفارشی" : "فرم داینامیک";
    $("entryModeBadge").classList.toggle("is-layout", useLayout);
    $("saveEntryBtn").textContent = "ذخیره رکورد";
    if (useLayout) {
        LayoutEngine.renderRuntime(template, container);
        $("saveEntryBtn").textContent = (template.layout.settings && template.layout.settings.submitText) || "ذخیره رکورد";
    }
    else template.fields.forEach(field => {
        const wrap = document.createElement("div");
        wrap.className = "form-group entry-field";
        wrap.setAttribute("data-field-wrap", "");
        const label = document.createElement("label");
        const typeLabel = dbTypeOf(field) + (field.length ? `(${field.length})` : field.precision ? `(${field.precision},${field.scale || 0})` : "");
        label.textContent = `${field.label}${field.required ? " *" : ""} (${typeLabel})`;
        wrap.appendChild(label);
        wrap.appendChild(buildEntryInput(field));
        const errSpan = document.createElement("span");
        errSpan.className = "field-error";
        wrap.appendChild(errSpan);
        container.appendChild(wrap);
    });

    $("editEntryId").value = "";
    revealSection($("entryFormSection"));
    revealSection($("entryListSection"));

    entryRecords = loadEntryRecords(template.id);
    entryCurrentPage = 1;
    renderEntriesTable();
}

$("entryTemplateSelector").addEventListener("change", () => {
    const template = savedTemplates.find(t => t.id === Number($("entryTemplateSelector").value)) || null;
    renderEntryForm(template);
});

function clearEntryForm() {
    $("entryFormContainer").querySelectorAll("[data-key]").forEach(inp => { inp.value = ""; });
    $("entryFormContainer").querySelectorAll(".field-error").forEach(span => { span.textContent = ""; });
    $("editEntryId").value = "";
    if (window.LayoutEngine) LayoutEngine.reset($("entryFormContainer"));
}
$("clearEntryBtn").onclick = clearEntryForm;

$("saveEntryBtn").onclick = () => {
    if (!currentEntryTemplate) { showToast("ابتدا یک قالب انتخاب کنید.", "error"); return; }
    const inputs = $("entryFormContainer").querySelectorAll("[data-key]");
    const data = {}; let hasError = false; let firstBad = null;

    inputs.forEach(inp => {
        const field = currentEntryTemplate.fields.find(f => f.key === inp.dataset.key);
        if (!field) return;
        const wrap = inp.closest("[data-field-wrap]");
        const errSpan = wrap.querySelector(".field-error");
        // فیلدی که به‌خاطر شرط نمایش (showIf) مخفی است اعتبارسنجی نمی‌شود
        if (wrap.classList.contains("is-hidden")) { data[field.key] = ""; errSpan.textContent = ""; return; }
        const effective = inp.dataset.req === "1" ? { ...field, required: true } : field;
        const err = validateFieldValue(effective, inp.value);
setFieldErrorState(inp, err);           // ✅ هم پیام، هم کلاس .has-error
if (err) { hasError = true; firstBad = firstBad || inp; }
        data[field.key] = inp.value;

        
    });

    if (hasError && window.LayoutEngine) LayoutEngine.reveal(firstBad);
    if (hasError) { showToast("برخی فیلدها نامعتبرند؛ راهنمای زیر هر فیلد را بررسی کنید.", "error"); return; }

    const editId = $("editEntryId").value;
    if (editId) {
        const idx = entryRecords.findIndex(r => r._id === editId);
        if (idx >= 0) entryRecords[idx] = { ...entryRecords[idx], ...data };
        showToast("رکورد ویرایش شد.", "success");
    } else {
        data._id = "e" + Date.now().toString(36) + Math.random().toString(36).slice(2, 7);
        data._createdAt = new Date().toISOString();
        entryRecords.push(data);
        showToast("رکورد جدید ذخیره شد.", "success");
    }
    persistEntryRecords(currentEntryTemplate.id, entryRecords);
if (window.Combobox) window.Combobox.refreshAll();

    clearEntryForm();
    entryCurrentPage = Math.max(1, Math.ceil(entryRecords.length / entryRowsPerPage));
    renderEntriesTable();
};

// --- لیست قابل نمایش رکوردهای ثبت‌شده (با صفحه‌بندی) ---
function renderEntriesTable() {
    const thead = $("entriesTableHead");
    const tbody = $("entriesTableBody");
    if (!currentEntryTemplate) { thead.innerHTML = ""; tbody.innerHTML = ""; return; }

    const total = entryRecords.length;
    if (!total) {
        thead.innerHTML = "";
        tbody.innerHTML = `<tr><td colspan="100%" class="entries-empty">هنوز داده‌ای برای این قالب ثبت نشده است.</td></tr>`;
        $("entryPageInfo").textContent = "صفحه ۰ از ۰";
        $("entryPrevBtn").disabled = true; $("entryNextBtn").disabled = true;
        return;
    }

    const totalPages = Math.ceil(total / entryRowsPerPage);
    if (entryCurrentPage > totalPages) entryCurrentPage = totalPages;
    const start = (entryCurrentPage - 1) * entryRowsPerPage;
    const pageData = entryRecords.slice(start, start + entryRowsPerPage);

    let headerHtml = `<th>#</th>`;
    currentEntryTemplate.fields.forEach(f => { headerHtml += `<th>${escapeHtml(f.label)}</th>`; });
    headerHtml += `<th>عملیات</th>`;
    thead.innerHTML = `<tr>${headerHtml}</tr>`;

    tbody.innerHTML = pageData.map((rec, index) => {
        let rowHtml = `<td class="_row_number">${start + index + 1}</td>`;
        currentEntryTemplate.fields.forEach(f => { rowHtml += `<td>${escapeHtml(rec[f.key] ?? "")}</td>`; });
        rowHtml += `<td><button class="btn ghost entry-edit-btn" data-id="${rec._id}">ویرایش</button> <button class="btn danger entry-del-btn" data-id="${rec._id}">حذف</button></td>`;
        return `<tr>${rowHtml}</tr>`;
    }).join("");

    tbody.querySelectorAll(".entry-edit-btn").forEach(btn => { btn.onclick = () => editEntryRecord(btn.dataset.id); });
    tbody.querySelectorAll(".entry-del-btn").forEach(btn => { btn.onclick = () => deleteEntryRecord(btn.dataset.id); });

    $("entryPageInfo").textContent = `صفحه ${entryCurrentPage} از ${totalPages} (مجموع ${total} رکورد)`;
    $("entryPrevBtn").disabled = entryCurrentPage === 1;
    $("entryNextBtn").disabled = entryCurrentPage === totalPages;
}

function editEntryRecord(id) {
    const rec = entryRecords.find(r => r._id === id);
    if (!rec) return;
    $("editEntryId").value = id;
    $("entryFormContainer").querySelectorAll("[data-key]").forEach(inp => { inp.value = rec[inp.dataset.key] ?? ""; });
    $("entryFormContainer").querySelectorAll(".field-error").forEach(span => { span.textContent = ""; });
    $("entryFormContainer").querySelectorAll(".has-error").forEach(el => el.classList.remove("has-error"));
    if (window.LayoutEngine) LayoutEngine.refresh($("entryFormContainer"));
    window.scrollTo({ top: $("entryFormSection").offsetTop - 20, behavior: "smooth" });
}

function deleteEntryRecord(id) {
    showConfirm("حذف رکورد", "این رکورد حذف شود؟ این عملیات قابل بازگشت نیست.", true).then(ok => {
        if (!ok) return;
        entryRecords = entryRecords.filter(r => r._id !== id);
        persistEntryRecords(currentEntryTemplate.id, entryRecords);
        renderEntriesTable();
        showToast("رکورد حذف شد.", "success");
    });
}

$("entryPrevBtn").onclick = () => { if (entryCurrentPage > 1) { entryCurrentPage--; renderEntriesTable(); } };
$("entryNextBtn").onclick = () => {
    const totalPages = Math.ceil(entryRecords.length / entryRowsPerPage);
    if (entryCurrentPage < totalPages) { entryCurrentPage++; renderEntriesTable(); }
};

$("exportEntriesBtn").onclick = () => {
    if (!currentEntryTemplate || !entryRecords.length) { showToast("داده‌ای برای دانلود وجود ندارد.", "error"); return; }
    const dataForExport = entryRecords.map(rec => {
        const row = {};
        currentEntryTemplate.fields.forEach(f => { row[f.label] = rec[f.key] ?? ""; });
        return row;
    });
    downloadExcel(dataForExport, `Entries_${currentEntryTemplate.name}.xlsx`, "Entries");
};


/* ================================================================
   JALALI CALENDAR UTIL (Gregorian ⇄ Jalali, بدون وابستگی خارجی)
   نمایش: جلالی | ذخیره‌سازی: ISO میلادی برای Oracle DATE
   ================================================================ */
const Jalali = (() => {
    const div = (a, b) => ~~(a / b);
    const mod = (a, b) => a - ~~(a / b) * b;
    const breaks = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178];

    function jalCal(jy) {
        const bl = breaks.length, gy = jy + 621;
        let leapJ = -14, jp = breaks[0], jm, jump, leap, leapG, march, n;
        if (jy < jp || jy >= breaks[bl - 1]) throw new Error("Invalid Jalaali year");
        for (let i = 1; i < bl; i++) {
            jm = breaks[i]; jump = jm - jp;
            if (jy < jm) break;
            leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4);
            jp = jm;
        }
        n = jy - jp;
        leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4);
        if (mod(jump, 33) === 4 && jump - n === 4) leapJ++;
        leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150;
        march = 20 + leapJ - leapG;
        if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33;
        leap = mod(mod(n + 1, 33) - 1, 4);
        if (leap === -1) leap = 4;
        return { leap, gy, march };
    }
    function g2d(gy, gm, gd) {
        let d = div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408;
        d = d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752;
        return d;
    }
    function d2g(jdn) {
        let j = 4 * jdn + 139361631;
        j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
        const i = div(mod(j, 1461), 4) * 5 + 308;
        const gd = div(mod(i, 153), 5) + 1;
        const gm = mod(div(i, 153), 12) + 1;
        const gy = div(j, 1461) - 100100 + div(8 - gm, 6);
        return { gy, gm, gd };
    }
    function j2d(jy, jm, jd) {
        const r = jalCal(jy);
        return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1;
    }
    function d2j(jdn) {
        let gy = d2g(jdn).gy, jy = gy - 621;
        const r = jalCal(jy);
        const jdn1f = g2d(gy, 3, r.march);
        let jm, jd, k = jdn - jdn1f;
        if (k >= 0) {
            if (k <= 185) { jm = 1 + div(k, 31); jd = mod(k, 31) + 1; return { jy, jm, jd }; }
            k -= 186;
        } else { jy -= 1; k += 179; if (r.leap === 1) k++; }
        jm = 7 + div(k, 30);
        jd = mod(k, 30) + 1;
        return { jy, jm, jd };
    }
    return {
        toJalaali(gy, gm, gd) { return d2j(g2d(gy, gm, gd)); },
        toGregorian(jy, jm, jd) { return d2g(j2d(jy, jm, jd)); },
        isLeapYear(jy) { return jalCal(jy).leap === 0; },
        monthLength(jy, jm) { return jm <= 6 ? 31 : jm <= 11 ? 30 : (this.isLeapYear(jy) ? 30 : 29); },
        isValid(jy, jm, jd) { return jy >= -61 && jy <= 3177 && jm >= 1 && jm <= 12 && jd >= 1 && jd <= this.monthLength(jy, jm); }
    };
})();

const JM_NAMES = ["فروردین","اردیبهشت","خرداد","تیر","مرداد","شهریور","مهر","آبان","آذر","دی","بهمن","اسفند"];

function buildJalaliPicker(field, opts = {}) {
    const wrap = document.createElement("div");
    wrap.className = "jdate-wrap lf-control";
    wrap.dataset.key = field.key;
    if (opts.required) wrap.dataset.req = "1";

    const visible = document.createElement("input");
    visible.type = "text";
    visible.className = "jdate-visible";
    visible.placeholder = opts.placeholder || "۱۴۰۳/۰۷/۰۸";
    visible.readOnly = true;
    visible.autocomplete = "off";

    const hidden = document.createElement("input");
    hidden.type = "hidden";

    let timeInput = null;
    if (opts.withTime) {
        timeInput = document.createElement("input");
        timeInput.type = "time";
        timeInput.className = "jdate-time";
    }

    const popup = document.createElement("div");
    popup.className = "jp-popup hidden";

    let currentJ = { jy: 1403, jm: 7, jd: 1 };

    function refreshVisible() {
        if (!hidden.value) { visible.value = ""; return; }
        const m = hidden.value.match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (!m) { visible.value = hidden.value; return; }
        try {
            const j = Jalali.toJalaali(+m[1], +m[2], +m[3]);
            visible.value = `${j.jy}/${String(j.jm).padStart(2, "0")}/${String(j.jd).padStart(2, "0")}`;
            currentJ = j;
        } catch (_) { visible.value = hidden.value; }
    }

    Object.defineProperty(wrap, "value", {
        get() { return hidden.value; },
        set(v) {
            hidden.value = v == null ? "" : String(v);
            if (timeInput) {
                const tm = hidden.value.match(/T(\d{2}:\d{2})/);
                timeInput.value = tm ? tm[1] : "";
            }
            refreshVisible();
        }
    });
    Object.defineProperty(wrap, "placeholder", {
        get() { return visible.placeholder; },
        set(v) { visible.placeholder = v; }
    });

    function commit(g, timeStr) {
        let iso = `${g.gy}-${String(g.gm).padStart(2, "0")}-${String(g.gd).padStart(2, "0")}`;
        if (timeInput && timeStr) iso += "T" + timeStr;
        hidden.value = iso;
        refreshVisible();
        wrap.dispatchEvent(new Event("change", { bubbles: true }));
        wrap.dispatchEvent(new Event("input", { bubbles: true }));
    }

    function renderDays() {
        const { jy, jm } = currentJ;
        const monthLen = Jalali.monthLength(jy, jm);
        const firstG = Jalali.toGregorian(jy, jm, 1);
        const firstDate = new Date(firstG.gy, firstG.gm - 1, firstG.gd);
        const startCol = (firstDate.getDay() + 1) % 7; // شنبه = 0

        const now = new Date();
        const todayJ = Jalali.toJalaali(now.getFullYear(), now.getMonth() + 1, now.getDate());

        let selJ = null;
        if (hidden.value) {
            const m = hidden.value.match(/^(\d{4})-(\d{2})-(\d{2})/);
            if (m) try { selJ = Jalali.toJalaali(+m[1], +m[2], +m[3]); } catch (_) {}
        }

        let html = "";
        const pm = jm === 1 ? 12 : jm - 1, py = jm === 1 ? jy - 1 : jy;
        const pl = Jalali.monthLength(py, pm);
        for (let i = 0; i < startCol; i++) {
            const d = pl - startCol + 1 + i;
            html += `<button type="button" class="other" data-jy="${py}" data-jm="${pm}" data-jd="${d}">${d}</button>`;
        }
        for (let d = 1; d <= monthLen; d++) {
            const cls = [
                selJ && selJ.jy === jy && selJ.jm === jm && selJ.jd === d ? "selected" : "",
                todayJ.jy === jy && todayJ.jm === jm && todayJ.jd === d ? "today" : ""
            ].filter(Boolean).join(" ");
            html += `<button type="button" class="${cls}" data-jy="${jy}" data-jm="${jm}" data-jd="${d}">${d}</button>`;
        }
        const rem = (7 - ((startCol + monthLen) % 7)) % 7;
        const nm = jm === 12 ? 1 : jm + 1, ny = jm === 12 ? jy + 1 : jy;
        for (let d = 1; d <= rem; d++) {
            html += `<button type="button" class="other" data-jy="${ny}" data-jm="${nm}" data-jd="${d}">${d}</button>`;
        }

        popup.innerHTML = `
            <div class="jp-head">
                <button type="button" data-nav="prev">‹</button>
                <span class="jp-title">${JM_NAMES[jm - 1]} ${jy}</span>
                <button type="button" data-nav="next">›</button>
            </div>
            <div class="jp-weekdays"><span>ش</span><span>ی</span><span>د</span><span>س</span><span>چ</span><span>پ</span><span>ج</span></div>
            <div class="jp-days">${html}</div>
            <div class="jp-foot">
                <button type="button" data-act="today">امروز</button>
                <button type="button" data-act="clear">پاک کردن</button>
            </div>`;

        popup.querySelectorAll("[data-nav]").forEach(b => b.onclick = e => {
            e.stopPropagation();
            const dir = b.dataset.nav === "next" ? 1 : -1;
            let nm2 = currentJ.jm + dir, ny2 = currentJ.jy;
            if (nm2 < 1) { nm2 = 12; ny2--; }
            if (nm2 > 12) { nm2 = 1; ny2++; }
            currentJ = { jy: ny2, jm: nm2, jd: 1 };
            renderDays();
        });
        popup.querySelectorAll(".jp-days button").forEach(b => b.onclick = e => {
            e.stopPropagation();
            const g = Jalali.toGregorian(+b.dataset.jy, +b.dataset.jm, +b.dataset.jd);
            commit(g, timeInput ? timeInput.value : null);
            close();
        });
        popup.querySelector('[data-act="today"]').onclick = e => {
            e.stopPropagation();
            const d = new Date();
            commit({ gy: d.getFullYear(), gm: d.getMonth() + 1, gd: d.getDate() }, timeInput ? (timeInput.value || "00:00") : null);
            close();
        };
        popup.querySelector('[data-act="clear"]').onclick = e => {
            e.stopPropagation();
            hidden.value = "";
            if (timeInput) timeInput.value = "";
            refreshVisible();
            close();
            wrap.dispatchEvent(new Event("change", { bubbles: true }));
            wrap.dispatchEvent(new Event("input", { bubbles: true }));
        };
    }

    function open() {
        if (hidden.value) {
            const m = hidden.value.match(/^(\d{4})-(\d{2})-(\d{2})/);
            if (m) try { currentJ = Jalali.toJalaali(+m[1], +m[2], +m[3]); } catch (_) {}
        } else {
            const d = new Date();
            currentJ = Jalali.toJalaali(d.getFullYear(), d.getMonth() + 1, d.getDate());
        }
        renderDays();
        popup.classList.remove("hidden");
    }
    function close() { popup.classList.add("hidden"); }

    visible.addEventListener("click", open);
    if (timeInput) timeInput.addEventListener("change", () => {
        if (!hidden.value) return;
        const m = hidden.value.match(/^(\d{4}-\d{2}-\d{2})/);
        if (m) {
            hidden.value = m[1] + (timeInput.value ? "T" + timeInput.value : "");
            wrap.dispatchEvent(new Event("change", { bubbles: true }));
            wrap.dispatchEvent(new Event("input", { bubbles: true }));
        }
    });
    // بستن با کلیک بیرون (فقط یک بار در سطح document ثبت می‌شود)
    if (!buildJalaliPicker._docBound) {
        buildJalaliPicker._docBound = true;
        document.addEventListener("mousedown", e => {
            document.querySelectorAll(".jdate-wrap").forEach(w => {
                if (!w.contains(e.target)) w.querySelector(".jp-popup")?.classList.add("hidden");
            });
        });
    }

    wrap.appendChild(visible);
    if (timeInput) wrap.appendChild(timeInput);
    wrap.appendChild(hidden);
    wrap.appendChild(popup);
    return wrap;
}

/* ================================================================
   سازندهٔ Combobox جستجوپذیر متصل به دادهٔ قالب دیگر
   field.dataSource = { templateId, displayKey, valueKey? }
   ================================================================ */
/* ================================================================
   Combobox جستجوپذیر (به سبک Select2) متصل به دادهٔ قالب دیگر
   field.dataSource = { templateId, displayKey, valueKey? }
   ================================================================ */
function buildCombobox(field, opts = {}) {
    const wrap = document.createElement("div");
    wrap.className = "combo-wrap lf-control";
    wrap.dataset.key = field.key;
    if (opts.required) wrap.dataset.req = "1";
    wrap.__field = field; // برای refresh بعدی

    const visible = document.createElement("input");
    visible.type = "text";
    visible.className = "combo-visible";
    visible.placeholder = opts.placeholder || "— جستجو و انتخاب —";
    visible.autocomplete = "off";
    visible.spellcheck = false;

    const hidden = document.createElement("input");
    hidden.type = "hidden";

    const clearBtn = document.createElement("button");
    clearBtn.type = "button";
    clearBtn.className = "combo-clear";
    clearBtn.setAttribute("aria-label", "پاک کردن انتخاب");
    clearBtn.textContent = "×";

    const list = document.createElement("div");
    list.className = "combo-list hidden";
    list.setAttribute("role", "listbox");

    let allOptions = [];
    let filtered = [];
    let activeIdx = -1;
    let loaded = false;

    /* ---------- بارگذاری داده از LocalStorage ---------- */
    function loadOptions() {
        const ds = field.dataSource || {};
        if (!ds.templateId) { allOptions = []; loaded = true; return; }

        let allEntries = {};
        try {
            allEntries = JSON.parse(localStorage.getItem(ENTRY_STORAGE_KEY) || "{}");
        } catch (_) { allEntries = {}; }

        const records = allEntries[String(ds.templateId)] || [];
        const tpl = savedTemplates.find(t => String(t.id) === String(ds.templateId));
        const displayKey = ds.displayKey || (tpl && tpl.fields[0] && tpl.fields[0].key);
        const valueKey = ds.valueKey || displayKey;
        if (!tpl || !displayKey) { allOptions = []; loaded = true; return; }

        allOptions = records.map(rec => {
            const label = String(rec[displayKey] ?? "").trim();
            const value = String(rec[valueKey] ?? "").trim();
            if (!label && !value) return null;
            return {
                value: value || label,
                label: label || value,
                search: (label + " " + value).toLowerCase()
            };
        }).filter(Boolean);

        loaded = true;
    }

    function getLabelOf(v) {
        const o = allOptions.find(x => String(x.value) === String(v));
        return o ? o.label : String(v);
    }
    function updateHasValue() { wrap.classList.toggle("has-value", !!hidden.value); }

    function setValue(v, silent) {
        hidden.value = v == null ? "" : String(v);
        visible.value = hidden.value ? getLabelOf(hidden.value) : "";
        updateHasValue();
        if (!silent) {
            wrap.dispatchEvent(new Event("change", { bubbles: true }));
            wrap.dispatchEvent(new Event("input",  { bubbles: true }));
        }
    }

    /* ---------- API .value / .placeholder ---------- */
    Object.defineProperty(wrap, "value", {
        get() { return hidden.value; },
        set(v) { if (!loaded) loadOptions(); setValue(v, true); }
    });
    Object.defineProperty(wrap, "placeholder", {
        get() { return visible.placeholder; },
        set(v) { visible.placeholder = v; }
    });

    /* ---------- Highlight کردن قسمت مطابق ---------- */
    function highlight(text, q) {
        if (!q) return escapeHtml(text);
        const i = text.toLowerCase().indexOf(q.toLowerCase());
        if (i < 0) return escapeHtml(text);
        return escapeHtml(text.slice(0, i)) +
            "<mark>" + escapeHtml(text.slice(i, i + q.length)) + "</mark>" +
            escapeHtml(text.slice(i + q.length));
    }

    /* ---------- فیلتر هوشمند (prefix > substring > fuzzy) ---------- */
    function filterOptions(q) {
        const query = (q || "").trim().toLowerCase();
        if (!query) return allOptions.slice();
        const words = query.split(/\s+/);
        const first = words[0];
        const starts = [], contains = [], rest = [];
        for (const o of allOptions) {
            if (o.search.startsWith(first)) starts.push(o);
            else if (words.every(w => o.search.includes(w))) contains.push(o);
            else if (o.search.includes(first)) rest.push(o);
        }
        return [...starts, ...contains, ...rest];
    }

    /* ---------- رندر لیست ---------- */
    function renderList(q) {
        filtered = filterOptions(q);
        const total = filtered.length;
        if (filtered.length > 100) filtered = filtered.slice(0, 100);
        activeIdx = -1;

        if (!loaded) {
            list.innerHTML = `<div class="combo-loading">در حال بارگذاری…</div>`;
            return;
        }
        if (!allOptions.length) {
            const srcTpl = savedTemplates.find(t => String(t.id) === String((field.dataSource || {}).templateId));
            list.innerHTML = `<div class="combo-empty">
                هیچ رکوردی در منبع داده یافت نشد.<br>
                <small>ابتدا در تب «فرم ورود دستی داده» قالب
                «${escapeHtml(srcTpl ? srcTpl.name : "—")}» را انتخاب و چند رکورد ثبت کنید.</small>
            </div>`;
            return;
        }
        if (!total) {
            list.innerHTML = `<div class="combo-empty">موردی با این عبارت یافت نشد.</div>`;
            return;
        }

        list.innerHTML =
            filtered.map((o, i) =>
                `<div class="combo-item" role="option" data-idx="${i}" data-value="${escapeHtml(o.value)}">
                    ${highlight(o.label, q)}
                 </div>`
            ).join("") +
            (total > 100
                ? `<div class="combo-more">${total - 100} مورد دیگر — جستجو را دقیق‌تر کنید</div>`
                : "") +
            `<div class="combo-foot">
                <span>${total} رکورد</span>
                <button type="button" data-act="refresh" title="بارگذاری مجدد">↻</button>
             </div>`;

        list.querySelectorAll(".combo-item").forEach(el => {
            el.addEventListener("mousedown", e => {
                e.preventDefault();
                const o = filtered[+el.dataset.idx];
                if (o) { setValue(o.value); close(); }
            });
            el.addEventListener("mouseenter", () => {
                list.querySelectorAll(".combo-item").forEach(x => x.classList.remove("active"));
                el.classList.add("active");
                activeIdx = +el.dataset.idx;
            });
        });
        const refBtn = list.querySelector('[data-act="refresh"]');
        if (refBtn) refBtn.addEventListener("mousedown", e => {
            e.preventDefault();
            refresh();
        });
    }

    function open() {
        if (!loaded) loadOptions();
        list.classList.remove("hidden");
        renderList("");
        setTimeout(() => {
            const cur = [...list.querySelectorAll(".combo-item")]
                .find(el => el.dataset.value === hidden.value);
            if (cur) cur.scrollIntoView({ block: "center" });
        }, 20);
    }
    function close() {
        list.classList.add("hidden");
        activeIdx = -1;
    }
    function refresh() {
        loaded = false;
        loadOptions();
        if (!list.classList.contains("hidden")) renderList(visible.value);
    }
    wrap.__refresh = refresh;

    /* ---------- رویدادها ---------- */
    visible.addEventListener("focus", () => {
        if (hidden.value) visible.value = ""; // برای جستجوی سریع، پاک شود
        open();
    });
    visible.addEventListener("input", () => {
        // اگر کاربر متن را دقیقاً برابر برچسب مقدار فعلی کرد، انتخاب را حفظ کن
        if (hidden.value && visible.value === getLabelOf(hidden.value)) return;
        if (hidden.value) { hidden.value = ""; updateHasValue(); }
        renderList(visible.value);
        list.classList.remove("hidden");
    });
    visible.addEventListener("keydown", e => {
        if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            if (list.classList.contains("hidden")) { open(); return; }
            if (!filtered.length) return;
            activeIdx = e.key === "ArrowDown"
                ? Math.min(filtered.length - 1, activeIdx + 1)
                : Math.max(0, activeIdx - 1);
            list.querySelectorAll(".combo-item").forEach((el, i) =>
                el.classList.toggle("active", i === activeIdx));
            list.querySelector(`.combo-item[data-idx="${activeIdx}"]`)
                ?.scrollIntoView({ block: "nearest" });
        } else if (e.key === "Enter") {
            if (activeIdx >= 0 && filtered[activeIdx] && !list.classList.contains("hidden")) {
                e.preventDefault();
                setValue(filtered[activeIdx].value);
                close();
            }
        } else if (e.key === "Escape") {
            close();
            if (hidden.value) visible.value = getLabelOf(hidden.value);
        } else if (e.key === "Tab") {
            if (!hidden.value && visible.value) visible.value = "";
            close();
        }
    });
    visible.addEventListener("blur", () => setTimeout(() => {
        if (!wrap.contains(document.activeElement)) {
            close();
            if (hidden.value) visible.value = getLabelOf(hidden.value);
            else visible.value = "";
        }
    }, 150));

    clearBtn.addEventListener("mousedown", e => {
        e.preventDefault(); e.stopPropagation();
        setValue("");
        visible.focus();
        open();
    });

    /* ---------- Mount ---------- */
    loadOptions(); // ✅ بارگذاری eager
    wrap.appendChild(visible);
    wrap.appendChild(clearBtn);
    wrap.appendChild(hidden);
    wrap.appendChild(list);
    return wrap;
}

/* ================================================================
   API سراسری برای رفرش همهٔ Comboboxها پس از افزودن رکورد جدید
   ================================================================ */
window.Combobox = {
    refreshAll(root = document) {
        root.querySelectorAll(".combo-wrap").forEach(w => w.__refresh && w.__refresh());
    }
};
// ================================================================
// 9. Initialize
// ================================================================

//window.CustomModal = {
//    open({ title, message, itemName, action, id }) {
//        // پر کردن مقادیر
//        document.getElementById('deleteItemTitle').textContent = itemName;
//        document.getElementById('deleteItemId').value = id;
//        const form = document.getElementById('deleteForm');
//        form.action = action;

//        // باز کردن
//        const modal = document.getElementById('deleteModal');
//        modal.classList.add('is-open');
//        document.body.classList.add('modal-open');
//    },
//    close() {
//        document.getElementById('deleteModal').classList.remove('is-open');
//        document.body.classList.remove('modal-open');
//    }
//};


function init() {
    initDefaultTemplates();
    renderTemplates();
    updateSelector();
    updateEntryTemplateSelector();
    clearBuilder();
}
init();