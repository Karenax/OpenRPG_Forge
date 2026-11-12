"""Check staged model cards with the native Studio reader using a pinned adapter."""
import json
import os
from pathlib import Path
import subprocess

import vault_storage as storage

base = Path('E:/OpenRPG_Forge/.build/asset-conversion')
stage = base/'townsmith-model-prefab-relink-stage-20261112'
adapter = base/'townsmith-model-prefab-native-adapter-20261112'
binary = Path('E:/OpenRPG_Forge/.build/cargo-target/debug/deps/forge_studio_frontend-6b79835d416d8db9.exe')
studio = Path('E:/OpenRPG_Forge/.build/frontend/studio-native/forge-studio.exe')
original = (stage/'plan.json').read_bytes()
adapted = (adapter/'plan.json').read_bytes()
plan = json.loads(original); checked = json.loads(adapted)
if checked.pop('schema') != 'openrpg.nested-archive-relink-stage.v1':
    raise ValueError('invalid adapter schema')
schema = plan.pop('schema')
if schema != 'openrpg.model-prefab-relink-stage.v1' or checked != plan:
    raise ValueError('adapter differs from staged plan')
for pack in plan['packs']:
    for filename in ('details.v1.json','profiles.v1.json'):
        if (stage/pack/filename).read_bytes() != (adapter/pack/filename).read_bytes():
            raise ValueError('adapter snapshot differs')
test_sha, studio_sha = storage.sha(binary.read_bytes()), storage.sha(studio.read_bytes())
env = os.environ.copy()
env['OPENRPG_ASSET_VAULT'] = 'F:/OpenRPG_AssetVault'
env['OPENRPG_MODEL_PREVIEW_STAGE'] = str(adapter)
result = subprocess.run([str(binary),'--ignored','--exact',
    'asset_library_native::tests::staged_model_catalog_redirects_preserve_cards_and_thumbnails','--nocapture'],
    env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
output = result.stdout + result.stderr
if result.returncode or '1 passed' not in output or f'Verified {len(plan["changed_ids"])} staged catalog redirects' not in output:
    raise ValueError('native reader failed: '+output[-4000:])
if ((stage/'plan.json').read_bytes() != original or (adapter/'plan.json').read_bytes() != adapted
        or storage.sha(binary.read_bytes()) != test_sha or storage.sha(studio.read_bytes()) != studio_sha):
    raise ValueError('verification inputs changed')
proof = {'state':'passed','plan_sha256':storage.sha(original),'adapter_sha256':storage.sha(adapted),
         'native_test_sha256':test_sha,'studio_binary_sha256':studio_sha,
         'registry_sha256':plan['registry_sha256'],'source_registry_sha256':plan['source_registry_sha256'],
         'cards':len(plan['changed_ids']), 'limitations':'Native reader, not interactive rendering.'}
with (base/'townsmith-model-prefab-native-stage-proof-20261112.json').open('x',encoding='utf8') as stream:
    json.dump(proof,stream,indent=2); stream.write('\n')
print(json.dumps(proof))
