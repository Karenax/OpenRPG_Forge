"""Apply the pinned local patch; preserve bootstrap backups and authoring proofs."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from datetime import datetime, timezone

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'scripts/delivery'))
from complete_rebuild import sha, write, regular_input

INSTALL = Path('F:/instal test/Installation')
PREPARE = Path('F:/OpenRPG_ImportWork/installed-vault-update-plan-v978')
ROOT = Path('F:/OpenRPG_ImportWork/installed-vault-update-apply-v992')
CLI = Path('F:/OpenRPG_ImportWork/delivery-external-vault-v974/target/release/forge_delivery.exe')

def call(cli, stage, *args):
    command = [str(cli), *map(str, args)]
    with (ROOT / f'{stage}.stdout.json').open('w', encoding='utf-8') as out, \
            (ROOT / f'{stage}.stderr.log').open('w', encoding='utf-8') as err:
        process = subprocess.Popen(command, stdout=out, stderr=err,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        write(ROOT / 'JOB.v1.json', {'stage': stage, 'pid': process.pid, 'command': command,
              'started_utc': datetime.now(timezone.utc).isoformat()})
        code = process.wait()
    if code:
        raise RuntimeError(f'{stage} exited {code}; inspect its stderr log')
    return json.loads((ROOT / f'{stage}.stdout.json').read_bytes())

def main():
    receipt = json.loads((PREPARE / 'RESULT.v1.json').read_bytes())
    plan_path = PREPARE / 'install-plan.json'
    if receipt['status'] != 'PREPARED_NOT_APPLIED' or sha(plan_path) != receipt['plan_sha256']:
        raise ValueError('Prepared plan mismatch')
    if sha(PREPARE / 'applications-vault-repair.forgepack') != receipt['archive_sha256']:
        raise ValueError('Prepared archive changed')
    plan = json.loads(plan_path.read_bytes())
    if Path(plan['root']) != INSTALL or sha(INSTALL / 'installation.json') != plan['expected_head']:
        raise ValueError('Installation head changed; prepare a new plan')
    cli_build = json.loads(Path('F:/OpenRPG_ImportWork/shared-vault-cli-build-v979/RESULT.v1.json').read_bytes())
    if sha(CLI) != cli_build['executable_sha256']:
        raise ValueError('CLI build changed')
    before = json.loads((INSTALL / 'installation.json').read_bytes())
    old_core = INSTALL / 'packages' / before['active']['core']['id'] / before['active']['core']['revision']
    old_descriptor = json.loads((old_core / 'package-manifest.json').read_bytes())
    old_pins = {row['path']: row for row in old_descriptor['files']}
    bootstraps = {'Forge-ContentManager.exe': 'bin/forge-hub.exe',
                  'forge-delivery.exe': 'bin/forge-delivery.exe'}
    for name, entry in bootstraps.items():
        regular_input(INSTALL, INSTALL / name)
        if sha(INSTALL / name) != old_pins[entry]['sha256']:
            raise ValueError(f'Bootstrap has independent edits; preserve {name}')
    prepared = json.loads(Path('F:/OpenRPG_ImportWork/installed-library-gui-probe-v971/PREPARED.v1.json').read_bytes())
    personal_before = {row['source']: sha(Path(row['source'])) for row in prepared['copies']}
    ROOT.mkdir(exist_ok=False)
    write(ROOT / 'before-state.json', before)
    write(ROOT / 'before-personal-files.json', personal_before)
    for name in bootstraps:
        shutil.copyfile(INSTALL / name, ROOT / (name + '.before'))
        if sha(ROOT / (name + '.before')) != sha(INSTALL / name):
            raise ValueError('Bootstrap backup changed during copy')
    state = call(CLI, 'apply', 'apply', plan_path)
    if state['active'] != plan['target'] or state['previous'] != before['active']:
        raise ValueError('Unexpected applied state')
    if state['user_root'] != before['user_root']:
        raise ValueError('User data root changed')
    core = INSTALL / 'packages' / state['active']['core']['id'] / state['active']['core']['revision']
    descriptor = json.loads((core / 'package-manifest.json').read_bytes())
    new_pins = {row['path']: row for row in descriptor['files']}
    bootstrap_results = []
    for name, entry in bootstraps.items():
        source = core / entry
        if sha(source) != new_pins[entry]['sha256']:
            raise ValueError('New bootstrap pin mismatch')
        destination = INSTALL / name
        if sha(destination) != old_pins[entry]['sha256']:
            raise ValueError('Bootstrap changed after preflight; preserve it')
        if destination.resolve().parent != INSTALL.resolve():
            raise ValueError('Bootstrap target escaped installation root')
        pending = INSTALL / (name + '.vault-update-v992.pending')
        with source.open('rb') as src, pending.open('xb') as dst:
            shutil.copyfileobj(src, dst)
            dst.flush()
            os.fsync(dst.fileno())
        if sha(pending) != new_pins[entry]['sha256']:
            raise ValueError('Pending bootstrap copy mismatch')
        os.replace(pending, destination)
        bootstrap_results.append({'file': name, 'before_sha256': old_pins[entry]['sha256'],
                                  'after_sha256': sha(destination)})
        write(ROOT / 'bootstrap-progress.json', bootstrap_results)
    launch_plan = call(INSTALL / 'forge-delivery.exe', 'editor-launch-plan',
                       'launch-plan', INSTALL, 'editor')
    vault = launch_plan['environment'].get('OPENRPG_ASSET_VAULT')
    if Path(vault or '') != Path('F:/OpenRPG_AssetVault'):
        raise ValueError('Installed CLI did not bind the private Vault')
    personal_after = {path: sha(Path(path)) for path in personal_before}
    write(ROOT / 'after-personal-files.json', personal_after)
    if personal_after != personal_before:
        raise ValueError('Authoring files changed during update; inspect before/after evidence')
    write(ROOT / 'RESULT.v1.json', {'status': 'LOCAL_VAULT_PATCH_APPLIED',
          'finished_utc': datetime.now(timezone.utc).isoformat(), 'installation_revision': state['revision'],
          'core_revision': state['active']['core']['revision'], 'rollback_snapshot_preserved': True,
          'bootstrap_results': bootstrap_results, 'authoring_files_unchanged': len(personal_before),
          'vault_binding': vault, 'gui_verified': False, 'studio_and_game_rebuilt': False,
          'release_qualified': False, 'full_import': False})

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        if ROOT.exists():
            write(ROOT / 'FAILURE.v1.json', {'error': str(error), 'full_import': False})
        raise
