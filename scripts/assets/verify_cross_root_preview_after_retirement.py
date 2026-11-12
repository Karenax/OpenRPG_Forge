"""Verify retired paths, surviving hashes, and the published native reader."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import vault_storage as storage


def verify(vault,stage,binary):
    vault,stage,binary=(p.resolve(strict=True) for p in (vault,stage,binary))
    plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
    registry=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    before,sb=registry.read_bytes(),support.read_bytes();current=json.loads(before)
    for pack,pins in plan['packs'].items():
        if current['packs'][pack]['revision']!=pins['revision']:raise ValueError('published revision changed')
    kept={}
    for pair in plan['candidate_paths']:
        old=Path(pair['old']);keep=storage.confined(vault,Path(pair['retained']))
        if old.exists():raise ValueError('retired path still exists: '+str(old))
        if keep not in kept:
            with keep.open('rb') as stream:kept[keep]=hashlib.file_digest(stream,'sha256').hexdigest()
        if kept[keep]!=pair['sha256']:raise ValueError('retained SHA changed')
    env=os.environ.copy();env['OPENRPG_ASSET_VAULT']=str(vault);env['OPENRPG_MODEL_PREVIEW_STAGE']=str(stage)
    binary_sha=storage.sha(binary.read_bytes())
    run=subprocess.run([str(binary),'--ignored','--exact',
        'asset_library_native::tests::published_model_preview_relinks_load_all_thumbnails','--nocapture'],
        env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
    output=run.stdout+run.stderr;count=len(plan['preview_ids'])
    if run.returncode or '1 passed' not in output or f'Verified {count} published model thumbnails' not in output:
        raise ValueError('native test failed: '+output[-4000:])
    if (registry.read_bytes()!=before or support.read_bytes()!=sb or (stage/'plan.json').read_bytes()!=plan_raw
        or storage.sha(binary.read_bytes())!=binary_sha):raise ValueError('inputs changed')
    return {'state':'passed','retired_paths_absent':len(plan['candidate_paths']),'kept_hashes':len(kept),
        'models':count,'registry_sha256':storage.sha(before),'native_test_sha256':binary_sha,'output':output}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','binary','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();result=verify(a.vault,a.stage,a.binary)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='output'}))
