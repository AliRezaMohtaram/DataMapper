/* jalali-picker.js — تقویم جلالی برای فیلدهای تاریخ و تاریخ‌وساعت (برگرفته از script.js نمونه)
   توابع سراسری: Jalali, buildJalaliPicker(field, opts)
   مقدار داخلی کنترل به‌صورت ISO میلادی (yyyy-MM-dd یا yyyy-MM-ddTHH:mm) است؛ کاربر تاریخ شمسی می‌بیند.
   استایل‌ها (.jdate-wrap, .jp-popup, ...) در theme.css نمونه هستند. */
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

/* ================================================================
   سازندهٔ کنترل تقویم جلالی — مقدار داخلی به‌صورت ISO میلادی ذخیره می‌شود
   (سازگار با Oracle DATE / TIMESTAMP)
   ================================================================ */
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
