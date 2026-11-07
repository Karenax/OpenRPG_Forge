/* Shared native desktop bridge for installed launchers. */
(() => {
  'use strict';
  const standalone = document.documentElement.dataset.forgeLauncher === 'standalone';
  const invoke = window.__TAURI__?.core?.invoke;
  if (typeof invoke !== 'function') {
    if (standalone) {
      const status = document.querySelector('#main [role="status"]');
      if (status) status.textContent = 'Le launcher ne peut pas accéder au jeu. Fermez-le puis réessayez.';
    }
    return;
  }

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
    `<p id="launcher-exit-description">${standalone ? 'Voulez-vous fermer le launcher ?' : 'Voulez-vous fermer OpenRPG Forge ?'}</p>` +
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
      const response = await invoke('execute_launcher_command', { command: { kind: 'window', action: 'close' } });
      if (response?.kind !== 'window-action-accepted') {
        throw new Error(response?.reason || 'La fenêtre est restée ouverte.');
      }
    } catch (error) {
      closing = false;
      exitConfirm.disabled = false;
      exitCancel.disabled = false;
      exitError.textContent = `La fermeture a échoué : ${String(error)}`;
      exitCancel.focus();
    }
  });

  function appendText(parent, tag, text, className) {
    const element = document.createElement(tag);
    element.textContent = text;
    if (className) element.className = className;
    parent.append(element);
    return element;
  }

  if (!standalone) {
    window.ForgeDesktop = Object.freeze({invoke, main, statusText, appendText});
    return;
  }
  startStandalone();

  function startStandalone() {
    const brand = document.getElementById('launcher-brand');
    const footer = document.getElementById('launcher-footer');
    const selections = new WeakMap();
    let launching = false;
    let trackedLaunch = null;
    let game = null;
    let problem = '';
    let loading = false;
    let generation = 0;
    let installing = true;
    let launchStatus = 'Prêt à jouer.';
    let rollbackStage = 'idle';
    let rollbackMessage = '';
    let rollbackError = '';
    let rollbackBusy = false;
    let restartNeeded = false;
    let uninstallStage = 'idle';
    let uninstallBusy = false;
    let uninstallError = '';
    let handoffPath = '';
    let legacyInstallationFound = false;
    let legacyBusy = false;

    function entryZone() {
      return game.modules.flatMap(module => module.zones).find(zone =>
        zone.selection.module_id === game.entry.module_id && zone.selection.area_id === game.entry.area_id);
    }

    function zoneFor(choice) {
      return choice?.module ? choice.module.zones[choice.select.selectedIndex] : entryZone();
    }

    function updateControls() {
      for (const control of main.querySelectorAll('button')) {
        if (control.id === 'refresh-game') {
          control.disabled = launching;
          continue;
        }
        if (!selections.has(control)) continue;
        const choice = selections.get(control);
        const zone = zoneFor(choice);
        const resume = !!choice?.resume;
        control.hidden = resume && zone?.resume.status !== 'ready';
        control.disabled = launching || legacyBusy || trackedLaunch !== null || !zone || zone.pending_resume === null || (resume
          ? zone.resume.status !== 'ready'
          : zone.pending_resume !== false || zone.resume.status !== 'absent');
      }
    }

    function resumeDescription(zone) {
      if (!zone) return 'Cette carte est indisponible.';
      if (zone.pending_resume === null) return zone.resume.status === 'unavailable'
        ? zone.resume.reason : 'Impossible de vérifier la demande de reprise.';
      if (zone.pending_resume && zone.resume.status !== 'ready') return 'Une reprise est en attente mais sa sauvegarde est indisponible.';
      if (zone.pending_resume) return 'Une reprise est en attente. Choisissez Reprendre.';
      if (zone.resume.status === 'ready') return 'Une sauvegarde existe. Choisissez Reprendre pour préserver votre progression.';
      if (zone.resume.status === 'unavailable') return zone.resume.reason;
      return '';
    }

    function button(parent, text, className) {
      const result = appendText(parent, 'button', text, className);
      result.type = 'button';
      return result;
    }

    function setLaunchStatus(message) {
      if (message === launchStatus) return;
      launchStatus = message;
      const status = document.getElementById('standalone-launch-status');
      if (status) status.textContent = message;
      statusText.textContent = message;
    }

    function scheduleLaunchObservation(launchId, delay = 2000) {
      setTimeout(() => observeLaunch(launchId), delay);
    }

    async function observeLaunch(launchId) {
      if (trackedLaunch !== launchId) return;
      try {
        const response = await invoke('execute_launcher_command', {command: {kind: 'game-status', launch_id: launchId}});
        if (trackedLaunch !== launchId) return;
        if (response.kind !== 'game-status' || response.launch_id !== launchId || !response.status) {
          throw new Error(response.reason || 'Réponse de suivi invalide.');
        }
        if (response.status.state === 'running') {
          setLaunchStatus('Lancement demandé. Vous pouvez revenir ici après avoir fermé le jeu.');
          scheduleLaunchObservation(launchId);
          return;
        }
        if (!['exited', 'superseded'].includes(response.status.state)) throw new Error('État du jeu inconnu.');
        trackedLaunch = null;
        if (response.status.state === 'superseded') {
          setLaunchStatus('Le suivi de ce lancement a changé. Les sauvegardes vont être vérifiées.');
        } else if (response.status.success) {
          setLaunchStatus('Le programme du jeu s’est arrêté. Les sauvegardes vont être vérifiées.');
        } else {
          setLaunchStatus('Le programme du jeu s’est arrêté avec une erreur. Consultez le journal game dans le dossier de données utilisateur.');
        }
        await refresh();
      } catch (error) {
        if (trackedLaunch !== launchId) return;
        setLaunchStatus(`Impossible de suivre le jeu : ${String(error)}. Nouvel essai automatique.`);
        scheduleLaunchObservation(launchId, 5000);
      }
    }

    function render() {
      main.replaceChildren();
      const panel = appendText(main, 'section', '', 'utility');
      if (handoffPath) {
        appendText(panel, 'h1', 'Désinstallation ouverte');
        appendText(panel, 'p', 'Poursuivez dans la fenêtre de désinstallation. Si nécessaire, rouvrez ce programme :');
        appendText(panel, 'p', handoffPath);
        if (uninstallError) {
          const alert = appendText(panel, 'p', uninstallError);
          alert.setAttribute('role', 'alert');
        }
        return;
      }
      if (restartNeeded) {
        const heading = appendText(panel, 'h1', 'Version précédente restaurée');
        heading.tabIndex = -1;
        const status = appendText(panel, 'p', 'Vos sauvegardes sont conservées. Fermez et rouvrez le lanceur pour jouer.');
        status.setAttribute('role', 'status');
        queueMicrotask(() => { if (heading.isConnected) heading.focus({preventScroll: true}); });
        return;
      }
      if (!game) {
        appendText(panel, 'h1', 'Votre jeu');
        const status = appendText(panel, 'p', loading
          ? 'Vérification du jeu installé…'
          : 'Le jeu est indisponible. Vérifiez son installation puis réessayez.');
        status.setAttribute('role', loading ? 'status' : 'alert');
        if (!loading) {
          button(panel, 'Réessayer', 'button primary').addEventListener('click', refreshOnDemand);
          if (problem) {
            const details = document.createElement('details');
            appendText(details, 'summary', 'Détails du problème');
            appendText(details, 'p', problem);
            panel.append(details);
          }
        }
        return;
      }
      const page = ['aventures', 'reglages'].includes(location.hash.slice(1))
        ? location.hash.slice(1) : 'accueil';
      document.querySelectorAll('.main-nav a').forEach(link => {
        if (link.hash === `#${page}`) link.setAttribute('aria-current', 'page');
        else link.removeAttribute('aria-current');
      });
      document.title = game.title;
      const visuals = game.presentation || {};
      const raster = value => typeof value === 'string' && /^data:image\/(png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(value);
      document.body.style.backgroundImage = raster(visuals.background)
        ? `linear-gradient(#10211fcc, #10211fcc), url("${visuals.background}")` : '';
      document.body.style.backgroundSize = 'cover';
      let icon = document.getElementById('game-presentation-icon');
      if (raster(visuals.icon)) {
        if (!icon) { icon = document.createElement('link'); icon.id = 'game-presentation-icon'; icon.rel = 'icon'; document.head.append(icon); }
        icon.href = visuals.icon;
      } else icon?.remove();
      if (page === 'reglages') {
        appendText(panel, 'h1', 'Réglages');
        appendText(panel, 'p', 'La qualité graphique se règle dans le jeu avec F2.');
        appendText(panel, 'p', 'Les réglages audio, d’affichage et d’accessibilité ne sont pas encore appliqués depuis ce launcher.');
        renderLegacySave(panel);
        renderRollback(panel);
        renderUninstall(panel);
        if (launchStatus !== 'Prêt à jouer.') {
          const status = appendText(panel, 'p', launchStatus);
          status.setAttribute('role', 'status');
          status.id = 'standalone-launch-status';
        }
        return;
      }
      appendText(panel, 'h1', page === 'accueil' ? game.title : 'Jouer');
      if (page === 'accueil' && raster(visuals.logo)) {
        const logo = document.createElement('img');
        logo.src = visuals.logo;
        logo.alt = `Logo de ${game.title}`;
        logo.style.cssText = 'display:block;max-width:100%;max-height:160px;object-fit:contain;margin-bottom:24px';
        panel.prepend(logo);
      }
      if (page === 'accueil' && game.summary) appendText(panel, 'p', game.summary);
      const start = button(panel, 'Jouer', 'button primary');
      selections.set(start, null);
      const resumeEntry = button(panel, 'Reprendre', 'button');
      selections.set(resumeEntry, {resume: true});
      const notice = resumeDescription(entryZone());
      const status = appendText(panel, 'p', launchStatus === 'Prêt à jouer.' && notice ? notice : launchStatus);
      status.setAttribute('role', 'status');
      status.id = 'standalone-launch-status';
      if (notice && launchStatus !== 'Prêt à jouer.') {
        const entryNotice = appendText(panel, 'p', notice);
        entryNotice.setAttribute('role', 'status');
      }
      const refreshButton = button(panel, 'Actualiser', 'button');
      refreshButton.id = 'refresh-game';
      refreshButton.addEventListener('click', refreshOnDemand);
      if (page === 'accueil') {
        updateControls();
        return;
      }
      appendText(panel, 'h2', 'Choisir une carte');
      const list = document.createElement('ul');
      list.className = 'utility-list';
      for (const module of game.modules) {
        const item = document.createElement('li');
        appendText(item, 'h3', module.name);
        const field = document.createElement('div');
        field.className = 'setting';
        const label = appendText(field, 'label', 'Carte');
        const select = document.createElement('select');
        select.id = `game-zone-${list.children.length}`;
        label.htmlFor = select.id;
        for (const [index, zone] of module.zones.entries()) {
          const option = appendText(select, 'option', zone.name);
          option.value = String(index);
          if (zone.selection.module_id === game.entry.module_id && zone.selection.area_id === game.entry.area_id) {
            option.selected = true;
          }
        }
        field.append(select);
        item.append(field);
        const play = button(item, 'Jouer cette carte', 'button');
        selections.set(play, {module, select});
        const resume = button(item, 'Reprendre cette carte', 'button');
        selections.set(resume, {module, select, resume: true});
        const saveStatus = appendText(item, 'p', resumeDescription(module.zones[select.selectedIndex]));
        saveStatus.setAttribute('role', 'status');
        select.addEventListener('change', () => {
          saveStatus.textContent = resumeDescription(module.zones[select.selectedIndex]);
          updateControls();
        });
        list.append(item);
      }
      panel.append(list);
      updateControls();
    }

    function renderLegacySave(panel) {
      if (!legacyInstallationFound) return;
      const choices = game.modules.flatMap(module => module.zones
        .filter(zone => zone.resume.status === 'absent' && zone.pending_resume === false)
        .map(zone => ({selection: zone.selection, label: `${module.name} — ${zone.name}`})));
      if (!choices.length) return;
      appendText(panel, 'h2', 'Ancienne sauvegarde');
      const field = appendText(panel, 'div', '', 'setting');
      const label = appendText(field, 'label', 'Carte');
      const select = document.createElement('select');
      select.id = 'legacy-save-zone';
      label.htmlFor = select.id;
      for (const [index, choice] of choices.entries()) {
        appendText(select, 'option', choice.label).value = String(index);
      }
      field.append(select);
      const review = button(panel, 'Rechercher une sauvegarde', 'button');
      const apply = button(panel, 'Importer la sauvegarde', 'button');
      const status = appendText(panel, 'p', '');
      status.setAttribute('role', 'status');
      let reviewed = null;
      const blocked = () => legacyBusy || rollbackBusy || uninstallBusy || launching || trackedLaunch !== null;
      function update() {
        review.disabled = blocked();
        select.disabled = blocked();
        apply.hidden = !reviewed;
        apply.disabled = blocked() || !reviewed;
      }
      select.addEventListener('change', () => {
        reviewed = null;
        status.textContent = '';
        update();
      });
      review.addEventListener('click', async () => {
        if (blocked()) return;
        const {selection} = choices[select.selectedIndex];
        reviewed = null;
        legacyBusy = true;
        status.textContent = 'Recherche de la sauvegarde…';
        update();
        try {
          const result = await invoke('execute_launcher_command', {command: {kind: 'review-legacy-save', selection}});
          if (result.kind !== 'legacy-save-reviewed') throw new Error(result.reason || 'Sauvegarde indisponible.');
          reviewed = {selection, saved_hash: result.saved_hash};
          status.textContent = 'Sauvegarde compatible trouvée. Vous pouvez l’importer dans ce jeu.';
        } catch (error) {
          status.textContent = error.message || String(error);
        } finally {
          legacyBusy = false;
          if (panel.isConnected) {
            update();
            (reviewed ? apply : review).focus({preventScroll: true});
          } else render();
        }
      });
      apply.addEventListener('click', async () => {
        if (blocked() || !reviewed) return;
        legacyBusy = true;
        status.textContent = 'Importation de la sauvegarde…';
        update();
        try {
          const result = await invoke('execute_launcher_command', {command: {kind: 'import-legacy-save', resume: reviewed}});
          if (result.kind !== 'legacy-save-imported') throw new Error(result.reason || 'Importation impossible.');
          legacyBusy = false;
          setLaunchStatus('Sauvegarde importée. Ouvrez Jouer et choisissez Reprendre cette carte.');
          await refresh();
          const notice = document.getElementById('standalone-launch-status');
          if (notice) { notice.tabIndex = -1; notice.focus({preventScroll: true}); }
        } catch (error) {
          reviewed = null;
          status.textContent = error.message || String(error);
        } finally {
          const failed = legacyBusy;
          legacyBusy = false;
          if (panel.isConnected) {
            update();
            if (failed) review.focus({preventScroll: true});
          } else if (failed) render();
        }
      });
      update();
    }

    function renderRollback(panel) {
      appendText(panel, 'h2', 'Version du jeu');
      if (rollbackStage === 'confirm') {
        appendText(panel, 'p', 'Restaurer la version précédente ? Vos sauvegardes seront conservées.');
        const cancel = button(panel, 'Annuler', 'button');
        cancel.disabled = rollbackBusy;
        cancel.addEventListener('click', () => {
          rollbackStage = 'idle'; rollbackError = ''; render();
          document.getElementById('restore-previous')?.focus({preventScroll: true});
        });
        if (!rollbackBusy) queueMicrotask(() => { if (cancel.isConnected) cancel.focus({preventScroll: true}); });
        const confirm = button(panel, 'Restaurer', 'button primary');
        confirm.disabled = rollbackBusy || uninstallBusy || launching || trackedLaunch !== null;
        confirm.addEventListener('click', applyRollback);
      } else {
        const prepare = button(panel, 'Restaurer la version précédente', 'button');
        prepare.id = 'restore-previous';
        prepare.disabled = rollbackBusy || uninstallBusy || launching || trackedLaunch !== null;
        prepare.addEventListener('click', prepareRollback);
      }
      if (rollbackBusy || rollbackMessage) {
        const status = appendText(panel, 'p', rollbackBusy ? 'Vérification en cours…' : rollbackMessage);
        status.setAttribute('role', 'status');
      }
      if (rollbackError) {
        const error = appendText(panel, 'p', rollbackError);
        error.setAttribute('role', 'alert');
        error.tabIndex = -1;
        queueMicrotask(() => { if (error.isConnected) error.focus({preventScroll: true}); });
      }
    }

    async function prepareRollback() {
      if (rollbackBusy || uninstallBusy || legacyBusy || launching || trackedLaunch !== null) return;
      uninstallStage = 'idle'; uninstallError = '';
      rollbackBusy = true; rollbackMessage = ''; rollbackError = ''; render();
      try {
        const response = await invoke('execute_launcher_command', {command: {kind: 'prepare-rollback'}});
        if (response.kind !== 'rollback-prepared') throw new Error(response.reason || 'Vérification impossible.');
        if (response.available) rollbackStage = 'confirm';
        else rollbackMessage = 'Aucune version précédente disponible.';
      } catch (error) {
        rollbackStage = 'idle'; rollbackError = `Restauration impossible : ${String(error)}`;
      } finally {
        rollbackBusy = false; render();
      }
    }

    async function applyRollback() {
      if (rollbackBusy || uninstallBusy || legacyBusy || launching || trackedLaunch !== null || rollbackStage !== 'confirm') return;
      rollbackBusy = true; rollbackError = ''; render();
      try {
        const response = await invoke('execute_launcher_command', {command: {kind: 'apply-rollback'}});
        if (response.kind !== 'rollback-completed') throw new Error(response.reason || 'Restauration impossible.');
        restartNeeded = true;
      } catch (error) {
        rollbackStage = 'idle'; rollbackError = `Restauration impossible : ${String(error)}. Vérifiez à nouveau la version précédente.`;
      } finally {
        rollbackBusy = false; render();
      }
    }

    function renderUninstall(panel) {
      appendText(panel, 'h2', 'Désinstaller le jeu');
      if (uninstallStage === 'confirm') {
        appendText(panel, 'p', 'Retirer les fichiers du jeu ? Vos sauvegardes seront conservées.');
        const cancel = button(panel, 'Annuler', 'button');
        cancel.disabled = uninstallBusy;
        cancel.addEventListener('click', () => {
          uninstallStage = 'idle'; uninstallError = ''; render();
          document.getElementById('uninstall-game')?.focus({preventScroll: true});
        });
        if (!uninstallBusy) queueMicrotask(() => { if (cancel.isConnected) cancel.focus({preventScroll: true}); });
        const confirm = button(panel, 'Ouvrir la désinstallation', 'button primary');
        confirm.disabled = uninstallBusy || launching || trackedLaunch !== null;
        confirm.addEventListener('click', startUninstall);
      } else {
        const prepare = button(panel, 'Désinstaller', 'button');
        prepare.id = 'uninstall-game';
        prepare.disabled = uninstallBusy || rollbackBusy || launching || trackedLaunch !== null;
        prepare.addEventListener('click', () => { rollbackStage = 'idle'; rollbackError = ''; uninstallStage = 'confirm'; uninstallError = ''; render(); });
      }
      if (uninstallBusy) {
        const status = appendText(panel, 'p', 'Ouverture de la désinstallation…');
        status.setAttribute('role', 'status');
      }
      if (uninstallError) {
        const alert = appendText(panel, 'p', uninstallError);
        alert.setAttribute('role', 'alert');
      }
    }

    async function startUninstall() {
      if (uninstallBusy || rollbackBusy || legacyBusy || launching || trackedLaunch !== null || uninstallStage !== 'confirm') return;
      uninstallBusy = true; uninstallError = ''; render();
      try {
        const response = await invoke('execute_launcher_command', {command: {kind: 'start-uninstall'}});
        if (response.kind !== 'uninstall-helper-started') throw new Error(response.reason || 'Ouverture impossible.');
        handoffPath = response.path;
        render();
        const closed = await invoke('execute_launcher_command', {command: {kind: 'window', action: 'close'}});
        if (closed.kind !== 'window-action-accepted') throw new Error(closed.reason || 'Fermez cette fenêtre avant de continuer.');
      } catch (error) {
        uninstallError = `Désinstallation impossible : ${String(error)}`;
      } finally {
        uninstallBusy = false; render();
      }
    }

    function refreshOnDemand() {
      if (legacyBusy) return;
      if (!launching && trackedLaunch === null) setLaunchStatus('Prêt à jouer.');
      return refresh();
    }

    async function refresh() {
      const request = ++generation;
      game = null;
      problem = '';
      loading = true;
      render();
      try {
        const response = await invoke('execute_launcher_command', { command: { kind: 'game-view' } });
        if (request !== generation) return;
        if (response.kind !== 'game-view' || !response.runtime_connected) {
          throw new Error(response.reason || 'La vérification du jeu a échoué.');
        }
        game = response.game;
        legacyInstallationFound = response.legacy_installation_found === true;
        brand.textContent = game.title;
        if (typeof game.presentation?.icon === 'string' && /^data:image\/(png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(game.presentation.icon)) {
          const icon = document.createElement('img');
          icon.src = game.presentation.icon;
          icon.alt = '';
          icon.style.cssText = 'width:28px;height:28px;object-fit:contain;vertical-align:middle;margin-right:12px';
          brand.prepend(icon);
        }
        brand.setAttribute('aria-label', `${game.title} — Accueil`);
        footer.textContent = game.title;
      } catch (error) {
        if (request !== generation) return;
        problem = String(error);
      } finally {
        if (request === generation) {
          loading = false;
          render();
        }
      }
    }

    main.addEventListener('click', async event => {
      const target = event.target.closest('button');
      if (!target || !selections.has(target) || target.disabled || launching || rollbackBusy || uninstallBusy || legacyBusy) return;
      const choice = selections.get(target);
      const zone = zoneFor(choice);
      if (!zone) return;
      launching = true;
      for (const control of main.querySelectorAll('button')) control.disabled = true;
      setLaunchStatus('Ouverture du jeu…');
      try {
        const command = choice?.resume
          ? {kind: 'request-resume', resume: {selection: zone.selection, saved_hash: zone.resume.saved_hash}}
          : choice?.module
            ? {kind: 'request-module-launch', selection: zone.selection}
            : {kind: 'request-entry-launch', selection: zone.selection};
        const response = await invoke('execute_launcher_command', {command});
        if (response.kind !== 'launch-started') throw new Error(response.reason || 'Le jeu n’a pas pu démarrer.');
        if (!Number.isSafeInteger(response.launch_id) || response.launch_id <= 0) {
          setLaunchStatus('Lancement demandé, mais son suivi est indisponible. Actualisez avant de réessayer.');
          return;
        }
        trackedLaunch = response.launch_id;
        setLaunchStatus('Lancement demandé. Vous pouvez revenir ici après avoir fermé le jeu.');
        scheduleLaunchObservation(trackedLaunch);
      } catch (error) {
        zone.pending_resume = null;
        setLaunchStatus(`Le jeu n’a pas pu démarrer : ${String(error)}. Actualisez avant de réessayer.`);
      } finally {
        launching = false;
        updateControls();
      }
    });
    window.addEventListener('hashchange', () => {
      if (installing) return;
      render();
      main.focus({preventScroll: true});
    });

    async function detectInstaller() {
      installing = true;
      document.querySelectorAll('.main-nav a').forEach(link => { link.hidden = true; });
      main.replaceChildren();
      const panel = appendText(main, 'section', '', 'utility');
      appendText(panel, 'h1', 'Votre jeu');
      const progress = appendText(panel, 'p', 'Vérification des fichiers du jeu…');
      progress.setAttribute('role', 'status');
      try {
        const response = await invoke('execute_launcher_command', {command: {kind: 'installer-status'}});
        if (response.kind === 'not-installer') {
          installing = false;
          document.querySelectorAll('.main-nav a').forEach(link => { link.hidden = false; });
          await refresh();
          return;
        }
        if (['uninstall-helper-ready', 'uninstall-completed', 'uninstall-needs-attention'].includes(response.kind)) {
          showUninstaller(response);
          return;
        }
        if (response.kind !== 'installer-ready') throw new Error(response.reason || 'Les fichiers du jeu sont indisponibles.');
        showInstaller(response);
      } catch (error) {
        main.replaceChildren();
        const failed = appendText(main, 'section', '', 'utility');
        const heading = appendText(failed, 'h1', 'Installation indisponible');
        heading.tabIndex = -1;
        const message = appendText(failed, 'p', `Impossible de vérifier les fichiers du jeu : ${String(error)}`);
        message.setAttribute('role', 'alert');
        button(failed, 'Réessayer', 'button primary').addEventListener('click', detectInstaller);
        heading.focus({preventScroll: true});
      }
    }

    function showUninstaller(initial) {
      let view = initial;
      let busy = false;
      let error = '';
      function renderUninstaller() {
        main.replaceChildren();
        const panel = appendText(main, 'section', '', 'utility');
        const done = view.kind === 'uninstall-completed';
        const attention = view.kind === 'uninstall-needs-attention';
        const heading = appendText(panel, 'h1', attention ? 'Vérification nécessaire' : done ? 'Jeu désinstallé' : `Désinstaller ${view.title}`);
        heading.tabIndex = -1;
        const description = appendText(panel, 'p', attention ? view.reason : done
          ? 'Vos sauvegardes sont conservées. Le nettoyage du programme temporaire sera demandé à la fermeture.'
          : 'Fermez les autres fenêtres du jeu. Vos sauvegardes seront conservées.');
        if (attention) description.setAttribute('role', 'alert');
        if (done && (view.preserved_files || view.preserved_packages)) {
          appendText(panel, 'p', 'Certains fichiers ont été conservés dans le dossier du jeu.');
        }
        if (done && view.counts_partial) {
          appendText(panel, 'p', 'Après l’interruption, le bilan des fichiers est partiel.');
        }
        appendText(panel, 'p', done || attention ? 'Programme temporaire :' : 'En cas d’interruption, rouvrez ce programme :');
        appendText(panel, 'p', view.path);
        const action = button(panel, done || attention ? 'Fermer' : 'Désinstaller', 'button primary');
        action.disabled = busy;
        action.addEventListener('click', attention ? closeUninstaller : done ? finishUninstaller : applyUninstall);
        if (busy) {
          const status = appendText(panel, 'p', done ? 'Fermeture…' : 'Désinstallation en cours…');
          status.setAttribute('role', 'status');
        }
        if (error) {
          const alert = appendText(panel, 'p', error);
          alert.setAttribute('role', 'alert');
          alert.tabIndex = -1;
          queueMicrotask(() => { if (alert.isConnected) alert.focus({preventScroll: true}); });
        } else if (!busy) {
          queueMicrotask(() => { if (heading.isConnected) heading.focus({preventScroll: true}); });
        }
      }
      async function applyUninstall() {
        if (busy) return;
        busy = true; error = ''; renderUninstaller();
        try {
          const response = await invoke('execute_launcher_command', {command: {kind: 'apply-uninstall'}});
          if (!['uninstall-completed', 'uninstall-needs-attention'].includes(response.kind)) throw new Error(response.reason || 'Désinstallation impossible.');
          view = response;
        } catch (reason) {
          error = `Impossible de poursuivre : ${String(reason)}. Conservez ce programme pour réessayer après vérification.`;
        } finally { busy = false; renderUninstaller(); }
      }
      async function finishUninstaller() {
        if (busy) return;
        busy = true; error = ''; renderUninstaller();
        try {
          const response = await invoke('execute_launcher_command', {command: {kind: 'cleanup-uninstall-helper'}});
          if (response.kind !== 'uninstall-cleanup-requested') throw new Error(response.reason || 'Nettoyage impossible.');
          const closed = await invoke('execute_launcher_command', {command: {kind: 'window', action: 'close'}});
          if (closed.kind !== 'window-action-accepted') throw new Error(closed.reason || 'Fermez cette fenêtre.');
        } catch (reason) {
          error = `Fermeture ou nettoyage temporaire impossible : ${String(reason)}. Le programme indiqué est conservé.`;
        } finally { busy = false; renderUninstaller(); }
      }
      async function closeUninstaller() {
        if (busy) return;
        busy = true; error = ''; renderUninstaller();
        try {
          const closed = await invoke('execute_launcher_command', {command: {kind: 'window', action: 'close'}});
          if (closed.kind !== 'window-action-accepted') throw new Error(closed.reason || 'Fermez cette fenêtre.');
        } catch (reason) { error = `Fermeture impossible : ${String(reason)}`; }
        finally { busy = false; renderUninstaller(); }
      }
      renderUninstaller();
    }

    function showInstaller(view) {
      let destination = view.destination;
      let stage = 'choose';
      let update = false;
      let busy = false;
      let busyMessage = '';
      let error = '';
      let launched = false;
      let alreadyInstalled = false;
      let playerEntry = null;
      let entryError = '';
      let needsRevalidation = false;
      brand.textContent = view.title;
      footer.textContent = view.title;
      document.title = `Installer ${view.title}`;

      async function run(command, expected, next) {
        if (busy) return;
        busy = true;
        busyMessage = command.kind === 'apply-install' ? 'Installation en cours…'
          : command.kind === 'launch-installed' ? 'Ouverture en cours…' : 'Vérification en cours…';
        error = '';
        renderInstaller();
        try {
          const response = await invoke('execute_launcher_command', {command});
          if (response.kind !== expected) throw new Error(response.reason || 'L’opération n’a pas abouti.');
          next(response);
        } catch (failure) {
          error = String(failure);
          if (command.kind === 'apply-install') stage = 'choose';
          if (['launch-installed', 'ensure-player-entry'].includes(command.kind)) needsRevalidation = true;
        } finally {
          busy = false;
          renderInstaller(true);
        }
      }

      function renderInstaller(focusHeading = false) {
        main.replaceChildren();
        const panel = appendText(main, 'section', '', 'utility');
        const heading = appendText(panel, 'h1', `Installer ${view.title}`);
        heading.tabIndex = -1;
        if (stage === 'choose') {
          appendText(panel, 'p', 'Choisissez où installer le jeu. Vos sauvegardes sont conservées séparément.');
          const form = document.createElement('form');
          const field = document.createElement('div');
          field.className = 'setting';
          const label = appendText(field, 'label', 'Dossier d’installation');
          label.htmlFor = 'install-destination';
          const input = document.createElement('input');
          input.id = 'install-destination';
          input.type = 'text';
          input.name = 'installation-directory';
          input.autocomplete = 'off';
          input.spellcheck = false;
          input.value = destination;
          input.disabled = busy;
          input.addEventListener('input', () => { destination = input.value; });
          field.append(input);
          form.append(field);
          const submit = appendText(form, 'button', 'Vérifier', 'button primary');
          submit.type = 'submit';
          submit.disabled = busy;
          form.addEventListener('submit', event => {
            event.preventDefault();
            destination = input.value;
            run({kind: 'prepare-install', destination}, 'install-prepared', response => {
              destination = response.destination;
              update = response.update;
              alreadyInstalled = response.already_installed === true;
              playerEntry = response.entry || null;
              entryError = response.entry_error || '';
              needsRevalidation = false;
              stage = alreadyInstalled ? 'complete' : 'confirm';
            });
          });
          panel.append(form);
        } else if (stage === 'confirm') {
          appendText(panel, 'p', update ? 'Une installation de ce jeu existe déjà. Elle sera mise à jour.' : 'Le jeu sera installé dans ce dossier :');
          appendText(panel, 'p', destination, 'notice');
          const confirm = button(panel, update ? 'Mettre à jour' : 'Installer', 'button primary');
          confirm.disabled = busy;
          confirm.addEventListener('click', () => run({kind: 'apply-install'}, 'install-completed', response => {
            playerEntry = response.entry || null;
            entryError = response.entry_error || '';
            needsRevalidation = false;
            stage = 'complete';
          }));
          const change = button(panel, 'Modifier le dossier', 'button');
          change.disabled = busy;
          change.addEventListener('click', () => { stage = 'choose'; renderInstaller(true); });
        } else {
          appendText(panel, 'p', alreadyInstalled ? 'Ce jeu est déjà installé.' : 'Installation terminée.');
          appendText(panel, 'p', destination, 'notice');
          if (playerEntry) {
            appendText(panel, 'p', 'Pour rouvrir le jeu plus tard, ouvrez ce fichier :');
            appendText(panel, 'p', playerEntry, 'notice');
          } else {
            const notice = appendText(panel, 'p', 'Le jeu est installé, mais son lanceur doit être créé ou réparé.');
            notice.setAttribute('role', entryError ? 'alert' : 'status');
            if (entryError) appendText(panel, 'p', entryError);
            const repair = button(panel, 'Créer ou réparer le lanceur', 'button');
            repair.disabled = busy || launched || needsRevalidation;
            repair.addEventListener('click', () => run({kind: 'ensure-player-entry'}, 'player-entry-ready', response => {
              playerEntry = response.path;
              entryError = '';
            }));
          }
          const open = button(panel, 'Ouvrir le jeu', 'button primary');
          open.disabled = busy || launched || needsRevalidation;
          open.addEventListener('click', () => run({kind: 'launch-installed'}, 'installed-launcher-started', () => { launched = true; }));
          if (needsRevalidation) {
            const verify = button(panel, 'Vérifier l’installation', 'button');
            verify.disabled = busy;
            verify.addEventListener('click', () => run({kind: 'reverify-installed'}, 'install-revalidated', response => {
              playerEntry = response.entry || null;
              entryError = response.entry_error || '';
              needsRevalidation = false;
            }));
          }
          if (alreadyInstalled) {
            const change = button(panel, 'Choisir un autre dossier', 'button');
            change.disabled = busy || launched;
            change.addEventListener('click', () => { stage = 'choose'; renderInstaller(true); });
          }
          if (launched) {
            const status = appendText(panel, 'p', 'Ouverture demandée.');
            status.setAttribute('role', 'status');
          }
        }
        if (busy) {
          const status = appendText(panel, 'p', busyMessage);
          status.setAttribute('role', 'status');
        }
        if (error) {
          const alert = appendText(panel, 'p', `L’opération a échoué : ${error}`);
          alert.setAttribute('role', 'alert');
          if (stage === 'choose') button(panel, 'Revérifier les fichiers', 'button').addEventListener('click', detectInstaller);
        }
        if (focusHeading) heading.focus({preventScroll: true});
      }
      renderInstaller();
    }
    detectInstaller();
  }

})();
