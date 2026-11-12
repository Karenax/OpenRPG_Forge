"""Turn a completed descriptor audit into a prepared-only, pinned plan."""
import argparse
import hashlib
import json
from pathlib import Path

from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid


def plan_batch(vault, source, batch, choices=None):
    raw = source.read_bytes()
    inventory = json.loads(raw)
    batches = batch if isinstance(batch, (list, tuple)) else [batch]
    if not batches:
        raise ValueError('no batches provided')
    choice_raw = choices.read_bytes() if choices is not None else None
    choice_map = None
    if choice_raw is not None:
        document = json.loads(choice_raw)
        if (document.get('schema') != 'openrpg.material-choice-audit.v1'
                or document.get('state') != 'audit_only'
                or document.get('source_sha256') != hashlib.sha256(raw).hexdigest()
                or document.get('registry_sha256') != inventory['registry_sha256']):
            raise ValueError('material choices are stale')
        selected = [x for x in document['results'] if x.get('state') == 'candidate_only']
        choice_map = {x['variant_id']: x for x in selected}
        if len(choice_map) != len(selected):
            raise ValueError('duplicate material choice')
    audited = []
    for path in batches:
        records = [json.loads(line) for line in path.read_bytes().splitlines()]
        if len(records) < 3:
            raise ValueError('batch has no completed results')
        head, end = records[0], records[-1]
        if (head.get('schema') != 'openrpg.descriptor-candidate-batch.v1'
                or head.get('state') != 'started' or head.get('source_sha256') != hashlib.sha256(raw).hexdigest()
                or head.get('registry_sha256') != inventory.get('registry_sha256')
                or end != {'state': 'complete', 'verified': head.get('planned'), 'models_removed': 0}
                or len(records) != head['planned'] + 2):
            raise ValueError('batch incomplete or inventory changed')
        if head.get('material_choices_sha256') != (hashlib.sha256(choice_raw).hexdigest() if choice_raw else None):
            raise ValueError('batch material choice proof differs')
        audited.extend((head, proof) for proof in records[1:-1])
    rows = {x['variant_id']: x for x in inventory['pairs']}
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
            'registry_sha256': inventory['registry_sha256'], 'candidates': []}
    recipes = {}
    for head, proof in audited:
        row = rows.get(proof.get('variant_id'))
        recipe = proof.get('recipe')
        keep_side = proof.get('retained_side', 'main')
        if keep_side not in ('main', 'variant'):
            raise ValueError('invalid retained material side')
        old_side = 'variant' if keep_side == 'main' else 'main'
        if choice_map is not None:
            choice = choice_map.get(proof.get('variant_id'))
            if (not choice or choice.get('main_id') != proof.get('main_id')
                    or choice.get('keep_side') != keep_side
                    or choice.get('preferred_controls', []) != proof.get('preferred_controls', [])):
                raise ValueError('batch direction differs from material choice')
        if (not row or proof.get('state') != 'verified_model_bytes'
                or row['main_id'].split('.')[1] != head.get('pack')
                or proof.get('main_id') != row['main_id']
                or proof.get('main_sha256') != row['main_sha']
                or proof.get('variant_sha256') != row['variant_sha']
                or not isinstance(recipe, dict) or recipe.get('schema') != 'openrpg.legacy-vault-descriptor.v1'
                or recipe.get('logical_sha256') != row[old_side + '_sha'] or row[old_side + '_id'] in recipes):
            raise ValueError('batch record or recipe does not match source inventory')
        plan['candidates'].append({'retained_id': row[keep_side + '_id'], 'retired_id': row[old_side + '_id'],
            'retained_uuid': vault_uuid(row[keep_side + '_id']), 'retired_uuid': vault_uuid(row[old_side + '_id']),
            'retained_sha256': row[keep_side + '_sha'], 'retired_sha256': row[old_side + '_sha'],
            'retained_path': row[keep_side + '_path'], 'retired_path': row[old_side + '_path'],
            'choice': 'existing ' + keep_side + ' card selected; legacy bytes retained; material appearance may differ',
            'preferred_controls': proof.get('preferred_controls', []),
            'ready_for_removal': False})
        recipes[row[old_side + '_id']] = recipe
    plan['candidate_count'] = len(plan['candidates'])
    prepare_transition(load_packs(vault.resolve(strict=True), plan), plan['candidates'], recipes)
    return plan, {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'], 'recipes': recipes}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--batch', type=Path, action='append', required=True)
    parser.add_argument('--choices', type=Path)
    parser.add_argument('--plan-output', type=Path, required=True)
    parser.add_argument('--recipes-output', type=Path, required=True)
    args = parser.parse_args()
    if args.plan_output.exists() or args.recipes_output.exists():
        raise ValueError('output already exists')
    plan, recipes = plan_batch(args.vault, args.source, args.batch, args.choices)
    for path, doc in ((args.plan_output, plan), (args.recipes_output, recipes)):
        with path.open('x', encoding='utf8', newline='\n') as stream:
            json.dump(doc, stream, ensure_ascii=False, indent=2)
            stream.write('\n')
    print(json.dumps({'state': 'planning', 'candidates': plan['candidate_count'], 'removed': 0}))
