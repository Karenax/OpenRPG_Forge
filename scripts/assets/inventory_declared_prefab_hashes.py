"""Find active assets that declare identical prefab SHA and LOD.

Declared identities are leads only; no source/model bytes are verified here.
"""
import argparse
import collections
import json
import re
from pathlib import Path
import vault_storage as storage

WORK = Path(r'F:\OpenRPG_ImportWork\hivemind-run')

def prefab_pin(card, entry):
    source = card.get('source_prefab')
    if isinstance(source, dict) and storage.valid_hex(source.get('sha256'), 64):
        return source['sha256']
    if isinstance(source, list):
        sources = [x for x in source if isinstance(x, dict) and x.get('source_kind') == 'asset'
                   and str(x.get('original_source_path', '')).lower().endswith('.prefab')
                   and storage.valid_hex(x.get('sha256'), 64)]
        if len(sources) == 1: return sources[0]['sha256']
    pin = entry.get('source_prefab_sha256')
    if storage.valid_hex(pin,64): return pin
    support = entry.get('source_support_provenance') or {}
    if str(support.get('source_file','')).lower().endswith('.prefab') and storage.valid_hex(support.get('source_sha256'),64):
        return support['source_sha256']
    return None


def inventory(vault, registry_sha, excluded, read_missing_sources=False):
    vault = vault.resolve(strict=True); index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    if storage.sha(before) != registry_sha: raise ValueError('registry changed')
    groups = collections.defaultdict(list); catalogs = {}; missing = collections.Counter(); missing_cards = []; seen = set(); source_cache = {}
    for pack, item in sorted(json.loads(before)['packs'].items()):
        path = storage.confined(vault, vault/item['path']/'details.v1.json'); raw = path.read_bytes()
        if storage.sha(raw) != item['files']['details.v1.json']: raise ValueError('details changed')
        for card in json.loads(raw)['assets']:
            ident = card['id']
            if ident in seen: raise ValueError('duplicate active ID')
            seen.add(ident)
            if ident in excluded: continue
            entry = {}
            try:
                cat = storage.confined(vault, Path(card['source_catalog']))
                if cat not in catalogs:
                    raw_cat=cat.read_bytes(); obj=json.loads(raw_cat)
                    catalogs[cat]=(storage.sha(raw_cat), {x['id']:x for x in obj['assets']})
                entry=catalogs[cat][1].get(ident,{})
            except (OSError, ValueError, KeyError, TypeError) as error:
                missing['catalog_unavailable'] += 1
            pin=prefab_pin(card,entry)
            pin_origin = 'declared_prefab_pin'
            if not pin and read_missing_sources:
                source=card.get('source_prefab')
                source=source.get('path') if isinstance(source,dict) else source
                if isinstance(source,str) and source.lower().endswith('.prefab'):
                    try:
                        if source.startswith('Assets/'):
                            parts=pack.split('.variant-')
                            base=WORK/parts[0].split('.')[1]/('source' if len(parts)==1 else 'variants/'+parts[1]+'/source')
                            path=storage.confined(Path(base.anchor),base/source)
                        elif Path(source).is_absolute():
                            path=storage.confined(vault,Path(source))
                        else:
                            raise ValueError('unsupported relative source')
                        if path not in source_cache:source_cache[path]=storage.sha(path.read_bytes())
                        pin=source_cache[path];pin_origin='observed_source_bytes'
                    except (OSError,ValueError):
                        missing['source_unavailable']+=1
            if not pin:
                missing['no_unambiguous_prefab_pin'] += 1
                missing_cards.append({'id': ident, 'pack': pack,
                                      'source_prefab': card.get('source_prefab'),
                                      'source_support_provenance': entry.get('source_support_provenance'),
                                      'model_path': card.get('model_path'),
                                      'model_sha256': card.get('model_sha256')})
                continue
            lod_match=re.search(r'-lod(\d+)$',ident,re.I); lod=int(lod_match.group(1)) if lod_match else 0
            groups[(pin,lod)].append({'id':ident,'pack':pack,'source_prefab':card.get('source_prefab'),
                                      'model_path':card.get('model_path'),'model_sha256':card.get('model_sha256'),
                                      'license':card.get('license'),'pin_origin':pin_origin})
    if index.read_bytes()!=before:raise ValueError('registry changed')
    if any(storage.sha(path.read_bytes())!=pin for path,(pin,_) in catalogs.items()):raise ValueError('catalog changed')
    repeated=[{'prefab_sha256':key[0],'lod':key[1],'cards':cards} for key,cards in sorted(groups.items()) if len(cards)>1]
    return {'state':'inventory_only','registry_sha256':registry_sha,'groups':repeated,'missing':dict(missing),
            'missing_cards':missing_cards,
            'excluded_ids':len(excluded),'unique_sources_read':len(source_cache),
            'limitations':'Declared pins are not reread unless marked observed; GLB semantics are not checked and the same source can have distinct roles.'}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--plan',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--read-missing-sources',action='store_true')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    plan=json.loads(a.plan.read_bytes());excluded={x['retired_id'] for x in plan['candidates']}
    result=inventory(a.vault,plan['registry_sha256'],excluded,a.read_missing_sources)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'groups':len(result['groups']),'cards':sum(len(g['cards']) for g in result['groups']),
                      'unique_sources_read':result['unique_sources_read'],'missing':result['missing']}))
