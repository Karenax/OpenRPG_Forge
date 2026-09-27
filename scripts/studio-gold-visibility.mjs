// Evidence-only hit testing. A rectangle inside the viewport may still be clipped
// or covered. Fixed-position children can escape an ancestor's clipping region,
// so ask the browser what is actually hit instead of inferring it from ancestry.
// Closed details can retain layout rectangles in WebView. Exclude only content
// intentionally collapsed, never controls merely clipped or outside the viewport.
export function presented(element) {
  if (element.closest('[hidden]') || !element.getClientRects().length) return false;
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    if (parent.tagName === 'DETAILS' && !parent.open) {
      const summary = [...parent.children].find(child => child.tagName === 'SUMMARY');
      if (!summary?.contains(element)) return false;
    }
  }
  return true;
}

export function fullyVisible(element, width, height, hitTest) {
  const r = element.getBoundingClientRect();
  if (!(r.width > 0 && r.height > 0 && r.top >= 0 && r.bottom <= height
    && r.left >= 0 && r.right <= width)) return false;
  const dx = Math.min(6, r.width / 4), dy = Math.min(6, r.height / 4);
  return [[r.x + r.width / 2, r.y + r.height / 2],
    [r.left + dx, r.top + dy], [r.right - dx, r.top + dy],
    [r.left + dx, r.bottom - dy], [r.right - dx, r.bottom - dy]]
    .every(([x, y]) => {
      const hit = hitTest(x, y);
      return !!hit && (hit === element || element.contains(hit));
    });
}
