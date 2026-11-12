"""Find Dungeon Builder variants whose only structural addition is collision.

Reads and hashes both complete logical GLBs. This is candidate triage, not a
render check or authorization to delete. Refuse unfamiliar scene structures.
"""
import argparse
import hashlib
import json
from pathlib import Path

import compare_variant_glb_bytes as compare
import vault_storage
from audit_witch_source_choices import triangle_positions

MAX_MODEL = 256 * 1024 * 1024


def without_asset_id(node, allow_source_revision_diff=False):
    result = dict(node)
    extras = dict(result.get('extras', {}))
    extras.pop('asset_id', None)
    if allow_source_revision_diff:
        extras.pop('source_sha256', None)
    result['extras'] = extras
    return result


def visible_materials(materials):
    result = dict(materials)
    cleaned = []
    for material in result.get('materials') or []:
        item = dict(material)
        # Only the material source SHA is expected to differ between pipelines;
        # all other extras may contain functional data and must match.
        if isinstance(item.get('extras'), dict):
            extras = dict(item['extras'])
            extras.pop('source_unity_sha256', None)
            item['extras'] = extras
        cleaned.append(item)
    result['materials'] = cleaned
    return result


def classify(main, variant, allow_source_revision_diff=False):
    a, b = main['geometry'], variant['geometry']
    an, bn = a.get('nodes') or [], b.get('nodes') or []
    am, bm = main['mesh_payloads'], variant['mesh_payloads']
    if len(bn) != 1 or len(bm) != 1 or len(an) < 2 or len(am) != len(an):
        raise ValueError('unexpected mesh or node count')
    if (without_asset_id(an[0], allow_source_revision_diff)
            != without_asset_id(bn[0], allow_source_revision_diff) or an[0].get('mesh') != 0):
        raise ValueError('visible node or provenance differs')
    if any(k in an[0] for k in ('children', 'skin', 'weights')):
        raise ValueError('visible node has unsupported hierarchy')
    if am[0] != bm[0] or main['images'] != variant['images']:
        raise ValueError('visible mesh or image payload differs')
    if visible_materials(main['materials']) != visible_materials(variant['materials']):
        raise ValueError('material or texture configuration differs')
    if main['private_extensions'] or variant['private_extensions'] or a.get('extensions') or b.get('extensions'):
        raise ValueError('unsupported model extensions')
    for index, node in enumerate(an[1:], 1):
        name = node.get('name', '')
        collision = node.get('extras', {}).get('openrpg_collision', {})
        triangle_collider = (name.startswith('UTM_') and isinstance(collision, dict)
                             and collision.get('schema') == 'source-collider.v1'
                             and collision.get('kind') == 'triangle_mesh')
        if (node.get('mesh') != index or node.get('extras', {}).get('source_role') != 'collision'
                or not (name.startswith('UCX_') or triangle_collider)
                or any(k in node for k in ('children', 'skin', 'weights'))):
            raise ValueError('additional node is not an independent collision mesh')
    asc, bsc = a.get('scenes') or [], b.get('scenes') or []
    if (a.get('scene') != 0 or b.get('scene') != 0 or len(asc) != 1 or len(bsc) != 1
            or asc[0].get('nodes') != list(range(len(an))) or bsc[0].get('nodes') != [0]
            or {k: v for k, v in asc[0].items() if k != 'nodes'}
            != {k: v for k, v in bsc[0].items() if k != 'nodes'}):
        raise ValueError('unsupported scene roots')
    return {'keep_side': 'main', 'preferred_controls': ['source_collision'],
            'collision_nodes': len(an) - 1,
            'limitations': 'Native reader and actual rendering still require verification.'}


def classify_single_node(main, variant, raw_main, raw_variant):
    a, b = main['geometry'], variant['geometry']
    an, bn = a.get('nodes') or [], b.get('nodes') or []
    if len(an) != 1 or len(bn) != 1 or len(main['mesh_payloads']) != 1 or len(variant['mesh_payloads']) != 1:
        raise ValueError('not a single-node model')
    if without_asset_id(an[0]) != without_asset_id(bn[0]):
        raise ValueError('node or source provenance differs')
    if (an[0].get('mesh') != 0 or any(k in an[0] for k in ('children','skin','weights'))
            or a.get('scene') != b.get('scene') or a.get('scenes') != b.get('scenes')):
        raise ValueError('unsupported hierarchy or scene')
    if (main['images'] != variant['images'] or visible_materials(main['materials']) != visible_materials(variant['materials'])
            or main['private_extensions'] or variant['private_extensions'] or a.get('extensions') or b.get('extensions')):
        raise ValueError('images, material settings or extensions differ')
    if triangle_positions(raw_main) != triangle_positions(raw_variant):
        raise ValueError('oriented position triangles differ')
    return {'keep_side':'main', 'preferred_controls':['matching_material_and_source'],
            'limitations':'Oriented positions match at five decimals; normals, tangents and UVs may differ. Visual/runtime not qualified.'}


def audit(vault, source, candidates, offset, limit, allow_source_revision_diff=False, single_node=False):
    vault = vault.resolve(strict=True)
    src, cand = source.read_bytes(), candidates.read_bytes()
    inv, doc = json.loads(src), json.loads(cand)
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if (doc.get('mode') != 'collision-candidates' or doc['source_sha256'] != hashlib.sha256(src).hexdigest()
            or hashlib.sha256(before).hexdigest() != inv['registry_sha256']):
        raise ValueError('inventory or registry changed')
    eligible = {x['variant_id']: x for x in doc['candidates']
                if x['main']['storage'] == x['variant']['storage'] == 'descriptor'
                and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches']
                and not x['variant_has_aliases']
                and all(x[side]['logical_bytes'] <= MAX_MODEL for side in ('main', 'variant'))}
    rows = [x for x in inv['pairs'] if x['variant_id'] in eligible][offset:offset + limit]
    if not rows:
        raise ValueError('no eligible candidates')
    results = []
    for number, row in enumerate(rows, 1):
        item = {'main_id': row['main_id'], 'variant_id': row['variant_id']}
        try:
            signatures = []
            raws = []
            for side in ('main', 'variant'):
                path = vault_storage.confined(vault, Path(row[side + '_path']))
                raw = vault_storage.read_bytes(vault, path, MAX_MODEL)
                if hashlib.sha256(raw).hexdigest() != row[side + '_sha']:
                    raise ValueError('model SHA changed')
                signatures.append(compare.signature(raw))
                raws.append(raw)
            choice = classify_single_node(*signatures,*raws) if single_node else classify(*signatures, allow_source_revision_diff)
            item.update(state='candidate_only', **choice)
        except (OSError, ValueError, KeyError, TypeError, IndexError) as error:
            item.update(state='review_required', reason=str(error))
        results.append(item)
        if number % 25 == 0 or number == len(rows):
            print(f'Checked {number}/{len(rows)} collision candidates', flush=True)
    if registry.read_bytes() != before or source.read_bytes() != src or candidates.read_bytes() != cand:
        raise ValueError('registry or inventory changed during audit')
    return {'schema': 'openrpg.material-choice-audit.v1', 'state': 'audit_only',
            'registry_sha256': inv['registry_sha256'], 'source_sha256': hashlib.sha256(src).hexdigest(),
            'candidate_inventory_sha256': hashlib.sha256(cand).hexdigest(), 'pack': 'dungeon-builder',
            'offset': offset, 'allow_source_revision_diff': allow_source_revision_diff, 'results': results,
            'limitations': 'Full SHA and structural comparison; actual render not checked.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--source', type=Path, required=True)
    p.add_argument('--candidates', type=Path, required=True)
    p.add_argument('--offset', type=int, default=0)
    p.add_argument('--limit', type=int, required=True)
    p.add_argument('--allow-source-revision-diff', action='store_true',
                   help='allow only source prefab SHA to differ; preserve historical provenance')
    p.add_argument('--single-node', action='store_true')
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.offset < 0 or not 1 <= args.limit <= 2000 or args.output.exists():
        p.error('invalid offset, limit, or existing output')
    doc = audit(args.vault, args.source, args.candidates, args.offset, args.limit,
                args.allow_source_revision_diff,args.single_node)
    with args.output.open('x', encoding='utf-8', newline='\n') as out:
        json.dump(doc, out, ensure_ascii=False, indent=2)
        out.write('\n')
    print(json.dumps({'checked': len(doc['results']), 'candidates': sum(x['state'] == 'candidate_only' for x in doc['results'])}))
