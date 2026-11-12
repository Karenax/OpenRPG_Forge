"""Select the complete Townsmith cart card, retaining the historic model."""
import argparse
import hashlib
import json
from pathlib import Path
import audit_military_container_choices as common
import vault_storage as storage

VAULT = Path(r'F:\OpenRPG_AssetVault')
GUID = 'abc00000000013862754103857300948'


def check(cards, entries, unused_sources=()):
    if not cards[0].get('license') or cards[0]['license'] != cards[1].get('license'):
        raise ValueError('license differs')
    if (cards[0].get('readiness', {}).get('metadata_ready') is not False
            or cards[1].get('readiness', {}).get('metadata_ready') is not True
            or cards[1].get('missing')):
        raise ValueError('expected incomplete and complete cards')
    hashes = []
    for card, entry in zip(cards, entries):
        if entry.get('id') != card['id'] or entry.get('sha256') != card.get('model_sha256'):
            raise ValueError('catalog model pin differs')
        source = card.get('source_prefab') or {}
        if source.get('guid') != GUID or source != entry.get('source_prefab'):
            raise ValueError('source identity differs')
        pin = source.get('sha256')
        path = storage.confined(VAULT, Path(source['path']))
        if not storage.valid_hex(pin, 64) or hashlib.sha256(path.read_bytes()).hexdigest() != pin:
            raise ValueError('source prefab SHA differs')
        support = entry.get('source_support_provenance') or {}
        mesh_pin = support.get('source_sha256')
        fbx = storage.confined(VAULT, VAULT / support['source_file'])
        if not storage.valid_hex(mesh_pin, 64) or hashlib.sha256(fbx.read_bytes()).hexdigest() != mesh_pin:
            raise ValueError('source FBX SHA differs')
        hashes.append((pin, mesh_pin))
    if hashes[0] != hashes[1]:
        raise ValueError('source revisions differ')
    a, b = (card.get('measurement', {}) for card in cards)
    for key in ('bounds', 'dimensions', 'triangles', 'vertices', 'nodes'):
        if not a.get(key) or a.get(key) != b.get(key):
            raise ValueError('measurement differs: ' + key)
    if entries[1].get('visual_review') != 'agent-reviewed-blender-cpu':
        raise ValueError('variant review missing')
    return {'keep_side': 'variant', 'preferred_controls': ['complete_metadata', 'verified_prefab_and_fbx'],
            'source_prefab_sha256': hashes[0][0], 'source_fbx_sha256': hashes[0][1],
            'limitations': 'Material slots and graph differ; no Studio/runtime placement or visual equivalence claim.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'source', 'candidates', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    if args.vault.resolve(strict=True) != VAULT.resolve(strict=True):
        parser.error('unexpected Vault')
    if args.output.exists():
        parser.error('output exists')
    result = common.audit(args.vault, args.source, args.candidates, mode='town-cart', guid=GUID,
                          sources=(), pack='townsmith', checker=check)
    with args.output.open('x', encoding='utf8') as file:
        json.dump(result, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(json.dumps({'count': len(result['results']), 'candidates': sum(x['state'] == 'candidate_only' for x in result['results'])}))
