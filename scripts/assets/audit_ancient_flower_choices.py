"""Choose the controlled-render URP flower conversion without claiming parity."""
import argparse
import hashlib
import json
from pathlib import Path

import audit_military_container_choices as common
import compare_variant_glb_bytes as compare
from audit_witch_source_choices import triangle_positions
import vault_storage as storage

VAULT = Path(r'F:\OpenRPG_AssetVault')
GUIDS = {'abc00000000006225239267477201202', 'abc00000000005377290520782814903'}


def check(cards, entries, unused_sources=()):
    if not cards[0].get('license') or cards[0]['license'] != cards[1].get('license'):
        raise ValueError('license differs')
    supports = []
    signatures = []
    triangles = []
    for card, entry in zip(cards, entries):
        if entry.get('id') != card['id'] or entry.get('sha256') != card.get('model_sha256'):
            raise ValueError('catalog identity differs')
        support = entry.get('source_support_provenance') or {}
        if support.get('source_guid') not in GUIDS or not storage.valid_hex(support.get('source_sha256'), 64):
            raise ValueError('source identity missing')
        source = storage.confined(VAULT, VAULT / support['source_file'])
        if hashlib.sha256(source.read_bytes()).hexdigest() != support['source_sha256']:
            raise ValueError('source prefab SHA differs')
        supports.append(support)
        raw = storage.read_bytes(VAULT, storage.confined(VAULT, Path(card['model_path'])), 512*1024*1024)
        if hashlib.sha256(raw).hexdigest() != card['model_sha256']:
            raise ValueError('model SHA differs')
        signatures.append(compare.signature(raw))
        triangles.append(triangle_positions(raw))
    if (supports[0]['source_guid'], supports[0]['source_sha256']) != (supports[1]['source_guid'], supports[1]['source_sha256']):
        raise ValueError('source identity differs')
    if triangles[0] != triangles[1]:
        raise ValueError('oriented position triangles differ')
    ga, gb = (s['geometry'] for s in signatures)
    if (ga.get('nodes') != gb.get('nodes') or ga.get('scene') != gb.get('scene')
            or [{k: v for k, v in x.items() if k != 'name'} for x in ga.get('scenes') or []]
            != [{k: v for k, v in x.items() if k != 'name'} for x in gb.get('scenes') or []]):
        raise ValueError('node transforms or scene hierarchy differ')
    a, b = (c.get('measurement', {}) for c in cards)
    for key in ('bounds', 'triangles', 'vertices'):
        if not a.get(key) or a.get(key) != b.get(key):
            raise ValueError('measurement differs: ' + key)
    delivery = entries[1].get('delivery_metadata_preserved') or {}
    contract = entries[1].get('functional_contract_preserved') or {}
    if (delivery.get('agent_visual_review') is not True
            or delivery.get('preview_origin') != 'actual-Forge-controlled-lighting-render'
            or contract.get('compute_native_cases_exact') != 360
            or contract.get('controlled_render_cases') != 4
            or contract.get('Studio_qualified') is not False):
        raise ValueError('controlled render evidence differs')
    return {'keep_side': 'variant', 'preferred_controls': ['controlled_render_review', 'matching_source_and_oriented_triangles'],
            'source_guid': supports[0]['source_guid'], 'source_prefab_sha256': supports[0]['source_sha256'],
            'limitations': 'Oriented positions match at five decimals, other vertex data and material graphs differ; Studio and runtime placement remain unqualified.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'source', 'candidates', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    if args.vault.resolve(strict=True) != VAULT.resolve(strict=True):
        parser.error('unexpected Vault')
    if args.output.exists():
        parser.error('output exists')
    result = common.audit(args.vault, args.source, args.candidates, mode='ancient-flowers', guid='',
                          sources=(), pack='mountain-ancient-mountain', checker=check)
    with args.output.open('x', encoding='utf8') as file:
        json.dump(result, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(json.dumps({'count': len(result['results']), 'candidates': sum(x['state'] == 'candidate_only' for x in result['results'])}))
