"""Exercise the actual common mesh adapters in an isolated Bevy World."""
from pathlib import Path
import datetime, hashlib, json, os, shutil, subprocess

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-common-collider-host-v717'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert O.is_dir() and not (O / 'RUN-RESULT.v1.json').exists()

def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()

receipt = W / 'house-runtime-state-glb-v675/RESULT.v1.json'
bindings = W / 'house-collider-binding-plan-v678/RESULT.v1.json'
convex = W / 'house-native-convex-candidate-v704/RESULT.v1.json'
v709 = W / 'source-collision-geometry-v709/RESULT.v1.json'
assert read(v709)['status'] == 'common_collision_geometry_verified'
candidates = {(r['source_guid'], r['source_file_id']): r for r in read(convex)['rows']}
box_checkpoint = W / 'house-native-box-checkpoint-v682/RESULT.v1.json'
boxes = read(box_checkpoint)['rows']
mesh_checkpoint = W / 'house-native-mesh-checkpoint-v685/RESULT.v1.json'
native_meshes = read(mesh_checkpoint)['rows']
source_rows = read(bindings)['rows']
cases = []
pins = {}
for entry in read(receipt)['rows']:
    assert sha(entry['glb']) == entry['sha256']
    rows = [r.copy() for r in source_rows if r['prefab_guid'] == entry['prefab_guid'] and r['kind'] != 'box']
    for row in rows:
        assert row['glb'] == entry['glb'] and row['glb_sha256'] == entry['sha256']
        if row['kind'] == 'assigned_mesh' and row['source_convex']:
            key = (row['source_component']['guid'], row['source_component']['file_id'])
            native = candidates[key]
            assert native['source_mesh_guid'] == row['source_mesh_reference']['guid']
            assert native['source_mesh_file_id'] == row['source_mesh_reference']['file_id']
            assert native['source_cooking_options'] == row['source_cooking_options']
            row['candidate'] = {k: native[k] for k in ['target_vertices', 'target_vertices_sha256']}
            assert sha(native['target_vertices']) == native['target_vertices_sha256']
            pins[native['target_vertices']] = native['target_vertices_sha256']
    cases.append(dict(native_meshes=[r for r in native_meshes if r['source_guid'] == entry['prefab_guid']], boxes=[r for r in boxes if r['source_guid'] == entry['prefab_guid']], glb=entry['glb'], glb_sha256=entry['sha256'], meshes=rows,
                      cooking_evidence=str(convex) + '#' + sha(convex) + '; residual-v707 remains unqualified'))
    pins[entry['glb']] = entry['sha256']
assert len(cases) == 24 and sum(len(c['meshes']) for c in cases) == 21
(O / 'src').mkdir(); (O / 'temp').mkdir()
name = 'house-common-collider-host-v717'
seed = W / 'house-rapier-box-query-v693/Cargo.toml'
cargo = seed.read_text('utf-8').split('[[bin]]')[0]
cargo = cargo.replace('name="openrpg-gladiator-viewport-lod-v469"', 'name="' + name + '"')
cargo = cargo.replace('[dependencies]', '[dependencies]\nforge_core={path="' + (R / 'crates/forge_core').as_posix() + '"}')
cargo += '[[bin]]\nname="' + name + '"\npath="src/main.rs"\n'
(O / 'Cargo.toml').write_text(cargo, 'utf-8')
shutil.copyfile(W / 'house-rapier-box-query-v693/Cargo.lock', O / 'Cargo.lock')
template = R / 'scripts/hivemind/candidates/VerifyHouseColliderBindingV717.rs'
(O / 'src/main.rs').write_text(template.read_text('utf-8').replace('@@ROOT@@', R.as_posix()), 'utf-8')
(O / 'CASES.v1.json').write_text(json.dumps(cases, indent=2), 'utf-8')
files = [Path(__file__), template, mesh_checkpoint, box_checkpoint, receipt, bindings, convex, v709, seed, O / 'Cargo.toml', O / 'src/main.rs', O / 'CASES.v1.json']
files += [R / 'apps/forge_studio/src/authoring_assets/source_graph_host.rs']
files += [R / 'crates/forge_engine/src' / n for n in ['source_colliders.rs', 'source_mesh_colliders.rs', 'source_collider_projection.rs', 'source_mesh_collider_projection.rs', 'source_graph_scene.rs', 'source_collider_scene.rs']]
for crate in ['forge_assets', 'forge_core']:
    files += [p for p in (R / 'crates' / crate).rglob('*') if p.is_file() and (p.suffix == '.rs' or p.name == 'Cargo.toml')]
pins.update({str(p): sha(p) for p in files})
state = dict(status='prepared', input_pins=pins, full_import_increment=0,
             physics_simulation_qualified=False, studio_verified=False)
def save():
    state['utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    tmp = O / 'RUN-RESULT.tmp'; tmp.write_text(json.dumps(state, indent=2), 'utf-8'); tmp.replace(O / 'RUN-RESULT.v1.json')
save()
env = os.environ.copy()
env.update(RUSTC=str(BIN/'rustc.exe'), RUSTDOC=str(BIN/'rustdoc.exe'), CARGO_INCREMENTAL='0', CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'), TEMP=str(O/'temp'), TMP=str(O/'temp'))
try:
    with (O / 'lock.log').open('wb') as log:
        code = subprocess.call([str(BIN/'cargo.exe'), 'generate-lockfile', '--offline', '--manifest-path', str(O/'Cargo.toml')], env=env, cwd=O, stdout=log, stderr=log)
    assert code == 0, 'lock generation failed'
    pins[str(O/'Cargo.lock')] = sha(O/'Cargo.lock')
    with (O / 'cargo.log').open('wb') as log:
        process = subprocess.Popen([str(BIN/'cargo.exe'), 'build', '--locked', '--offline', '-j', '1', '--manifest-path', str(O/'Cargo.toml')], env=env, cwd=O, stdout=log, stderr=log)
        state.update(status='building', pid=process.pid); save(); code = process.wait()
    assert code == 0, 'build failed: cargo.log'
    exe = W / 'firepit-static-glb-product-check-v2/target/debug' / (name + '.exe')
    pins[str(exe)] = sha(exe)
    with (O/'STDOUT.log').open('wb') as out, (O/'STDERR.log').open('wb') as err:
        process = subprocess.Popen([str(exe), str(O/'CASES.v1.json')], env=env, cwd=O, stdout=out, stderr=err)
        state.update(status='checking', pid=process.pid); save(); code=process.wait()
    state['exit_code'] = code
    assert code == 0, 'World check failed: STDERR.log'
    state['unchanged_input_pins'] = all(sha(p) == h for p,h in pins.items())
    assert state['unchanged_input_pins'], 'pinned inputs changed'
    result = read(O/'STDOUT.log')
    assert result['common_mesh_projection_verified']
    state.update(status='complete_source_collider_scene_verified', owned_mixed_control_verified=result['owned_mixed_control_verified'], complete_scene_route_verified=result['complete_scene_route_verified'], scene_observations=result['scene_observations'], controlled_contacts_verified=result['controlled_contacts_verified'], contact_cases=result['contact_cases'], backend_registered=result['backend_registered'], backend_registration_verified=result['backend_registration_verified'], backend_ray_samples=sum(r['count'] for r in result['backend_ray_results']), native_ray_value_residuals=sum(len(r['failures']) for r in result['backend_ray_results']), carriers=result['carriers'],
                 assigned_mesh_colliders=result['assigned_mesh_colliders'], empty_source_meshes=result['empty_source_meshes'],
                 negative_controls=result['negative_controls'], native_box_points=result['native_box_points'], native_box_rays=result['native_box_rays'], output_sha256=sha(O/'STDOUT.log'))
except Exception as error:
    state.update(status='failed', error=str(error)); save(); raise
save()
print(json.dumps({k:v for k,v in state.items() if k != 'input_pins'}, indent=2))
