"""Run native checks while legacy files are exclusively locked against reads.

Windows only. Locks the exact planned retired paths, never changes file contents,
and releases every handle even if the child check fails.
"""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import subprocess

import vault_storage


def plan_fingerprint(plan):
    return hashlib.sha256(json.dumps(plan, sort_keys=True, ensure_ascii=False, separators=(',', ':')).encode('utf8')).hexdigest()


def validate_scope(plan, manifest):
    aliases = manifest.get('aliases_requiring_project_migration')
    rows = plan.get('candidates')
    if not isinstance(aliases, list) or not aliases or not isinstance(rows, list) or not rows:
        raise ValueError('empty stage or retirement scope')
    by_id = {item['retired_id']: item for item in aliases}
    if len(by_id) != len(aliases):
        raise ValueError('duplicate stage identities')
    seen = set()
    for row in rows:
        ident = row['retired_id']
        if ident in seen or ident not in by_id:
            raise ValueError('retirement identity is repeated or outside stage')
        seen.add(ident)
        for key in ('retained_id', 'retired_id', 'retained_uuid', 'retired_uuid', 'retained_sha256', 'retired_sha256'):
            if row[key] != by_id[ident][key]:
                raise ValueError('retirement pins differ from stage')
        if str(row['retained_path']).replace('\\', '/').casefold() == str(row['retired_path']).replace('\\', '/').casefold():
            raise ValueError('shared model must never be locked as a retired copy')
    return len(aliases)


def validate_export_baseline(baseline_raw, manifest_raw):
    baseline = json.loads(baseline_raw)
    aliases = json.loads(manifest_raw)['aliases_requiring_project_migration']
    expected_pins = {a['retired_id']: a['retired_sha256'] for a in aliases}
    exports = baseline.get('exports')
    if (len(expected_pins) != len(aliases)
            or baseline.get('schema') != 'openrpg.variant-export-baseline.v1'
            or baseline.get('stage_manifest_sha256') != hashlib.sha256(manifest_raw).hexdigest()
            or not isinstance(exports, dict) or set(exports) != set(expected_pins)):
        raise ValueError('baseline does not match stage')
    for ident, entry in exports.items():
        if (not isinstance(entry, dict) or entry.get('sha256') != expected_pins[ident]
                or entry.get('outcome') not in ('success', 'error')
                or (entry['outcome'] == 'error' and not isinstance(entry.get('error'), str))):
            raise ValueError('invalid baseline export outcome')
    return hashlib.sha256(baseline_raw).hexdigest()


def validate(vault, plan, command, env):
    if os.name != 'nt':
        raise ValueError('exclusive Windows file sharing required')
    vault = vault.resolve(strict=True)
    stage_manifest = Path(env['OPENRPG_CONSOLIDATION_STAGE']) / 'transition.json'
    manifest_raw = stage_manifest.read_bytes()
    expected_aliases = validate_scope(plan, json.loads(manifest_raw))
    baseline_path = env.get('OPENRPG_EXPORT_BASELINE')
    baseline_sha = None
    if baseline_path:
        baseline_path = Path(baseline_path).resolve(strict=True)
        baseline_raw = baseline_path.read_bytes()
        baseline_sha = validate_export_baseline(baseline_raw, manifest_raw)
    paths = [vault_storage.confined(vault, Path(row['retired_path'])) for row in plan['candidates']]
    if not paths or len(set(paths)) != len(paths):
        raise ValueError('empty or duplicate legacy paths')
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p,
                       ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    create.restype = ctypes.c_void_p
    close = kernel.CloseHandle
    close.argtypes = [ctypes.c_void_p]
    close.restype = ctypes.c_int
    handles = []
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    registry_before = registry.read_bytes()
    try:
        for path in paths:
            name = str(path)
            if not name.startswith('\\\\?\\'):
                name = '\\\\?\\' + name
            handle = create(name, 0x80000000, 0, None, 3, 0x00200000, None)
            if handle == ctypes.c_void_p(-1).value:
                raise ctypes.WinError(ctypes.get_last_error())
            handles.append(handle)
        child = subprocess.Popen(command, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                 text=True, encoding='utf-8', errors='replace')
        lines = []
        for line in child.stdout:
            lines.append(line)
            print(line, end='', flush=True)
        returncode = child.wait()
        output = ''.join(lines)
        if returncode:
            raise ValueError('native check failed while legacy paths locked: ' + output[-4000:])
        check = 'baseline export outcomes' if baseline_path else 'exports'
        expected_message = f'Verified {expected_aliases} published legacy references, {check} and unique visible IDs'
        if '1 passed' not in output or expected_message not in output:
            raise ValueError('native check did not prove the complete stage')
        if registry.read_bytes() != registry_before:
            raise ValueError('registry changed during exclusive-read check')
        if stage_manifest.read_bytes() != manifest_raw:
            raise ValueError('stage changed during exclusive-read check')
        if baseline_path and hashlib.sha256(baseline_path.read_bytes()).hexdigest() != baseline_sha:
            raise ValueError('export baseline changed during exclusive-read check')
        return {'state': 'passed', 'exclusive_legacy_paths': len(handles),
                'child_command': command, 'child_stdout': output,
                'child_stderr': '', 'models_removed': 0,
                'registry_sha256': hashlib.sha256(registry_before).hexdigest(),
                'plan_fingerprint': plan_fingerprint(plan),
                'test_binary_sha256': hashlib.sha256(Path(command[0]).read_bytes()).hexdigest(),
                'export_baseline': str(baseline_path) if baseline_path else None,
                'export_baseline_sha256': baseline_sha,
                'stage_manifest_sha256': hashlib.sha256(manifest_raw).hexdigest()}
    finally:
        for handle in reversed(handles):
            close(handle)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--test-binary', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    plan = json.loads(args.plan.read_text(encoding='utf-8'))
    env = dict(os.environ, OPENRPG_ASSET_VAULT=str(args.vault), OPENRPG_CONSOLIDATION_STAGE=str(args.stage))
    test = 'asset_library_native::tests::published_consolidation_resolves_and_exports_all_legacy_models'
    result = validate(args.vault, plan, [str(args.test_binary), test, '--ignored', '--exact', '--nocapture'], env)
    with args.output.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': result['state'], 'locked_paths': result['exclusive_legacy_paths'], 'models_removed': 0}))
