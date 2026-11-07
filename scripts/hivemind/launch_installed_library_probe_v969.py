"""Launch the installed Studio with the private Vault and an isolated saved workspace."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, shutil, subprocess

INSTALL = Path('F:/instal test/Installation')
W = Path('F:/OpenRPG_ImportWork')
VAULT = Path('F:/OpenRPG_AssetVault')
O = W/'installed-library-gui-probe-v969'

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

assert not O.exists()
state_path = INSTALL/'installation.json'
state_hash = sha(state_path)
state = read(state_path)
core = state['active']['core']
package = INSTALL/'packages'/core['id']/core['revision']
manifest_path = package/'package-manifest.json'
manifest_hash = sha(manifest_path)
manifest = read(manifest_path)
definition = manifest['product']
app = definition['apps']['editor']
exe = package/app['executable']
pin = next(f['sha256'] for f in manifest['files'] if f['path'] == app['executable'])
assert sha(exe) == pin
source_workspace = Path(state['user_root'])/'workspaces'/definition['id']
assert source_workspace.is_dir()
assert (VAULT/'metadata/asset-import-registry.v1.json').is_file()
assert (VAULT/'metadata/asset-source-support-registry.v1.json').is_file()
assert 'asset-library' not in state['active']['packs']
O.mkdir()
workspace = O/'workspace'
workspace.mkdir()
copies = []
for folder, dirs, files in os.walk(source_workspace, followlinks=False):
    parent = Path(folder)
    assert not parent.is_junction() and not parent.is_symlink()
    assert all(not (parent/d).is_junction() and not (parent/d).is_symlink() for d in dirs)
    for name in files:
        source = parent/name
        assert not source.is_symlink() and not source.is_junction()
        digest = sha(source)
        target = workspace/source.relative_to(source_workspace)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        assert sha(source) == sha(target) == digest
        copies.append(dict(source=str(source), isolated_copy=str(target), sha256=digest, bytes=source.stat().st_size))
bindings = dict(install=str(INSTALL), app=str(package), user=state['user_root'], workspace=str(workspace))
for pack_id, pack in state['active']['packs'].items():
    bindings['pack:'+pack_id] = str(INSTALL/'packages'/pack['id']/pack['revision'])
def expand(value):
    for key, target in bindings.items():
        value = value.replace('{'+key+'}', target)
    assert '{' not in value and '}' not in value, value
    return value
env = os.environ.copy()
for key in list(env):
    if any(key.upper().startswith(prefix.upper()) for prefix in app['clear_env_prefixes']):
        del env[key]
for key, value in app['env'].items():
    if key != 'OPENRPG_ASSET_VAULT':
        env[key] = expand(value)
env['OPENRPG_ASSET_VAULT'] = str(VAULT)
env['WEBVIEW2_USER_DATA_FOLDER'] = str(O/'browser-profile')
(O/'temp').mkdir()
env['TEMP'] = env['TMP'] = str(O/'temp')
assert env['FORGE_AUTHORING_ROOT'] == str(workspace)
assert sha(state_path) == state_hash and sha(manifest_path) == manifest_hash and sha(exe) == pin
prepared = dict(status='installed_library_probe_prepared', created_utc=datetime.now(timezone.utc).isoformat(),
                executable=str(exe), executable_sha256=pin, installed_core_revision=core['revision'],
                installation_state_sha256=state_hash, source_workspace=str(source_workspace),
                isolated_workspace=str(workspace), copies=copies,
                vault=str(VAULT), browser_profile=str(O/'browser-profile'), original_session_closed=False,
                library_pack_installed=False, source_compile_parity_verified=False, full_import=False,
                input_pins={str(state_path):state_hash,str(manifest_path):manifest_hash,str(exe):pin,
                            str(Path(__file__).resolve()):sha(Path(__file__))})
(O/'PREPARED.v1.json').write_text(json.dumps(prepared, indent=2), 'utf-8')
with (O/'studio.stdout.log').open('xb') as out, (O/'studio.stderr.log').open('xb') as err:
    child = subprocess.Popen([str(exe), *[expand(arg) for arg in app['args']]], cwd=package, env=env,
                             stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
job = dict(studio_pid=child.pid, launched_utc=datetime.now(timezone.utc).isoformat(), executable=str(exe),
           vault=str(VAULT), isolated_workspace=str(workspace), source_session_preserved=True,
           actual_gui_verified=False, persistent_launcher_repair=False, full_import=False)
(O/'JOB.v1.json').write_text(json.dumps(job, indent=2), 'utf-8')
print(json.dumps(job))
