"""Reopen the actual installed patch and its retained payload, without mutation."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess

W = Path('F:/OpenRPG_ImportWork')
I = Path('F:/instal test/Installation')
O = W / 'installed-vault-patch-verification-v993'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with p.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    applied = read(W / 'installed-vault-update-apply-v992/RESULT.v1.json')
    state = read(I / 'installation.json')
    before = read(W / 'installed-vault-update-apply-v992/before-state.json')
    assert applied['status'] == 'LOCAL_VAULT_PATCH_APPLIED'
    assert state['revision'] == applied['installation_revision']
    assert state['active']['core']['revision'] == applied['core_revision']
    assert state['previous'] == before['active']
    assert state['user_root'] == before['user_root']
    head = sha(I / 'installation.json')
    current = I / 'packages' / state['active']['core']['id'] / state['active']['core']['revision']
    previous = I / 'packages' / before['active']['core']['id'] / before['active']['core']['revision']
    old_files = {r['path']: r for r in read(previous / 'package-manifest.json')['files']}
    new_files = {r['path']: r for r in read(current / 'package-manifest.json')['files']}
    changed = {'bin/forge-hub.exe', 'bin/forge-delivery.exe'}
    assert set(old_files) <= set(new_files)
    checked = []
    for name, pin in new_files.items():
        p = current / name
        assert p.resolve().is_relative_to(current.resolve()) and p.is_file()
        digest = sha(p)
        assert digest == pin['sha256'], name
        if name in old_files and name not in changed:
            assert digest == old_files[name]['sha256'], name
        checked.append({'path': name, 'sha256': digest})
    for row in applied['bootstrap_results']:
        assert sha(I / row['file']) == row['after_sha256']
        assert sha(W / 'installed-vault-update-apply-v992' / (row['file'] + '.before')) == row['before_sha256']
    personal = read(W / 'installed-vault-update-apply-v992/after-personal-files.json')
    assert all(sha(Path(p)) == digest for p, digest in personal.items())
    O.mkdir(exist_ok=False)
    env = os.environ.copy()
    env.update(TEMP=str(O), TMP=str(O))
    outputs = {}
    for command in ('verify', 'launch-plan'):
        args = [str(I / 'forge-delivery.exe'), command, str(I)]
        if command == 'launch-plan':
            args.append('editor')
        with (O / (command + '.stdout.json')).open('wb') as out, (O / (command + '.stderr.log')).open('wb') as err:
            process = subprocess.Popen(args, stdout=out, stderr=err, env=env, creationflags=subprocess.CREATE_NO_WINDOW)
            (O / 'JOB.v1.json').write_text(json.dumps({'pid': process.pid, 'command': args}), 'utf-8')
            assert process.wait() == 0, command
        outputs[command] = read(O / (command + '.stdout.json'))
    assert outputs['launch-plan']['environment']['OPENRPG_ASSET_VAULT'] == 'F:/OpenRPG_AssetVault'
    assert sha(I / 'installation.json') == head
    (O / 'payload-sha256.v1.json').write_text(json.dumps(checked, indent=2), 'utf-8')
    result = dict(status='INSTALLED_PATCH_REOPENED_AND_VERIFIED', finished_utc=datetime.now(timezone.utc).isoformat(),
                  installation_revision=state['revision'], core_revision=state['active']['core']['revision'],
                  installation_head_sha256=head, payload_files_rehashed=len(checked),
                  retained_payload_files_verified=len(old_files) - len(changed), authoring_files_unchanged=len(personal),
                  rollback_snapshot_preserved=True, installed_cli_verify_exit=0, editor_vault_binding='F:/OpenRPG_AssetVault',
                  normal_hub_gui_verified=False, gui_observation='Computer Use app approval timed out; no GUI completion claimed',
                  studio_and_game_rebuilt=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(result))

if __name__ == '__main__':
    main()
