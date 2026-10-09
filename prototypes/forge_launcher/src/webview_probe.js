/* CLI-only diagnostic injected after the actual embedded page finishes loading. */
(async () => {
  const token = __FORGE_PROBE_TOKEN__;
  const exitMode = __FORGE_PROBE_EXIT__;
  const invoke = window.__TAURI__?.core?.invoke;
  const report = { token, complete: true, stage: 'finished' };
  if (typeof invoke !== 'function') return;
  if (document.readyState === 'loading') {
    await new Promise(resolve => document.addEventListener('DOMContentLoaded', resolve, { once: true }));
  }
  if (!document.getElementById('main') || window.__FORGE_LAUNCHER_PROBE_ACTIVE__) return;
  window.__FORGE_LAUNCHER_PROBE_ACTIVE__ = token;
  try {
    if (typeof invoke !== 'function') throw new Error('Tauri bridge is absent');
    await invoke('complete_launcher_probe', { report: { token, stage: 'script-started' } });
    location.hash = '#aventures';
    const snapshot = await invoke('execute_launcher_command', { command: { kind: 'snapshot' } });
    await invoke('complete_launcher_probe', { report: { token, stage: 'snapshot-returned' } });
    let button;
    let status;
    for (let attempt = 0; attempt < 160; attempt++) {
      button = document.getElementById('launch-tidewater');
      status = document.getElementById('launch-tidewater-status');
      if (button && status && !status.textContent.includes('Vérification du jeu installé')) break;
      await new Promise(resolve => setTimeout(resolve, 50));
    }
    Object.assign(report, {
      title: document.title, url: location.href, bridge: true,
      styles_loaded: Array.from(document.querySelectorAll('link[rel="stylesheet"]'))
        .some(link => new URL(link.href).pathname === '/styles.css' && !!link.sheet),
      button_present: !!button, button_disabled: button?.disabled ?? true,
      snapshot_kind: snapshot.kind, runtime_connected: snapshot.runtime_connected,
      status: status?.textContent ?? '',
    });
    const dialog = document.getElementById('launcher-exit-dialog');
    const cancel = document.getElementById('launcher-exit-cancel');
    const escape = () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    if (!dialog || !cancel) throw new Error('Exit dialog absent');
    escape();
    report.dialog_opened = dialog.open && dialog.matches(':modal');
    report.focus_on_cancel = document.activeElement === cancel;
    const bounds = dialog.getBoundingClientRect();
    report.dialog_fits_viewport = bounds.width > 0 && bounds.left >= 0 && bounds.right <= innerWidth && bounds.top >= 0 && bounds.bottom <= innerHeight;
    escape(); report.escape_cancelled = !dialog.open;
    escape(); cancel.click(); report.cancel_button_worked = !dialog.open;
    if (exitMode) { escape(); report.complete = false; report.stage = 'exit-menu-ready'; }
  } catch (error) {
    report.error = String(error);
  }
  if (typeof invoke === 'function') {
    await invoke('complete_launcher_probe', { report });
    if (exitMode && !report.error) document.getElementById('launcher-exit-confirm').click();
  }
})();
