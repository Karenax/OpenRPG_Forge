"""Choose reviewed Witch House books while preserving historical versions."""
import argparse
import hashlib
import json
from pathlib import Path
import audit_military_container_choices as common
from audit_witch_source_choices import triangle_positions
import vault_storage as storage

GUID = 'abc00000000000685439161669535067'
ROOT = Path(r'F:\OpenRPG_ImportWork\hivemind-run\witch-house')
SRC = ROOT / 'source'
VAR = ROOT / 'variants/7a87221d80c1a122/source'
SOURCES = (SRC / 'Assets/Hivemind/WitchHouse/HDRP(Default)/Art/Prefabs/SM_OpenBook_lo.prefab',
           VAR / 'Assets/Hivemind/WitchHouse/URP/Art/Prefabs/SM_OpenBook_lo.prefab')
MESHES = (SRC / 'Assets/Hivemind/WitchHouse/HDRP(Default)/Art/Meshes/SM_OpenBook_lo.fbx',
          VAR / 'Assets/Hivemind/WitchHouse/URP/Art/Meshes/SM_OpenBook_lo.fbx')


def check(cards, entries, sources=SOURCES, meshes=MESHES):
    if not cards[0].get('license') or cards[0]['license'] != cards[1].get('license'):
        raise ValueError('license differs')
    for card, entry, source, mesh in zip(cards, entries, sources, meshes):
        if (card.get('missing') or card.get('readiness', {}).get('metadata_ready') is not True
                or entry.get('id') != card['id'] or entry.get('sha256') != card.get('model_sha256')
                or entry.get('source_guid') != GUID or entry.get('geometry_roundtrip_passed') is not True
                or entry.get('source_authored_frames_preserved') is not True):
            raise ValueError('metadata, source identity or frame evidence differs')
        if entry.get('source_prefab') != card.get('source_prefab') or entry.get('source_fbx') != card.get('source_fbx'):
            raise ValueError('source catalog differs')
        fbx = entry['source_fbx']
        if len(fbx) != 1:
            raise ValueError('unexpected source mesh count')
        for path, expected in ((source, entry['source_prefab'].get('sha256')), (mesh, fbx[0].get('sha256'))):
            storage.confined(Path(path.anchor), path)
            if not storage.valid_hex(expected, 64) or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                raise ValueError('source SHA differs')
    primary = entries[0]
    if primary.get('material_review_pending') is not False or primary.get('visual_review') != 'agent_reviewed':
        raise ValueError('primary material review missing')
    if entries[0]['source_fbx'][0]['sha256'] != entries[1]['source_fbx'][0]['sha256']:
        raise ValueError('source mesh differs')
    a, b = (card.get('measurement', {}) for card in cards)
    for key in ('bounds', 'dimensions', 'triangles'):
        if not a.get(key) or a.get(key) != b.get(key):
            raise ValueError('measurement differs: ' + key)
    if ([x.get('matrix_world_column_major') for x in a.get('nodes', [])]
            != [x.get('matrix_world_column_major') for x in b.get('nodes', [])] or not a.get('nodes')):
        raise ValueError('model transforms differ')
    vault = Path(r'F:\OpenRPG_AssetVault')
    model_bytes = []
    for card in cards:
        path = storage.confined(vault, Path(card['model_path']))
        raw = storage.read_bytes(vault, path, 512 * 1024 * 1024)
        if hashlib.sha256(raw).hexdigest() != card.get('model_sha256'):
            raise ValueError('model SHA differs')
        model_bytes.append(raw)
    if triangle_positions(model_bytes[0]) != triangle_positions(model_bytes[1]):
        raise ValueError('oriented position triangles differ')
    return {'keep_side': 'main', 'preferred_controls': ['reviewed_primary', 'source_mesh_verified'],
            'source_prefab_sha256': [e['source_prefab']['sha256'] for e in entries],
            'limitations': 'Oriented position triangles match at five decimals; source revisions, vertex counts and materials may differ. Visual and runtime equivalence not established.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'source', 'candidates', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('output exists')
    result = common.audit(args.vault, args.source, args.candidates, mode='witch-book', guid=GUID,
                          sources=SOURCES, pack='witch-house', checker=check)
    with args.output.open('x', encoding='utf8') as file:
        json.dump(result, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(json.dumps({'count': len(result['results']), 'candidates': sum(x['state'] == 'candidate_only' for x in result['results'])}))
