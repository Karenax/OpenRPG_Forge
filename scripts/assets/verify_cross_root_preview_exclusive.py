"""Exercise the staged native reader while every obsolete preview is locked."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import subprocess
import vault_storage as storage
from verify_cross_root_preview_stage import verify as verify_stage


def verify(vault, stage, binary, studio, published=False):
    if os.name != 'nt':
        raise ValueError('Windows exclusive handles required')
    vault, stage, binary, studio = (p.resolve(strict=True) for p in (vault, stage, binary, studio))
    if not published:
        verify_stage(vault, stage)
    plan_raw = (stage/'plan.json').read_bytes()
    plan = json.loads(plan_raw)
    paths = [storage.confined(vault, Path(p['old'])) for p in plan['candidate_paths']]
    if not paths or len(paths) != len(set(paths)):
        raise ValueError('invalid candidate paths')
    registry = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    support = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    registry_before, support_before = registry.read_bytes(), support.read_bytes()
    if published:
        current = json.loads(registry_before)
        for pack, pins in plan['packs'].items():
            if current['packs'][pack]['revision'] != pins['revision']:
                raise ValueError('preview transition not active')
    binary_sha = storage.sha(binary.read_bytes())
    studio_sha = storage.sha(studio.read_bytes())
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
    create.restype = ctypes.c_void_p
    close = kernel.CloseHandle
    close.argtypes = [ctypes.c_void_p]
    handles = []
    try:
        for path in paths:
            name = str(path)
            if not name.startswith('\\\\?\\'):
                name = '\\\\?\\' + name
            handle = create(name, 0x80000000, 0, None, 3, 0x00200000, None)
            if handle == ctypes.c_void_p(-1).value:
                raise ctypes.WinError(ctypes.get_last_error())
            handles.append(handle)
        env = os.environ.copy()
        env['OPENRPG_ASSET_VAULT'] = str(vault)
        env['OPENRPG_MODEL_PREVIEW_STAGE'] = str(stage)
        test = 'published_model_preview_relinks_load_all_thumbnails' if published else 'staged_model_preview_relinks_load_all_thumbnails'
        run = subprocess.run([str(binary),'--ignored','--exact',
            'asset_library_native::tests::'+test,'--nocapture'],
            env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
        output = run.stdout + run.stderr
        count = len(plan['preview_ids'])
        label = 'published' if published else 'staged'
        if run.returncode or '1 passed' not in output or f'Verified {count} {label} model thumbnails' not in output:
            raise ValueError('native preview check failed: ' + output[-4000:])
        if (registry.read_bytes() != registry_before or support.read_bytes() != support_before
                or (stage/'plan.json').read_bytes() != plan_raw or storage.sha(binary.read_bytes()) != binary_sha
                or storage.sha(studio.read_bytes()) != studio_sha):
            raise ValueError('inputs changed')
        return {'state':'passed','published':published,'old_paths_locked':len(paths),'models':count,
            'stage_plan_sha256':storage.sha(plan_raw),'registry_sha256':storage.sha(registry_before),
            'source_registry_sha256':storage.sha(support_before),'native_test_sha256':binary_sha,
            'studio_binary_sha256':studio_sha,'output':output}
    finally:
        for handle in reversed(handles):
            close(handle)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','binary','studio','output'):
        p.add_argument('--'+name, type=Path, required=True)
    p.add_argument('--published', action='store_true')
    a = p.parse_args()
    result = verify(a.vault, a.stage, a.binary, a.studio, a.published)
    with a.output.open('x',encoding='utf8') as f:
        json.dump(result,f,indent=2)
        f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k != 'output'}))
