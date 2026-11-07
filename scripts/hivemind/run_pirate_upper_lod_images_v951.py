"""Capture a bounded upper-LOD image lot using the existing controlled HDRP helper."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, shutil, subprocess, sys

S = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W/'pirate-upper-lod-images-v951'
PROJECT = W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def save(value):
    value.update(updated_utc=datetime.now(timezone.utc).isoformat(), full_import=False, images_installed_in_vault=False)
    p = O/'RUN-RESULT.tmp'
    p.write_text(json.dumps(value, indent=2), 'utf-8')
    p.replace(O/'RUN-RESULT.v1.json')

def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS, '-NoProfile', '-Command', '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'
    frontier_path = W/'pirate-upper-lod-frontier-v950/RESULT.v1.json'
    frontier = read(frontier_path)
    registry = read(V/'metadata/asset-source-support-registry.v1.json')
    assert registry['revision'] == frontier['catalog_revision']
    for p, h in frontier['input_pins'].items():
        assert sha(p) == h, p
    ownership_path = W/'pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json'
    ownership = read(ownership_path)
    assert not ownership['asset_dll_found']
    known = {row['private_path']:row for row in ownership['helpers']}
    for version in (591, 819):
        name = f'PirateWoodLodMigrationV{version}.cs'
        controlled = W/f'pirate-wood-lod-migration-v{version}'/name
        known[str(PROJECT/'Assets/Editor'/name)] = dict(controlled_source=str(controlled), sha256=sha(controlled))
    helpers = []
    for private in (PROJECT/'Assets').rglob('*.cs'):
        assert str(private) in known, str(private)
        row = known[str(private)]
        controlled = Path(row['controlled_source'])
        assert controlled.is_file() and sha(private) == sha(controlled) == row['sha256'], str(private)
        helpers.append(dict(private_path=str(private), controlled_source=str(controlled), sha256=sha(private)))
    assert not any((PROJECT/'Assets').rglob('*.dll'))
    selected, controls = [], {}
    for group in frontier['eligible_groups']:
        for card in group['cards']:
            if len(selected) == 24:
                break
            selected.append((card, group['expected_empty_lod_slots']))
            controls[card['source_asset_path']] = (group['control'], group['expected_empty_lod_slots'])
        if len(selected) == 24:
            break
    assert len(selected) == 24
    O.mkdir()
    (O/'temp').mkdir()
    (O/'private-input-epoch').mkdir()
    pins = {str(p):sha(p) for p in (PROJECT/'Assets').rglob('*') if p.is_file()}
    snapshots = []
    for name, digest in list(pins.items()):
        p = Path(name)
        if p.suffix in {'.mat', '.meta', '.shadergraph', '.asset'}:
            copy = O/'private-input-epoch'/p.relative_to(PROJECT)
            copy.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, copy)
            assert sha(copy) == digest
            pins[str(copy)] = digest
            snapshots.append(dict(live_path=name, snapshot=str(copy), sha256=digest))
    cases = []
    for index, (card, empty) in enumerate([*controls.values(), *selected]):
        for field, digest in [('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256')]:
            source = V/card[field]
            assert sha(source) == card[digest]
            pins[str(source)] = card[digest]
        assert sha(PROJECT/card['source_asset_path']) == card['source_sha256']
        control = index < len(controls)
        folder = O/f'case-{index:02d}'
        folder.mkdir()
        cases.append(dict(id=card['id']+':lod0-control' if control else card['id'], source_card_id=card['id'],
                          prefab_path=card['source_asset_path'], prefab_guid=card['source_guid'],
                          requested_lod=card['original_lod_level'], expected_empty_lod_slots=empty,
                          index_candidate=not control, output=str(folder/'NATIVE-RESULT.v1.json')))
    for filename, value in [('REQUEST.v1.json', dict(cases=cases, output=str(O/'NATIVE-RESULT.v1.json'))),
                            ('PRIVATE-EPOCH.v1.json', dict(private_input_snapshots=snapshots)),
                            ('HELPER-OWNERSHIP.v1.json', dict(helpers=helpers, asset_dll_found=False))]:
        (O/filename).write_text(json.dumps(value, indent=2), 'utf-8')
    for p in [Path(__file__), ownership_path, frontier_path, V/'metadata/asset-source-support-registry.v1.json',
              PROJECT/'Packages/manifest.json', PROJECT/'ProjectSettings/ProjectSettings.asset',
              S/'run_pirate_thumbnail_batch_0193_0199_v334.py', S/'queue_delta_firepit_particle_behavior_v4.py',
              O/'REQUEST.v1.json', O/'PRIVATE-EPOCH.v1.json', O/'HELPER-OWNERSHIP.v1.json']:
        pins[str(p)] = sha(p)
    for helper in helpers:
        pins[helper['controlled_source']] = helper['sha256']
    prepared = dict(sub_batch='L08.IMAGE.PIRATE.UPPER_LOD.01', expected_cards=24, expected_images=48,
                    expected_capture_cases=len(cases), cases=cases, candidate_ids=[c['id'] for c, _ in selected],
                    input_pins=pins, catalog_revision=registry['revision'], full_import=False,
                    source_version_parity_qualified=False)
    (O/'PREPARED.v1.json').write_text(json.dumps(prepared, indent=2), 'utf-8')
    save(dict(status='upper_lod_capture_prepared', cards_requested=24, control_cases=len(controls)))
    with (O/'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
                                 stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid, one_shot=True,
                                             created_utc=datetime.now(timezone.utc).isoformat())), 'utf-8')
    print(json.dumps(dict(controller_pid=child.pid, cards=24, capture_cases=len(cases), root=str(O))))

def run():
    recipe = S/'run_pirate_thumbnail_batch_0193_0199_v334.py'
    tree = ast.parse(recipe.read_text('utf-8-sig'))
    selected = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom)) or isinstance(n, ast.FunctionDef) and n.name == 'run']
    count = len(read(O/'PREPARED.v1.json')['cases'])
    code = ast.unparse(ast.Module(body=selected, type_ignores=[]))
    code = code.replace('OpenRPGSourceLibraryThumbnailBatchV1.Export', 'OpenRPGSourceLibraryLodThumbnailBatchV353.Export')
    assert "len(batch['cases']) == 7" in code
    code = code.replace("len(batch['cases']) == 7", f"len(batch['cases']) == {count}")
    code = code.replace('seven_pirate_source_cards', 'pirate_upper_lod_cases')
    scope = dict(S=S, W=W, V=V, O=O, PROJECT=PROJECT, PS=PS, read=read, sha=sha, save=save)
    exec(compile(code, str(recipe)+'#bounded-upper-source-lods', 'exec'), scope)
    scope['run']()

if __name__ == '__main__':
    if len(sys.argv) == 1:
        prepare()
    else:
        assert sys.argv[1:] == ['run']
        run()
