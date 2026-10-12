/* Import page: while validation or commit runs in the background, polls [data-import-progress] (JSON from
   Imports/Progress/{id}) and moves the bar; reloads the page once the step has ended. */
(function () {
  'use strict';

  var card = document.querySelector('[data-import-progress]');
  if (!card) return;

  var url = card.getAttribute('data-import-progress');
  var bar = card.querySelector('[data-progress-bar]');
  var text = card.querySelector('[data-progress-text]');
  var meter = card.querySelector('[role="progressbar"]');
  var fa = new Intl.NumberFormat('fa-IR', { useGrouping: false });
  var failures = 0;

  if (bar) bar.style.transition = 'width .4s ease';

  function tick() {
    fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin', cache: 'no-store' })
      .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
      .then(function (p) {
        failures = 0;
        if (!p.busy) { window.location.reload(); return; }
        var pct = p.total > 0 ? Math.min(100, (100 * p.processed) / p.total) : 0;
        if (bar) bar.style.width = pct.toFixed(1) + '%';
        if (text) text.textContent = fa.format(p.processed) + ' از ' + fa.format(p.total) + ' سطر';
        if (meter) meter.setAttribute('aria-valuenow', String(p.processed));
        setTimeout(tick, 1500);
      })
      .catch(function () {
        // a network blip should not stop the page; give up after a minute of failures
        if (++failures < 40) setTimeout(tick, 1500);
      });
  }

  setTimeout(tick, 1000);
})();
