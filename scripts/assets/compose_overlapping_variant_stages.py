"""Combine tested stages with overlapping packs, without rereading models.

For every retained card, the complete prepared detail and profile must equal
the corresponding record in its original stage. Payload and native-reader
qualification remain requirements of the eventual publication/retirement.
"""
import argparse
import hashlib
import json
from pathlib import Path

import vault_storage
from compose_variant_transitions import compose
from prepare_variant_metadata_transition import _index, encoded, load_packs, prepare_transition, verify_stage


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build(vault, sources, output, allowed_root=None):
    allowed = (allowed_root or Path(r'E:\OpenRPG_Forge\.build\asset-conversion')).resolve(strict=True)
    vault = vault.resolve(strict=True)
    parent = output.parent.resolve(strict=True)
    if not parent.is_relative_to(allowed) or output.exists():
        raise ValueError('invalid or existing stage output')
    vault_storage.confined(allowed, parent)
    inputs = [(p, r) for p, r, _ in sources]
    plan, recipes_doc = compose(inputs)
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if sha(before) != plan['registry_sha256']:
        raise ValueError('registry changed')
    source_records, source_pins = {}, []
    for part, recipe_doc, folder in sources:
        folder = folder.resolve(strict=True)
        if not folder.is_relative_to(allowed):
            raise ValueError('source stage outside work root')
        vault_storage.confined(allowed, folder)
        manifest_raw = (folder / 'transition.json').read_bytes()
        manifest = json.loads(manifest_raw)
        source_pins.append({'path': str(folder), 'manifest_sha256': sha(manifest_raw)})
        current = load_packs(vault, part)
        verify_stage(folder, current, part, recipe_doc['recipes'])
        staged = {}
        for pack in manifest['packs']:
            staged[pack] = {'details': json.loads((folder / pack / 'details.v1.json').read_bytes()),
                            'profiles': json.loads((folder / pack / 'profiles.v1.json').read_bytes())}
        idx = _index(staged)
        for row in part['candidates']:
            ident = row['retained_id']
            if ident in source_records and source_records[ident] != idx[ident]:
                raise ValueError('one retained card has conflicting source stages')
            source_records[ident] = idx[ident]
    current = load_packs(vault, plan)
    proposed = prepare_transition(current, plan['candidates'], recipes_doc['recipes'])
    combined = _index(proposed['packs'])
    for ident, record in source_records.items():
        if combined.get(ident) != record:
            raise ValueError('combined retained metadata differs from tested stage: ' + ident)
    if registry.read_bytes() != before:
        raise ValueError('registry changed while composing')
    manifest = {'schema': 'openrpg.variant-metadata-transition.v1', 'state': 'prepared_only',
                'registry_sha256': plan['registry_sha256'], 'includes_legacy_recipes': True,
                'aliases_requiring_project_migration': proposed['aliases_requiring_project_migration'],
                'not_ready_reasons': ['native reader and publication remain pending'],
                'composed_sources': source_pins, 'packs': {}}
    output.mkdir()
    for pack in proposed['changed_packs']:
        if not pack or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789.-_' for c in pack):
            raise ValueError('invalid pack id')
        dest = output / pack
        dest.mkdir()
        docs, pins = proposed['packs'][pack], {}
        for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
            raw = encoded(docs[key])
            with (dest / filename).open('xb') as stream:
                stream.write(raw)
            pins[filename] = sha(raw)
        pins['revision'] = sha(encoded({'details': docs['details'], 'profiles': docs['profiles']}))
        manifest['packs'][pack] = pins
    with (output / 'transition.json').open('xb') as stream:
        stream.write(encoded(manifest))
    verify_stage(output, current, plan, recipes_doc['recipes'])
    if registry.read_bytes() != before:
        raise ValueError('registry changed during composition')
    return plan, recipes_doc, manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--input', nargs=3, action='append', required=True, metavar=('PLAN', 'RECIPES', 'STAGE'))
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--plan-output', type=Path, required=True)
    parser.add_argument('--recipes-output', type=Path, required=True)
    args = parser.parse_args()
    if args.plan_output.exists() or args.recipes_output.exists():
        raise ValueError('output exists')
    sources = [(json.loads(Path(p).read_bytes()), json.loads(Path(r).read_bytes()), Path(s))
               for p, r, s in args.input]
    plan, recipes, manifest = build(args.vault, sources, args.output)
    for path, doc in ((args.plan_output, plan), (args.recipes_output, recipes)):
        with path.open('x', encoding='utf8', newline='\n') as stream:
            json.dump(doc, stream, ensure_ascii=False, indent=2)
            stream.write('\n')
    print(json.dumps({'state': 'prepared_only', 'candidates': plan['candidate_count'],
                      'packs': len(manifest['packs']), 'models_removed': 0}))
