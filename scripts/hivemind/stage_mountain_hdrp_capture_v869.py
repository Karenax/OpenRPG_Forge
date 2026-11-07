"""Stage preserved HDRP sources in an isolated project; never copy purchased scripts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re, shutil

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W/'mountain-hdrp-capture-stage-v869'
P = O/'project'
SEED = W/'unity-library-source-thumbnail-pirate-bench-v2/project'
assert not O.exists()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

pointer = V/'metadata/asset-source-support-registry.v1.json'
registry = read(pointer)
catalog_path = V/registry['path']
assert sha(catalog_path) == registry['sha256']
cards = [a for a in read(catalog_path)['assets'] if a['pack_id']=='hivemind.mountain-ancient-mountain' and not a.get('preview')]
assert len(cards) == 47 and all(isinstance(a.get('original_lod_level'),int) and 0<=a['original_lod_level']<=5 for a in cards)
manifest_path = Path(cards[0]['source_manifest_path'])
assert sha(manifest_path) == cards[0]['source_manifest_sha256']
manifest = read(manifest_path)
assert manifest['archive_sha256'] == cards[0]['source_archive_sha256']
source_root = Path(manifest['source_root']).resolve()
asset_rows = {r['guid']:r for r in manifest['files'] if r['kind']=='asset'}
rows_by_path = {}
for row in manifest['files']:
    if row['kind'] in {'asset','asset.meta'}:
        rows_by_path.setdefault(row['source_path'],[]).append(row)
blocked = {'.cs','.dll','.exe','.asmdef','.asmref','.rsp','.py','.ps1','.bat','.cmd','.so','.dylib','.unitypackage','.wlt'}
selected_paths = {r['source_path'] for r in manifest['files'] if r['source_path'].startswith('Assets/HIVEMIND/HDRP/TheMessengerMountain/') and Path(r['source_path']).suffix.lower() not in blocked}
text_extensions = {'.mat','.prefab','.asset','.anim','.controller','.shadergraph','.shadersubgraph','.shader','.hlsl','.cginc','.terrainlayer'}
queue = list(selected_paths)
seen = set()
blocked_dependencies = []
while queue:
    path = queue.pop()
    if path in seen:
        continue
    seen.add(path)
    for row in rows_by_path[path]:
        src = manifest_path.parent/row['stored_path']
        assert src.resolve().is_relative_to(source_root)
        if row['kind']=='asset' and Path(path).suffix.lower() not in text_extensions:
            continue
        assert sha(src)==row['sha256']
        for guid in set(re.findall(r'[a-f0-9]{32}',src.read_text('utf-8-sig',errors='replace'))):
            dependency = asset_rows.get(guid)
            if dependency is None:
                continue  # External SDK/internal graph IDs remain for Unity qualification.
            dependency_path = dependency['source_path']
            if Path(dependency_path).suffix.lower() in blocked:
                blocked_dependencies.append(dict(source_path=path,guid=guid,dependency_path=dependency_path))
            elif dependency_path not in selected_paths:
                selected_paths.add(dependency_path);queue.append(dependency_path)
for card in cards:
    row = asset_rows[card['source_guid']]
    assert row['sha256']==card['source_sha256'] and row['source_path']==card['source_asset_path']
    assert card['source_asset_path'] in selected_paths
assert not blocked_dependencies, 'Capture requires an excluded script/plugin dependency; preserve and review before staging'
O.mkdir();P.mkdir()
pins = {str(p):sha(p) for p in [Path(__file__),pointer,catalog_path,manifest_path]}
copies = []

def copy(source,target,expected=None,kind='controlled-scaffold'):
    source = Path(source);target = Path(target)
    assert target.resolve().is_relative_to(P.resolve()) and not target.exists()
    digest = sha(source)
    assert expected is None or digest==expected
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source,target)
    assert sha(target)==digest
    pins[str(source)] = digest
    copies.append(dict(source=str(source),target=str(target),sha256=digest,bytes=target.stat().st_size,kind=kind))

for path in sorted(selected_paths):
    for row in rows_by_path[path]:
        src = manifest_path.parent/row['stored_path']
        relative = src.resolve().relative_to(source_root)
        copy(src,P/relative,row['sha256'],'purchased-render-data')
for folder in ['Packages','ProjectSettings']:
    for src in (SEED/folder).rglob('*'):
        if src.is_file():copy(src,P/src.relative_to(SEED))
for folder in ['HDRPDefaultResources','OpenRPGProbe']:
    for src in (SEED/'Assets'/folder).rglob('*'):
        if src.is_file():
            assert src.suffix.lower() not in blocked
            copy(src,P/src.relative_to(SEED))
    meta = SEED/'Assets'/(folder+'.meta')
    if meta.exists():copy(meta,P/meta.relative_to(SEED))
for name in ['DefaultVolumeProfile.asset','DefaultVolumeProfile.asset.meta','UniversalRenderPipelineGlobalSettings.asset','UniversalRenderPipelineGlobalSettings.asset.meta']:
    src = SEED/'Assets'/name
    if src.exists():copy(src,P/src.relative_to(SEED))
helpers = [R/'scripts/hivemind/candidates/SourceLibraryThumbnailBatchV1.cs', W/'pirate-wood-lod-migration-v819/PirateWoodLodMigrationV819.cs']
for helper in helpers:copy(helper,P/'Assets/Editor'/helper.name,kind='controlled-helper')
assert set((P/'Assets').rglob('*.cs'))=={P/'Assets/Editor'/p.name for p in helpers}
assert not list((P/'Assets').rglob('*.dll'))
result = dict(status='isolated_mountain_hdrp_render_sources_staged_no_unity_launched',
              project=str(P),catalog_revision=registry['revision'],cards=cards,candidate_cards=47,
              copied_files=len(copies),copied_bytes=sum(c['bytes'] for c in copies),copies=copies,input_pins=pins,
              purchased_scripts_copied=0,purchased_plugins_copied=0,controlled_helpers=2,
              selected_source_paths=len(selected_paths),blocked_dependencies=blocked_dependencies,
              prefab_groups=len({a['source_asset_path'] for a in cards}),
              requested_lod_levels=sorted({a['original_lod_level'] for a in cards}),
              shader_import_and_sdk_references_qualified=False,images_admitted=0,
              actual_studio_gui_verified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['cards','copies','input_pins']}))
