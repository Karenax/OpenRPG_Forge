const state = {
  meta: null,
  items: [],
  total: 0,
  offset: 0,
  pageSize: 72,
  selected: new Map(),
  activeAsset: null,
  loading: false,
  queryController: null,
  saveTimer: null,
};

const elements = {
  summary: document.querySelector('#catalog-summary'),
  grid: document.querySelector('#asset-grid'),
  empty: document.querySelector('#empty-state'),
  resultCount: document.querySelector('#result-count'),
  loadMore: document.querySelector('#load-more'),
  search: document.querySelector('#search-input'),
  source: document.querySelector('#source-filter'),
  pack: document.querySelector('#pack-filter'),
  format: document.querySelector('#format-filter'),
  kind: document.querySelector('#kind-filter'),
  selectedOnly: document.querySelector('#selected-filter'),
  refresh: document.querySelector('#refresh-button'),
  selectionCount: document.querySelector('#selection-count'),
  selectionList: document.querySelector('#selection-list'),
  selectionEmpty: document.querySelector('#selection-empty'),
  selectionNote: document.querySelector('#selection-note'),
  saveSelection: document.querySelector('#save-selection'),
  copySelection: document.querySelector('#copy-selection'),
  storagePath: document.querySelector('#storage-path'),
  dialog: document.querySelector('#asset-dialog'),
  closeDialog: document.querySelector('#close-dialog'),
  dialogSource: document.querySelector('#dialog-source'),
  dialogTitle: document.querySelector('#dialog-title'),
  dialogId: document.querySelector('#dialog-id'),
  dialogPreview: document.querySelector('#dialog-preview'),
  viewer: document.querySelector('#model-viewer'),
  viewerMessage: document.querySelector('#viewer-message'),
  loadModel: document.querySelector('#load-model'),
  detailSource: document.querySelector('#detail-source'),
  detailPack: document.querySelector('#detail-pack'),
  detailKind: document.querySelector('#detail-kind'),
  detailFormat: document.querySelector('#detail-format'),
  detailSize: document.querySelector('#detail-size'),
  detailLicense: document.querySelector('#detail-license'),
  detailPath: document.querySelector('#detail-path'),
  detailVariants: document.querySelector('#detail-variants'),
  detailTags: document.querySelector('#detail-tags'),
  dialogSelect: document.querySelector('#dialog-select'),
  copyId: document.querySelector('#copy-id'),
  sourceLink: document.querySelector('#source-link'),
  toast: document.querySelector('#toast'),
};

function formatNumber(value) {
  return new Intl.NumberFormat('fr-FR').format(value);
}

function formatBytes(value) {
  if (!value) return 'Inconnue';
  const units = ['o', 'Ko', 'Mo', 'Go'];
  let size = value;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024;
    unit += 1;
  }
  return `${size >= 10 || unit === 0 ? size.toFixed(0) : size.toFixed(1)} ${units[unit]}`;
}

async function api(url, options = {}) {
  const response = await fetch(url, options);
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(payload.error || `Erreur HTTP ${response.status}`);
  return payload;
}

function showToast(message) {
  elements.toast.textContent = message;
  elements.toast.classList.add('show');
  window.clearTimeout(showToast.timer);
  showToast.timer = window.setTimeout(() => elements.toast.classList.remove('show'), 2200);
}

function addOption(select, value, label) {
  const option = document.createElement('option');
  option.value = value;
  option.textContent = label;
  select.append(option);
}

function setupMetadata(meta) {
  state.meta = meta;
  elements.summary.textContent = `${formatNumber(meta.logicalModelCount)} modèles · ${formatNumber(meta.viewerReadyCount)} inspectables en 3D`;
  for (const [source, count] of Object.entries(meta.sourceCounts)) {
    addOption(elements.source, source, `${source} · ${formatNumber(count)}`);
  }
  for (const pack of meta.packs) {
    addOption(elements.pack, pack.id, `${pack.name} · ${formatNumber(pack.count)}`);
  }
}

function updatePackAvailability() {
  const kenneyOnly = !elements.source.value || elements.source.value === 'Kenney';
  elements.pack.disabled = !kenneyOnly;
  if (!kenneyOnly && elements.pack.value) elements.pack.value = '';
}

function queryParameters(offset = 0) {
  const params = new URLSearchParams({ offset: String(offset), limit: String(state.pageSize) });
  if (elements.search.value.trim()) params.set('q', elements.search.value.trim());
  if (elements.source.value) params.set('source', elements.source.value);
  if (elements.pack.value) params.set('pack', elements.pack.value);
  if (elements.format.value) params.set('format', elements.format.value);
  if (elements.kind.value) params.set('kind', elements.kind.value);
  if (elements.selectedOnly.checked) params.set('selected', 'true');
  return params;
}

async function loadAssets(reset = true) {
  if (state.loading) state.queryController?.abort();
  state.queryController = new AbortController();
  state.loading = true;
  elements.loadMore.disabled = true;
  if (reset) {
    state.offset = 0;
    state.items = [];
    elements.grid.replaceChildren();
    elements.resultCount.textContent = 'Recherche…';
  }
  try {
    const payload = await api(`/api/assets?${queryParameters(state.offset)}`, { signal: state.queryController.signal });
    state.total = payload.total;
    state.items.push(...payload.items);
    state.offset = state.items.length;
    renderAssetPage(payload.items);
    elements.resultCount.textContent = `${formatNumber(payload.total)} résultat${payload.total > 1 ? 's' : ''}`;
    elements.empty.hidden = payload.total !== 0;
    elements.loadMore.hidden = state.offset >= payload.total;
  } catch (error) {
    if (error.name !== 'AbortError') {
      elements.resultCount.textContent = 'Erreur de lecture';
      showToast(error.message);
    }
  } finally {
    state.loading = false;
    elements.loadMore.disabled = false;
  }
}

function imageElement(item) {
  if (!item.previewUrl) {
    const placeholder = document.createElement('div');
    placeholder.className = 'asset-image asset-placeholder';
    placeholder.textContent = '3D';
    return placeholder;
  }
  const image = document.createElement('img');
  image.className = 'asset-image';
  image.src = item.previewUrl;
  image.alt = '';
  image.loading = 'lazy';
  image.decoding = 'async';
  image.addEventListener('error', () => {
    const placeholder = document.createElement('div');
    placeholder.className = 'asset-image asset-placeholder';
    placeholder.textContent = '3D';
    image.replaceWith(placeholder);
  }, { once: true });
  return image;
}

function renderAssetPage(items) {
  const fragment = document.createDocumentFragment();
  for (const item of items) {
    const card = document.createElement('article');
    card.className = `asset-card${state.selected.has(item.id) ? ' is-selected' : ''}`;
    card.dataset.assetId = item.id;

    const openButton = document.createElement('button');
    openButton.type = 'button';
    openButton.className = 'open-asset';
    openButton.setAttribute('aria-label', `Examiner ${item.name}`);
    openButton.append(imageElement(item));

    const copy = document.createElement('span');
    copy.className = 'asset-copy';
    const name = document.createElement('strong');
    name.textContent = item.name;
    const id = document.createElement('code');
    id.textContent = item.id;
    const meta = document.createElement('span');
    meta.className = 'asset-meta';
    const source = document.createElement('span');
    source.textContent = item.packName || item.source;
    const format = document.createElement('span');
    format.textContent = `· ${item.format.toUpperCase()}`;
    meta.append(source, format);
    copy.append(name, id, meta);
    openButton.append(copy);
    openButton.addEventListener('click', () => openAsset(item.id));

    const selectButton = document.createElement('button');
    selectButton.type = 'button';
    selectButton.className = 'quick-select';
    selectButton.textContent = state.selected.has(item.id) ? '✓' : '+';
    selectButton.setAttribute('aria-label', state.selected.has(item.id) ? `Retirer ${item.name}` : `Sélectionner ${item.name}`);
    selectButton.addEventListener('click', () => toggleSelection(item));

    card.append(openButton, selectButton);
    fragment.append(card);
  }
  elements.grid.append(fragment);
}

function updateVisibleCards() {
  for (const card of elements.grid.querySelectorAll('.asset-card')) {
    const selected = state.selected.has(card.dataset.assetId);
    card.classList.toggle('is-selected', selected);
    const button = card.querySelector('.quick-select');
    button.textContent = selected ? '✓' : '+';
    button.setAttribute('aria-label', selected ? 'Retirer de la sélection' : 'Ajouter à la sélection');
  }
}

function selectionItem(item) {
  const row = document.createElement('div');
  row.className = 'selection-item';
  const copy = document.createElement('div');
  const name = document.createElement('strong');
  name.textContent = item.name;
  const id = document.createElement('code');
  id.textContent = item.id;
  copy.append(name, id);
  const remove = document.createElement('button');
  remove.type = 'button';
  remove.className = 'remove-selection';
  remove.textContent = '×';
  remove.setAttribute('aria-label', `Retirer ${item.name}`);
  remove.addEventListener('click', () => toggleSelection(item));
  row.append(copy, remove);
  return row;
}

function updateSelectionUi() {
  elements.selectionCount.textContent = formatNumber(state.selected.size);
  elements.selectionEmpty.hidden = state.selected.size !== 0;
  elements.selectionList.replaceChildren();
  const recent = [...state.selected.values()].slice(-40).reverse();
  for (const item of recent) elements.selectionList.append(selectionItem(item));
  if (state.selected.size > recent.length) {
    const remainder = document.createElement('div');
    remainder.className = 'selection-empty';
    remainder.textContent = `+ ${formatNumber(state.selected.size - recent.length)} autres éléments enregistrés`;
    elements.selectionList.append(remainder);
  }
  elements.saveSelection.disabled = false;
  elements.copySelection.disabled = state.selected.size === 0;
  updateVisibleCards();
  if (state.activeAsset) updateDialogSelectButton();
}

function toggleSelection(item) {
  if (state.selected.has(item.id)) state.selected.delete(item.id);
  else state.selected.set(item.id, item);
  updateSelectionUi();
  window.clearTimeout(state.saveTimer);
  state.saveTimer = window.setTimeout(() => persistSelection(true), 350);
  if (elements.selectedOnly.checked) window.setTimeout(() => loadAssets(true), 380);
}

async function persistSelection(silent = false) {
  elements.saveSelection.disabled = true;
  try {
    const payload = await api('/api/selection', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ assetIds: [...state.selected.keys()], note: elements.selectionNote.value }),
    });
    elements.storagePath.textContent = `Enregistré dans ${payload.storagePath}`;
    if (!silent) showToast('Sélection enregistrée.');
  } catch (error) {
    showToast(error.message);
  } finally {
    elements.saveSelection.disabled = false;
  }
}

async function openAsset(assetId) {
  try {
    const item = await api(`/api/asset?id=${encodeURIComponent(assetId)}`);
    state.activeAsset = item;
    elements.dialogSource.textContent = item.source;
    elements.dialogTitle.textContent = item.name;
    elements.dialogId.textContent = item.id;
    elements.detailSource.textContent = item.source;
    elements.detailPack.textContent = item.packName || 'Asset individuel';
    elements.detailKind.textContent = item.kind === 'animation' ? 'Animation' : 'Modèle 3D';
    elements.detailFormat.textContent = item.format.toUpperCase();
    elements.detailSize.textContent = formatBytes(item.fileSize);
    elements.detailLicense.textContent = item.license;
    elements.detailPath.textContent = item.modelRelativePath;

    elements.detailVariants.replaceChildren();
    for (const variant of item.variants) {
      const tag = document.createElement('span');
      tag.textContent = variant.format.toUpperCase();
      tag.title = variant.path;
      elements.detailVariants.append(tag);
    }
    elements.detailTags.replaceChildren();
    for (const value of [...item.categories, ...item.tags].slice(0, 18)) {
      const tag = document.createElement('span');
      tag.textContent = value;
      elements.detailTags.append(tag);
    }

    elements.viewer.removeAttribute('src');
    elements.viewer.classList.remove('is-visible');
    elements.dialogPreview.hidden = !item.previewUrl;
    elements.dialogPreview.src = item.previewUrl || '';
    elements.dialogPreview.alt = item.previewUrl ? `Aperçu de ${item.name}` : '';
    elements.loadModel.hidden = !item.viewerSupported;
    elements.loadModel.disabled = false;
    elements.loadModel.textContent = 'Charger l’aperçu 3D';
    elements.viewerMessage.textContent = item.viewerSupported
      ? 'Rotation : glisser · Zoom : molette ou pincement. La 3D ne se charge qu’à votre demande.'
      : `${item.format.toUpperCase()} : miniature disponible, normalisation GLB nécessaire pour la vue 3D interactive.`;
    if (item.previewUrl) elements.viewer.setAttribute('poster', item.previewUrl);
    else elements.viewer.removeAttribute('poster');

    elements.sourceLink.hidden = !item.sourcePage;
    if (item.sourcePage) elements.sourceLink.href = item.sourcePage;
    updateDialogSelectButton();
    if (!elements.dialog.open) elements.dialog.showModal();
  } catch (error) {
    showToast(error.message);
  }
}

function updateDialogSelectButton() {
  if (!state.activeAsset) return;
  const selected = state.selected.has(state.activeAsset.id);
  elements.dialogSelect.textContent = selected ? 'Retirer de ma sélection' : 'Ajouter à ma sélection';
  elements.dialogSelect.classList.toggle('primary', !selected);
}

async function loadActiveModel() {
  const item = state.activeAsset;
  if (!item?.viewerSupported) return;
  if (item.logicalBytes > 250 * 1024 * 1024) {
    const confirmed = window.confirm(`Cet asset représente environ ${formatBytes(item.logicalBytes)} avec ses dépendances. Le chargement peut être long. Continuer ?`);
    if (!confirmed) return;
  }
  elements.loadModel.disabled = true;
  elements.loadModel.textContent = 'Chargement…';
  elements.viewerMessage.textContent = 'Chargement de la géométrie et des matériaux locaux…';
  elements.viewer.classList.add('is-visible');
  elements.viewer.setAttribute('src', item.modelUrl);
}

elements.viewer.addEventListener('load', () => {
  elements.viewer.classList.add('is-visible');
  elements.dialogPreview.hidden = true;
  elements.loadModel.hidden = true;
  elements.viewerMessage.textContent = 'Rotation : glisser · Zoom : molette ou pincement.';
});

elements.viewer.addEventListener('error', () => {
  elements.loadModel.disabled = false;
  elements.loadModel.textContent = 'Réessayer la 3D';
  elements.viewerMessage.textContent = 'Ce fichier n’a pas pu être affiché directement. La sélection et son chemin précis restent disponibles.';
});

function closeDialog() {
  elements.viewer.removeAttribute('src');
  elements.viewer.classList.remove('is-visible');
  state.activeAsset = null;
  elements.dialog.close();
}

async function copyText(value, confirmation) {
  try {
    await navigator.clipboard.writeText(value);
    showToast(confirmation);
  } catch {
    showToast('Copie impossible dans ce navigateur.');
  }
}

async function copyDiscussion() {
  const entries = [...state.selected.values()];
  if (!entries.length) return;
  const lines = [
    'Sélection OpenRPG Asset Atlas',
    elements.selectionNote.value.trim() ? `Direction artistique : ${elements.selectionNote.value.trim()}` : '',
    '',
    ...entries.map((item) => `- ${item.id} | ${item.source}${item.packName ? ` / ${item.packName}` : ''} | ${item.modelRelativePath}`),
  ].filter((line, index, all) => line || all[index - 1]);
  await copyText(lines.join('\n'), `${formatNumber(entries.length)} références copiées pour Codex.`);
}

function scheduleSearch() {
  window.clearTimeout(scheduleSearch.timer);
  scheduleSearch.timer = window.setTimeout(() => loadAssets(true), 220);
}

async function initialize() {
  try {
    const [meta, selection] = await Promise.all([api('/api/meta'), api('/api/selection')]);
    setupMetadata(meta);
    elements.selectionNote.value = selection.note || '';
    elements.storagePath.textContent = `Fichier : ${selection.storagePath}`;
    for (const entry of selection.entries || []) state.selected.set(entry.id, entry);
    updateSelectionUi();
    await loadAssets(true);
    const deepLink = new URLSearchParams(window.location.search);
    const linkedAsset = deepLink.get('asset');
    if (linkedAsset) {
      await openAsset(linkedAsset);
      if (deepLink.get('load3d') === '1') await loadActiveModel();
    }
  } catch (error) {
    elements.summary.textContent = 'Vault indisponible';
    elements.resultCount.textContent = error.message;
    showToast(error.message);
  }
}

elements.search.addEventListener('input', scheduleSearch);
for (const filter of [elements.source, elements.pack, elements.format, elements.kind, elements.selectedOnly]) {
  filter.addEventListener('change', () => {
    if (filter === elements.source) updatePackAvailability();
    loadAssets(true);
  });
}
elements.loadMore.addEventListener('click', () => loadAssets(false));
elements.refresh.addEventListener('click', () => window.location.reload());
elements.saveSelection.addEventListener('click', () => persistSelection(false));
elements.copySelection.addEventListener('click', copyDiscussion);
elements.closeDialog.addEventListener('click', closeDialog);
elements.dialog.addEventListener('click', (event) => {
  if (event.target === elements.dialog) closeDialog();
});
elements.dialog.addEventListener('cancel', (event) => {
  event.preventDefault();
  closeDialog();
});
elements.loadModel.addEventListener('click', loadActiveModel);
elements.dialogSelect.addEventListener('click', () => {
  if (state.activeAsset) toggleSelection(state.activeAsset);
});
elements.copyId.addEventListener('click', () => {
  if (state.activeAsset) copyText(state.activeAsset.id, 'Identifiant copié.');
});

initialize();
