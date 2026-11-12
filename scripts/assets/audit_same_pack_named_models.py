"""Sequential, resumable comparison of same-pack source-name candidates.

The JSONL output is evidence only, not a selection or removal plan.
"""
import argparse
import collections
import json
import os
import re
from pathlib import Path
import compare_variant_glb_bytes as compare
import vault_storage as storage

WORK = Path(r'F:\OpenRPG_ImportWork\hivemind-run')


def fingerprint(group):
    return storage.sha(json.dumps(group, sort_keys=True, ensure_ascii=False, separators=(',', ':')).encode('utf8'))


def audit(vault, source, output, resume=False):
    vault = vault.resolve(strict=True); src = source.read_bytes(); inv = json.loads(src)
    index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json'); before = index.read_bytes()
    if storage.sha(before) != inv['registry_sha256']:
        raise ValueError('registry changed')
    groups = [g for g in inv['groups'] if g['measurements_exact']]
    header = {'schema': 'openrpg.same-pack-comparison.v1', 'state': 'started',
              'registry_sha256': inv['registry_sha256'], 'source_sha256': storage.sha(src), 'planned': len(groups)}
    done = set()
    if output.exists():
        if not resume: raise ValueError('existing audit needs explicit resume')
        lines = output.read_bytes().splitlines()
        if not lines or json.loads(lines[0]) != header: raise ValueError('audit header mismatch')
        for line in lines[1:]:
            row = json.loads(line)
            if row.get('state') == 'complete': raise ValueError('audit already complete')
            key = row.get('group_sha256')
            if not key or key in done: raise ValueError('invalid or repeated result')
            done.add(key)
    elif resume:
        raise ValueError('no audit to resume')
    allowed = {fingerprint(g) for g in groups}
    if len(allowed) != len(groups) or not done <= allowed: raise ValueError('group scope changed')
    catalogs = {}
    with output.open('a' if resume else 'x', encoding='utf8', newline='\n') as out:
        def write(item):
            out.write(json.dumps(item, ensure_ascii=False, separators=(',', ':')) + '\n'); out.flush(); os.fsync(out.fileno())
        if not resume: write(header)
        for group in groups:
            key = fingerprint(group)
            if key in done: continue
            if index.read_bytes() != before: raise ValueError('registry changed during audit')
            row = {'state': 'compared', 'group_sha256': key, 'pack': group['pack'],
                   'source_filename': group['source_filename'], 'lod': group['lod'], 'cards': []}
            signatures = []
            for card in group['cards']:
                item = {'id': card['id']}
                try:
                    path = storage.confined(vault, Path(card['model_path']))
                    raw = storage.read_bytes(vault, path, 256*1024*1024)
                    if storage.sha(raw) != card['model_sha256']: raise ValueError('model SHA differs')
                    signatures.append(compare.signature(raw))
                    source_path = card.get('source_prefab'); source_path = source_path.get('path') if isinstance(source_path, dict) else source_path
                    if not isinstance(source_path, str) or not source_path.startswith('Assets/'):
                        raise ValueError('unknown source prefab path')
                    parts = group['pack'].split('.variant-')
                    base = WORK / parts[0].split('.')[1] / ('source' if len(parts) == 1 else 'variants/'+parts[1]+'/source')
                    prefab = storage.confined(Path(base.anchor), base/source_path); source_bytes = prefab.read_bytes()
                    item['source_sha256'] = storage.sha(source_bytes)
                    item['component_counts'] = dict(collections.Counter(re.findall(rb'(?m)^--- !u!(\d+) &', source_bytes)))
                    item['component_counts'] = {k.decode('ascii'): v for k,v in item['component_counts'].items()}
                    catalog = storage.confined(vault, Path(card['source_catalog']))
                    if catalog not in catalogs:
                        cat_raw = catalog.read_bytes()
                        catalogs[catalog] = (storage.sha(cat_raw), {x['id']: x for x in json.loads(cat_raw)['assets']})
                    pin, entries = catalogs[catalog]
                    entry = entries[card['id']]
                    item['catalog_sha256'] = pin
                    item['catalog_source_sha256'] = entry.get('source_prefab_sha256') or entry.get('source_sha256')
                    item['source_pin_matches'] = item['catalog_source_sha256'] == item['source_sha256']
                    item['components'] = [{'mesh_sha256': x.get('mesh_sha256'), 'mesh_name': x.get('mesh_name'),
                        'material_guids': x.get('material_guids'), 'lod': (x.get('lod') or {}).get('screenRelativeHeight')}
                        for x in entry.get('components', [])]
                    item['license'] = card.get('license'); item['state'] = 'compared'
                except (OSError, ValueError, KeyError, IndexError, TypeError, StopIteration) as error:
                    if len(signatures) == len(row['cards']): signatures.append(None)
                    item.update(state='review_required', reason=str(error)); row['state'] = 'review_required'
                row['cards'].append(item)
            row['comparisons'] = []
            for i, a in enumerate(signatures):
                for j in range(i+1, len(signatures)):
                    b = signatures[j]
                    if a is None or b is None: continue
                    row['comparisons'].append({'left': row['cards'][i]['id'], 'right': row['cards'][j]['id'],
                        'mesh_payloads_exact': a['mesh_payloads'] == b['mesh_payloads'],
                        'images_exact': a['images'] == b['images'], 'materials_exact': a['materials'] == b['materials'],
                        'transforms_exact': a['transforms'] == b['transforms'],
                        'source_components_same_counts': row['cards'][i].get('component_counts') == row['cards'][j].get('component_counts')})
            write(row); done.add(key)
            print(f'Compared {len(done)}/{len(groups)} same-pack groups', flush=True)
        if (index.read_bytes() != before or source.read_bytes() != src
                or any(storage.sha(path.read_bytes()) != pin for path, (pin, _) in catalogs.items())):
            raise ValueError('inputs changed')
        write({'state': 'complete', 'groups': len(done)})
    return len(done)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault','source','output'): p.add_argument('--'+n, type=Path, required=True)
    p.add_argument('--resume', action='store_true')
    a = p.parse_args()
    print(json.dumps({'state':'complete','groups':audit(a.vault,a.source,a.output,a.resume)}))
