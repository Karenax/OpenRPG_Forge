/* Desktop-only bridge for the unchanged L’Aube des royaumes presentation. */
(() => {
  'use strict';
  const invoke = window.__TAURI__?.core?.invoke;
  if (typeof invoke !== 'function') return;

  const main = document.getElementById('main');
  const statusText = document.getElementById('announcer');

  const exitStyle = document.createElement('style');
  exitStyle.textContent = `
    #launcher-exit-dialog { color: #263e46; background: #f5f0df; border: 1px solid #bca87d;
      border-radius: 8px; padding: 30px; width: min(480px, calc(100vw - 48px)); box-sizing: border-box;
      box-shadow: 0 18px 80px #0008; }
    #launcher-exit-dialog::backdrop { background: #071b27aa; }
    #launcher-exit-dialog h2 { font: 30px Georgia, serif; margin: 0 0 16px; }
    #launcher-exit-dialog p { margin: 0 0 24px; }
    #launcher-exit-dialog .exit-actions { display: flex; justify-content: flex-end; gap: 12px; }
    #launcher-exit-dialog button { font: inherit; padding: 10px 20px; border: 1px solid #a58a5e;
      border-radius: 4px; background: #eee8d6; color: #263e46; cursor: pointer; }
    #launcher-exit-dialog #launcher-exit-confirm { background: #a57439; color: #fff; }
    #launcher-exit-dialog button:focus-visible { outline: 3px solid #39718a; outline-offset: 3px; }
    #launcher-exit-dialog button:disabled { opacity: .6; cursor: wait; }
    #launcher-exit-error:empty { display: none; }
    #launcher-exit-dialog #launcher-exit-error { margin-top: 20px; margin-bottom: 0; }
  `;
  document.head.append(exitStyle);
  const exitDialog = document.createElement('dialog');
  exitDialog.id = 'launcher-exit-dialog';
  exitDialog.setAttribute('aria-labelledby', 'launcher-exit-title');
  exitDialog.setAttribute('aria-describedby', 'launcher-exit-description');
  exitDialog.innerHTML = '<h2 id="launcher-exit-title">Quitter le launcher ?</h2>' +
    '<p id="launcher-exit-description">Voulez-vous fermer OpenRPG Forge ?</p>' +
    '<div class="exit-actions"><button type="button" id="launcher-exit-cancel" autofocus>Annuler</button>' +
    '<button type="button" id="launcher-exit-confirm">Quitter</button></div>' +
    '<p id="launcher-exit-error" role="alert"></p>';
  document.body.append(exitDialog);
  const exitCancel = exitDialog.querySelector('#launcher-exit-cancel');
  const exitConfirm = exitDialog.querySelector('#launcher-exit-confirm');
  const exitError = exitDialog.querySelector('#launcher-exit-error');
  let previousFocus;
  let closing = false;
  function cancelExit() {
    if (closing) return;
    exitDialog.close();
    if (previousFocus?.isConnected) previousFocus.focus();
  }
  document.addEventListener('keydown', event => {
    if (event.key !== 'Escape' || event.repeat) return;
    event.preventDefault();
    if (exitDialog.open) { cancelExit(); return; }
    previousFocus = document.activeElement;
    exitError.textContent = '';
    exitDialog.showModal();
    exitCancel.focus();
  });
  exitDialog.addEventListener('cancel', event => { event.preventDefault(); cancelExit(); });
  exitCancel.addEventListener('click', cancelExit);
  exitConfirm.addEventListener('click', async () => {
    if (closing) return;
    closing = true;
    exitConfirm.disabled = true;
    exitCancel.disabled = true;
    try {
      await invoke('execute_launcher_command', { command: { kind: 'window', action: 'close' } });
    } catch (error) {
      closing = false;
      exitConfirm.disabled = false;
      exitCancel.disabled = false;
      exitError.textContent = `La fermeture a échoué : ${String(error)}`;
      exitCancel.focus();
    }
  });

  const readyLaunchButtons = new WeakSet();
  const moduleChoices = new WeakMap();
  let launching = false;

  function updateLaunchButtons() {
    for (const button of main.querySelectorAll('#launch-tidewater, .launch-module')) {
      button.disabled = launching || !readyLaunchButtons.has(button);
    }
  }

  function appendText(parent, tag, text, className) {
    const element = document.createElement(tag);
    element.textContent = text;
    if (className) element.className = className;
    parent.append(element);
    return element;
  }

  async function loadModules(container, refresh) {
    refresh.disabled = true;
    container.replaceChildren();
    const status = appendText(container, 'p', 'Vérification des modules installés…');
    status.setAttribute('role', 'status');
    try {
      const result = await invoke('execute_launcher_command', { command: { kind: 'list-modules' } });
      if (!container.isConnected) return;
      if (result.kind !== 'modules') throw new Error(result.reason || 'Inventaire indisponible.');
      status.textContent = result.modules.length ? 'Choisissez un module et sa carte.' :
        'Aucun module prêt à jouer dans cette installation.';
      const list = document.createElement('ul');
      list.className = 'utility-list';
      for (const module of result.modules) {
        const item = document.createElement('li');
        appendText(item, 'h3', module.name);
        appendText(item, 'p', `Version ${module.version}`);
        if (!module.zones.length) {
          appendText(item, 'p', 'Ce module ne contient aucune carte jouable.');
          list.append(item);
          continue;
        }
        const field = document.createElement('div');
        field.className = 'setting';
        const label = appendText(field, 'label', 'Carte à jouer');
        const select = document.createElement('select');
        select.id = `installed-module-zone-${list.children.length}`;
        label.htmlFor = select.id;
        for (const index of module.zones.keys()) {
          const option = appendText(select, 'option', `Carte ${index + 1}`);
          option.value = String(index);
        }
        field.append(select);
        item.append(field);
        const button = appendText(item, 'button', 'Lancer cette carte →', 'button primary launch-module');
        button.type = 'button';
        button.setAttribute('aria-label', `Lancer une carte de ${module.name}`);
        const launchStatus = appendText(item, 'p', 'Ce module est installé.');
        launchStatus.setAttribute('role', 'status');
        moduleChoices.set(button, { module, select, status: launchStatus });
        readyLaunchButtons.add(button);
        button.disabled = launching;
        list.append(item);
      }
      container.append(list);
      if (result.rejected.length) {
        const details = document.createElement('details');
        appendText(details, 'summary', `Modules indisponibles (${result.rejected.length})`);
        const rejected = document.createElement('ul');
        for (const entry of result.rejected) {
          appendText(rejected, 'li', `${entry.pack_id} : ${entry.reason}`);
        }
        details.append(rejected);
        container.append(details);
      }
    } catch (error) {
      if (container.isConnected) status.textContent = `Impossible de lire les modules : ${String(error)}`;
    } finally {
      if (refresh.isConnected) refresh.disabled = false;
    }
  }

  async function showAdventure() {
    if (location.hash !== '#aventures') return;
    const panel = main.querySelector('.utility');
    if (!panel) return;
    panel.innerHTML = '<p class="eyebrow">Vos aventures</p>' +
      '<h1>L’île Tidewater</h1>' +
      '<p>Explorez la carte dans le moteur natif OpenRPG Forge : marchez sur la côte, entrez dans l’eau et découvrez ses paysages.</p>' +
      '<button class="button primary" type="button" id="launch-tidewater">Lancer l’île Tidewater →</button>' +
      '<p id="launch-tidewater-status" role="status">Vérification du jeu installé…</p>' +
      '<p class="notice">Cette promenade est une démonstration en cours de création. Les sauvegardes et la création de personnage ne sont pas encore disponibles.</p>' +
      '<h2>Modules installés</h2>' +
      '<button class="button" type="button" id="refresh-installed-modules">Actualiser les modules</button>' +
      '<div id="installed-modules" aria-label="Modules installés"></div>';
    const button = panel.querySelector('#launch-tidewater');
    const status = panel.querySelector('#launch-tidewater-status');
    button.disabled = true;
    const modules = panel.querySelector('#installed-modules');
    const refresh = panel.querySelector('#refresh-installed-modules');
    refresh.addEventListener('click', () => loadModules(modules, refresh));
    loadModules(modules, refresh);
    try {
      const result = await invoke('execute_launcher_command', { command: { kind: 'snapshot' } });
      if (location.hash !== '#aventures' || !button.isConnected) return;
      if (result.kind === 'snapshot' && result.runtime_connected) {
        readyLaunchButtons.add(button);
        button.disabled = launching;
        status.textContent = 'L’île est prête à jouer.';
      } else {
        status.textContent = result.reason || 'Les fichiers de l’île sont absents ou incomplets. Vérifiez l’installation.';
      }
    } catch (error) {
      if (status.isConnected) status.textContent = `Impossible de vérifier le jeu : ${String(error)}`;
    }
  }

  main.addEventListener('click', async event => {
    const button = event.target.closest('#launch-tidewater, .launch-module');
    if (!button || button.disabled || launching) return;
    const choice = moduleChoices.get(button);
    const selection = choice?.module.zones[choice.select.selectedIndex];
    if (choice && !selection) return;
    const status = choice?.status || document.getElementById('launch-tidewater-status');
    launching = true;
    updateLaunchButtons();
    status.textContent = choice ? `Ouverture de ${choice.module.name}…` : 'Ouverture de l’île…';
    try {
      const command = choice ? { kind: 'request-module-launch', selection } : { kind: 'request-launch' };
      const result = await invoke('execute_launcher_command', { command });
      if (result.kind === 'launch-started') {
        status.textContent = choice ? `${choice.module.name} est ouvert dans une nouvelle fenêtre.` :
          'L’île Tidewater est ouverte dans une nouvelle fenêtre.';
      } else {
        status.textContent = result.reason || 'Le jeu n’a pas pu démarrer.';
      }
      statusText.textContent = status.textContent;
    } catch (error) {
      status.textContent = `Le jeu n’a pas pu démarrer : ${String(error)}`;
      statusText.textContent = status.textContent;
    } finally {
      launching = false;
      updateLaunchButtons();
    }
  });

  const homeNote = document.querySelector('.home-note');
  if (homeNote) homeNote.textContent = 'L’île Tidewater et vos modules installés sont disponibles depuis le menu Jouer.';
  window.addEventListener('hashchange', () => {
    queueMicrotask(() => {
      showAdventure();
      const note = document.querySelector('.home-note');
      if (note) note.textContent = 'L’île Tidewater et vos modules installés sont disponibles depuis le menu Jouer.';
    });
  });
  showAdventure();
})();
