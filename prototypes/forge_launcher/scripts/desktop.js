/* Desktop-only bridge for the unchanged L’Aube des royaumes presentation. */
(() => {
  'use strict';
  const invoke = window.__TAURI__?.core?.invoke;
  if (typeof invoke !== 'function') return;

  const main = document.getElementById('main');
  const statusText = document.getElementById('announcer');

  async function showAdventure() {
    if (location.hash !== '#aventures') return;
    const panel = main.querySelector('.utility');
    if (!panel) return;
    panel.innerHTML = '<p class="eyebrow">Vos aventures</p>' +
      '<h1>L’île Tidewater</h1>' +
      '<p>Explorez la carte dans le moteur natif OpenRPG Forge : marchez sur la côte, entrez dans l’eau et découvrez ses paysages.</p>' +
      '<button class="button primary" type="button" id="launch-tidewater">Lancer l’île Tidewater →</button>' +
      '<p id="launch-tidewater-status" role="status">Vérification du jeu installé…</p>' +
      '<p class="notice">Cette promenade est une démonstration en cours de création. Les sauvegardes et la création de personnage ne sont pas encore disponibles.</p>';
    const button = panel.querySelector('#launch-tidewater');
    const status = panel.querySelector('#launch-tidewater-status');
    button.disabled = true;
    try {
      const result = await invoke('execute_launcher_command', { command: { kind: 'snapshot' } });
      if (location.hash !== '#aventures' || !button.isConnected) return;
      if (result.kind === 'snapshot' && result.runtime_connected) {
        button.disabled = false;
        status.textContent = 'L’île est prête à jouer.';
      } else {
        status.textContent = 'Les fichiers du jeu sont absents ou incomplets. Réextrayez toute l’archive ZIP.';
      }
    } catch (error) {
      status.textContent = `Impossible de vérifier le jeu : ${String(error)}`;
    }
  }

  main.addEventListener('click', async event => {
    const button = event.target.closest('#launch-tidewater');
    if (!button || button.disabled) return;
    const status = document.getElementById('launch-tidewater-status');
    button.disabled = true;
    status.textContent = 'Ouverture de l’île…';
    try {
      const result = await invoke('execute_launcher_command', { command: { kind: 'request-launch' } });
      if (result.kind === 'launch-started') {
        status.textContent = 'L’île Tidewater est ouverte dans une nouvelle fenêtre.';
      } else {
        status.textContent = result.reason || 'Le jeu n’a pas pu démarrer.';
      }
      statusText.textContent = status.textContent;
    } catch (error) {
      status.textContent = `Le jeu n’a pas pu démarrer : ${String(error)}`;
      statusText.textContent = status.textContent;
    } finally {
      button.disabled = false;
    }
  });

  const homeNote = document.querySelector('.home-note');
  if (homeNote) homeNote.textContent = 'L’île Tidewater est disponible depuis le menu Jouer.';
  window.addEventListener('hashchange', () => {
    queueMicrotask(() => {
      showAdventure();
      const note = document.querySelector('.home-note');
      if (note) note.textContent = 'L’île Tidewater est disponible depuis le menu Jouer.';
    });
  });
  showAdventure();
})();
