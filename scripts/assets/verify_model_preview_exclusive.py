"""Test all published model thumbnails while old PNGs deny sharing."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import subprocess

import vault_storage as storage


def verify(vault, plan_path, redirect_stage, binary):
    if os.name != 'nt':raise ValueError('Windows exclusive handles required')
    vault=vault.resolve(strict=True)
    plan_raw=plan_path.read_bytes();plan=json.loads(plan_raw)
    redirect_raw=(redirect_stage/'plan.json').read_bytes();redirect=json.loads(redirect_raw)
    if (plan.get('schema')!='openrpg.model-preview-relink-stage.v1'
            or redirect.get('schema')!='openrpg.model-catalog-redirect-stage.v1'
            or redirect.get('prior_preview_plan_sha256')!=storage.sha(plan_raw)
            or redirect.get('candidate_paths')!=plan.get('candidate_paths')):
        raise ValueError('preview transition plans differ')
    paths=[storage.confined(vault,Path(p['old'])) for p in plan['candidate_paths']]
    if not paths or len(set(paths))!=len(paths):raise ValueError('invalid candidate paths')
    reg=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    before,support_before=reg.read_bytes(),support.read_bytes()
    current=json.loads(before)
    for pack,pins in redirect['packs'].items():
        if current['packs'][pack]['revision']!=pins['revision']:raise ValueError('redirect not active')
    kernel=ctypes.WinDLL('kernel32',use_last_error=True);create=kernel.CreateFileW
    create.argtypes=[ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
    create.restype=ctypes.c_void_p;close=kernel.CloseHandle;close.argtypes=[ctypes.c_void_p]
    handles=[]
    try:
        for path in paths:
            name=str(path)
            if not name.startswith('\\\\?\\'):name='\\\\?\\'+name
            handle=create(name,0x80000000,0,None,3,0x00200000,None)
            if handle==ctypes.c_void_p(-1).value:raise ctypes.WinError(ctypes.get_last_error())
            handles.append(handle)
        env=os.environ.copy();env['OPENRPG_ASSET_VAULT']=str(vault);env['OPENRPG_MODEL_PREVIEW_STAGE']=str(redirect_stage.resolve(strict=True))
        command=[str(binary.resolve(strict=True)),'--ignored','--exact',
                 'asset_library_native::tests::published_model_preview_relinks_load_all_thumbnails','--nocapture']
        run=subprocess.run(command,env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
        output=run.stdout+run.stderr
        ids={ident for row in plan['candidate_paths'] for ident in row['ids']}
        if run.returncode or '1 passed' not in output or f'Verified {len(ids)} published model thumbnails' not in output:
            raise ValueError('native preview check failed: '+output[-4000:])
        if (reg.read_bytes()!=before or support.read_bytes()!=support_before
                or plan_path.read_bytes()!=plan_raw or (redirect_stage/'plan.json').read_bytes()!=redirect_raw):
            raise ValueError('inputs changed')
        return {'state':'passed','old_paths_locked':len(paths),'models':len(ids),
                'plan_sha256':storage.sha(plan_raw),'redirect_plan_sha256':storage.sha(redirect_raw),
                'registry_sha256':storage.sha(before),'source_registry_sha256':storage.sha(support_before),
                'native_test_sha256':storage.sha(binary.read_bytes()),'output':output,
                'limitations':'Native search and thumbnails only; external consumers and older snapshots require recovery.'}
    finally:
        for handle in reversed(handles):close(handle)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','plan','redirect-stage','binary','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=verify(a.vault,a.plan,a.redirect_stage,a.binary)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='output'}))
