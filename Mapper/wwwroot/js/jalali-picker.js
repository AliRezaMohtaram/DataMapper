/* jalali-picker.js — کنترل تاریخ و تاریخ‌وساعت فرم ثبت رکورد (توابع سراسری: Jalali, buildJalaliPicker(field, opts))
   مقدار کنترل ISO میلادی است و کاربر تاریخ شمسی می‌بیند. خودِ تقویم و ساعت از MX است (mx.js: input[data-jdate],
   input[data-jtime])؛ تقویم قدیمی (jp-*) کنار گذاشته شد. */
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
   سازندهٔ کنترل تاریخ (و ساعت) فرم ثبت رکورد — روی تقویم MX (همان تقویم MProcess)
   - کادر تاریخ input[data-jdate] است: تایپ مستقیم (۱۴۰۵/۰۷/۲۴ یا 1405/7/24)، دکمهٔ تقویم، کلیدهای جهت،
     PageUp/PageDown، Enter، Escape؛ تاریخ‌وساعت یک کادر ساعت input[data-jtime] هم کنارش دارد.
   - مقدار کنترل (wrap.value) مثل قبل ISO میلادی است: yyyy-MM-dd یا yyyy-MM-ddTHH:mm (سازگار با Oracle DATE /
     TIMESTAMP)؛ متنی که تاریخ درستی نیست همان‌طور برمی‌گردد تا اعتبارسنجی خطا بدهد.
   ================================================================ */
function buildJalaliPicker(field, opts = {}) {
    const two = n => String(n).padStart(2, "0");
    const latin = s => String(s ?? "").replace(/[۰-۹]/g, d => "۰۱۲۳۴۵۶۷۸۹".indexOf(d)).replace(/[٠-٩]/g, d => "٠١٢٣٤٥٦٧٨٩".indexOf(d)).trim();

    const wrap = document.createElement("div");
    wrap.className = "jdate-field lf-control" + (opts.withTime ? " with-time" : "");
    wrap.dataset.key = field.key;
    if (opts.required) wrap.dataset.req = "1";

    const date = document.createElement("input");
    date.type = "text";
    date.className = "input ltr";
    date.dataset.jdate = "";
    date.inputMode = "numeric";
    date.autocomplete = "off";
    date.placeholder = opts.placeholder || "۱۴۰۵/۰۱/۰۱";
    date.setAttribute("aria-label", field.label || field.title || field.key);
    wrap.appendChild(date);

    let time = null;
    if (opts.withTime) {
        time = document.createElement("input");
        time.type = "text";
        time.className = "input ltr nums";
        time.dataset.jtime = "";
        time.inputMode = "numeric";
        time.autocomplete = "off";
        time.placeholder = "08:00";
        time.setAttribute("aria-label", "ساعت");
        wrap.appendChild(time);
    }

    /** Jalali text (1405/07/24, Persian digits) → ISO Gregorian, or null. */
    function toIso(text) {
        const m = latin(text).match(/^(\d{4})[\/\-.](\d{1,2})[\/\-.](\d{1,2})$/);
        if (!m) return null;
        const jy = +m[1], jm = +m[2], jd = +m[3];
        if (jy >= 1700) return `${jy}-${two(jm)}-${two(jd)}`; // already Gregorian
        if (!Jalali.isValid(jy, jm, jd)) return null;
        const g = Jalali.toGregorian(jy, jm, jd);
        return `${g.gy}-${two(g.gm)}-${two(g.gd)}`;
    }

    function toJalali(iso) {
        const m = String(iso ?? "").match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (!m) return String(iso ?? "");
        try {
            const j = Jalali.toJalaali(+m[1], +m[2], +m[3]);
            return `${j.jy}/${two(j.jm)}/${two(j.jd)}`.replace(/\d/g, d => "۰۱۲۳۴۵۶۷۸۹"[d]); // as MX's calendar writes it
        } catch (_) { return String(iso); }
    }

    Object.defineProperty(wrap, "value", {
        get() {
            const text = date.value.trim();
            if (!text) return "";
            const iso = toIso(text);
            if (!iso) return text; // not a date: validation reports it
            const t = time ? latin(time.value).match(/^(\d{1,2})[:.](\d{2})$/) : null;
            return t ? `${iso}T${two(+t[1])}:${t[2]}` : iso;
        },
        set(v) {
            const text = v == null ? "" : String(v);
            date.value = text ? toJalali(text) : "";
            if (time) {
                const tm = text.match(/T(\d{2}:\d{2})/);
                time.value = tm ? tm[1] : "";
            }
        }
    });
    Object.defineProperty(wrap, "placeholder", {
        get() { return date.placeholder; },
        set(v) { if (v) date.placeholder = v; }
    });

    // MX wraps an input in its calendar/clock (needs a parent: the wrap); later forms are enhanced by MX itself too.
    if (window.MX?.jdate) MX.jdate(date);
    if (time && window.MX?.jtime) MX.jtime(time);
    return wrap;
}
