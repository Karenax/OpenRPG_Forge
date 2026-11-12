"""Reconstruct staged source pairs; do not publish or remove Vault files."""
import hashlib
import json
from pathlib import Path

from materialize_source_support_asset import materialize

base=Path('E:/OpenRPG_Forge/.build/asset-conversion')
stage=base/'source-variant-shared-meta-stage-20261112'
destination=base/'source-variant-shared-meta-probe-20261112'
report=base/'source-variant-shared-meta-probe-20261112.json'
vault=Path('F:/OpenRPG_AssetVault')
plan=json.loads((stage/'plan.json').read_bytes())
ids=set(plan['changed_ids'])
cards=[x for x in json.loads((stage/'source-support.v1.json').read_bytes())['assets'] if x['id'] in ids]
if len(cards)!=len(ids) or destination.exists() or report.exists():raise ValueError('invalid selection or probe exists')
destination.mkdir()
seen={};result=[]
for card in cards:
    logical=card['source_asset_path'].casefold()
    pins=(card['source_sha256'],card['source_meta_sha256'])
    if logical in seen:
        if seen[logical]!=pins:raise ValueError('same Unity path with different payloads')
        continue
    seen[logical]=pins
    item=materialize(vault,card,destination)
    result.append({'id':card['id'],'path':card['source_asset_path'],'source_sha256':pins[0],'meta_sha256':pins[1]})
with report.open('x',encoding='utf8') as stream:
    json.dump({'state':'pairs_materialized','plan_sha256':hashlib.sha256((stage/'plan.json').read_bytes()).hexdigest(),
               'cards':len(cards),'unique_paths':len(result),'items':result,
               'limitations':'Source and sidecar only; no dependency graph, Unity import or runtime verification.'},stream,indent=2)
    stream.write('\n')
print(json.dumps({'state':'pairs_materialized','cards':len(cards),'unique_paths':len(result)}))
