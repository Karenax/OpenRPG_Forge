/* Presentation adapter only; all plans, validation and mutations live in Rust. */
const invoke = window.__TAURI__.core.invoke;
const $ = id => document.getElementById(id);
let pending = null;
let currentRoot = '';
let destinationDirty = false;
let busy = false;
let installed = false;
let hasRollback = false;
let subtitleOverride = null;
const kind = {applications:'Applications',assets:'Assets',module:'Module'};
const sizes = n => `${(n / 1024 / 1024).toFixed(1)} Mio`;
const kindLabel = value => kind[value] || 'Contenu Forge';

function item(list, label, detail) {
  const li = document.createElement('li');
  const name = document.createElement('strong');
  name.textContent = label;
  const meta = document.createElement('span');
  meta.textContent = detail;
  li.append(name, meta);
  list.append(li);
  return li;
}

function moduleActions(row, module) {
  row.style.flexWrap = 'wrap';
  const actions = document.createElement('div');
  actions.style.cssText = 'display:flex;flex-wrap:wrap;gap:8px;flex-basis:100%';
  module.zones.forEach((zone, index) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'button secondary';
    button.dataset.moduleLaunch = module.module_id;
    button.textContent = module.zones.length === 1 ? 'Jouer ce module' : `Jouer la zone ${index + 1}`;
    button.title = `Carte ${zone.area_id}`;
    button.onclick = () => task(async () => {
      await call({action:'launch_module',selection:{module_id:module.module_id,area_id:zone.area_id}});
      subtitleOverride = `${module.name} est ouvert dans le jeu séparé.`;
    }, `Ouverture de ${module.name}…`);
    actions.append(button);
  });
  row.append(actions);
}

async function call(request) {
  return invoke('delivery', {request});
}

function clearPreview() {
  pending = null;
  $('preview').hidden = true;
  $('changes').replaceChildren();
  $('summary').textContent = '';
  updateActionStates();
}

function updateActionStates() {
  const blocked = busy || destinationDirty;
  $('play').disabled = blocked || !installed;
  $('edit').disabled = blocked || !installed;
  $('select').disabled = blocked;
  $('bundled').disabled = blocked;
  $('verify').disabled = blocked || !installed;
  $('rollback').disabled = blocked || !hasRollback;
  $('apply').disabled = blocked || !pending;
  $('cancel').disabled = busy || !pending;
  $('choose-root').disabled = busy;
  $('set-root').disabled = busy || !destinationDirty;
  $('root-path').disabled = busy;
  document.querySelectorAll('[data-module-launch]').forEach(button => { button.disabled = blocked || !installed; });
}

function acceptRootResult(result) {
  if (typeof result.root !== 'string') return;
  currentRoot = result.root;
  destinationDirty = false;
  $('root-path').value = currentRoot;
  if (result.plan_invalidated) {
    clearPreview();
    subtitleOverride = null;
  }
}

async function task(action, message) {
  subtitleOverride = null;
  $('error').hidden = true;
  $('status').textContent = message;
  busy = true;
  updateActionStates();
  try {
    await action();
  } catch (error) {
    $('error').textContent = String(error);
    $('error').hidden = false;
  } finally {
    try {
      await refresh();
    } finally {
      busy = false;
      updateActionStates();
    }
  }
}

async function refresh() {
  try {
    const state = await call({action:'status'});
    installed = Boolean(state.installed);
    $('bundled').hidden = installed;
    $('content-title').textContent = installed ? 'Gérer le contenu' : 'Préparer le contenu';
    hasRollback = installed && Boolean(state.state && state.state.previous);
    if (!destinationDirty) {
      currentRoot = state.root;
      $('root-path').value = currentRoot;
    }
    $('location').textContent = `Dossier actuel · ${state.root}`;
    $('destination-status').textContent = destinationDirty
      ? 'Modifiez le dossier puis validez pour continuer.'
      : `Les fichiers seront conservés dans ${state.root}.`;
    $('packs').replaceChildren();
    if (installed) {
      const modules = await call({action:'modules'});
      const admitted = new Map((Array.isArray(modules.modules) ? modules.modules : []).map(module => [module.module_id,module]));
      const rejected = new Map((Array.isArray(modules.rejected) ? modules.rejected : []).map(module => [module.pack_id,module.reason]));
      $('title').textContent = 'Gérer votre installation';
      $('subtitle').textContent = subtitleOverride || 'Ajoutez des contenus, vérifiez les fichiers ou revenez à une version précédente.';
      $('version').textContent = `Installation ${state.state.revision}`;
      $('status').textContent = 'Installation prête · contenus séparés';
      (Array.isArray(state.packs) ? state.packs : []).forEach(pack => {
        const version = pack.version ? `Version ${pack.version}` : '';
        const module = admitted.get(pack.id);
        const row = item($('packs'), module ? module.name : pack.label || 'Contenu Forge', [kindLabel(pack.kind), version].filter(Boolean).join(' · '));
        if (module) moduleActions(row,module);
        const problem = rejected.get(pack.id) || pack.problem;
        if (problem) {
          const diagnostic = document.createElement('p');
          diagnostic.textContent = `Contenu indisponible : ${problem}`;
          diagnostic.style.cssText = 'flex-basis:100%;overflow-wrap:anywhere';
          row.style.flexWrap = 'wrap';
          row.append(diagnostic);
        }
      });
    } else {
      $('title').innerHTML = 'Installer et gérer<br><em>vos contenus.</em>';
      $('subtitle').textContent = 'Ajoutez des packages et vérifiez votre installation Forge.';
      subtitleOverride = null;
      $('version').textContent = 'Installation locale';
      $('status').textContent = 'Préparez la livraison fournie ou ajoutez vos contenus Forge.';
    }
    updateActionStates();
  } catch (error) {
    installed = false;
    hasRollback = false;
    $('error').textContent = String(error);
    $('error').hidden = false;
    updateActionStates();
  }
}

function preview(response) {
  pending = response.plan;
  $('preview').hidden = false;
  $('changes').replaceChildren();
  $('summary').textContent = pending.operation === 'rollback'
    ? `La version précédente sera restaurée dans ${pending.root}. Vos projets et vos sauvegardes seront conservés.`
    : `${response.packages.length} contenu(s) seront vérifiés et installés dans ${pending.root}.`;
  response.packages.forEach(pack => {
    const version = pack.version ? `Version ${pack.version}` : '';
    item($('changes'), pack.label || 'Contenu Forge', [kindLabel(pack.kind), version, sizes(pack.bytes), `${pack.files} fichiers`].filter(Boolean).join(' · '));
  });
  updateActionStates();
}

$('play').onclick = () => task(() => call({action:'launch',app:'game'}), 'Démarrage du jeu…');
$('edit').onclick = () => task(() => call({action:'launch',app:'editor'}), 'Ouverture de l’éditeur…');
$('verify').onclick = () => task(async () => {
  await call({action:'verify'});
  $('subtitle').textContent = 'Vérification terminée : tous les fichiers correspondent à leur contenu.';
  subtitleOverride = 'Vérification terminée : tous les fichiers correspondent à leur contenu.';
}, 'Vérification des fichiers…');
$('select').onclick = () => task(async () => {
  const files = await call({action:'select'});
  if (files.length) preview(await call({action:'prepare',files}));
}, 'Sélection des contenus…');
$('bundled').onclick = () => task(async () => {
  const {files} = await call({action:'bundled'});
  if (!files.length) throw 'Aucune livraison jointe. Ajoutez des contenus Forge.';
  preview(await call({action:'prepare',files}));
}, 'Vérification de la livraison…');
$('choose-root').onclick = () => task(async () => {
  const result = await call({action:'select_root'});
  if (result.cancelled) {
    $('destination-status').textContent = 'La destination reste inchangée.';
    return;
  }
  acceptRootResult(result);
}, 'Choisissez un dossier…');
$('set-root').onclick = () => task(async () => {
  const result = await call({action:'set_root',path:$('root-path').value});
  acceptRootResult(result);
}, 'Mise à jour du dossier…');
$('rollback').onclick = () => task(async () => preview(await call({action:'rollback'})), 'Préparation du retour…');
$('apply').onclick = () => task(async () => {
  if (!pending) throw 'Préparez un contenu avant installation.';
  await call({action:'apply',command_id:pending.command_id});
  clearPreview();
}, 'Installation en cours…');
$('cancel').onclick = clearPreview;
$('root-path').addEventListener('input', () => {
  const dirty = $('root-path').value !== currentRoot;
  if (dirty && !destinationDirty) {
    clearPreview();
    subtitleOverride = null;
  }
  destinationDirty = dirty;
  updateActionStates();
});
refresh();
