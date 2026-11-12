"""Check both identities and thumbnails after source alias publication."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import vault_storage as storage


def verify(vault,stage,binary,studio):
    vault,stage,binary,studio=(p.resolve(strict=True) for p in (vault,stage,binary,studio))
    plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
    catalog=(stage/'source-support.v1.json').read_bytes()
    source=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    models=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    sr,mr=source.read_bytes(),models.read_bytes();sp=json.loads(sr)
    if (storage.sha(catalog)!=plan['new_catalog_sha256'] or sp['sha256']!=plan['new_catalog_sha256']
            or storage.sha(mr)!=plan['asset_registry_sha256']):raise ValueError('published registry differs')
    test_sha,studio_sha=storage.sha(binary.read_bytes()),storage.sha(studio.read_bytes())
    env=os.environ.copy();env['OPENRPG_ASSET_VAULT']=str(vault);env['OPENRPG_SOURCE_ALIAS_STAGE']=str(stage)
    run=subprocess.run([str(binary),'--ignored','--exact',
        'asset_library_native::tests::published_source_aliases_preserve_search_and_thumbnails','--nocapture'],
        env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
    output=run.stdout+run.stderr;count=len(plan['pairs'])
    if run.returncode or '1 passed' not in output or f'Verified {count} published source aliases' not in output:
        raise ValueError('published native reader failed: '+output[-4000:])
    if (source.read_bytes()!=sr or models.read_bytes()!=mr or (stage/'plan.json').read_bytes()!=plan_raw
        or (stage/'source-support.v1.json').read_bytes()!=catalog or storage.sha(binary.read_bytes())!=test_sha
        or storage.sha(studio.read_bytes())!=studio_sha):raise ValueError('verification inputs changed')
    return {'state':'passed','pairs':count,'source_registry_sha256':storage.sha(sr),
        'asset_registry_sha256':storage.sha(mr),'stage_plan_sha256':storage.sha(plan_raw),
        'native_test_sha256':test_sha,'studio_binary_sha256':studio_sha,'output':output,
        'limitations':'Native reader, not interactive visual or runtime qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','stage','binary','studio','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=verify(a.vault,a.stage,a.binary,a.studio)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='output'}))
