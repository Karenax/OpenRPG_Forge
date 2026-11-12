"""Rebase a prepared plan only when every involved original record is unchanged."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import _index, load_packs, prepare_transition, encoded, verify_stage


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def verify_unchanged(plan, staged_packs, current_packs):
    staged = _index(staged_packs)
    current = _index(current_packs)
    groups = {}
    for row in plan['candidates']:
        keep, retired = row['retained_id'], row['retired_id']
        groups.setdefault(keep, []).append(retired)
    all_retired = [retired for items in groups.values() for retired in items]
    if len(all_retired) != len(set(all_retired)) or set(groups).intersection(all_retired):
        raise ValueError('overlapping consolidation candidates')
    for keep, retired_ids in groups.items():
        if len(retired_ids) != len(set(retired_ids)):
            raise ValueError('duplicate retired identity')
        if keep not in staged or keep not in current:
            raise ValueError('candidate identities changed')
        _, staged_detail, staged_profile = staged[keep]
        _, current_detail, current_profile = current[keep]
        variants = staged_detail.get('consolidated_variants')
        if not isinstance(variants, list) or any(not isinstance(v, dict) for v in variants):
            raise ValueError('staged historical metadata missing')
        for retired in retired_ids:
            if retired not in current or retired in staged:
                raise ValueError('candidate identities changed')
            _, retired_detail, retired_profile = current[retired]
            matches = [v for v in variants if v.get('retired_id') == retired]
            if (len(matches) != 1 or matches[0]['original_detail'] != retired_detail
                    or matches[0]['original_profile'] != retired_profile):
                raise ValueError('candidate metadata or profile changed: ' + retired)
        original = copy.deepcopy(staged_detail)
        remaining = [v for v in original['consolidated_variants'] if v.get('retired_id') not in retired_ids]
        if 'consolidated_variants' in current_detail:
            original['consolidated_variants'] = remaining
        else:
            if remaining:
                raise ValueError('unexpected historical metadata')
            del original['consolidated_variants']
        if original != current_detail or staged_profile != current_profile:
            raise ValueError('candidate retained metadata or profile changed: ' + keep)


def rebase(vault, plan, recipe_document, stage):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    allowed = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if not stage.is_relative_to(allowed):
        raise ValueError('stage outside conversion workspace')
    vault_storage.confined(allowed, stage)
    manifest = json.loads((stage / 'transition.json').read_bytes())
    if (manifest.get('state') != 'prepared_only' or manifest.get('registry_sha256') != plan['registry_sha256']
            or recipe_document.get('registry_sha256') != plan['registry_sha256']
            or recipe_document.get('state') != 'prepared_only'):
        raise ValueError('original stage or recipe pins mismatch')
    if set(recipe_document['recipes']) != {x['retired_id'] for x in plan['candidates']}:
        raise ValueError('recipe identities mismatch')
    staged_packs = {}
    for pack, pins in manifest['packs'].items():
        docs = {}
        for filename, key in (('details.v1.json', 'details'), ('profiles.v1.json', 'profiles')):
            raw = vault_storage.confined(allowed, stage / pack / filename).read_bytes()
            if sha(raw) != pins[filename]:
                raise ValueError('staged metadata changed')
            docs[key] = json.loads(raw)
        staged_packs[pack] = docs
    registry = vault / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    rebased = copy.deepcopy(plan)
    rebased['registry_sha256'] = sha(before)
    current = load_packs(vault, rebased)
    verify_unchanged(plan, staged_packs, current)
    if registry.read_bytes() != before:
        raise ValueError('registry changed during rebase')
    recipes = copy.deepcopy(recipe_document)
    recipes['registry_sha256'] = rebased['registry_sha256']
    return rebased, recipes


def write_new(path, data):
    with path.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def write_rebased_stage(stage, destination, current_packs, plan, recipes):
    """Rebuild metadata against the new registry; model payloads are not read.

    The source stage's metadata hashes are verified first. Publication must
    still verify every model and qualify the new stage in the native reader.
    """
    stage = Path(stage).resolve(strict=True)
    destination = Path(destination)
    if destination.exists():
        raise ValueError('rebased stage already exists')
    original_raw = (stage / 'transition.json').read_bytes()
    original = json.loads(original_raw)
    for pack, pins in original['packs'].items():
        for filename in ('details.v1.json', 'profiles.v1.json'):
            if sha((stage / pack / filename).read_bytes()) != pins[filename]:
                raise ValueError('source stage metadata changed')
    proposed = prepare_transition(current_packs, plan['candidates'], recipes)
    if proposed['aliases_requiring_project_migration'] != original['aliases_requiring_project_migration']:
        raise ValueError('rebased aliases changed')
    if set(proposed['changed_packs']) != set(original['packs']):
        raise ValueError('rebased pack scope changed')
    manifest = copy.deepcopy(original)
    manifest['registry_sha256'] = plan['registry_sha256']
    manifest['rebased_from_stage_sha256'] = sha(original_raw)
    manifest['rebase_note'] = 'Metadata only; model payloads and native reader must be requalified before publication.'
    manifest['packs'] = {}
    destination.mkdir(exist_ok=False)
    for pack in proposed['changed_packs']:
        out = destination / pack
        out.mkdir()
        docs, pins = proposed['packs'][pack], {}
        for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
            raw = encoded(docs[key])
            with (out / filename).open('xb') as stream:
                stream.write(raw)
            pins[filename] = sha(raw)
        pins['revision'] = sha(encoded({'profiles': docs['profiles'], 'details': docs['details']}))
        manifest['packs'][pack] = pins
    with (destination / 'transition.json').open('xb') as stream:
        stream.write(encoded(manifest))
    verify_stage(destination, current_packs, plan, recipes)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--recipes', type=Path, required=True)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--plan-output', type=Path, required=True)
    parser.add_argument('--recipes-output', type=Path, required=True)
    parser.add_argument('--stage-output', type=Path)
    args = parser.parse_args()
    if args.plan_output.exists() or args.recipes_output.exists() or (args.stage_output and args.stage_output.exists()):
        raise ValueError('output already exists')
    plan, recipes = rebase(args.vault, json.loads(args.plan.read_bytes()),
                           json.loads(args.recipes.read_bytes()), args.stage)
    write_new(args.plan_output, plan)
    write_new(args.recipes_output, recipes)
    if args.stage_output:
        allowed = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
        if not args.stage_output.parent.resolve(strict=True).is_relative_to(allowed):
            raise ValueError('stage output outside conversion workspace')
        current = load_packs(args.vault.resolve(strict=True), plan)
        write_rebased_stage(args.stage, args.stage_output, current, plan, recipes['recipes'])
    print(json.dumps({'state': 'rebased_only', 'candidates': len(plan['candidates']),
                      'registry_sha256': plan['registry_sha256']}))
