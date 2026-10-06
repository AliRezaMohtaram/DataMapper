/* =========================================================
   MX.JS — UI behaviours for the MX design system
   ---------------------------------------------------------
   Modals (stacked, focus-trapped) · Toasts · Command palette
   Wizard · Filter chips · Tabs · Dropdowns · Sidebar
   Theme state lives in theme.js; this file only calls
   window.ThemeManager when it exists.

   Declarative hooks:
     data-open="modalId"        open a modal
     data-close                 close the nearest modal
     data-action="mode:light"   run a registered action
     data-dd                    toggle the closest .dd dropdown
     data-tabs / data-tab="id"  segmented tabs → panes
     data-filter-group          filter chips for a table
     data-wizard                multi-step flow inside a modal
     data-sidebar-toggle        collapse rail / open mobile nav
   ========================================================= */

(() => {
    "use strict";

    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));

    const FA_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
    const fa = v => String(v).replace(/\d/g, d => FA_DIGITS[d]);
    const faNum = n => fa(Number(n).toLocaleString("en-US").replace(/,/g, "٬"));

    const FOCUSABLE =
        'a[href],button:not([disabled]),input:not([disabled]):not([type="hidden"]),' +
        'select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

    const visibleFocusables = root =>
        $$(FOCUSABLE, root).filter(el => el.offsetParent !== null || el === document.activeElement);


    /* =====================================================
       MODALS
       ===================================================== */

    const stack = [];

    const resolve = target =>
        typeof target === "string" ? document.getElementById(target) : target;

    function openModal(target) {
        const el = resolve(target);
        if (!el || el.classList.contains("open")) return;

        stack.push({ el, returnTo: document.activeElement });
        el.style.zIndex = String(300 + stack.length * 2);
        el.classList.add("open");
        el.setAttribute("aria-hidden", "false");
        document.documentElement.classList.add("mx-lock");
        el.dispatchEvent(new CustomEvent("mx:open"));

        setTimeout(() => {
            const first = $("[autofocus]", el) || visibleFocusables($(".modal-body", el) || el)[0];
            first?.focus({ preventScroll: true });
        }, 90);
    }

    function closeModal(target) {
        const el = target ? resolve(target) : stack[stack.length - 1]?.el;
        const index = stack.findIndex(s => s.el === el);
        if (index === -1) return;

        const [{ returnTo }] = stack.splice(index, 1);
        el.classList.remove("open");
        el.setAttribute("aria-hidden", "true");
        if (!stack.length) document.documentElement.classList.remove("mx-lock");
        el.dispatchEvent(new CustomEvent("mx:close"));
        returnTo?.focus?.({ preventScroll: true });
    }

    const topModal = () => stack[stack.length - 1]?.el;


    /* =====================================================
       TOASTS
       ===================================================== */

    const TOAST_ICONS = { success: "i-check", danger: "i-alert", warning: "i-alert", info: "i-info", primary: "i-zap" };

    function toast(message, tone = "success", title = "") {
        let host = $(".toast-stack");
        if (!host) {
            host = document.createElement("div");
            host.className = "toast-stack";
            host.setAttribute("aria-live", "polite");
            document.body.appendChild(host);
        }

        const el = document.createElement("div");
        el.className = `toast tone-${tone}`;
        el.setAttribute("role", "status");
        el.innerHTML =
            `<svg class="ico toast-ico"><use href="#${TOAST_ICONS[tone] || "i-check"}"/></svg>` +
            `<div><div class="toast-title"></div><div class="toast-msg"></div></div>`;
        const titleEl = $(".toast-title", el);
        if (title) titleEl.textContent = title; else titleEl.remove();
        $(".toast-msg", el).textContent = message;
        host.appendChild(el);

        setTimeout(() => {
            el.classList.add("out");
            el.addEventListener("animationend", () => el.remove(), { once: true });
        }, 3800);
    }


    /* =====================================================
       ACTIONS  (data-action="name:arg")
       ===================================================== */

    const actions = {
        mode: v => window.ThemeManager?.set("mode", v),
        accent: v => window.ThemeManager?.set("accent", v),
        preset: v => window.ThemeManager?.set("preset", v),
        "theme-open": () => window.ThemeManager?.open(),
        "theme-toggle": () => {
            const tm = window.ThemeManager;
            if (!tm) return;
            const dark = tm.get().mode === "dark" ||
                (tm.get().mode === "system" && matchMedia("(prefers-color-scheme: dark)").matches);
            tm.set("mode", dark ? "light" : "dark");
        },
        sidebar: () => toggleSidebar()
    };

    function runAction(spec, el) {
        const i = spec.indexOf(":");
        const name = i === -1 ? spec : spec.slice(0, i);
        const arg = i === -1 ? undefined : spec.slice(i + 1);
        actions[name]?.(arg, el);
    }


    /* =====================================================
       COMMAND PALETTE  (#cmdk)
       ===================================================== */

    function initPalette() {
        const root = $("#cmdk");
        if (!root) return;

        const input = $("input", root);
        const items = $$(".cmdk-item", root);
        const empty = $(".cmdk-empty", root);
        let active = 0;

        const visible = () => items.filter(el => !el.hidden);

        function highlight(n) {
            const list = visible();
            if (!list.length) return;
            active = (n + list.length) % list.length;
            list.forEach((el, k) => el.classList.toggle("active", k === active));
            list[active].scrollIntoView({ block: "nearest" });
        }

        function filter() {
            const raw = input.value.trim();
            const q = raw.toLowerCase();
            items.forEach(el => {
                // "Search X for …" items appear only while typing and carry the query in their link
                if (el.dataset.searchHref !== undefined) {
                    el.hidden = !q;
                    el.href = el.dataset.searchHref + encodeURIComponent(raw);
                    $$("[data-q]", el).forEach(b => { b.textContent = raw; });
                    return;
                }
                const hay = (el.textContent + " " + (el.dataset.k || "")).toLowerCase();
                el.hidden = !!q && !hay.includes(q);
            });
            $$(".cmdk-group", root).forEach(group => {
                let n = group.nextElementSibling, any = false;
                while (n && !n.classList.contains("cmdk-group")) {
                    if (n.classList.contains("cmdk-item") && !n.hidden) any = true;
                    n = n.nextElementSibling;
                }
                group.hidden = !any;
            });
            if (empty) empty.hidden = visible().length > 0;
            highlight(0);
        }

        input.addEventListener("input", filter);
        input.addEventListener("keydown", e => {
            if (e.key === "ArrowDown") { e.preventDefault(); highlight(active + 1); }
            else if (e.key === "ArrowUp") { e.preventDefault(); highlight(active - 1); }
            else if (e.key === "Enter") { e.preventDefault(); visible()[active]?.click(); }
        });
        items.forEach(el => el.addEventListener("mousemove", () => {
            const k = visible().indexOf(el);
            if (k !== active) highlight(k);
        }));
        root.addEventListener("mx:open", () => { input.value = ""; filter(); });
    }


    /* =====================================================
       WIZARD  ([data-wizard] on the .modal)
       ===================================================== */

    function initWizard(root) {
        const panes = $$("[data-step-pane]", root);
        const steps = $$(".step", root);
        const prev = $("[data-wizard-prev]", root);
        const next = $("[data-wizard-next]", root);
        const label = next && $("[data-label]", next);
        const counter = $("[data-wizard-counter]", root);
        const body = $(".modal-body", root);
        const last = panes.length - 1;
        let current = 0;

        function go(n) {
            current = Math.max(0, Math.min(n, last));
            panes.forEach((p, k) => { p.hidden = k !== current; });
            steps.forEach((s, k) => {
                s.classList.toggle("done", k < current);
                s.classList.toggle("active", k === current);
                s.setAttribute("aria-current", k === current ? "step" : "false");
            });
            if (prev) prev.disabled = current === 0;
            if (label) label.textContent = current === last ? next.dataset.finish : next.dataset.next;
            if (counter) counter.textContent = `مرحله ${fa(current + 1)} از ${fa(last + 1)}`;
            body?.scrollTo(0, 0);
            root.dispatchEvent(new CustomEvent("mx:step", { detail: { step: current } }));
        }

        next?.addEventListener("click", () => {
            const ok = root.dispatchEvent(
                new CustomEvent("mx:wizard-validate", { cancelable: true, detail: { step: current } }));
            if (!ok) return;
            if (current < last) go(current + 1);
            else root.dispatchEvent(new CustomEvent("mx:wizard-finish", { bubbles: true }));
        });
        prev?.addEventListener("click", () => go(current - 1));
        steps.forEach((s, k) => s.addEventListener("click", () => { if (k < current) go(k); }));
        root.closest(".modal-overlay")?.addEventListener("mx:open", () => go(0));

        root.mxWizard = { go, get step() { return current; } };
        go(0);
    }


    /* =====================================================
       FILTER CHIPS  ([data-filter-group] → table rows[data-status])
       ===================================================== */

    const filterGroups = [];

    function initFilterGroup(group) {
        const table = $(group.dataset.target);
        const counter = group.dataset.count ? $(group.dataset.count) : null;
        if (!table) return;

        function apply() {
            const filter = $(".fchip.active", group)?.dataset.filter || "all";
            const rows = $$("tbody tr[data-status]", table);
            let shown = 0;

            rows.forEach(tr => {
                const match = filter === "all" || tr.dataset.status.split(" ").includes(filter);
                tr.hidden = !match;
                if (match) shown++;
            });
            $$(".fchip", group).forEach(chip => {
                const n = $(".n", chip);
                if (!n) return;
                const f = chip.dataset.filter;
                n.textContent = fa(f === "all" ? rows.length
                    : rows.filter(tr => tr.dataset.status.split(" ").includes(f)).length);
            });
            const emptyRow = $("[data-empty-row]", table);
            if (emptyRow) emptyRow.hidden = shown > 0;
            if (counter) counter.textContent = fa(shown);
        }

        group.addEventListener("click", e => {
            const chip = e.target.closest(".fchip");
            if (!chip) return;
            $$(".fchip", group).forEach(c => {
                c.classList.toggle("active", c === chip);
                c.setAttribute("aria-pressed", String(c === chip));
            });
            apply();
        });

        filterGroups.push(apply);
        apply();
    }


    /* =====================================================
       SIDEBAR
       ===================================================== */

    const SIDEBAR_KEY = "mx-sidebar";
    const mqTablet = matchMedia("(max-width: 1100px)");
    const mqMobile = matchMedia("(max-width: 760px)");
    let userCollapsed = false;
    let tabletExpanded = false;
    try { userCollapsed = localStorage.getItem(SIDEBAR_KEY) === "collapsed"; } catch { /* storage blocked */ }

    function syncSidebar() {
        const app = $(".app");
        if (!app) return;
        if (mqMobile.matches) {
            app.classList.remove("is-collapsed");
        } else {
            app.classList.remove("nav-open");
            app.classList.toggle("is-collapsed", mqTablet.matches ? !tabletExpanded : userCollapsed);
        }
    }

    function toggleSidebar() {
        const app = $(".app");
        if (!app) return;
        if (mqMobile.matches) {
            app.classList.toggle("nav-open");
            return;
        }
        if (mqTablet.matches) {
            tabletExpanded = !tabletExpanded;
        } else {
            userCollapsed = !userCollapsed;
            try { localStorage.setItem(SIDEBAR_KEY, userCollapsed ? "collapsed" : "expanded"); } catch { /* ignore */ }
        }
        syncSidebar();
    }


    /* =====================================================
       GLOBAL EVENTS
       ===================================================== */

    let pressedOn = null;
    document.addEventListener("mousedown", e => { pressedOn = e.target; });

    document.addEventListener("click", e => {
        const t = e.target;

        // Palette item: close the palette, then let its own hook run below.
        const paletteItem = t.closest(".cmdk-item");
        if (paletteItem) closeModal("cmdk");

        const opener = t.closest("[data-open]");
        if (opener) { e.preventDefault(); openModal(opener.dataset.open); }

        const closer = t.closest("[data-close]");
        if (closer) { closeModal(closer.closest(".modal-overlay")); }

        const action = t.closest("[data-action]");
        if (action) runAction(action.dataset.action, action);

        if (t.closest("[data-sidebar-toggle]") || t.classList.contains("scrim")) toggleSidebar();
        else if (t.closest(".app.nav-open .nav-item")) $(".app").classList.remove("nav-open");

        // Click on the dimmed backdrop (press and release both on it)
        if (t.classList.contains("modal-overlay") && pressedOn === t && !t.hasAttribute("data-static")) {
            closeModal(t);
        }

        // Tabs
        const tab = t.closest("[data-tab]");
        if (tab) {
            const group = tab.closest("[data-tabs]");
            $$("[data-tab]", group).forEach(b => {
                const on = b === tab;
                b.classList.toggle("active", on);
                b.setAttribute("aria-selected", String(on));
                const pane = document.getElementById(b.dataset.tab);
                if (pane) pane.hidden = !on;
            });
        }

        // Dropdowns
        const ddToggle = t.closest("[data-dd]");
        const ownDd = ddToggle?.closest(".dd");
        $$(".dd.open").forEach(dd => {
            if (dd !== ownDd && !dd.contains(t)) dd.classList.remove("open");
        });
        if (ownDd) {
            const open = ownDd.classList.toggle("open");
            ddToggle.setAttribute("aria-expanded", String(open));
        } else if (t.closest(".dd-item")) {
            t.closest(".dd")?.classList.remove("open");
        }
    });

    document.addEventListener("keydown", e => {
        const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName) ||
            document.activeElement?.isContentEditable;

        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
            e.preventDefault();
            if ($("#cmdk")?.classList.contains("open")) closeModal("cmdk"); else openModal("cmdk");
            return;
        }
        if (e.key === "/" && !typing && !stack.length) {
            e.preventDefault();
            openModal("cmdk");
            return;
        }

        if (e.key === "Escape") {
            if ($("#themeDrawer.open")) return;           // theme.js closes the drawer
            const open = $$(".dd.open");
            if (open.length) { open.forEach(d => d.classList.remove("open")); return; }
            const top = topModal();
            if (top && !top.hasAttribute("data-static")) closeModal(top);
            return;
        }

        // Focus trap inside the top-most modal
        if (e.key === "Tab" && stack.length) {
            const list = visibleFocusables(topModal());
            if (!list.length) return;
            const first = list[0], lastEl = list[list.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); lastEl.focus(); }
            else if (!e.shiftKey && document.activeElement === lastEl) { e.preventDefault(); first.focus(); }
        }
    });


    /* =====================================================
       INIT
       ===================================================== */

    function init() {
        syncSidebar();
        mqTablet.addEventListener("change", syncSidebar);
        mqMobile.addEventListener("change", syncSidebar);
        $$(".modal-overlay").forEach(m => m.setAttribute("aria-hidden", "true"));
        initPalette();
        $$("[data-wizard]").forEach(initWizard);
        $$("[data-filter-group]").forEach(initFilterGroup);
    }

    window.MX = {
        open: openModal,
        close: closeModal,
        toast,
        fa,
        faNum,
        actions,
        refreshFilters: () => filterGroups.forEach(apply => apply())
    };

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
