// Bound browser editing redraws without changing GPU queue or fence semantics.
(() => {
  if (new URLSearchParams(globalThis.location?.search || "").get("authoring") !== "1"
      || !globalThis.requestAnimationFrame || !globalThis.cancelAnimationFrame) return;
  const request = globalThis.requestAnimationFrame.bind(globalThis);
  const cancel = globalThis.cancelAnimationFrame.bind(globalThis);
  const jobs = new Map();
  const activeInterval = 1000 / 60;
  const idleInterval = 500;
  let activeUntil = (globalThis.performance?.now() || 0) + 2000;
  function activity() {
    activeUntil = (globalThis.performance?.now() || 0) + 2000;
  }
  for (const event of ["pointermove", "pointerdown", "pointerup", "click", "wheel",
    "keydown", "keyup", "input", "change", "resize", "focus", "visibilitychange"]) {
    globalThis.addEventListener?.(event, activity, { capture: true, passive: true });
  }
  let nextId = -1;
  let nativeFrame = null;
  let previous = -Infinity;
  function schedule() {
    if (jobs.size && nativeFrame === null) nativeFrame = request(deliver);
  }
  function deliver(timestamp) {
    nativeFrame = null;
    // Keep callbacks, world and GPU resources resident; resume the same jobs.
    if (globalThis.document?.querySelector('.authoring-shell[data-game-paused="true"]')) {
      schedule();
      return;
    }
    if (globalThis.document?.querySelector('.forge-map-loading[aria-busy="true"]')) {
      activeUntil = timestamp + 2000;
    }
    const interval = timestamp <= activeUntil ? activeInterval : idleInterval;
    if (timestamp - previous < interval) { schedule(); return; }
    previous = timestamp;
    const batch = [...jobs.entries()];
    for (const [id, callback] of batch) {
      if (!jobs.delete(id)) continue;
      try { callback.call(globalThis, timestamp); }
      catch (error) { setTimeout(() => { throw error; }, 0); }
    }
    schedule();
  }
  globalThis.requestAnimationFrame = function (callback) {
    if (typeof callback !== "function") return request(callback);
    const id = nextId--;
    jobs.set(id, callback);
    schedule();
    return id;
  };
  globalThis.cancelAnimationFrame = function (id) {
    if (!jobs.delete(id)) { cancel(id); return; }
    if (!jobs.size && nativeFrame !== null) {
      cancel(nativeFrame);
      nativeFrame = null;
    }
  };
})();
