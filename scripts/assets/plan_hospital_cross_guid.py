"""Prepare reviewed Hospital door consolidation across distinct Unity GUIDs.

Model and source pins are reread; graph/material parity is not claimed.
"""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from prepare_variant_metadata_transition import load_packs, prepare_transition, _index
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_descriptor_recipe, MAX_CONSOLIDATION_MODEL_BYTES

ROOT = Path(r'F:\OpenRPG_ImportWork\hivemind-run\modular-abandoned-hospital')
PACK = 'hivemind.modular-abandoned-hospital'


def plan(vault, inventory, audit):
    vault = vault.resolve(strict=True); src = inventory.read_bytes(); audit_raw = audit.read_bytes()
    leads, evidence = json.loads(src), json.loads(audit_raw)
    index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json'); before = index.read_bytes()
    if (storage.sha(before) != leads['registry_sha256'] or evidence.get('state') != 'audit_only'
            or evidence.get('registry_sha256') != leads['registry_sha256']):
        raise ValueError('registry or comparison is stale')
    groups = [g for g in leads['groups'] if g['base_pack'] == PACK]
    audited = {(g['source_filename'], g['lod']): g for g in evidence['groups']}
    if len(groups) != 12 or len(audited) != 12:
        raise ValueError('unexpected Hospital group scope')
    candidates, recipes, comparisons = [], {}, {}
    for group in groups:
        key = (group['source_filename'], group['lod']); checked = audited.get(key)
        cards = group['cards']
        if not checked or len(cards) != 3 or len(checked['variants']) != 3 or len(checked['comparisons']) != 2:
            raise ValueError('incomplete three-way comparison')
        if [c['id'] for c in cards] != [c['id'] for c in checked['variants']]:
            raise ValueError('group identities changed')
        if '.variant-' in cards[0]['pack'] or any('.variant-' not in c['pack'] for c in cards[1:]):
            raise ValueError('unexpected primary and variant ordering')
        if any(v.get('state') != 'compared' for v in checked['variants']):
            raise ValueError('model or source comparison incomplete')
        for comp in checked['comparisons']:
            if not all(comp.get(k) is True for k in ('mesh_payloads_exact', 'transforms_exact', 'source_fbx_exact')):
                raise ValueError('geometry, transform or FBX differs')
            comparisons[comp['against']] = comp
        keep = cards[0]
        for old in cards[1:]:
            if old['id'] not in comparisons or old['id'] in recipes:
                raise ValueError('comparison missing or repeated')
            keep_path = storage.confined(vault, Path(keep['model_path']))
            old_path = storage.confined(vault, Path(old['model_path']))
            if keep_path == old_path or storage.digest(vault, keep_path, MAX_CONSOLIDATION_MODEL_BYTES) != keep['model_sha256']:
                raise ValueError('retained model pin differs')
            recipe = build_descriptor_recipe(vault, old_path, old['model_sha256'])
            candidates.append({'retained_id': keep['id'], 'retired_id': old['id'],
                'retained_uuid': vault_uuid(keep['id']), 'retired_uuid': vault_uuid(old['id']),
                'retained_path': keep['model_path'], 'retired_path': old['model_path'],
                'retained_sha256': keep['model_sha256'], 'retired_sha256': old['model_sha256'],
                'choice': 'Keep reviewed HDRP conversion; reconstruct old distinct-GUID variant on request',
                'preferred_controls': ['reviewed_material', 'matching_fbx_and_geometry'], 'ready_for_removal': False})
            recipes[old['id']] = recipe
    result = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
              'registry_sha256': leads['registry_sha256'], 'candidate_count': len(candidates), 'candidates': candidates}
    packs = load_packs(vault, result); active = _index(packs); catalogs = {}
    by_id = {v['id']: v for g in evidence['groups'] for v in g['variants']}
    for ident in {item[k] for item in candidates for k in ('retained_id', 'retired_id')}:
        _, card, _ = active[ident]
        if not card.get('license') or card.get('missing') or card.get('readiness', {}).get('metadata_ready') is not True:
            raise ValueError('incomplete card')
        pack = card['id'].split('.')[2]
        root = ROOT / ('variants/' + pack.removeprefix('variant-') + '/source' if pack.startswith('variant-') else 'source')
        for field in ('source_prefab', 'source_fbx'):
            path = storage.confined(Path(root.anchor), root / card[field])
            if storage.sha(path.read_bytes()) != by_id[ident]['source_sha256'][field]:
                raise ValueError('source changed')
        path = storage.confined(vault, Path(card['source_catalog'])); raw = path.read_bytes(); pin = storage.sha(raw)
        if path in catalogs and catalogs[path] != pin: raise ValueError('catalog changed')
        catalogs[path] = pin
        entry = next(x for x in json.loads(raw)['assets'] if x['id'] == ident)
        if entry.get('sha256') != card['model_sha256'] or entry.get('geometry_roundtrip_passed') is not True:
            raise ValueError('catalog pin or geometry evidence changed')
        if entry.get('source_sha256') != by_id[ident]['source_sha256']['source_prefab']:
            raise ValueError('catalog source pin differs')
        if '.variant-' not in ident:
            pending_lod = ident.endswith('abc00000000009895398176658190184-lod3')
            expected_review = 'pending' if pending_lod else 'agent-preview-review-passed'
            if entry.get('visual_review') != expected_review or entry.get('material_review_pending') is not False:
                raise ValueError('primary review state changed')
    for item in candidates:
        if active[item['retained_id']][1]['license'] != active[item['retired_id']][1]['license']:
            raise ValueError('license differs')
        if item['retained_id'].endswith('abc00000000009895398176658190184-lod3'):
            item['choice'] = 'Keep HDRP material-reviewed conversion; LOD3 visual review remains pending'
            item['preferred_controls'] = ['material_reviewed_visual_pending', 'matching_fbx_and_geometry']
    prepare_transition(packs, candidates, recipes)
    if (index.read_bytes() != before or inventory.read_bytes() != src or audit.read_bytes() != audit_raw
            or any(storage.sha(path.read_bytes()) != pin for path, pin in catalogs.items())):
        raise ValueError('inputs changed')
    return result, {'state': 'prepared_only', 'registry_sha256': leads['registry_sha256'], 'recipes': recipes}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault', 'inventory', 'audit', 'plan', 'recipes'):
        p.add_argument('--' + n, type=Path, required=True)
    args = p.parse_args()
    if args.plan.exists() or args.recipes.exists(): p.error('output exists')
    result, recipes = plan(args.vault, args.inventory, args.audit)
    for path, value in ((args.plan, result), (args.recipes, recipes)):
        with path.open('x', encoding='utf8') as out:
            json.dump(value, out, ensure_ascii=False, indent=2); out.write('\n')
    print(json.dumps({'state': 'planning_only', 'candidates': result['candidate_count'], 'models_removed': 0}))
