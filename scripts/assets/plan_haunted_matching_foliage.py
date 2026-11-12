"""Prepare six explicitly compared Haunted Village foliage consolidations.

The source prefabs have different LOD data. Their source and historical GLB
references remain retained; this does not qualify runtime LOD behavior.
"""
import argparse
import copy
import hashlib
import json
import re
from pathlib import Path

import compare_variant_glb_bytes as compare
import vault_storage as storage
from prepare_variant_metadata_transition import encoded, load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_descriptor_recipe


def same_except_tangents(a, b):
    left, right = copy.deepcopy(a['mesh_payloads']), copy.deepcopy(b['mesh_payloads'])
    if len(left) != len(right): return False
    for amesh, bmesh in zip(left, right, strict=True):
        if len(amesh) != len(bmesh): return False
        for ap, bp in zip(amesh, bmesh, strict=True):
            if 'TANGENT' not in ap['attributes'] or 'TANGENT' not in bp['attributes']: return False
            del ap['attributes']['TANGENT'];del bp['attributes']['TANGENT']
    return left == right


def plan(vault, evidence, candidate_batch=False, collision_batch=False, tangent_evidence=None, triangle_evidence=None):
    vault = vault.resolve(strict=True)
    source = evidence.read_bytes()
    audit = json.loads(source)
    expected_pack = ('hivemind.modular-haunted-village.variant-47d3871747c9fcfb'
                     if candidate_batch else 'hivemind.modular-haunted-village')
    tangent_batch = tangent_evidence is not None
    if tangent_batch != (triangle_evidence is not None) or (tangent_batch and (candidate_batch or not collision_batch)):
        raise ValueError('tangent batch requires both evidence files and normalized collision scope')
    selected = [g for g in audit['groups'] if g['pack'] == expected_pack
                and len(g['comparison']) == 1
                and all(g['comparison'][0][k] for k in ('images_exact', 'materials_exact', 'transforms_exact'))
                and g['comparison'][0]['mesh_payloads_exact'] != tangent_batch
                and g['comparison'][0]['source_components_same_counts'] != collision_batch]
    if ((not candidate_batch and not collision_batch and (len(selected) != 6 or {g['source_filename'] for g in selected} != {'sm_wildcarrot_m_01', 'sm_wildcarrot_m_02', 'sm_wildcarrot_s_01'}))
            or (collision_batch and not tangent_batch and len(selected) != 16)
            or (tangent_batch and len(selected) != 4)
            or (candidate_batch and not collision_batch and len(selected) != 38)):
        raise ValueError('unexpected source groups')
    index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    tangent_rows = {}
    if tangent_batch:
        t=json.loads(tangent_evidence.read_bytes());tri=json.loads(triangle_evidence.read_bytes())
        if t['registry_sha256']!=storage.sha(before) or tri['registry_sha256']!=storage.sha(before):
            raise ValueError('tangent evidence registry changed')
        triangles={(x['source_filename'],x['lod']):x for x in tri['groups']}
        for row in t['groups']:
            key=(row['source_filename'],row['lod']);other=triangles.get(key)
            if (not other or not other['oriented_triangles_equal_5_decimals'] or row['ids']!=other['ids']
                    or row['model_sha256']!=other['model_sha256']
                    or sum(p['different'] for p in row['primitives'])!=1
                    or any(p['handedness_sign_flips'] or p['max_abs_difference']>0.000101 for p in row['primitives'])):
                raise ValueError('tangent or triangle evidence differs')
            tangent_rows[key]=row
        if len(tangent_rows)!=4:raise ValueError('unexpected tangent evidence count')
    ids = {card['id'] for g in selected for card in g['cards']}
    cards = {}
    for entry in json.loads(before)['packs'].values():
        data = storage.confined(vault, vault/entry['path']/'details.v1.json').read_bytes()
        if storage.sha(data) != entry['files']['details.v1.json']:
            raise ValueError('details changed')
        for card in json.loads(data)['assets']:
            if card['id'] in ids:
                if card['id'] in cards: raise ValueError('duplicate identity')
                cards[card['id']] = card
    if set(cards) != ids: raise ValueError('missing active card')
    rows, recipes = [], {}
    for group in selected:
        original = group['cards']
        if len(original) != 2 or any(x['state'] != 'verified' for x in original):
            raise ValueError('source evidence incomplete')
        # Preserve the real-looking GUID over the synthetic abc000... ID.
        actual = [x for x in original if not re.search(r'-abc0{3,}\d+(?:-lod\d+)?$', x['id'])]
        if len(actual) != 1: raise ValueError('no unique source GUID choice')
        keep = cards[actual[0]['id']]
        old_source = next(x for x in original if x['id'] != actual[0]['id'])
        old = cards[old_source['id']]
        if not keep.get('license') or keep['license'] != old.get('license'):
            raise ValueError('license or metadata requires review')
        if candidate_batch:
            if (keep.get('missing') != old.get('missing')
                    or keep.get('readiness', {}).get('metadata_ready') is not False
                    or old.get('readiness', {}).get('metadata_ready') is not False):
                raise ValueError('candidate qualification state changed')
            for card in (keep, old):
                if Path(card['model_path']).relative_to(vault).parts[0].lower() != 'candidates':
                    raise ValueError('candidate model is outside candidate storage')
        elif (keep.get('missing') or old.get('missing')
              or not keep.get('readiness', {}).get('metadata_ready')
              or not old.get('readiness', {}).get('metadata_ready')):
            raise ValueError('license or metadata requires review')
        if collision_batch and any('collisions' not in card.get('not_configured', [])
                                   or 'lods' not in card.get('not_configured', []) for card in (keep, old)):
            raise ValueError('source collision or LOD is configured; explicit migration required')
        for x in original:
            path = storage.confined(Path(Path(x['source_path']).anchor), Path(x['source_path']))
            if storage.sha(path.read_bytes()) != x['source_sha256']:
                raise ValueError('source prefab changed')
        paths = [storage.confined(vault, Path(a['model_path'])) for a in (keep, old)]
        blobs = [storage.read_bytes(vault, p, 256*1024*1024) for p in paths]
        if any(storage.sha(data) != card['model_sha256'] for data, card in zip(blobs, (keep, old), strict=True)):
            raise ValueError('logical model SHA changed')
        a, b = (compare.signature(data) for data in blobs)
        if any(a[key] != b[key] for key in ('transforms', 'materials', 'images')):
            raise ValueError('model comparison changed')
        if tangent_batch:
            measured=tangent_rows.get((group['source_filename'],group['lod']))
            if (not measured or set(measured['ids'])!={keep['id'],old['id']}
                    or set(measured['model_sha256'])!={keep['model_sha256'],old['model_sha256']}
                    or not same_except_tangents(a,b)):
                raise ValueError('tangent-only comparison or pins differ')
        elif a['mesh_payloads'] != b['mesh_payloads']:
            raise ValueError('model mesh changed')
        aliases = old.get('consolidated_variants', [])
        expected_aliases = 0 if candidate_batch else 1
        if len(aliases) != expected_aliases or any(x.get('legacy_recipe', {}).get('schema') != 'openrpg.legacy-vault-descriptor.v1' for x in aliases):
            raise ValueError('unexpected historical alias set')
        recipes[old['id']] = build_descriptor_recipe(vault, paths[1], old['model_sha256'])
        rows.append({'retained_id': keep['id'], 'retired_id': old['id'],
            'retained_uuid': vault_uuid(keep['id']), 'retired_uuid': vault_uuid(old['id']),
            'retained_path': str(paths[0]), 'retired_path': str(paths[1]),
            'retained_sha256': keep['model_sha256'], 'retired_sha256': old['model_sha256'],
            'independent_aliases_to_migrate': [{'retired_id': x['retired_id'],
                'metadata_sha256': hashlib.sha256(encoded(x)).hexdigest()} for x in aliases],
            'choice': ('matching model and materials; source collisions and LOD are not configured; both source prefabs and historical GLB remain available'
                       if collision_batch else 'matching model and materials; retain real-looking source GUID; preserve distinct source LOD data and historical GLB'),
            'ready_for_removal': False})
        if len(rows) % 5 == 0 or len(rows) == len(selected):
            print(f'Verified {len(rows)}/{len(selected)} candidate pairs', flush=True)
    result = {'schema':'openrpg.variant-consolidation-plan.v1', 'state':'planning',
              'registry_sha256':storage.sha(before), 'candidate_count':len(rows), 'candidates':rows}
    prepare_transition(load_packs(vault, result), rows, recipes)
    if index.read_bytes()!=before or evidence.read_bytes()!=source: raise ValueError('inputs changed')
    return result, {'state':'prepared_only', 'registry_sha256':result['registry_sha256'], 'recipes':recipes}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for key in ('vault','evidence','plan-output','recipes-output'): p.add_argument('--'+key,type=Path,required=True)
    modes=p.add_mutually_exclusive_group()
    modes.add_argument('--candidate-batch', action='store_true', help='Plan the 38 incomplete URP candidate pairs without changing readiness')
    modes.add_argument('--collision-batch', action='store_true', help='Plan 16 normalized pairs with source collision differences not configured in either active profile')
    modes.add_argument('--candidate-collision-batch', action='store_true', help='Plan 16 incomplete candidate pairs with unconfigured source collision differences')
    modes.add_argument('--tangent-batch', action='store_true', help='Plan four normalized pairs differing in one bounded tangent component')
    p.add_argument('--tangent-evidence',type=Path)
    p.add_argument('--triangle-evidence',type=Path)
    a=p.parse_args()
    if a.plan_output.exists() or a.recipes_output.exists(): p.error('output exists')
    if a.tangent_batch != bool(a.tangent_evidence and a.triangle_evidence):p.error('tangent batch requires both evidence files')
    result, recipes = plan(a.vault,a.evidence,a.candidate_batch or a.candidate_collision_batch,
                           a.collision_batch or a.candidate_collision_batch or a.tangent_batch,
                           a.tangent_evidence,a.triangle_evidence)
    for path, doc in ((a.plan_output,result),(a.recipes_output,recipes)):
        with path.open('x',encoding='utf8',newline='\n') as f: json.dump(doc,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'state':'planning','candidates':len(result['candidates']),'removed':0}))
