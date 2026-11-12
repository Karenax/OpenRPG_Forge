"""Run a native source reader while old source paths deny all sharing."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import subprocess

import vault_storage as storage
from stage_source_support_relinks import norm


def verify(vault, plan_file, ids_file, binary):
    if os.name != 'nt':
        raise ValueError('Windows exclusive handles required')
    vault = vault.resolve(strict=True)
    plan_raw, ids_raw = plan_file.read_bytes(), ids_file.read_bytes()
    plan, ids = json.loads(plan_raw), json.loads(ids_raw)
    if (plan.get('schema') != 'openrpg.source-support-relinks-stage.v1' or not isinstance(ids, list)
            or not ids or len(set(ids)) != len(ids) or not set(ids).issubset(plan['changed_ids'])):
        raise ValueError('invalid plan or selection')
    paths = []
    for row in plan['paths']:
        norm(row['old'])
        paths.append(storage.confined(vault, vault/row['old']))
    if not paths or len(set(paths)) != len(paths):
        raise ValueError('invalid old path list')
    support = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    registry = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    support_before, registry_before = support.read_bytes(), registry.read_bytes()
    if json.loads(support_before).get('sha256') != plan['staged_catalog_sha256']:
        raise ValueError('relinked catalog not active')
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p,
                       ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    create.restype = ctypes.c_void_p
    close = kernel.CloseHandle
    close.argtypes = [ctypes.c_void_p]
    handles = []
    try:
        for path in paths:
            native = str(path)
            if not native.startswith('\\\\?\\'):
                native = '\\\\?\\' + native
            handle = create(native, 0x80000000, 0, None, 3, 0x00200000, None)
            if handle == ctypes.c_void_p(-1).value:
                raise ctypes.WinError(ctypes.get_last_error())
            handles.append(handle)
        env = os.environ.copy()
        env['OPENRPG_ASSET_VAULT'] = str(vault)
        env['OPENRPG_ASSET_VERIFY_IDS_JSON'] = str(ids_file.resolve(strict=True))
        command = [str(binary.resolve(strict=True)), '--ignored', '--exact',
                   'asset_library_native::tests::live_selected_source_support', '--nocapture']
        run = subprocess.run(command, env=env, capture_output=True, text=True, encoding='utf8', errors='replace')
        output = run.stdout + run.stderr
        if run.returncode or '1 passed' not in output or f'Selected private sources checked: {len(ids)} details' not in output:
            raise ValueError('native source reader failed under exclusive locks: '+output[-4000:])
        if (support.read_bytes() != support_before or registry.read_bytes() != registry_before
                or plan_file.read_bytes() != plan_raw or ids_file.read_bytes() != ids_raw):
            raise ValueError('inputs changed during check')
        return {'state': 'passed', 'old_paths_locked': len(handles), 'selected_cards': len(ids),
                'binary_sha256': storage.sha(binary.read_bytes()), 'plan_sha256': storage.sha(plan_raw),
                'source_registry_sha256': storage.sha(support_before), 'output': output,
                'limitations': 'Selected native-reader paths only; Unity dependencies and external consumers unverified.'}
    finally:
        for handle in reversed(handles):
            close(handle)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'plan', 'ids', 'binary', 'output'):
        p.add_argument('--'+name, type=Path, required=True)
    a = p.parse_args()
    result = verify(a.vault, a.plan, a.ids, a.binary)
    with a.output.open('x', encoding='utf8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k != 'output'}))
