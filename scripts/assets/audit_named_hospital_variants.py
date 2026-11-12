"""Compare Hospital door variants with distinct GUIDs; never remove assets."""
import argparse
import json
from pathlib import Path
import compare_variant_glb_bytes as compare
import vault_storage as storage

ROOT = Path(r'F:\OpenRPG_ImportWork\hivemind-run\modular-abandoned-hospital')
LIMIT = 256 * 1024 * 1024


def audit(vault, inventory):
    vault = vault.resolve(strict=True)
    raw_inventory = inventory.read_bytes(); leads = json.loads(raw_inventory)
    index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json'); before = index.read_bytes()
    if storage.sha(before) != leads['registry_sha256']:
        raise ValueError('registry changed')
    groups = [g for g in leads['groups'] if g['base_pack'] == 'hivemind.modular-abandoned-hospital']
    ids = {c['id'] for g in groups for c in g['cards']}
    cards = {}
    for entry in json.loads(before)['packs'].values():
        path = storage.confined(vault, vault / entry['path'] / 'details.v1.json'); raw = path.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']:
            raise ValueError('active details changed')
        for card in json.loads(raw)['assets']:
            if card['id'] in ids:
                if card['id'] in cards:
                    raise ValueError('duplicate identity')
                cards[card['id']] = card
    if set(cards) != ids:
        raise ValueError('missing cards')
    out = []
    for group in groups:
        row = {'source_filename': group['source_filename'], 'lod': group['lod'], 'variants': [], 'comparisons': []}
        signatures = []
        for lead in group['cards']:
            card = cards[lead['id']]
            record = {'id': card['id']}
            try:
                if card.get('model_path') != lead['model_path'] or card.get('model_sha256') != lead['model_sha256']:
                    raise ValueError('model pin changed')
                path = storage.confined(vault, Path(card['model_path']))
                raw = storage.read_bytes(vault, path, LIMIT)
                if storage.sha(raw) != card['model_sha256']:
                    raise ValueError('logical model SHA differs')
                sig = compare.signature(raw)
                pack = lead['pack']
                base = ROOT / ('source' if '.variant-' not in pack else 'variants/' + pack.split('.variant-')[-1] + '/source')
                files = {}
                for key in ('source_prefab', 'source_fbx'):
                    value = card.get(key)
                    if not isinstance(value, str) or not value.startswith('Assets/'):
                        raise ValueError('unexpected source path')
                    source = storage.confined(Path(base.anchor), base / value)
                    files[key] = storage.sha(source.read_bytes())
                record.update(state='compared', logical_bytes=len(raw), source_sha256=files,
                              license=card.get('license'), readiness=card.get('readiness'))
                signatures.append(sig)
            except (OSError, ValueError, KeyError, TypeError, IndexError) as error:
                record.update(state='review_required', reason=str(error))
                signatures.append(None)
            row['variants'].append(record)
        for pos in range(1, len(signatures)):
            a, b = signatures[0], signatures[pos]
            if a is None or b is None:
                continue
            row['comparisons'].append({'against': row['variants'][pos]['id'],
                'mesh_payloads_exact': a['mesh_payloads'] == b['mesh_payloads'],
                'images_exact': a['images'] == b['images'], 'materials_exact': a['materials'] == b['materials'],
                'transforms_exact': a['transforms'] == b['transforms'],
                'source_prefab_exact': row['variants'][0]['source_sha256']['source_prefab'] == row['variants'][pos]['source_sha256']['source_prefab'],
                'source_fbx_exact': row['variants'][0]['source_sha256']['source_fbx'] == row['variants'][pos]['source_sha256']['source_fbx']})
        out.append(row)
        print(f"Compared {len(out)}/{len(groups)} Hospital source-name groups", flush=True)
    if index.read_bytes() != before or inventory.read_bytes() != raw_inventory:
        raise ValueError('inputs changed during comparison')
    return {'state': 'audit_only', 'registry_sha256': leads['registry_sha256'], 'groups': out,
            'limitations': 'Filename matches are candidates, not proof of identical semantics or permission to remove.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault', 'inventory', 'output'):
        p.add_argument('--'+n, type=Path, required=True)
    args = p.parse_args()
    if args.output.exists(): p.error('output exists')
    result = audit(args.vault, args.inventory)
    with args.output.open('x', encoding='utf8') as out:
        json.dump(result, out, ensure_ascii=False, indent=2); out.write('\n')
    print(json.dumps({'groups': len(result['groups']), 'state': result['state']}))
