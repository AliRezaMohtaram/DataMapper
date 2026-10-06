// ================================================================
// layout-designer.js — طراح Layout فرم (نسخه MVC)
// برگرفته از layout.js نمونه: مدل داده و رندر زمان‌اجرا بدون تغییر؛
// ذخیره‌سازی localStorage با فراخوانی سرور جایگزین شده است.
// داده‌های صفحه از <script id="ldData" type="application/json"> خوانده می‌شود.
// وابستگی: ui.js ($، escapeHtml، showToast، showConfirm، showAlertModal)
// ================================================================
/*
 مدل داده (JSON) — در ستون LayoutJson جدول TemplateLayout ذخیره می‌شود.

 layout = {
   version : 1,
   settings: { title, description, submitText, labelPosition: 'top' | 'side' },
   blocks  : [ Block, ... ]                 // ترتیب آرایه = ترتیب نمایش
 }

 Block (مشترک): { id, type, span: 2..12 (شبکهٔ ۱۲ ستونی), showIf: null | { key, op: 'eq'|'neq'|'filled'|'empty', value } }
   type 'field'     + fieldKey, widget: 'auto'|'textarea'|'select'|'radio', label, placeholder, help, required, options[]
   type 'heading'   + text, level (1..3)
   type 'paragraph' + text
   type 'divider'
   type 'spacer'    + height
   type 'pagebreak' + title                 // شروع مرحلهٔ جدید (فرم چندمرحله‌ای)

 قواعد:
  - هر فیلد نسخه حداکثر یک‌بار در Layout قرار می‌گیرد (کلید داده یکتاست).
  - فیلدهای اجباریِ چیدمان‌نشده هنگام نمایش به‌صورت خودکار به انتهای فرم اضافه می‌شوند.
  - اعتبارسنجی نهایی ساختار در سرور (LayoutJsonValidator) هم انجام می‌شود.
*/

// ---- شیم‌های لازم برای رندر پیش‌نمایش ----
const dbTypeOf = f => String(f.dbType || "").toUpperCase();

function setFieldErrorState(control, message) {
    if (!control) return;
    const wrap = control.closest("[data-field-wrap]") || control.closest(".lf-field");
    const errEl = wrap ? wrap.querySelector(".field-error") : null;
    if (errEl) errEl.textContent = message || "";
    control.classList.toggle("has-error", !!message);
}

// در پیش‌نمایش فقط الزامی بودن و عددی بودن بررسی می‌شود.
function validateFieldValue(field, value) {
    const isEmpty = value === undefined || value === null || String(value).trim() === "";
    if (field.required && isEmpty) return "این فیلد اجباری است";
    if (isEmpty) return null;
    if (field.type === "number" && isNaN(Number(String(value).replace(/,/g, "")))) {
        return `مقدار «${value}» برای «${field.label}» باید یک عدد باشد.`;
    }
    return null;
}

(function () {
    const L = window.LayoutEngine = {};

    // در صفحهٔ ورود داده (بدون طراح) #ldData وجود ندارد؛ فقط موتور رندر استفاده می‌شود.
    const ldEl = document.getElementById("ldData");
    const PAGE = ldEl ? JSON.parse(ldEl.textContent) : {};
    const READ_ONLY = !!PAGE.readOnly;
    // نسخهٔ منتشرشده: فقط ظاهر قابل تغییر است؛ حذف فیلد و تغییر الزامی/نوع ورودی/گزینه‌ها/شرط نمایش فیلدها قفل است.
    const RESTRICTED = !!PAGE.restricted;
    const LAYOUT_VERSION = 1;

    const uid = () => "b" + Math.random().toString(36).slice(2, 9);
    const clone = o => JSON.parse(JSON.stringify(o));
    const esc = s => escapeHtml(s);
    const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
    const fieldOf = (tpl, key) => tpl.fields.find(f => f.key === key);

    const TYPE_LABEL = { text: "متن", number: "عدد", date: "تاریخ", datetime: "تاریخ و ساعت", any: "آزاد" };
    const TAGS = { field: "فیلد", heading: "عنوان", paragraph: "متن", divider: "خط", spacer: "فاصله", pagebreak: "مرحله" };
    const STATIC_DEFAULTS = {
        heading: { text: "عنوان بخش", level: 2 },
        paragraph: { text: "متن توضیحی را اینجا بنویسید." },
        divider: {}, spacer: { height: 24 }, pagebreak: { title: "مرحلهٔ بعد" }
    };
    const OPS = { eq: "برابر باشد با", neq: "برابر نباشد با", filled: "پر باشد", empty: "خالی باشد" };

    const emptyLayout = () => ({ version: LAYOUT_VERSION, settings: { title: "", description: "", submitText: "ذخیره رکورد", labelPosition: "top" }, blocks: [] });
    const newFieldBlock = key => ({ id: uid(), type: "field", fieldKey: key, span: 6, widget: "auto", label: "", placeholder: "", help: "", required: false, options: [], showIf: null });
    const newStatic = type => ({ id: uid(), type, span: 12, showIf: null, ...clone(STATIC_DEFAULTS[type]) });

    // ============================================================
    // ۱) سازندهٔ مشترک بلوک‌ها (هم زمان‌اجرا، هم بوم طراحی)
    // ============================================================
    function widgetOf(field, block) {
        if (dbTypeOf(field) === "CLOB" && (!block.widget || block.widget === "auto")) return "textarea"; // CLOB به‌طور پیش‌فرض چندخطی است
        if (["text", "any"].includes(field.type) && ["textarea", "select", "radio"].includes(block.widget)) return block.widget;
        return { number: "number", date: "date", datetime: "datetime" }[field.type] || "input";
    }

    function buildControl(field, block) {
        /* ۰) فیلد متصل به منبع دادهٔ «لیست ثابت» → لیست کشویی (field.dataOptions = [{value, label}]) */
        if (Array.isArray(field.dataOptions) && block.widget !== "radio") {
            const sel = el("select", "lf-control", `<option value="">— انتخاب کنید —</option>` +
                field.dataOptions.map(o => `<option value="${esc(o.value)}">${esc(o.label || o.value)}</option>`).join(""));
            sel.dataset.key = field.key;
            if (block.required) sel.dataset.req = "1";
            return sel;
        }

        /* ۱) فیلد دارای منبع دادهٔ داینامیک → Combobox جستجوپذیر
              (اولویت بالاتر از widget — چون ممکن است widget پیش‌فرض input باشد) */
        if (field.dataSource && field.dataSource.id && typeof buildCombobox === "function") {
            return buildCombobox(field, {
                required: block.required,
                placeholder: block.placeholder
            });
        }

        /* ۲) تاریخ/تاریخ‌وساعت → تقویم جلالی (ذخیره‌سازی ISO میلادی) */
        if ((field.type === "date" || field.type === "datetime") && typeof buildJalaliPicker === "function") {
            return buildJalaliPicker(field, {
                required: block.required,
                placeholder: block.placeholder,
                withTime: field.type === "datetime"
            });
        }

        /* ۳) بقیهٔ حالت‌ها — بدون تغییر */
        const w = widgetOf(field, block);
        let c;
        if (w === "textarea") { c = el("textarea"); c.rows = 4; }
        else if (w === "select") {
            c = el("select", null, `<option value="">— انتخاب کنید —</option>` +
                (block.options || []).map(o => `<option value="${esc(o)}">${esc(o)}</option>`).join(""));
        } else if (w === "radio") {
            c = el("div", "lf-radio");
            const name = "r" + uid();
            (block.options || []).forEach(o => c.appendChild(
                el("label", "lf-radio-item",
                    `<input type="radio" name="${name}" value="${esc(o)}"><span>${esc(o)}</span>`)));
            Object.defineProperty(c, "value", {
                get() { const r = c.querySelector("input:checked"); return r ? r.value : ""; },
                set(v) { c.querySelectorAll("input").forEach(r => { r.checked = r.value === String(v); }); }
            });
        } else {
            c = el("input");
            c.type = { number: "number", date: "date", datetime: "datetime-local" }[w] || "text";
            if (w === "number") c.step = dbTypeOf(field) === "INTEGER" ? "1" : "any";
        }
        if (w !== "radio" && block.placeholder) c.placeholder = block.placeholder;
        c.dataset.key = field.key;
        if (block.required) c.dataset.req = "1";
        c.classList.add("lf-control");
        return c;
    }

    function buildFieldBlock(tpl, block) {
        const field = fieldOf(tpl, block.fieldKey);
        const wrap = el("div", "lf-block lf-field");
        wrap.dataset.blockId = block.id;
        wrap.setAttribute("data-field-wrap", "");
        wrap.style.setProperty("--span", block.span || 12);
        if (!field) { wrap.innerHTML = `<div class="lf-missing">فیلد «${esc(block.fieldKey)}» دیگر در قالب وجود ندارد</div>`; return wrap; }
        const req = field.required || block.required;
        const main = el("div", "lf-main");
        main.appendChild(buildControl(field, block));
        if (block.help) main.appendChild(el("div", "lf-help", esc(block.help)));
        main.appendChild(el("span", "field-error"));
        wrap.appendChild(el("label", "lf-label", `${esc(block.label || field.label)}${req ? ' <em class="req-mark">*</em>' : ""}`));
        wrap.appendChild(main);
        return wrap;
    }

    function buildStaticBlock(b) {
        const w = el("div", "lf-block lf-" + b.type);
        w.dataset.blockId = b.id;
        w.style.setProperty("--span", b.span || 12);
        if (b.type === "heading") w.innerHTML = `<div class="lf-h lf-h${b.level || 2}">${esc(b.text)}</div>`;
        else if (b.type === "paragraph") w.innerHTML = `<p class="lf-p">${esc(b.text)}</p>`;
        else if (b.type === "divider") w.innerHTML = "<hr>";
        else if (b.type === "spacer") w.style.height = (b.height || 24) + "px";
        return w;
    }

    // فیلدهای اجباریِ چیدمان‌نشده به انتها اضافه می‌شوند؛ بلوک‌های مربوط به فیلدهای حذف‌شده نادیده گرفته می‌شوند
    function effectiveBlocks(tpl) {
        const blocks = tpl.layout.blocks.filter(b => b.type !== "field" || fieldOf(tpl, b.fieldKey));
        const placed = new Set(blocks.filter(b => b.type === "field").map(b => b.fieldKey));
        const missing = tpl.fields.filter(f => f.required && !placed.has(f.key));
        if (missing.length) {
            blocks.push({ ...newStatic("heading"), text: "فیلدهای اجباری چیدمان‌نشده", level: 3 });
            missing.forEach(f => blocks.push(newFieldBlock(f.key)));
        }
        return blocks;
    }

    // ============================================================
    // ۲) رندر زمان‌اجرا (فرم واقعی ورود داده / پیش‌نمایش طراح)
    // ============================================================
    function validateStepEl(stepEl, tpl) {
        let ok = true, first = null;
        stepEl.querySelectorAll("[data-key]").forEach(inp => {
            const f = fieldOf(tpl, inp.dataset.key);
            const wrap = inp.closest("[data-field-wrap]");
            const errEl = wrap && wrap.querySelector(".field-error");
            if (!f || !wrap || wrap.classList.contains("is-hidden")) { if (errEl) errEl.textContent = ""; return; }
            const msg = validateFieldValue(inp.dataset.req === "1" ? { ...f, required: true } : f, inp.value);
            if (typeof setFieldErrorState === "function") setFieldErrorState(inp, msg);
            else if (errEl) errEl.textContent = msg || "";
            if (msg) { ok = false; first = first || inp; }
        });
        return { ok, first };
    }

    L.renderRuntime = function (tpl, container) {
        const st = tpl.layout.settings || {};
        const form = el("div", "lf-form" + (st.labelPosition === "side" ? " label-side" : ""));
        if (st.title) form.appendChild(el("div", "lf-title", esc(st.title)));
        if (st.description) form.appendChild(el("div", "lf-desc", esc(st.description)));

        // تقسیم بلوک‌ها به مرحله‌ها با pagebreak
        const steps = [{ title: "مرحلهٔ ۱", grid: el("div", "lf-grid") }];
        effectiveBlocks(tpl).forEach(b => {
            if (b.type === "pagebreak") { steps.push({ title: b.title || `مرحلهٔ ${steps.length + 1}`, grid: el("div", "lf-grid") }); return; }
            const node = b.type === "field" ? buildFieldBlock(tpl, b) : buildStaticBlock(b);
            if (b.showIf) node.dataset.showIf = JSON.stringify(b.showIf);
            steps[steps.length - 1].grid.appendChild(node);
        });
        const live = steps.filter(s => s.grid.children.length);
        if (!live.length) live.push(steps[0]);
        const multi = live.length > 1;

        let dots = [], nameEl = null, prevBtn = null, nextBtn = null, cur = 0;
        if (multi) {
            const prog = el("div", "lf-progress");
            const dotWrap = el("div", "lf-dots");
            dots = live.map(() => dotWrap.appendChild(el("span", "lf-dot")));
            nameEl = el("div", "lf-stepname");
            prog.append(dotWrap, nameEl);
            form.appendChild(prog);
        }
        const stepEls = live.map(s => { const d = el("section", "lf-step"); d.appendChild(s.grid); form.appendChild(d); return d; });

        function show(i) {
            cur = Math.max(0, Math.min(stepEls.length - 1, i));
            stepEls.forEach((s, j) => { s.hidden = j !== cur; });
            if (!multi) return;
            dots.forEach((d, j) => { d.classList.toggle("active", j === cur); d.classList.toggle("done", j < cur); });
            nameEl.textContent = `مرحلهٔ ${cur + 1} از ${stepEls.length} — ${live[cur].title}`;
            prevBtn.disabled = cur === 0;
            nextBtn.hidden = cur === stepEls.length - 1;
        }
        if (multi) {
            const nav = el("div", "lf-nav");
            prevBtn = el("button", "btn ghost", "قبلی"); prevBtn.type = "button";
            nextBtn = el("button", "btn primary", "بعدی"); nextBtn.type = "button";
            prevBtn.onclick = () => show(cur - 1);
            nextBtn.onclick = () => {
                const r = validateStepEl(stepEls[cur], tpl);
                if (!r.ok) { showToast("فیلدهای این مرحله را تکمیل یا اصلاح کنید.", "error"); L.reveal(r.first); return; }
                show(cur + 1);
            };
            nav.append(prevBtn, nextBtn);
            form.appendChild(nav);
        }

        // منطق شرطی (showIf): منبعِ مخفی مثل مقدار خالی حساب می‌شود؛ دو گذر برای وابستگی‌های زنجیره‌ای
        function applyLogic() {
            for (let pass = 0; pass < 2; pass++) {
                form.querySelectorAll("[data-show-if]").forEach(w => {
                    const rule = JSON.parse(w.dataset.showIf);
                    const src = [...form.querySelectorAll("[data-key]")].find(c => c.dataset.key === rule.key);
                    const v = src && !src.closest(".is-hidden") ? String(src.value ?? "").trim() : "";
                    const show = rule.op === "eq" ? v === String(rule.value)
                        : rule.op === "neq" ? v !== String(rule.value)
                            : rule.op === "filled" ? v !== "" : v === "";
                    w.classList.toggle("is-hidden", !show);
                });
            }
        }
        form.addEventListener("input", applyLogic);
        form.addEventListener("change", applyLogic);
        form.addEventListener("input", e => {
            const ctl = e.target.closest && e.target.closest("[data-key]");
            if (ctl && typeof setFieldErrorState === "function") setFieldErrorState(ctl, "");
        });
        form.addEventListener("change", e => {
            const ctl = e.target.closest && e.target.closest("[data-key]");
            if (ctl && typeof setFieldErrorState === "function") setFieldErrorState(ctl, "");
        });
        form._refresh = applyLogic;
        form._reset = () => { applyLogic(); show(0); };
        form._show = show;
        form._steps = stepEls;

        container.appendChild(form);
        applyLogic();
        show(0);
    };

    // ---- ابزارهای صفحهٔ ورود داده (record-form.js) ----
    // چیدمان خودکار برای نسخه‌ای که Layout ندارد (همان منطق «چیدمان خودکار» طراح)
    L.autoLayout = fields => {
        const l = emptyLayout();
        l.blocks = autoBlocks({ fields });
        return l;
    };

    // اعتبارسنجی همهٔ مرحله‌ها؛ در صورت خطا به اولین کنترل نامعتبر می‌رود
    L.validate = (container, tpl) => {
        const form = container.querySelector(".lf-form");
        if (!form) return true;
        for (let i = 0; i < form._steps.length; i++) {
            const r = validateStepEl(form._steps[i], tpl);
            if (!r.ok) { L.reveal(r.first); return false; }
        }
        return true;
    };

    // مقدار کنترل‌های قابل‌مشاهده → { key: value } (فیلدهای پنهان‌شده با شرط نمایش ارسال نمی‌شوند)
    L.collect = container => {
        const out = {};
        container.querySelectorAll("[data-key]").forEach(inp => {
            const wrap = inp.closest("[data-field-wrap]");
            if (wrap && wrap.classList.contains("is-hidden")) return;
            out[inp.dataset.key] = inp.value == null ? "" : String(inp.value);
        });
        return out;
    };

    L.setValues = (container, values) => {
        container.querySelectorAll("[data-key]").forEach(inp => {
            const v = values[inp.dataset.key];
            if (v === undefined || v === null) return;
            inp.value = v;
        });
        L.refresh(container);
    };

    L.setErrors = (container, errors) => {
        let first = null;
        container.querySelectorAll("[data-key]").forEach(inp => {
            const msg = errors[inp.dataset.key];
            if (msg) { setFieldErrorState(inp, msg); first = first || inp; }
        });
        if (first) L.reveal(first);
        return !!first;
    };

    L.reset = c => { const f = c.querySelector(".lf-form"); if (f) f._reset(); };
    L.refresh = c => { const f = c.querySelector(".lf-form"); if (f) f._refresh(); };
    // رفتن به مرحلهٔ حاوی کنترل نامعتبر و فوکوس روی آن (برای فرم داینامیک هم فقط اسکرول/فوکوس انجام می‌شود)
    L.reveal = ctl => {
        if (!ctl) return;
        const form = ctl.closest(".lf-form");
        if (form) { const i = form._steps.findIndex(s => s.contains(ctl)); if (i >= 0) form._show(i); }
        setTimeout(() => { ctl.scrollIntoView({ block: "center", behavior: "smooth" }); if (ctl.focus) ctl.focus({ preventScroll: true }); }, 30);
    };

    // ============================================================
    // ۳) طراح Layout (تب ۴) — سه‌پنل: پالت المان‌ها | بوم | ویژگی‌ها
    // ============================================================
    let dTpl = null, dLayout = null, dSel = null, dDirty = false, dPreview = false, dDevice = "desktop", drag = null;

    const blockById = id => dLayout && dLayout.blocks.find(b => b.id === id);
    const placedKeys = () => new Set(dLayout.blocks.filter(b => b.type === "field").map(b => b.fieldKey));
    const requiredMissing = () => { const p = placedKeys(); return dTpl.fields.filter(f => f.required && !p.has(f.key)); };
    const markDirty = () => { if (dDirty) return; dDirty = true; renderTopbar(); };

    // حذف بلوک‌های یتیم/تکراری و شرط‌های وابسته به فیلد ناموجود؛ تعداد حذف‌شده‌ها را برمی‌گرداند
    function sanitize() {
        if (!dLayout) return 0;
        dLayout.settings = { ...emptyLayout().settings, ...(dLayout.settings || {}) };
        const keys = new Set(dTpl.fields.map(f => f.key)), seen = new Set();
        let removed = 0;
        dLayout.blocks = dLayout.blocks.filter(b => {
            if (b.type !== "field") return true;
            if (!keys.has(b.fieldKey) || seen.has(b.fieldKey)) { removed++; return false; }
            seen.add(b.fieldKey); return true;
        });
        dLayout.blocks.forEach(b => { if (b.showIf && !seen.has(b.showIf.key)) b.showIf = null; });
        if (removed) showToast(`${removed} بلوک مربوط به فیلدهای حذف‌شدهٔ قالب از Layout برداشته شد.`, "info");
        return removed;
    }

    function hasBlocks(l) { return !!(l && Array.isArray(l.blocks) && l.blocks.length); }

    function loadFromPage() {
        dTpl = {
            id: PAGE.versionId,
            name: PAGE.templateName,
            fields: PAGE.fields || [],
            layout: hasBlocks(PAGE.layout) ? clone(PAGE.layout) : undefined
        };
        dSel = null;
        dPreview = READ_ONLY;
        dLayout = PAGE.layout ? clone(PAGE.layout) : emptyLayout();
        dDirty = false;
        if (!READ_ONLY && sanitize()) dDirty = true;
        renderAll();
    }

    // ارسال JSON به سرور (با توکن Antiforgery)
    async function post(url, body) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        try {
            const res = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token ? token.value : ""
                },
                body: JSON.stringify(body)
            });
            const data = await res.json().catch(() => null);
            if (!res.ok || !data) return { success: false, message: "پاسخ نامعتبر از سرور (" + res.status + ")." };
            return data;
        } catch (_) {
            return { success: false, message: "ارتباط با سرور برقرار نشد." };
        }
    }

    function autoBlocks(tpl) {
        const out = [], many = tpl.fields.length > 10; // فرم‌های بلند به مرحله‌های ۸ فیلدی شکسته می‌شوند
        tpl.fields.forEach((f, i) => {
            if (many && i > 0 && i % 8 === 0) out.push({ ...newStatic("pagebreak"), title: `مرحلهٔ ${i / 8 + 1}` });
            const b = newFieldBlock(f.key);
            b.span = ["number", "date", "datetime"].includes(f.type) ? 4 : 6;
            out.push(b);
        });
        return out;
    }

    // ---------- رندر کلی ----------
    function renderAll() { renderTopbar(); renderPalette(); renderStage(); renderProps(); }

    function renderTopbar() {
        const has = !!dTpl, a = $byId("ldActions"), badge = $byId("ldStatus");
        badge.textContent = !has ? "" : dDirty ? "ذخیره‌نشده" : dTpl.layout ? "Layout ذخیره‌شده" : "بدون Layout — فرم داینامیک";
        badge.className = "mode-badge" + (has && dDirty ? " is-dirty" : has && dTpl.layout ? " is-layout" : "");
        const seg = (items, cur) => `<div class="ld-seg">${items.map(([k, l]) => `<button type="button" data-a="${k}" class="${k === cur ? "on" : ""}">${l}</button>`).join("")}</div>`;
        const seg2 = (items, cur) => `<div class="ld-seg">${items.map(([k, l]) => `<button type="button" data-a="${k}" class="${k === cur ? "on" : ""}">${l}</button>`).join("")}</div>`;
        a.innerHTML = !has ? "" : READ_ONLY
            ? seg2([["desktop", "دسکتاپ"], ["mobile", "موبایل"]], dDevice)
            : seg2([["edit", "ویرایش"], ["preview", "پیش‌نمایش"]], dPreview ? "preview" : "edit") +
              (dPreview ? seg2([["desktop", "دسکتاپ"], ["mobile", "موبایل"]], dDevice) : `<button type="button" class="btn ghost" data-a="auto">چیدمان خودکار</button>`) +
              `<button type="button" class="btn ghost" data-a="json">کپی JSON</button>` +
              (dTpl.layout ? `<button type="button" class="btn danger" data-a="remove">حذف Layout</button>` : "") +
              `<button type="button" class="btn primary" data-a="save">ذخیره Layout</button>`;
        a.querySelectorAll("[data-a]").forEach(b => { b.onclick = () => action(b.dataset.a); });
    }

    async function action(a) {
        if (a === "edit" || a === "preview") { dPreview = a === "preview"; if (dPreview) dSel = null; renderAll(); }
        else if (a === "desktop" || a === "mobile") { dDevice = a; renderAll(); }
        else if (a === "auto") { await applyAuto(); }
        else if (a === "json") {
            try { await navigator.clipboard.writeText(JSON.stringify(dLayout, null, 2)); showToast("JSON مربوط به Layout کپی شد.", "success"); }
            catch (_) { showToast("کپی به کلیپ‌بورد ممکن نشد.", "error"); }
        }
        else if (a === "remove") { await removeLayout(); }
        else if (a === "save") { await saveLayout(); }
    }

    async function applyAuto() {
        if (dLayout.blocks.length && !(await showConfirm("چیدمان خودکار", "چیدمان فعلی با یک چیدمان خودکار از فیلدهای قالب جایگزین می‌شود. ادامه می‌دهید؟", true))) return;
        dLayout.blocks = autoBlocks(dTpl);
        if (!dLayout.settings.title) dLayout.settings.title = dTpl.name;
        dSel = null; dDirty = true; renderAll();
        showToast("چیدمان خودکار ساخته شد؛ حالا می‌توانید آن را دلخواه بچینید.", "success");
    }

    async function removeLayout() {
        if (!(await showConfirm("حذف Layout", `Layout نسخهٔ «${dTpl.name}» حذف می‌شود و ورود داده به فرم داینامیک برمی‌گردد.`, true))) return;
        const r = await post(PAGE.urls.remove, { versionId: PAGE.versionId });
        if (!r.success) { showToast(r.message || "حذف Layout انجام نشد.", "error"); return; }
        dTpl.layout = undefined;
        dLayout = emptyLayout(); dSel = null; dDirty = false;
        renderAll();
        showToast(r.message || "Layout حذف شد.", "success");
    }

    async function saveLayout() {
        if (RESTRICTED) {
            const placed = placedKeys();
            const lost = dTpl.fields.filter(f => !placed.has(f.key)).map(f => f.label);
            if (lost.length) {
                await showAlertModal("Layout ذخیره نشد", "در نسخهٔ منتشرشده همه فیلدها باید در Layout باشند. این فیلدها چیده نشده‌اند:\n" + lost.map(l => "• " + l).join("\n") + "\n\nاز «چیدمان خودکار» یا پالت استفاده کنید.", true);
                return;
            }
        }
        if (!dLayout.blocks.some(b => b.type === "field")) { showToast("حداقل یک فیلد باید در Layout قرار بگیرد.", "error"); return; }
        const problems = dLayout.blocks
            .filter(b => b.type === "field" && ["select", "radio"].includes(b.widget) && !(b.options || []).length)
            .map(b => `برای «${b.label || fieldOf(dTpl, b.fieldKey).label}» گزینه‌ای تعریف نشده است.`);
        if (problems.length) { await showAlertModal("Layout ذخیره نشد", problems.map(p => "• " + p).join("\n"), true); return; }
        const missing = requiredMissing();
        if (missing.length) {
            const ok = await showConfirm("فیلد اجباریِ چیدمان‌نشده",
                `این فیلدهای اجباری در Layout نیستند:\n${missing.map(f => "• " + f.label).join("\n")}\n\nدر فرم نهایی به‌صورت خودکار به انتهای فرم اضافه می‌شوند. ذخیره شود؟`);
            if (!ok) return;
        }
        const payload = { ...clone(dLayout), version: LAYOUT_VERSION };
        const r = await post(PAGE.urls.save, { versionId: PAGE.versionId, layoutJson: JSON.stringify(payload) });
        if (!r.success) { showToast(r.message || "ذخیره Layout انجام نشد.", "error"); return; }
        dTpl.layout = payload;
        dDirty = false;
        renderAll();
        showToast(r.message || "Layout ذخیره شد.", "success");
    }

    // ---------- پالت (پنل راست) ----------
    let palTab = "fields", palQuery = "";
    const ICON = { text: "Aa", number: "#", date: "D", datetime: "DT", any: "*" };

    function renderPalette() {
        const p = $byId("ldPalette");
        if (!dTpl) { p.innerHTML = `<div class="ld-note">ابتدا یک قالب انتخاب کنید.</div>`; return; }
        p.innerHTML = `<div class="ld-tabs">
                <button type="button" data-t="fields" class="${palTab === "fields" ? "on" : ""}">فیلدها<span>${placedKeys().size}/${dTpl.fields.length}</span></button>
                <button type="button" data-t="els" class="${palTab === "els" ? "on" : ""}">المان‌ها</button></div>
            ${palTab === "fields" ? `<input type="text" class="ld-search" placeholder="جستجوی فیلد..." value="${esc(palQuery)}">` : ""}
            <div id="ldPalList"></div>`;
        p.querySelectorAll(".ld-tabs button").forEach(t => t.onclick = () => { palTab = t.dataset.t; renderPalette(); });
        const q = p.querySelector(".ld-search");
        if (q) q.addEventListener("input", () => { palQuery = q.value; fillPalList(); });
        fillPalList();
    }

    function fillPalList() {
        const list = $byId("ldPalList"); if (!list) return;
        const placed = placedKeys();
        let h = "";
        if (palTab === "fields") {
            const q = palQuery.trim().toLowerCase();
            const rows = dTpl.fields.filter(f => !q || `${f.label} ${f.key}`.toLowerCase().includes(q));
            h = rows.map(f => {
                const on = placed.has(f.key);
                return `<div class="ld-item ${on ? "placed" : ""}" ${on ? "" : 'draggable="true"'} data-kind="field" data-key="${esc(f.key)}">
                    <span class="ld-ico">${ICON[f.type] || "Aa"}</span>
                    <div><b>${esc(f.label)}${f.required ? ' <em class="req-mark">*</em>' : ""}</b><small>${esc(f.key)}</small></div>
                    <span class="ld-check">${on ? "✓" : "+"}</span></div>`;
            }).join("") || `<div class="ld-note">موردی یافت نشد.</div>`;
            const left = dTpl.fields.length - placed.size;
            if (left > 0 && !q) h += `<button type="button" class="btn ghost ld-addall">افزودن همهٔ ${left} فیلد باقی‌مانده</button>`;
        } else {
            h = [["heading", "عنوان بخش", "H"], ["paragraph", "متن توضیحی", "¶"], ["divider", "خط جداکننده", "—"], ["spacer", "فاصله", "↕"], ["pagebreak", "مرحلهٔ جدید (صفحه‌بندی)", "⎘"]]
                .map(([t, l, i]) => `<div class="ld-item" draggable="true" data-kind="static" data-type="${t}"><span class="ld-ico">${i}</span><div><b>${l}</b></div><span class="ld-check">+</span></div>`).join("");
        }
        list.innerHTML = h;
        const all = list.querySelector(".ld-addall");
        if (all) all.onclick = () => {
            const keys = placedKeys();
            dTpl.fields.filter(f => !keys.has(f.key)).forEach(f => {
                const nb = newFieldBlock(f.key); nb.span = ["number", "date", "datetime"].includes(f.type) ? 4 : 6; dLayout.blocks.push(nb);
            });
            dDirty = true; renderAll();
        };
        list.querySelectorAll(".ld-item").forEach(it => {
            const payload = () => it.dataset.kind === "field" ? { kind: "new", type: "field", key: it.dataset.key } : { kind: "new", type: it.dataset.type };
            it.addEventListener("dragstart", e => { drag = payload(); e.dataTransfer.setData("text/plain", "new"); e.dataTransfer.effectAllowed = "copy"; });
            it.addEventListener("dragend", () => { drag = null; clearDropMarks(); });
            it.addEventListener("click", () => {
                if (it.classList.contains("placed")) {
                    const bl = dLayout.blocks.find(x => x.type === "field" && x.fieldKey === it.dataset.key);
                    if (bl) { select(bl.id); const n = document.querySelector(`#ldStage [data-block-id="${bl.id}"]`); if (n) n.scrollIntoView({ block: "center", behavior: "smooth" }); }
                } else { drag = payload(); doDrop(dSel && blockById(dSel) ? dSel : null, true); }
            });
        });
    }

    function moveBlock(id, d) {
        const i = dLayout.blocks.findIndex(x => x.id === id), j = i + d;
        if (i < 0 || j < 0 || j >= dLayout.blocks.length) return;
        const [bl] = dLayout.blocks.splice(i, 1); dLayout.blocks.splice(j, 0, bl);
        dDirty = true; renderAll();
        const n = document.querySelector(`#ldStage [data-block-id="${id}"]`); if (n) n.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }

    // ---------- بوم ----------
    function clearDropMarks() {
        document.querySelectorAll("#ldStage .drop-before, #ldStage .drop-after, #ldStage .ld-end.over").forEach(n => n.classList.remove("drop-before", "drop-after", "over"));
    }
    function isAfter(node, block, e) {
        const r = node.getBoundingClientRect();
        if ((block.span || 12) >= 12) return e.clientY > r.top + r.height / 2;
        const f = (e.clientX - r.left) / r.width;
        return getComputedStyle(node).direction === "rtl" ? f < 0.5 : f > 0.5;
    }

    function doDrop(targetId, after) {
        const p = drag; drag = null; clearDropMarks();
        if (!p) return;
        let block;
        if (p.kind === "new") {
            if (p.type === "field" && placedKeys().has(p.key)) return;
            block = p.type === "field" ? newFieldBlock(p.key) : newStatic(p.type);
        } else {
            const i = dLayout.blocks.findIndex(b => b.id === p.id);
            if (i < 0 || p.id === targetId) return;
            block = dLayout.blocks.splice(i, 1)[0];
        }
        let idx = targetId ? dLayout.blocks.findIndex(b => b.id === targetId) : dLayout.blocks.length;
        if (idx < 0) idx = dLayout.blocks.length; else if (after) idx++;
        dLayout.blocks.splice(idx, 0, block);
        dSel = block.id; dDirty = true;
        renderAll();
    }

    function removeBlock(id) {
        const b = blockById(id); if (!b) return;
        if (RESTRICTED && b.type === "field") {
            showToast("در نسخهٔ منتشرشده حذف فیلد از فرم مجاز نیست؛ برای این کار نسخهٔ جدید بسازید.", "error");
            return;
        }
        dLayout.blocks = dLayout.blocks.filter(x => x.id !== id);
        if (b.type === "field") dLayout.blocks.forEach(x => { if (x.showIf && x.showIf.key === b.fieldKey) x.showIf = null; });
        if (dSel === id) dSel = null;
        dDirty = true; renderAll();
    }

    function designBlock(block) {
        let node;
        if (block.type === "field") node = buildFieldBlock(dTpl, block);
        else if (block.type === "pagebreak") {
            node = el("div", "lf-block lf-pagebreak", `<span>— شروع ${esc(block.title || "مرحلهٔ جدید")} —</span>`);
            node.dataset.blockId = block.id; node.style.setProperty("--span", 12);
        } else node = buildStaticBlock(block);
        node.classList.add("ld-block");
        node.draggable = true;
        if ((block.span || 12) >= 12) node.classList.add("wide");
        if (block.showIf) node.classList.add("has-logic");
        const ctl = node.querySelector(".lf-control");
        if (ctl && block.type === "field" && ["INPUT", "TEXTAREA"].includes(ctl.tagName) && !ctl.placeholder) { const fd = fieldOf(dTpl, block.fieldKey); if (fd) ctl.placeholder = TYPE_LABEL[fd.type] || ""; }
        if (block.id === dSel) node.classList.add("selected");
        node.insertAdjacentHTML("beforeend",
            `<div class="ld-chrome"><span class="ld-grip" title="بکشید تا جابه‌جا شود">⋮⋮</span><span class="ld-tag">${TAGS[block.type]}</span>
             ${block.showIf ? '<span class="ld-logic-tag">شرطی</span>' : ""}
             <button type="button" data-act="up" title="بالا (Alt+↑)">↑</button><button type="button" data-act="down" title="پایین (Alt+↓)">↓</button>
             ${block.type === "field" ? "" : '<button type="button" data-act="dup" title="تکثیر">⧉</button>'}<button type="button" data-act="del" title="حذف">✕</button></div>
             <div class="ld-resize" title="تغییر عرض"></div>`);

        node.addEventListener("click", e => {
            e.stopPropagation();
            const act = e.target.closest("[data-act]");
            if (act && act.dataset.act === "del") return removeBlock(block.id);
            if (act && act.dataset.act === "up") return moveBlock(block.id, -1);
            if (act && act.dataset.act === "down") return moveBlock(block.id, 1);
            if (act && act.dataset.act === "dup") {
                const copy = { ...clone(block), id: uid() };
                dLayout.blocks.splice(dLayout.blocks.indexOf(block) + 1, 0, copy);
                dSel = copy.id; dDirty = true; return renderAll();
            }
            select(block.id);
        });
        node.addEventListener("dragstart", e => {
            drag = { kind: "move", id: block.id };
            e.dataTransfer.setData("text/plain", "move"); e.dataTransfer.effectAllowed = "move";
            requestAnimationFrame(() => node.classList.add("dragging"));
        });
        node.addEventListener("dragend", () => { drag = null; clearDropMarks(); node.classList.remove("dragging"); });
        node.addEventListener("dragover", e => {
            if (!drag) return;
            e.preventDefault(); clearDropMarks();
            node.classList.add(isAfter(node, block, e) ? "drop-after" : "drop-before");
        });
        node.addEventListener("drop", e => { e.preventDefault(); e.stopPropagation(); doDrop(block.id, isAfter(node, block, e)); });

        // تغییر عرض با دستگیرهٔ لبه (گام‌های ستون از شبکهٔ ۱۲تایی)
        node.querySelector(".ld-resize").addEventListener("pointerdown", e => {
            e.preventDefault(); e.stopPropagation();
            select(block.id);
            const colW = node.parentElement.getBoundingClientRect().width / 12;
            const nr = node.getBoundingClientRect();
            const rtl = getComputedStyle(node).direction === "rtl";
            node.draggable = false;
            const grid = node.parentElement, tag = node.querySelector(".ld-tag"), tagText = tag.textContent;
            const guides = el("div", "ld-guides", "<i></i>".repeat(12)); grid.prepend(guides);
            const move = ev => {
                const w = rtl ? nr.right - ev.clientX : ev.clientX - nr.left;
                const span = Math.max(2, Math.min(12, Math.round(w / colW)));
                if (span !== block.span) { block.span = span; node.style.setProperty("--span", span); node.classList.toggle("wide", span >= 12); tag.textContent = `${span}/12`; }
            };
            const up = () => {
                window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up);
                guides.remove(); tag.textContent = tagText;
                node.draggable = true; markDirty(); renderProps();
            };
            window.addEventListener("pointermove", move); window.addEventListener("pointerup", up);
        });
        return node;
    }

    function refreshBlock(id) {
        const b = blockById(id), old = document.querySelector(`#ldStage [data-block-id="${id}"]`);
        if (!b || !old) return renderStage();
        old.replaceWith(designBlock(b));
    }

    function select(id) {
        dSel = id;
        document.querySelectorAll("#ldStage .ld-block").forEach(n => n.classList.toggle("selected", n.dataset.blockId === id));
        renderProps();
    }

    function headHtml() {
        const st = dLayout.settings;
        return `<div class="lf-title" contenteditable="true" spellcheck="false" data-ph="عنوان فرم (اختیاری)">${esc(st.title)}</div>` +
            (st.description ? `<div class="lf-desc">${esc(st.description)}</div>` : "");
    }
    function paintHead(head) {
        head.innerHTML = headHtml();
        const t = head.querySelector(".lf-title");
        t.addEventListener("input", () => {
            dLayout.settings.title = t.textContent; markDirty();
            const inp = document.querySelector("#ldProps [data-set=title]"); if (inp) inp.value = t.textContent;
        });
        t.addEventListener("keydown", e => { if (e.key === "Enter") { e.preventDefault(); t.blur(); } });
        t.addEventListener("focus", () => { if (dSel) select(null); });
    }

    function renderStage() {
        const stage = $byId("ldStage");
        stage.innerHTML = "";
        $byId("ldBoard").classList.toggle("is-preview", !!dTpl && dPreview);
        if (!dTpl) {
            stage.innerHTML = `<div class="ld-empty"><div class="ld-empty-title">طراح Layout فرم</div>
                <p>یک قالب انتخاب کنید تا فرم آن را به‌صورت گرافیکی بچینید. قالبی که Layout داشته باشد در «ورود دستی داده» به‌جای فرم داینامیک، با همین چیدمان بارگذاری می‌شود.</p></div>`;
            return;
        }
        const paper = el("div", "ld-paper" + (dPreview && dDevice === "mobile" ? " is-mobile" : ""));
        if (dPreview) {
            L.renderRuntime({ ...dTpl, layout: dLayout }, paper);
            paper.appendChild(el("div", "ld-preview-foot", `<button type="button" class="btn primary" disabled>${esc(dLayout.settings.submitText || "ذخیره رکورد")}</button><small>پیش‌نمایش — داده‌ای ذخیره نمی‌شود</small>`));
            stage.appendChild(paper);
            return;
        }
        const missing = requiredMissing();
        if (missing.length) stage.appendChild(el("div", "ld-warn", `فیلدهای اجباریِ چیدمان‌نشده: ${missing.map(f => esc(f.label)).join("، ")} — در فرم نهایی خودکار به انتها اضافه می‌شوند.`));

        const form = el("div", "lf-form is-design" + (dLayout.settings.labelPosition === "side" ? " label-side" : ""));
        const head = el("div", "ld-head");
        paintHead(head);
        const grid = el("div", "lf-grid");
        dLayout.blocks.forEach(b => grid.appendChild(designBlock(b)));
        if (!dLayout.blocks.length) {
            const cta = el("div", "ld-cta", `<b>فرم خالی است</b><p>فیلدها را از پنل کناری بکشید و اینجا رها کنید، یا با یک چیدمان خودکار شروع کنید.</p><button type="button" class="btn primary">چیدمان خودکار از فیلدهای قالب</button>`);
            cta.querySelector("button").onclick = applyAuto;
            grid.appendChild(cta);
        }
        const end = el("div", "ld-end", "رها کنید تا به انتهای فرم اضافه شود");
        end.addEventListener("dragover", e => { if (!drag) return; e.preventDefault(); clearDropMarks(); end.classList.add("over"); });
        end.addEventListener("dragleave", () => end.classList.remove("over"));
        end.addEventListener("drop", e => { e.preventDefault(); doDrop(null, false); });
        form.append(head, grid, end);
        paper.appendChild(form);
        paper.addEventListener("click", e => { if (e.target === paper || e.target === form || e.target === grid) select(null); });
        stage.appendChild(paper);
    }

    // ---------- پنل ویژگی‌ها (چپ) ----------
    const F = (label, ctl) => `<div class="ld-f"><label>${label}</label>${ctl}</div>`;
    const SEG = (name, items, cur) => `<div class="ld-seg" data-seg="${name}">${items.map(([v, l]) => `<button type="button" data-v="${v}" class="${String(v) === String(cur) ? "on" : ""}">${l}</button>`).join("")}</div>`;
    const SPANS = [[3, "¼"], [4, "⅓"], [6, "½"], [8, "⅔"], [9, "¾"], [12, "کامل"]];

    function renderProps() {
        const p = $byId("ldProps");
        if (!dTpl) { p.innerHTML = `<div class="ld-note">ویژگی‌ها اینجا نمایش داده می‌شوند.</div>`; return; }
        if (dPreview) { p.innerHTML = ""; return; }
        const b = blockById(dSel);
        if (b) renderBlockProps(p, b); else renderFormProps(p);
    }

    function renderFormProps(p) {
        const st = dLayout.settings;
        const steps = dLayout.blocks.filter(x => x.type === "pagebreak").length + 1;
        p.innerHTML = `<div class="ld-sec-title">تنظیمات فرم</div>
            ${F("عنوان فرم", `<input type="text" data-set="title" value="${esc(st.title)}">`)}
            ${F("توضیح فرم", `<textarea rows="3" data-set="description">${esc(st.description)}</textarea>`)}
            ${F("متن دکمهٔ ذخیره", `<input type="text" data-set="submitText" value="${esc(st.submitText)}">`)}
            ${F("جایگاه عنوان فیلدها", SEG("labelPosition", [["top", "بالا"], ["side", "کنار"]], st.labelPosition))}
            <div class="ld-stats"><span>${placedKeys().size} فیلد</span><span>${steps} مرحله</span><span>${dLayout.blocks.filter(x => x.showIf).length} شرط</span></div>
            <div class="ld-hint">برای ویرایش یک المان روی آن کلیک کنید. عرض را با دستگیرهٔ لبهٔ المان یا از پنل ویژگی‌ها تغییر دهید. کلید Delete المان انتخاب‌شده را حذف می‌کند.</div>`;
        p.querySelectorAll("[data-set]").forEach(inp => inp.addEventListener("input", () => {
            st[inp.dataset.set] = inp.value; markDirty();
            const h = document.querySelector("#ldStage .ld-head"); if (h && !h.contains(document.activeElement)) paintHead(h);
        }));
        p.querySelectorAll("[data-seg] button").forEach(btn => btn.onclick = () => {
            st.labelPosition = btn.dataset.v; markDirty();
            const f = document.querySelector("#ldStage .lf-form"); if (f) f.classList.toggle("label-side", st.labelPosition === "side");
            renderFormProps(p);
        });
    }

    function renderBlockProps(p, b) {
        const f = b.type === "field" ? fieldOf(dTpl, b.fieldKey) : null;
        let h = `<div class="ld-sec-title">${b.type === "field" ? esc(f ? f.label : b.fieldKey) : TAGS[b.type]}${f ? `<span>${TYPE_LABEL[f.type] || ""}</span>` : ""}</div>`;

        if (b.type === "field" && f) {
            const canWidget = ["text", "any"].includes(f.type), w = b.widget || "auto";
            h += F("عنوان نمایشی", `<input type="text" data-prop="label" value="${esc(b.label)}" placeholder="${esc(f.label)}">`);
            if (canWidget) h += F("نوع ورودی", SEG("widget", [["auto", "خط تکی"], ["textarea", "چندخطی"], ["select", "لیست"], ["radio", "رادیویی"]], w));
            if (canWidget && ["select", "radio"].includes(w)) h += F("گزینه‌ها (هر خط یک گزینه)", `<textarea rows="4" data-prop="options">${esc((b.options || []).join("\n"))}</textarea>`);
            if (w !== "radio") h += F("متن نمونه (Placeholder)", `<input type="text" data-prop="placeholder" value="${esc(b.placeholder)}">`);
            h += F("توضیح زیر فیلد", `<input type="text" data-prop="help" value="${esc(b.help)}">`);

            h += `<div class="ld-f" style="display:flex;align-items:center;gap:10px;">
        <label style="flex:1;margin:0;">اجباری</label>
        <label class="switch" title="اجباری">
            <input type="checkbox" data-prop="required" ${f.required || b.required ? "checked" : ""} ${f.required ? "disabled" : ""}>
            <span class="slider"></span>
        </label>
        ${f.required ? '<small style="color:var(--faint);">(طبق قالب)</small>' : ""}
      </div>`;
        }
        if (b.type === "heading") {
            h += F("متن عنوان", `<input type="text" data-prop="text" value="${esc(b.text)}">`);
            h += F("اندازه", SEG("level", [[1, "بزرگ"], [2, "متوسط"], [3, "کوچک"]], b.level || 2));
        }
        if (b.type === "paragraph") h += F("متن", `<textarea rows="5" data-prop="text">${esc(b.text)}</textarea>`);
        if (b.type === "spacer") h += F("ارتفاع (پیکسل)", `<input type="number" min="4" max="200" data-prop="height" value="${b.height || 24}">`);
        if (b.type === "pagebreak") h += F("عنوان مرحله", `<input type="text" data-prop="title" value="${esc(b.title)}">`) + `<div class="ld-hint">المان‌های بعد از این نقطه در مرحلهٔ جدیدی نمایش داده می‌شوند و کاربر با دکمه‌های «قبلی/بعدی» بین مرحله‌ها حرکت می‌کند.</div>`;
        if (b.type !== "pagebreak") h += F("عرض", SEG("span", SPANS, b.span || 12));

        // شرط نمایش
        if (b.type !== "pagebreak") {
            const sources = dLayout.blocks.filter(x => x.type === "field" && x.id !== b.id && fieldOf(dTpl, x.fieldKey));
            const r = b.showIf, sb = r && sources.find(x => x.fieldKey === r.key);
            h += `<div class="ld-sec-title">شرط نمایش</div>`;
            h += F("این المان را نمایش بده", `<select data-logic="key"><option value="">همیشه</option>${sources.map(x => `<option value="${esc(x.fieldKey)}" ${r && r.key === x.fieldKey ? "selected" : ""}>وقتی «${esc(x.label || fieldOf(dTpl, x.fieldKey).label)}»</option>`).join("")}</select>`);
            if (r) {
                h += F("شرط", `<select data-logic="op">${Object.entries(OPS).map(([k, l]) => `<option value="${k}" ${r.op === k ? "selected" : ""}>${l}</option>`).join("")}</select>`);
                if (["eq", "neq"].includes(r.op)) {
                    const opts = sb && ["select", "radio"].includes(sb.widget) ? sb.options || [] : null;
                    h += F("مقدار", opts
                        ? `<select data-logic="value"><option value="">—</option>${opts.map(o => `<option value="${esc(o)}" ${r.value === o ? "selected" : ""}>${esc(o)}</option>`).join("")}</select>`
                        : `<input type="text" data-logic="value" value="${esc(r.value)}">`);
                }
            }
        }
        h += `<button type="button" class="btn danger ld-del">حذف از فرم</button>`;
        p.innerHTML = h;

        if (RESTRICTED && b.type === "field") {
            p.querySelectorAll('[data-prop="required"], [data-prop="options"], [data-seg="widget"] button, [data-logic], .ld-del')
                .forEach(x => { x.disabled = true; });
            p.insertAdjacentHTML("beforeend", `<div class="ld-hint">الزامی بودن، نوع ورودی، گزینه‌ها، شرط نمایش و حذف این فیلد در نسخهٔ منتشرشده قفل است.</div>`);
        }

        const changed = rerender => { markDirty(); refreshBlock(b.id); if (rerender) renderProps(); };
        p.querySelectorAll("[data-prop]").forEach(inp => inp.addEventListener(inp.type === "checkbox" ? "change" : "input", () => {
            const k = inp.dataset.prop;
            let v = inp.type === "checkbox" ? inp.checked : inp.value;
            if (k === "options") v = v.split("\n").map(s => s.trim()).filter(Boolean);
            if (k === "height") v = Math.max(4, Math.min(200, Number(v) || 24));
            b[k] = v; changed(false);
        }));
        p.querySelectorAll("[data-seg] button").forEach(btn => btn.onclick = () => {
            const k = btn.parentElement.dataset.seg;
            let v = btn.dataset.v;
            if (k === "level" || k === "span") v = Number(v);
            b[k] = v;
            if (k === "widget" && ["select", "radio"].includes(v) && !(b.options || []).length) b.options = ["گزینه ۱", "گزینه ۲"];
            changed(true);
        });
        p.querySelectorAll("[data-logic]").forEach(inp => inp.addEventListener("change", () => {
            const k = inp.dataset.logic;
            if (k === "key") b.showIf = inp.value ? { key: inp.value, op: "eq", value: "" } : null;
            else b.showIf[k] = inp.value;
            changed(k !== "value");
        }));
        const txt = p.querySelector('[data-logic="value"]');
        if (txt && txt.tagName === "INPUT") txt.addEventListener("input", () => { b.showIf.value = txt.value; markDirty(); });
        p.querySelector(".ld-del").onclick = () => removeBlock(b.id);
    }

    // ---------- کلیدها ----------
    document.addEventListener("keydown", e => {
        if (READ_ONLY || dPreview || !dTpl) return;
        if (/INPUT|TEXTAREA|SELECT/.test(e.target.tagName)) return;
        if (e.key === "Delete" && dSel) removeBlock(dSel);
        if (e.altKey && dSel && (e.key === "ArrowUp" || e.key === "ArrowDown")) { e.preventDefault(); moveBlock(dSel, e.key === "ArrowUp" ? -1 : 1); }
        if (e.key === "Escape" && dSel) select(null);
    });

    // ---------- راه‌اندازی ----------
    L.init = function () {
        if (!document.getElementById("ldBoard")) return; // صفحهٔ ورود داده: طراح راه‌اندازی نمی‌شود
        loadFromPage();
        window.addEventListener("beforeunload", e => {
            if (dDirty) { e.preventDefault(); e.returnValue = ""; }
        });
    };
    L.init();
})();
