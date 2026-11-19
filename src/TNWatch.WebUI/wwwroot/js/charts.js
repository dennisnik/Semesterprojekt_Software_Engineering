// TN-Watch chart interop (Chart.js 4.4.1, loaded globally as window.Chart).
const instances = new WeakMap();

const tickFormat = new Intl.DateTimeFormat('de-DE', { timeZone: 'Europe/Berlin', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
const fullFormat = new Intl.DateTimeFormat('de-DE', { timeZone: 'Europe/Berlin', dateStyle: 'medium', timeStyle: 'short' });
const num = (v, d) => v.toLocaleString('de-DE', { minimumFractionDigits: d, maximumFractionDigits: d });

// points: [{ t: epochMs, v: volt, c: ampere }], thresholds: { normal: 12.4, warning: 11.8 }
export function render(canvas, points, thresholds) {
  destroy(canvas);
  if (!canvas || !window.Chart) return;

  const first = points.length ? points[0].t : Date.now();
  const last = points.length ? points[points.length - 1].t : Date.now();
  const flat = y => [{ x: first, y }, { x: last, y }];

  const chart = new Chart(canvas, {
    type: 'line',
    data: {
      datasets: [
        { label: 'Spannung (V)', data: points.map(p => ({ x: p.t, y: p.v })), yAxisID: 'yV', borderColor: '#1c6dd0', backgroundColor: '#1c6dd0', borderWidth: 2, pointRadius: 0, tension: 0.15 },
        { label: 'Entladestrom (A)', data: points.map(p => ({ x: p.t, y: p.c })), yAxisID: 'yA', borderColor: '#e8710a', backgroundColor: '#e8710a', borderWidth: 1.5, pointRadius: 0, tension: 0.15 },
        { label: `Warnung < ${num(thresholds.normal, 1)} V`, data: flat(thresholds.normal), yAxisID: 'yV', borderColor: '#d9a400', borderDash: [6, 4], borderWidth: 1, pointRadius: 0 },
        { label: `Kritisch < ${num(thresholds.warning, 1)} V`, data: flat(thresholds.warning), yAxisID: 'yV', borderColor: '#e03131', borderDash: [6, 4], borderWidth: 1, pointRadius: 0 },
      ],
    },
    options: {
      animation: false,
      parsing: false,
      normalized: true,
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'nearest', axis: 'x', intersect: false },
      scales: {
        x: { type: 'linear', min: first, max: last, ticks: { maxTicksLimit: 6, callback: v => tickFormat.format(new Date(v)) }, grid: { color: '#eceef1' } },
        yV: { position: 'left', title: { display: true, text: 'Spannung (V)' }, ticks: { callback: v => num(v, 1) }, grid: { color: '#eceef1' } },
        yA: { position: 'right', title: { display: true, text: 'Strom (A)' }, ticks: { callback: v => num(v, 2) }, grid: { drawOnChartArea: false } },
      },
      plugins: {
        legend: { position: 'bottom', labels: { boxWidth: 14, usePointStyle: false } },
        tooltip: {
          filter: item => item.datasetIndex < 2,
          callbacks: {
            title: items => items.length ? fullFormat.format(new Date(items[0].parsed.x)) : '',
            label: item => item.datasetIndex === 0 ? ` ${num(item.parsed.y, 2)} V` : ` ${num(item.parsed.y, 2)} A`,
          },
        },
      },
    },
  });
  instances.set(canvas, chart);
}

export function destroy(canvas) {
  const existing = canvas && instances.get(canvas);
  if (existing) {
    existing.destroy();
    instances.delete(canvas);
  }
}
