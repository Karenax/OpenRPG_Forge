"""Compose disjoint, equally pinned plans and recipes without publishing."""
import argparse
import json
from pathlib import Path


def compose(inputs):
    if not inputs:
        raise ValueError('no plans')
    pin = None
    candidates, recipes, kept, retired_ids = [], {}, {}, set()
    for plan, document in inputs:
        if plan.get('schema') != 'openrpg.variant-consolidation-plan.v1':
            raise ValueError('unexpected plan schema')
        current = plan.get('registry_sha256')
        if pin is None:
            pin = current
        if not current or current != pin or document.get('registry_sha256') != pin or document.get('state') != 'prepared_only':
            raise ValueError('registry pin or recipe state differs')
        rows = plan.get('candidates')
        local = document.get('recipes')
        if not isinstance(rows, list) or plan.get('candidate_count') != len(rows) or not isinstance(local, dict):
            raise ValueError('invalid candidate or recipe list')
        if set(local) != {r['retired_id'] for r in rows}:
            raise ValueError('recipe identities differ')
        for row in rows:
            keep, retired = row['retained_id'], row['retired_id']
            keep_pin = (row['retained_uuid'], row['retained_sha256'], row['retained_path'])
            if (keep == retired or retired in retired_ids
                    or (keep in kept and kept[keep] != keep_pin)):
                raise ValueError('overlapping identity plans')
            if local[retired].get('logical_sha256') != row['retired_sha256']:
                raise ValueError('legacy recipe SHA differs')
            kept[keep] = keep_pin
            retired_ids.add(retired)
            candidates.append(row)
            recipes[retired] = local[retired]
    if set(kept).intersection(retired_ids):
        raise ValueError('overlapping identity plans')
    return ({'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': pin,
             'state': 'planning', 'candidate_count': len(candidates), 'candidates': candidates},
            {'state': 'prepared_only', 'registry_sha256': pin, 'recipes': recipes})


def write_new(path, value):
    with path.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', nargs=2, action='append', metavar=('PLAN', 'RECIPES'), required=True)
    parser.add_argument('--plan-output', type=Path, required=True)
    parser.add_argument('--recipes-output', type=Path, required=True)
    args = parser.parse_args()
    if args.plan_output.exists() or args.recipes_output.exists():
        raise ValueError('output already exists')
    sources = [(json.loads(Path(plan).read_bytes()), json.loads(Path(recipes).read_bytes()))
               for plan, recipes in args.input]
    plan, recipes = compose(sources)
    write_new(args.plan_output, plan)
    write_new(args.recipes_output, recipes)
    print(json.dumps({'state': plan['state'], 'candidates': plan['candidate_count']}))
