"""Select the reviewed Dark Fantasy sword; historic appearance remains distinct."""
import argparse
import hashlib
import json
from pathlib import Path

import audit_military_container_choices as common
import vault_storage as storage

GUID = 'd15f08ba73cc6fb4c8ef28748644ed7f'
ROOT = Path(r'F:\OpenRPG_ImportWork\hivemind-run\dark-fantasy-weapons')
SOURCES = (
    ROOT / 'source/Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Sword_3.prefab',
    ROOT / 'variants/9fc89155834cd4fd/source/Assets/HIVEMIND/DarkFantasyWeapons/Weapons/Built-In/Prefabs/SM_Sword_3.prefab',
)
MESH = ROOT / 'source/Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Models/SM_Sword_3/Mesh/SM_Sword_3.fbx'


def check(cards, entries, sources=SOURCES, mesh=MESH):
    if not cards[0].get('license') or cards[0]['license'] != cards[1].get('license'):
        raise ValueError('license differs')
    for card, entry, source in zip(cards, entries, sources):
        if (card.get('missing') or card.get('readiness', {}).get('metadata_ready') is not True
                or entry.get('id') != card['id'] or entry.get('sha256') != card.get('model_sha256')
                or entry.get('source_guid') != GUID or entry.get('geometry_roundtrip_passed') is not True):
            raise ValueError('metadata or source identity differs')
        expected = entry.get('source_prefab_sha256', entry.get('source_sha256'))
        if not storage.valid_hex(expected, 64) or hashlib.sha256(source.read_bytes()).hexdigest() != expected:
            raise ValueError('source prefab SHA differs')
    primary = entries[0]
    expected_mesh = primary.get('source_fbx_sha256')
    storage.confined(Path(mesh.anchor), mesh)
    if not storage.valid_hex(expected_mesh, 64) or hashlib.sha256(mesh.read_bytes()).hexdigest() != expected_mesh:
        raise ValueError('primary FBX SHA differs')
    if primary.get('visual_review') != 'prior_agent_review_preserved' or primary.get('model_bytes_equal_to_active_asset') is not True:
        raise ValueError('primary review missing')
    a, b = (card.get('measurement', {}) for card in cards)
    for key in ('bounds', 'dimensions', 'triangles', 'nodes'):
        if not a.get(key) or a.get(key) != b.get(key):
            raise ValueError('measurement differs: ' + key)
    return {'keep_side': 'main', 'preferred_controls': ['reviewed_primary', 'source_prefab_and_fbx_verified'],
            'source_prefab_sha256': [e.get('source_prefab_sha256', e.get('source_sha256')) for e in entries],
            'source_fbx_sha256': expected_mesh,
            'limitations': 'Different vertex counts, textures and materials; render and runtime equivalence not established.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'source', 'candidates', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('output exists')
    result = common.audit(args.vault, args.source, args.candidates, mode='dark-sword', guid=GUID,
                          sources=SOURCES, pack='dark-fantasy-weapons', checker=check)
    with args.output.open('x', encoding='utf8') as file:
        json.dump(result, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(json.dumps({'count': len(result['results']), 'candidates': sum(x['state'] == 'candidate_only' for x in result['results'])}))
