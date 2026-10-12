"""Exercise old-to-new installation and rollback in fresh, isolated test roots."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--previous', type=Path, required=True)
    parser.add_argument('--delivery', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    args.evidence.mkdir(parents=True, exist_ok=False)
    root = args.evidence / 'installation'
    user = args.evidence / 'user-data'
    env = dict(os.environ, FORGE_DELIVERY_USER_ROOT=str(user))
    cli = args.delivery / 'forge-delivery.exe'
    def call(*command):
        result = subprocess.run([str(cli), *map(str, command)], env=env,
                                capture_output=True, text=True, encoding='utf-8')
        if result.returncode:
            raise RuntimeError(result.stderr[-4000:])
        return json.loads(result.stdout)
    def apply(plan, name):
        file = args.evidence / f'{name}-plan.json'
        file.write_text(json.dumps(plan, indent=2) + '\n', encoding='utf-8')
        return call('apply', file)
    def packs(delivery):
        index = json.loads((delivery / 'delivery.json').read_bytes())
        result = []
        for entry in index['packages']:
            archive = delivery / entry['file']
            if sha(archive) != entry['archive_sha256']:
                raise ValueError(f'Archive changed: {archive}')
            result.append(archive)
        return result
    old = apply(call('plan', root, *packs(args.previous)), 'previous')
    workspace = user / 'workspaces/openrpg-forge'
    edited = next(workspace.rglob('authoring.json'))
    edited.write_bytes(edited.read_bytes() + b'\n  \n')
    sentinel = workspace / 'user-retouch.txt'
    sentinel.write_text('User work must survive upgrade and rollback.\n', encoding='utf-8')
    user_before = {p.relative_to(user).as_posix(): sha(p) for p in user.rglob('*') if p.is_file()}
    new = apply(call('plan', root, *packs(args.delivery)), 'upgrade')
    if old['revision'] == new['revision']:
        raise ValueError('Upgrade did not create a new installation revision')
    if old['active']['core'] == new['active']['core']:
        raise ValueError('Upgrade did not install the new application package')
    call('verify', root)
    user_after = {p.relative_to(user).as_posix(): sha(p) for p in user.rglob('*') if p.is_file()}
    if user_after != user_before:
        raise ValueError('Upgrade changed user work')
    rollback = apply(call('rollback-plan', root), 'rollback')
    call('verify', root)
    user_rollback = {p.relative_to(user).as_posix(): sha(p) for p in user.rglob('*') if p.is_file()}
    if user_rollback != user_before:
        raise ValueError('Rollback changed user work')
    # The rollback creates an audited revision, restoring the previous package set.
    if rollback['active'] != old['active']:
        raise ValueError('Rollback did not restore the previous package set')
    report = {'schema': 'forge.delivery.upgrade-validation.v1', 'status': 'PASS_LOCAL_UPGRADE_ROLLBACK',
              'validation_script_sha256': sha(Path(__file__)),
              'previous_revision': old['revision'], 'upgrade_revision': new['revision'],
              'rollback_revision': rollback['revision'], 'user_files_preserved': len(user_before),
              'edited_authoring_preserved': True, 'full_file_verification': True,
              'visual_acceptance': False, 'gate3_closed': False, 'release_qualified': False}
    (args.evidence / 'verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
