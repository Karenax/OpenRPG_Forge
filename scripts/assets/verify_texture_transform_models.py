"""Recheck logged export failures with a SHA-pinned Rust test.

Materializes one exact logical GLB at a time in the designated work directory.
The test confirms byte admission, not the renderer's visual handling of UVs.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

import vault_storage


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def failure_ids(log, marker='KHR_texture_transform'):
    prefix = 'Pre-existing export failure for '
    result = set()
    for line in log.splitlines():
        if line.startswith(prefix) and marker in line:
            result.add(line[len(prefix):].split(': ', 1)[0])
    return result


def verify(vault, plan_path, log_path, test_binary, work, marker='KHR_texture_transform',
           test_name='private_glb_passes_governed_media_validation'):
    vault = vault.resolve(strict=True)
    work = work.resolve(strict=True)
    allowed = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if not work.is_relative_to(allowed):
        raise ValueError('work directory outside conversion workspace')
    registry = vault / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    plan = json.loads(plan_path.read_bytes())
    if sha(before) != plan['registry_sha256']:
        raise ValueError('registry changed')
    log_raw = log_path.read_bytes()
    ids = failure_ids(log_raw.decode('utf8'), marker)
    selected = [r for r in plan['candidates'] if r['retired_id'] in ids]
    if not ids or len(selected) != len(ids):
        raise ValueError('failure list does not match plan')
    executable_sha = sha(test_binary.read_bytes())
    target = work / 'current-model.glb'
    if target.exists():
        raise ValueError('previous temporary model requires inspection')
    results = []
    for index, item in enumerate(selected, 1):
        model = vault_storage.confined(vault, Path(item['retired_path']))
        raw = vault_storage.read_bytes(vault, model, 512 * 1024 * 1024)
        if sha(raw) != item['retired_sha256']:
            raise ValueError('logical model SHA changed')
        try:
            with target.open('xb') as stream:
                stream.write(raw)
            environment = dict(os.environ, OPENRPG_TEST_GLB=str(target),
                               OPENRPG_TEST_GLB_SHA256=item['retired_sha256'])
            completed = subprocess.run([str(test_binary), test_name,
                '--ignored', '--exact', '--nocapture'], env=environment,
                capture_output=True, text=True, encoding='utf8', errors='replace')
            output = completed.stdout + completed.stderr
            passed = completed.returncode == 0 and '1 passed' in output
            results.append({'id': item['retired_id'], 'sha256': item['retired_sha256'],
                            'state': 'passed' if passed else 'failed',
                            'test_output': output if not passed else None})
        finally:
            target.unlink(missing_ok=True)
        if index % 10 == 0:
            print(f'validated {index}/{len(selected)} models', flush=True)
    if registry.read_bytes() != before or sha(test_binary.read_bytes()) != executable_sha:
        raise ValueError('registry or test binary changed')
    return {'state': 'finished', 'registry_sha256': sha(before), 'baseline_log_sha256': sha(log_raw),
            'test_binary_sha256': executable_sha, 'results': results,
            'limitations': 'Media validation only; no Studio render, placement or runtime qualification.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--log', type=Path, required=True)
    parser.add_argument('--test-binary', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--error-marker', default='KHR_texture_transform')
    parser.add_argument('--test-name', default='private_glb_passes_governed_media_validation')
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('output already exists')
    result = verify(args.vault, args.plan, args.log, args.test_binary, args.work,
                    args.error_marker, args.test_name)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'count': len(result['results']), 'passed': sum(x['state'] == 'passed' for x in result['results'])}))
