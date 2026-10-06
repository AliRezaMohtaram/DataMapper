/* combobox.js — Combobox جستجوپذیر با جستجوی سمت سرور (برای فیلدهای متصل به منبع داده)
   برگرفته از buildCombobox نمونه (همان کلاس‌های CSS: combo-wrap, combo-visible, combo-list, ...)
   اما گزینه‌ها از endpoint منبع داده خوانده می‌شود:
     field.dataSource = { id, url: "/DataSources/Options", labels: { <value>: <label> } }
   مقدار کنترل (.value) همان «مقدار واقعی» است؛ کاربر «عنوان نمایشی» را می‌بیند. */
(() => {
    "use strict";

    const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[c]));

    window.buildCombobox = function (field, opts = {}) {
        const ds = field.dataSource || {};
        const wrap = document.createElement("div");
        wrap.className = "combo-wrap lf-control";
        wrap.dataset.key = field.key;
        if (opts.required) wrap.dataset.req = "1";

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

        const labels = Object.assign({}, ds.labels || {}); // value → label (مقدارهای شناخته‌شده)
        let items = [];
        let activeIdx = -1;
        let timer = null;
        let seq = 0;

        const labelOf = v => (v in labels ? labels[v] : String(v));
        const updateHasValue = () => wrap.classList.toggle("has-value", !!hidden.value);

        function url(params) {
            const u = new URL(ds.url, window.location.origin);
            u.searchParams.set("id", ds.id);
            Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== "") u.searchParams.set(k, v); });
            return u.toString();
        }

        async function fetchJson(params) {
            const res = await fetch(url(params), { headers: { Accept: "application/json" } });
            return res.json();
        }

        function setValue(v, silent) {
            hidden.value = v == null ? "" : String(v);
            visible.value = hidden.value ? labelOf(hidden.value) : "";
            updateHasValue();

            // عنوان مقدار ناشناخته (مثلاً هنگام ویرایش) از سرور گرفته می‌شود
            if (hidden.value && !(hidden.value in labels)) {
                const current = hidden.value;
                fetchJson({ value: current }).then(d => {
                    const hit = d && d.items && d.items[0];
                    if (hit) labels[hit.value] = hit.label;
                    if (hidden.value === current) visible.value = labelOf(current);
                }).catch(() => { });
            }

            if (!silent) {
                wrap.dispatchEvent(new Event("change", { bubbles: true }));
                wrap.dispatchEvent(new Event("input", { bubbles: true }));
            }
        }

        Object.defineProperty(wrap, "value", {
            get() { return hidden.value; },
            set(v) { setValue(v, true); }
        });
        Object.defineProperty(wrap, "placeholder", {
            get() { return visible.placeholder; },
            set(v) { visible.placeholder = v; }
        });

        function highlight(text, q) {
            if (!q) return esc(text);
            const i = text.toLowerCase().indexOf(q.toLowerCase());
            if (i < 0) return esc(text);
            return esc(text.slice(0, i)) + "<mark>" + esc(text.slice(i, i + q.length)) + "</mark>" + esc(text.slice(i + q.length));
        }

        function render(q, total, error) {
            activeIdx = -1;

            if (error) { list.innerHTML = `<div class="combo-empty">${esc(error)}</div>`; return; }
            if (!items.length) {
                list.innerHTML = `<div class="combo-empty">${q ? "موردی با این عبارت یافت نشد." : "این منبع داده گزینه‌ای ندارد."}</div>`;
                return;
            }

            list.innerHTML =
                items.map((o, i) =>
                    `<div class="combo-item" role="option" data-idx="${i}" data-value="${esc(o.value)}">${highlight(o.label, q)}</div>`
                ).join("") +
                (total > items.length ? `<div class="combo-more">${total - items.length} مورد دیگر — جستجو را دقیق‌تر کنید</div>` : "") +
                `<div class="combo-foot"><span>${total} گزینه</span></div>`;

            list.querySelectorAll(".combo-item").forEach(el => {
                el.addEventListener("mousedown", e => {
                    e.preventDefault();
                    const o = items[+el.dataset.idx];
                    if (o) { labels[o.value] = o.label; setValue(o.value); close(); }
                });
                el.addEventListener("mouseenter", () => {
                    list.querySelectorAll(".combo-item").forEach(x => x.classList.remove("active"));
                    el.classList.add("active");
                    activeIdx = +el.dataset.idx;
                });
            });
        }

        async function load(q) {
            const mine = ++seq;
            list.innerHTML = '<div class="combo-loading">در حال بارگذاری…</div>';

            try {
                const d = await fetchJson({ q, take: 30 });
                if (mine !== seq) return; // پاسخ قدیمی
                items = d.items || [];
                render(q, d.total || items.length, d.error);
            } catch (_) {
                if (mine !== seq) return;
                items = [];
                render(q, 0, "ارتباط با سرور برقرار نشد.");
            }
        }

        function open() { list.classList.remove("hidden"); load(""); }
        function close() { list.classList.add("hidden"); activeIdx = -1; }

        visible.addEventListener("focus", () => { if (hidden.value) visible.value = ""; open(); });

        visible.addEventListener("input", () => {
            if (hidden.value && visible.value === labelOf(hidden.value)) return;
            if (hidden.value) { hidden.value = ""; updateHasValue(); }
            list.classList.remove("hidden");
            clearTimeout(timer);
            timer = setTimeout(() => load(visible.value.trim()), 250);
        });

        visible.addEventListener("keydown", e => {
            if (e.key === "ArrowDown" || e.key === "ArrowUp") {
                e.preventDefault();
                if (list.classList.contains("hidden")) { open(); return; }
                if (!items.length) return;
                activeIdx = e.key === "ArrowDown" ? Math.min(items.length - 1, activeIdx + 1) : Math.max(0, activeIdx - 1);
                list.querySelectorAll(".combo-item").forEach((el, i) => el.classList.toggle("active", i === activeIdx));
                list.querySelector(`.combo-item[data-idx="${activeIdx}"]`)?.scrollIntoView({ block: "nearest" });
            } else if (e.key === "Enter") {
                if (activeIdx >= 0 && items[activeIdx] && !list.classList.contains("hidden")) {
                    e.preventDefault();
                    const o = items[activeIdx];
                    labels[o.value] = o.label;
                    setValue(o.value);
                    close();
                }
            } else if (e.key === "Escape") {
                close();
                if (hidden.value) visible.value = labelOf(hidden.value);
            } else if (e.key === "Tab") {
                if (!hidden.value && visible.value) visible.value = "";
                close();
            }
        });

        visible.addEventListener("blur", () => setTimeout(() => {
            if (!wrap.contains(document.activeElement)) {
                close();
                visible.value = hidden.value ? labelOf(hidden.value) : "";
            }
        }, 150));

        clearBtn.addEventListener("mousedown", e => {
            e.preventDefault();
            e.stopPropagation();
            setValue("");
            visible.focus();
            open();
        });

        wrap.append(visible, clearBtn, hidden, list);
        return wrap;
    };
})();
