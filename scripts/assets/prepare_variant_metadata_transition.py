"""Construct a consolidation on in-memory metadata copies; never publish it.

The output is a proposed transition, not permission to retire a model: project
references and catalog/runtime compatibility must be migrated independently.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path

import vault_storage
from variant_legacy_recipe import verify_recipe, MAX_CONSOLIDATION_MODEL_BYTES


def _index(packs):
    found = {}
    for pack, docs in packs.items():
        details, profiles = docs['details'], docs['profiles']
        if details.get('pack_id') != pack:
            raise ValueError('pack identity mismatch: ' + pack)
        assets, profile_list = details.get('assets'), profiles.get('profiles')
        if not isinstance(assets, list) or not isinstance(profile_list, list):
            raise ValueError('invalid pack arrays: ' + pack)
        profile_by_id = {}
        for profile in profile_list:
            ident = profile['external_id']
            if ident in profile_by_id:
                raise ValueError('duplicate profile: ' + ident)
            profile_by_id[ident] = profile
        for asset in assets:
            ident = asset['id']
            if ident in found or ident not in profile_by_id:
                raise ValueError('duplicate asset or missing profile: ' + ident)
            if profile_by_id[ident].get('model_content_sha256') != asset.get('model_sha256'):
                raise ValueError('profile hash mismatch: ' + ident)
            found[ident] = (pack, asset, profile_by_id[ident])
        if set(profile_by_id) != {asset['id'] for asset in assets}:
            raise ValueError('orphan profiles: ' + pack)
    return found


def prepare_transition(packs, candidates, recipes=None):
    proposed = copy.deepcopy(packs)
    index = _index(proposed)
    kept_ids = {item['retained_id'] for item in candidates}
    retired_ids = [item['retired_id'] for item in candidates]
    if len(retired_ids) != len(set(retired_ids)) or kept_ids.intersection(retired_ids):
        raise ValueError('overlapping consolidation candidates')
    changed = set()
    aliases = []
    for item in candidates:
        keep, retire = item['retained_id'], item['retired_id']
        if keep not in index or retire not in index:
            raise ValueError('candidate missing from active metadata')
        keep_pack, keep_asset, keep_profile = index[keep]
        retire_pack, retire_asset, retire_profile = index[retire]
        for prefix, asset in (('retained', keep_asset), ('retired', retire_asset)):
            if asset.get('model_sha256') != item[prefix + '_sha256']:
                raise ValueError(prefix + ' SHA changed')
            if asset.get('model_path') != item[prefix + '_path']:
                raise ValueError(prefix + ' path changed')
        existing = keep_asset.get('consolidated_variants', [])
        if (not isinstance(existing, list)
                or any(not isinstance(v, dict) or not isinstance(v.get('retired_id'), str) for v in existing)
                or len({v['retired_id'] for v in existing}) != len(existing)
                or any(v['retired_id'] == retire for v in existing)):
            raise ValueError('existing consolidated metadata is invalid or overlaps candidate')
        inherited = retire_asset.get('consolidated_variants', [])
        if inherited:
            pins = item.get('independent_aliases_to_migrate')
            if not isinstance(pins, list) or len(pins) != len(inherited):
                raise ValueError('retiring an asset with existing aliases requires explicit migration')
            existing_ids = {v['retired_id'] for v in existing}
            inherited_ids = set()
            for alias, pin in zip(inherited, pins, strict=True):
                ident = alias.get('retired_id')
                recipe = alias.get('legacy_recipe') or {}
                if (not isinstance(ident, str) or ident in inherited_ids or ident in existing_ids
                        or ident in index or ident == retire
                        or recipe.get('schema') != 'openrpg.legacy-vault-descriptor.v1'
                        or recipe.get('logical_sha256') != alias.get('retired_model_sha256')
                        or pin != {'retired_id': ident, 'metadata_sha256': hashlib.sha256(encoded(alias)).hexdigest()}):
                    raise ValueError('independent alias migration differs from pinned metadata')
                inherited_ids.add(ident)
        elif item.get('independent_aliases_to_migrate'):
            raise ValueError('migration pins provided without historical aliases')
        preserved = {
            'retired_id': retire, 'retired_uuid': item['retired_uuid'],
            'retired_model_sha256': item['retired_sha256'],
            'original_detail': copy.deepcopy(retire_asset),
            'original_profile': copy.deepcopy(retire_profile),
        }
        if recipes is not None:
            recipe = recipes.get(retire)
            if (not isinstance(recipe, dict)
                    or recipe.get('schema') not in ('openrpg.legacy-glb-recipe.v1', 'openrpg.legacy-vault-descriptor.v1', 'openrpg.legacy-inline-glb.v1')
                    or recipe.get('logical_sha256') != item['retired_sha256']):
                raise ValueError('missing or mismatched legacy recipe')
            preserved['legacy_recipe'] = copy.deepcopy(recipe)
        keep_asset['consolidated_variants'] = existing + copy.deepcopy(inherited) + [preserved]
        docs = proposed[retire_pack]
        docs['details']['assets'] = [a for a in docs['details']['assets'] if a['id'] != retire]
        docs['profiles']['profiles'] = [p for p in docs['profiles']['profiles'] if p['external_id'] != retire]
        groups = docs['details'].get('duplicate_groups')
        if groups is not None:
            if not isinstance(groups, list) or any(not isinstance(group, list) for group in groups):
                raise ValueError('invalid duplicate groups')
            clean = [[value for value in group if value != retire] for group in groups]
            docs['details']['duplicate_groups'] = [group for group in clean if len(group) > 1]
        aliases.append({key: item[key] for key in ('retained_id', 'retired_id', 'retained_uuid',
                                                    'retired_uuid', 'retained_sha256', 'retired_sha256')})
        changed.update((keep_pack, retire_pack))
    final_index = _index(proposed)
    if recipes is not None and set(recipes) != {row['retired_id'] for row in aliases}:
        raise ValueError('legacy recipe set does not match candidates')
    if any(row['retired_id'] in final_index or row['retained_id'] not in final_index for row in aliases):
        raise ValueError('identity migration inconsistent')
    return {'state': 'prepared_only', 'changed_packs': sorted(changed),
            'aliases_requiring_project_migration': aliases, 'packs': proposed}


def load_packs(vault, plan):
    registry_path = vault / 'metadata/asset-import-registry.v1.json'
    before = registry_path.read_bytes()
    if hashlib.sha256(before).hexdigest() != plan['registry_sha256']:
        raise ValueError('registry changed')
    registry = json.loads(before)
    wanted = {item[prefix + '_id'] for item in plan['candidates'] for prefix in ('retained', 'retired')}
    packs = {}
    for pack, entry in sorted(registry['packs'].items()):
        root = (vault / entry['path']).resolve(strict=True)
        if not root.is_relative_to(vault.resolve(strict=True)):
            raise ValueError('pack outside Vault')
        docs = {}
        for name, key in (('details.v1.json', 'details'), ('profiles.v1.json', 'profiles')):
            raw = (root / name).read_bytes()
            if hashlib.sha256(raw).hexdigest() != entry['files'][name]:
                raise ValueError('pack metadata SHA changed: ' + pack)
            docs[key] = json.loads(raw)
        if any(a['id'] in wanted for a in docs['details']['assets']):
            packs[pack] = docs
    if registry_path.read_bytes() != before:
        raise ValueError('registry changed during preparation')
    return packs


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + '\n').encode('utf-8')


def write_stage(work_root, name, vault, plan, recipes=None):
    root = work_root.resolve(strict=True)
    allowed = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if not root.is_relative_to(allowed):
        raise ValueError('work root must be in the asset conversion workspace')
    if not name or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for c in name):
        raise ValueError('invalid stage name')
    packs = load_packs(vault, plan)
    proposed = prepare_transition(packs, plan['candidates'], recipes)
    if recipes is not None:
        physical_vault = vault.resolve(strict=True)
        for index, item in enumerate(plan['candidates'], 1):
            retained = Path(item['retained_path']).resolve(strict=True)
            if not retained.is_relative_to(physical_vault):
                raise ValueError('retained model outside Vault')
            raw = vault_storage.read_bytes(physical_vault, retained, MAX_CONSOLIDATION_MODEL_BYTES)
            retired = vault_storage.confined(physical_vault, Path(item['retired_path']))
            verify_recipe(raw, recipes[item['retired_id']], item['retained_sha256'], item['retired_sha256'],
                          vault=physical_vault, retired_path=retired)
            if index % 25 == 0 or index == len(plan['candidates']):
                print(f"Reverified {index}/{len(plan['candidates'])} model pairs", flush=True)
    stage = root / name
    stage.mkdir(exist_ok=False)
    manifest = {'schema': 'openrpg.variant-metadata-transition.v1', 'state': 'prepared_only',
                'registry_sha256': plan['registry_sha256'],
                'aliases_requiring_project_migration': proposed['aliases_requiring_project_migration'],
                'not_ready_reasons': ['project references not migrated', 'runtime and Studio not qualified',
                                      'no atomic publication or retirement performed'], 'packs': {}}
    manifest['includes_legacy_recipes'] = recipes is not None
    for pack in proposed['changed_packs']:
        if not pack or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789.-_' for c in pack):
            raise ValueError('invalid pack ID')
        dest = stage / pack
        dest.mkdir()
        docs = proposed['packs'][pack]
        entries = {}
        for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
            raw = encoded(docs[key])
            with (dest / filename).open('xb') as stream:
                stream.write(raw)
            entries[filename] = hashlib.sha256(raw).hexdigest()
        entries['revision'] = hashlib.sha256(encoded({'profiles': docs['profiles'], 'details': docs['details']})).hexdigest()
        manifest['packs'][pack] = entries
    if hashlib.sha256((vault / 'metadata/asset-import-registry.v1.json').read_bytes()).hexdigest() != plan['registry_sha256']:
        raise ValueError('registry changed during staging; discard stage')
    with (stage / 'transition.json').open('xb') as stream:
        stream.write(encoded(manifest))
    return stage, manifest


def verify_stage(stage, original_packs, plan, recipes=None):
    manifest = json.loads((stage / 'transition.json').read_bytes())
    if manifest.get('state') != 'prepared_only' or manifest.get('registry_sha256') != plan['registry_sha256']:
        raise ValueError('wrong stage or registry pin')
    if bool(manifest.get('includes_legacy_recipes', False)) != (recipes is not None):
        raise ValueError('stage recipe mode mismatch')
    expected = prepare_transition(original_packs, plan['candidates'], recipes)
    if manifest.get('aliases_requiring_project_migration') != expected['aliases_requiring_project_migration']:
        raise ValueError('alias mapping changed')
    if set(manifest['packs']) != set(expected['changed_packs']):
        raise ValueError('stage pack set changed')
    for pack, file_info in manifest['packs'].items():
        docs = {}
        for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
            raw = (stage / pack / filename).read_bytes()
            if hashlib.sha256(raw).hexdigest() != file_info[filename]:
                raise ValueError('stage file SHA changed: ' + pack)
            docs[key] = json.loads(raw)
        if docs != expected['packs'][pack]:
            raise ValueError('stage content differs from recomputed transition: ' + pack)
        if hashlib.sha256(encoded({'profiles': docs['profiles'], 'details': docs['details']})).hexdigest() != file_info['revision']:
            raise ValueError('stage revision mismatch: ' + pack)
    return {'status': 'passed', 'packs': len(manifest['packs']),
            'aliases': len(manifest['aliases_requiring_project_migration']),
            'published': False, 'models_removed': 0}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--work-root', type=Path, required=True)
    parser.add_argument('--stage-name', required=True)
    parser.add_argument('--recipes', type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding='utf-8'))
    recipes = None
    if args.recipes:
        document = json.loads(args.recipes.read_text(encoding='utf-8'))
        if document.get('registry_sha256') != plan['registry_sha256'] or document.get('state') != 'prepared_only':
            raise ValueError('legacy recipes use a different registry')
        recipes = document['recipes']
    stage, manifest = write_stage(args.work_root, args.stage_name, args.vault, plan, recipes)
    print(json.dumps({'stage': str(stage), 'state': manifest['state'],
                      'changed_packs': len(manifest['packs']),
                      'aliases': len(manifest['aliases_requiring_project_migration'])}))
