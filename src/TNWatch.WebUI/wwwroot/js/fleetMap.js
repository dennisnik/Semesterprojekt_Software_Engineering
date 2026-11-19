// TN-Watch map interop (Leaflet 1.9.4, loaded globally as window.L).
// One map per circuit/page. Markers are diffed by node id; icons are only rebuilt when their look changes.

const CHECK_SVG = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12.5l4.2 4.2L19 7" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>';

let map = null;
let dotnet = null;
let routeMode = false;
let fitted = false;
let anchor = null;
const markers = new Map();

export function init(element, dotnetRef, options) {
  dotnet = dotnetRef;
  map = L.map(element, { zoomControl: false, worldCopyJump: true })
    .setView([options.centerLatitude, options.centerLongitude], options.zoom);
  L.control.zoom({ position: 'bottomright' }).addTo(map);
  L.tileLayer(options.tileUrl, { maxZoom: 19, attribution: options.attribution }).addTo(map);
  map.on('move zoom resize', positionAnchor);
  window.addEventListener('resize', positionAnchor);
}

function buildIcon(n) {
  const bubble = routeMode
    ? `<div class="tn-bubble${n.selected ? ' is-selected' : ''}">${n.selected ? CHECK_SVG : ''}</div>`
    : '';
  return L.divIcon({
    className: 'tn-marker',
    html: `${bubble}<div class="tn-pin status-${n.status}${n.active ? ' is-active' : ''}"></div>`,
    iconSize: [22, 22],
    iconAnchor: [11, 11],
  });
}

export function setNodes(nodes, isRouteMode) {
  if (!map) return;
  routeMode = isRouteMode;
  const seen = new Set();

  for (const n of nodes) {
    if (n.lat == null || n.lon == null) continue;
    seen.add(n.id);
    const look = `${n.status}|${n.selected}|${routeMode}|${n.active}`;
    let m = markers.get(n.id);
    if (!m) {
      m = L.marker([n.lat, n.lon], { icon: buildIcon(n), title: n.id, keyboard: true, riseOnHover: true });
      m.on('click', () => dotnet && dotnet.invokeMethodAsync('OnMarkerClick', n.id));
      m.addTo(map);
      m.tnLook = look;
      markers.set(n.id, m);
    } else {
      const ll = m.getLatLng();
      if (ll.lat !== n.lat || ll.lng !== n.lon) m.setLatLng([n.lat, n.lon]);
      if (m.tnLook !== look) {
        m.setIcon(buildIcon(n));
        m.tnLook = look;
      }
    }
    m.setZIndexOffset(n.active || n.selected ? 1000 : 0);
  }

  for (const [id, m] of markers) {
    if (!seen.has(id)) {
      m.remove();
      markers.delete(id);
    }
  }

  if (!fitted && seen.size > 0) {
    fitted = true;
    fitAll();
  }
  positionAnchor();
}

export function fitAll() {
  if (!map || markers.size === 0) return;
  const bounds = L.latLngBounds([...markers.values()].map(m => m.getLatLng()));
  const wide = map.getSize().x > 768;
  map.fitBounds(bounds.pad(0.15), { maxZoom: 13, paddingTopLeft: [wide ? 380 : 20, 20], paddingBottomRight: [20, 20] });
}

export function focusNode(id) {
  const m = markers.get(id);
  if (m) map.flyTo(m.getLatLng(), Math.max(map.getZoom(), 13), { duration: 0.6 });
}

// Keeps a Blazor-rendered card (node popup, Mockup 2) next to its marker while the map moves.
export function anchorElement(element, id) {
  anchor = element ? { element, id } : null;
  positionAnchor();
}

function positionAnchor() {
  if (!anchor || !map) return;
  const { element, id } = anchor;
  const m = markers.get(id);
  if (!m) {
    element.style.visibility = 'hidden';
    return;
  }
  const p = map.latLngToContainerPoint(m.getLatLng());
  const size = map.getSize();
  const w = element.offsetWidth;
  const h = element.offsetHeight;
  let left = p.x + 6;
  let top = p.y - h - 16;
  if (left + w > size.x - 12) left = p.x - w - 6;
  if (top < 12) top = p.y + 20;
  left = Math.max(12, Math.min(left, size.x - w - 12));
  top = Math.max(12, Math.min(top, size.y - h - 12));
  element.style.left = `${left}px`;
  element.style.top = `${top}px`;
  element.style.visibility = 'visible';
}

export function dispose() {
  window.removeEventListener('resize', positionAnchor);
  if (map) map.remove();
  map = null;
  dotnet = null;
  anchor = null;
  fitted = false;
  markers.clear();
}
