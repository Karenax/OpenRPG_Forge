"""Refine source pins and LOD values for an existing same-pack audit."""
import argparse
import json
import re
from pathlib import Path
import vault_storage as storage

WORK = Path(r'F:\OpenRPG_ImportWork\hivemind-run')


def refine(vault, previous, inventory):
    vault = vault.resolve(strict=True); prev = previous.read_bytes(); src = inventory.read_bytes()
    lines = [json.loads(x) for x in prev.splitlines()]
    if not lines or lines[-1].get('state') != 'complete': raise ValueError('comparison incomplete')
    expected = json.loads(src); index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json'); before = index.read_bytes()
    if (storage.sha(before) != expected['registry_sha256'] or lines[0]['registry_sha256'] != expected['registry_sha256']
            or lines[0]['source_sha256'] != storage.sha(src)):
        raise ValueError('stale input')
    ids = {x['id'] for group in lines[1:-1] for x in group['cards']}; cards = {}
    for entry in json.loads(before)['packs'].values():
        path = storage.confined(vault, vault/entry['path']/'details.v1.json'); raw = path.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']: raise ValueError('details changed')
        for card in json.loads(raw)['assets']:
            if card['id'] in ids:
                if card['id'] in cards: raise ValueError('repeated identity')
                cards[card['id']] = card
    if set(cards) != ids: raise ValueError('missing card')
    catalogs = {}; out = []
    for group in lines[1:-1]:
        row = {'pack': group['pack'], 'source_filename': group['source_filename'], 'lod': group['lod'],
               'comparison': group['comparisons'], 'cards': []}
        for old in group['cards']:
            card = cards[old['id']]; item = {'id': old['id']}
            try:
                cat = storage.confined(vault, Path(card['source_catalog']))
                if cat not in catalogs:
                    raw = cat.read_bytes(); catalogs[cat] = (storage.sha(raw), {x['id']: x for x in json.loads(raw)['assets']})
                cat_sha, entries = catalogs[cat]; entry = entries[card['id']]
                if cat_sha != old.get('catalog_sha256') or entry.get('sha256') != card.get('model_sha256'):
                    raise ValueError('catalog or model pin changed')
                source = card.get('source_prefab'); source = source.get('path') if isinstance(source, dict) else source
                if not isinstance(source, str) or not source.startswith('Assets/'): raise ValueError('unknown source path')
                parts = group['pack'].split('.variant-')
                base = WORK/parts[0].split('.')[1]/('source' if len(parts)==1 else 'variants/'+parts[1]+'/source')
                path = storage.confined(Path(base.anchor),base/source); content = path.read_bytes(); sha = storage.sha(content)
                support = entry.get('source_support_provenance') or {}
                pin = entry.get('source_prefab_sha256') or entry.get('source_sha256') or support.get('source_sha256')
                if not storage.valid_hex(pin,64) or sha != pin or sha != old.get('source_sha256'):
                    raise ValueError('source pin unavailable or changed')
                text = content.decode('utf8',errors='strict')
                thresholds = re.findall(r'(?m)^\s*-\s*screenRelativeHeight:\s*([^\s]+)',text)
                lod_groups = re.findall(r'(?ms)^--- !u!205 &[^\r\n]*\r?\n(.*?)(?=^--- !u!|\Z)', text)
                bounds = []
                for lod in lod_groups:
                    size = re.search(r'(?m)^\s*m_Size:\s*([^\r\n]+)', lod)
                    center = re.search(r'(?m)^\s*m_LocalReferencePoint:\s*([^\r\n]+)', lod)
                    bounds.append({'size': size.group(1).strip() if size else None,
                                   'center': center.group(1).strip() if center else None})
                item.update(state='verified', source_sha256=sha, source_path=str(path),
                            lod_thresholds=thresholds, lod_group_bounds=bounds, component_counts=old['component_counts'],
                            metadata_ready=card.get('readiness',{}).get('metadata_ready'),
                            missing=card.get('missing'), catalog_sha256=cat_sha)
            except (OSError,ValueError,KeyError,TypeError,UnicodeDecodeError) as error:
                item.update(state='review_required', reason=str(error))
            row['cards'].append(item)
        out.append(row)
    if (index.read_bytes()!=before or previous.read_bytes()!=prev or inventory.read_bytes()!=src
            or any(storage.sha(path.read_bytes())!=sha for path,(sha,_) in catalogs.items())):
        raise ValueError('inputs changed during refinement')
    return {'state':'audit_only','registry_sha256':expected['registry_sha256'],'groups':out,
            'limitations':'Component counts and LOD thresholds alone do not establish full prefab equivalence.'}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','previous','inventory','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=refine(a.vault,a.previous,a.inventory)
    with a.output.open('x',encoding='utf8') as out:json.dump(result,out,ensure_ascii=False,indent=2);out.write('\n')
    print(json.dumps({'groups':len(result['groups']),'cards_verified':sum(c['state']=='verified' for g in result['groups'] for c in g['cards'])}))
