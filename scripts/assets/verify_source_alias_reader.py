"""Pin a native staged-source test to the installed Studio and current Vault."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import vault_storage as storage


def verify(vault, stage, binary, studio):
    vault, stage, binary, studio = (p.resolve(strict=True) for p in (vault, stage, binary, studio))
    plan_raw = (stage/'plan.json').read_bytes()
    plan = json.loads(plan_raw)
    catalog = (stage/'source-support.v1.json').read_bytes()
    registry = (vault/'metadata/asset-import-registry.v1.json').read_bytes()
    support = (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()
    pins = (storage.sha(plan_raw), storage.sha(catalog), storage.sha(binary.read_bytes()), storage.sha(studio.read_bytes()))
    if pins[1] != plan['new_catalog_sha256'] or storage.sha(registry) != plan['asset_registry_sha256'] or storage.sha(support) != plan['source_registry_sha256']:
        raise ValueError('stage or registry changed')
    env = os.environ.copy()
    env['OPENRPG_ASSET_VAULT'] = str(vault)
    env['OPENRPG_SOURCE_ALIAS_STAGE'] = str(stage)
    run = subprocess.run([str(binary),'--ignored','--exact',
        'asset_library_native::tests::staged_source_aliases_preserve_search_and_details','--nocapture'],
        env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
    output = run.stdout + run.stderr
    count = len(plan['pairs'])
    if run.returncode or '1 passed' not in output or f'Verified {count} staged source aliases' not in output:
        raise ValueError('native test failed: '+output[-4000:])
    if ((stage/'plan.json').read_bytes() != plan_raw or (stage/'source-support.v1.json').read_bytes() != catalog
        or (vault/'metadata/asset-import-registry.v1.json').read_bytes() != registry
        or (vault/'metadata/asset-source-support-registry.v1.json').read_bytes() != support
        or storage.sha(binary.read_bytes()) != pins[2] or storage.sha(studio.read_bytes()) != pins[3]):
        raise ValueError('test inputs changed')
    return {'state':'passed','stage_plan_sha256':pins[0],'stage_catalog_sha256':pins[1],
        'native_test_sha256':pins[2],'studio_binary_sha256':pins[3],
        'source_registry_sha256':storage.sha(support),'asset_registry_sha256':storage.sha(registry),
        'pairs':count,'output':output}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','binary','studio','output'):
        p.add_argument('--'+name, type=Path, required=True)
    a = p.parse_args()
    result = verify(a.vault,a.stage,a.binary,a.studio)
    with a.output.open('x',encoding='utf8') as f:
        json.dump(result,f,indent=2)
        f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k != 'output'}))
