"""Plan one active Hospital surgical-light version and preserve old GLBs.

Both source prefabs and historical materials remain recoverable. Selecting the
URP material preserves its declared emission, but is not a rendered comparison
or a qualification of the differing Unity LOD/collider metadata.
"""
import argparse
import hashlib
import json
from pathlib import Path

import audit_named_hospital_variants as audit
import audit_variant_material_choices as materials
import vault_storage as storage
from prepare_variant_metadata_transition import encoded, load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_descriptor_recipe


def plan(vault, source):
    vault = vault.resolve(strict=True)
    source_raw = source.read_bytes()
    inventory = json.loads(source_raw)
    if len(inventory['groups']) != 4 or {x['lod'] for x in inventory['groups']} != {0, 1, 2, 3}:
        raise ValueError('expected exactly four Hospital lamp LODs')
    checked = audit.audit(vault, source)
    rows, recipes = [], {}
    for lead, result in zip(inventory['groups'], checked['groups'], strict=True):
        if len(lead['cards']) != 2 or len(result['comparisons']) != 1:
            raise ValueError('unexpected pair size')
        comp = result['comparisons'][0]
        if not all(comp[x] for x in ('mesh_payloads_exact', 'transforms_exact', 'source_fbx_exact')):
            raise ValueError('source geometry or transformations differ')
        if any(x['state'] != 'compared' for x in result['variants']):
            raise ValueError('source comparison incomplete')
        old, keep = lead['cards']
        if (old['pack'] != 'hivemind.modular-abandoned-hospital'
                or keep['pack'] != 'hivemind.modular-abandoned-hospital.variant-2de362b88cc8b897'):
            raise ValueError('unexpected version or ordering')
        old_path = storage.confined(vault, Path(old['model_path']))
        keep_path = storage.confined(vault, Path(keep['model_path']))
        old_doc, keep_doc = materials.glb_document(vault, old_path), materials.glb_document(vault, keep_path)
        old_m, keep_m = old_doc.get('materials', []), keep_doc.get('materials', [])
        if (len(old_m) != 1 or len(keep_m) != 1 or old_m[0].get('emissiveTexture')
                or not keep_m[0].get('emissiveTexture')
                or keep_m[0].get('extensions', {}).get('KHR_materials_emissive_strength', {}).get('emissiveStrength') != 18.308712005615234):
            raise ValueError('expected emission controls differ')
        if storage.digest(vault, keep_path) != keep['model_sha256']:
            raise ValueError('retained model SHA differs')
        recipe = build_descriptor_recipe(vault, old_path, old['model_sha256'])
        recipes[old['id']] = recipe
        rows.append({'retained_id': keep['id'], 'retired_id': old['id'],
                     'retained_uuid': vault_uuid(keep['id']), 'retired_uuid': vault_uuid(old['id']),
                     'retained_path': str(keep_path), 'retired_path': str(old_path),
                     'retained_sha256': keep['model_sha256'], 'retired_sha256': old['model_sha256'],
                     'choice': 'URP emission retained; prior HDRP bytes, prefab and profile remain recoverable; LOD/collider behavior requires qualification',
                     'preferred_controls': ['emissive_texture', 'source_fbx', 'historical_prefab'],
                     'ready_for_removal': False})
    result = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
              'registry_sha256': inventory['registry_sha256'], 'candidate_count': len(rows), 'candidates': rows}
    packs = load_packs(vault, result)
    assets = {a['id']: a for p in packs.values() for a in p['details']['assets']}
    for row in rows:
        a, b = assets[row['retained_id']], assets[row['retired_id']]
        if not a.get('license') or a['license'] != b.get('license'):
            raise ValueError('license missing or different')
        aliases = b.get('consolidated_variants', [])
        if len(aliases) != 2 or any(x.get('legacy_recipe', {}).get('schema') != 'openrpg.legacy-vault-descriptor.v1' for x in aliases):
            raise ValueError('unexpected historical alias set')
        row['independent_aliases_to_migrate'] = [
            {'retired_id': x['retired_id'], 'metadata_sha256': hashlib.sha256(encoded(x)).hexdigest()}
            for x in aliases]
    prepare_transition(packs, rows, recipes)
    if source.read_bytes() != source_raw:
        raise ValueError('source inventory changed')
    return result, {'state': 'prepared_only', 'registry_sha256': result['registry_sha256'], 'recipes': recipes}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for key in ('vault', 'source', 'plan-output', 'recipes-output'):
        p.add_argument('--' + key, type=Path, required=True)
    a = p.parse_args()
    if a.plan_output.exists() or a.recipes_output.exists():
        p.error('output exists')
    result, recipes = plan(a.vault, a.source)
    for path, doc in ((a.plan_output, result), (a.recipes_output, recipes)):
        with path.open('x', encoding='utf8', newline='\n') as f:
            json.dump(doc, f, ensure_ascii=False, indent=2)
            f.write('\n')
    print(json.dumps({'state': 'planning', 'candidates': len(result['candidates']), 'removed': 0}))
