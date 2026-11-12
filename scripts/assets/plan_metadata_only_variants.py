"""Prepare recipes for GLBs differing in metadata or explicit default values.

This is a planning tool. It does not establish visual qualification or publish.
"""
import argparse
import csv
import hashlib
import itertools
import json
import re
import struct
from pathlib import Path

import vault_storage
from audit_variant_exact_candidates import diffs
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_recipe


def metadata_only(entry):
    if entry.get('binary_chunk_exact') is not True or entry.get('truncated') is not False:
        return False
    paths = entry.get('differences')
    return isinstance(paths, list) and bool(paths) and all(
        re.fullmatch(r'\$/nodes/\d+/extras/asset_id', path)
        or re.fullmatch(r'\$/nodes/\d+/extras/source_sha256', path)
        or re.fullmatch(r'\$/materials/\d+/extras/source_unity_sha256', path)
        or re.fullmatch(r'\$/materials/\d+/extras/material_program_sha256', path)
        or re.fullmatch(r'\$/materials/\d+/extras/(source_graph_sha256|source_material_sha256)', path)
        or re.fullmatch(r'\$/materials/\d+/extras/unity_material_sha256', path)
        or path == '$/extras/source_asset_id'
        or re.fullmatch(r'\$/images/\d+/name', path)
        or re.fullmatch(r'\$/materials/\d+/doubleSided \(missing\)', path)
        or path == '$/asset/extras (missing)'
        or re.fullmatch(r'\$/nodes/\d+/extras/(asset_id|source_sha256|source_pipeline|static_source_equivalence_proof_sha256) \(missing\)', path)
        or re.fullmatch(r'\$/materials/\d+/extras/(source_pipeline|source_unity_material_sha256) \(missing\)', path)
        or re.fullmatch(r'\$/materials/\d+/extras/(render_parity_unqualified|source_unity_pipeline|source_unity_shader_guid|source_unity_shader_sha256) \(missing\)', path)
        or re.fullmatch(r'\$/extras/(source_pipeline|visual_parity_pending|urp_gpu_sampling_pending|source_model_dependencies) \(missing\)', path)
        for path in paths)


def verify_documents(first, second, expected):
    actual = list(itertools.islice(diffs(first, second), 501))
    if len(actual) > 500 or actual != expected or not metadata_only(
            {'binary_chunk_exact': True, 'truncated': False, 'differences': actual}):
        raise ValueError('current GLB differences differ from metadata-only audit')
    for path in actual:
        extra = re.fullmatch(r'\$/extras/(source_pipeline|visual_parity_pending|urp_gpu_sampling_pending|source_model_dependencies) \(missing\)', path)
        if extra:
            field = extra.group(1)
            for doc in (first, second):
                values = doc.get('extras', {})
                if field not in values:
                    continue
                value = values[field]
                if field == 'source_pipeline' and value not in ('URP', 'HDRP'):
                    raise ValueError('invalid source pipeline')
                if field in ('visual_parity_pending', 'urp_gpu_sampling_pending') and value is not True:
                    raise ValueError('invalid pending qualification flag')
                if field == 'source_model_dependencies' and (not isinstance(value, str) or not value or len(value) > 1024):
                    raise ValueError('invalid source dependency description')
        material_extra = re.fullmatch(r'\$/materials/(\d+)/extras/(render_parity_unqualified|source_unity_pipeline|source_unity_shader_guid|source_unity_shader_sha256) \(missing\)', path)
        if material_extra:
            index, field = int(material_extra.group(1)), material_extra.group(2)
            for doc in (first, second):
                values = doc['materials'][index].get('extras', {})
                if field not in values:
                    continue
                value = values[field]
                if field == 'render_parity_unqualified' and value is not True:
                    raise ValueError('invalid parity flag')
                if field == 'source_unity_pipeline' and value not in ('URP', 'HDRP'):
                    raise ValueError('invalid material pipeline')
                if field.endswith('guid') and (not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{32}', value)):
                    raise ValueError('invalid source shader GUID')
                if field.endswith('sha256') and (not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value)):
                    raise ValueError('invalid source shader SHA')
        unity_hash = re.fullmatch(r'\$/materials/(\d+)/extras/unity_material_sha256', path)
        if unity_hash:
            index = int(unity_hash.group(1))
            for doc in (first, second):
                value = doc['materials'][index]['extras']['unity_material_sha256']
                if not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value):
                    raise ValueError('invalid Unity material SHA')
        hash_field = re.fullmatch(r'\$/materials/(\d+)/extras/(source_graph_sha256|source_material_sha256)', path)
        if hash_field:
            index, field = int(hash_field.group(1)), hash_field.group(2)
            for doc in (first, second):
                value = doc['materials'][index]['extras'][field]
                if not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value):
                    raise ValueError('invalid source material provenance SHA')
        match = re.fullmatch(r'\$/materials/(\d+)/doubleSided \(missing\)', path)
        if match:
            index = int(match.group(1))
            left = first['materials'][index].get('doubleSided', False)
            right = second['materials'][index].get('doubleSided', False)
            if type(left) is not bool or type(right) is not bool or left != right:
                raise ValueError('double-sided rendering differs')
        if path == '$/asset/extras (missing)':
            permitted = {'source_static_equivalence_proof_sha256', 'source_variant_glb_sha256',
                         'static_source_equivalence_proof_sha256', 'source_base_glb_sha256'}
            for doc in (first, second):
                value = doc.get('asset', {}).get('extras')
                if value is not None and (not isinstance(value, dict) or not set(value) <= permitted
                        or any(not isinstance(v, str) or not re.fullmatch(r'[0-9a-f]{64}', v) for v in value.values())):
                    raise ValueError('unrecognized asset provenance extras')
        provenance = re.fullmatch(r'\$/(nodes|materials)/(\d+)/extras/(asset_id|source_sha256|source_pipeline|static_source_equivalence_proof_sha256|source_unity_material_sha256) \(missing\)', path)
        if provenance:
            collection, index, field = provenance.group(1), int(provenance.group(2)), provenance.group(3)
            for doc in (first, second):
                extras = doc[collection][index].get('extras', {})
                if field in extras:
                    value = extras[field]
                    if not isinstance(value, str) or not value or len(value) > 1024:
                        raise ValueError('invalid provenance value')
                    if field.endswith('sha256') and not re.fullmatch(r'[0-9a-f]{64}', value):
                        raise ValueError('invalid provenance SHA')


def verify_differences(first, second, expected):
    def document(raw):
        length = struct.unpack_from('<I', raw, 12)[0]
        return vault_storage.decode_json(raw[20:20 + length])
    verify_documents(document(first), document(second), expected)


def prepare(vault, report, comparison, audit):
    vault = vault.resolve(strict=True)
    registry = vault / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    pin = hashlib.sha256(before).hexdigest()
    evidence = json.loads(audit.read_bytes())
    if evidence['registry_sha256'] != pin:
        raise ValueError('stale audit')
    with comparison.open(encoding='utf-8-sig', newline='') as stream:
        models = {r['id']: r for r in csv.DictReader(stream)}
    with (report / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as stream:
        identities = {r['id']: r for r in csv.DictReader(stream)}
    candidates, recipes, seen = [], {}, set()
    for entry in evidence['results']:
        if not metadata_only(entry):
            raise ValueError('difference is not limited to qualified metadata')
        ident = entry['id']; pair, model = identities[ident], models[ident]
        keep, retired = pair['main_id'], pair['variant_id']
        if keep == retired or keep in seen or retired in seen:
            raise ValueError('overlapping identities')
        seen.update((keep, retired))
        for side in ('main', 'variant'):
            if pair[side + '_path'] != model[side + '_path'] or pair[side + '_sha'] != model[side + '_sha']:
                raise ValueError('candidate evidence mismatches')
        candidates.append({'retained_id': keep, 'retired_id': retired,
            'retained_uuid': vault_uuid(keep), 'retired_uuid': vault_uuid(retired),
            'retained_path': model['main_path'], 'retired_path': model['variant_path'],
            'retained_sha256': model['main_sha'], 'retired_sha256': model['variant_sha'],
            'metadata_differences': entry['differences'], 'ready_for_removal': False})
    if not candidates:
        raise ValueError('no candidates')
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': pin,
            'state': 'planning', 'candidate_count': len(candidates), 'candidates': candidates}
    prepare_transition(load_packs(vault, plan), candidates)
    for index, item in enumerate(candidates, 1):
        a = vault_storage.read_bytes(vault, vault_storage.confined(vault, Path(item['retained_path'])), 512 * 1024 * 1024)
        b = vault_storage.read_bytes(vault, vault_storage.confined(vault, Path(item['retired_path'])), 512 * 1024 * 1024)
        recipes[item['retired_id']] = build_recipe(a, b, item['retained_sha256'], item['retired_sha256'])
        verify_differences(a, b, item['metadata_differences'])
        if index % 25 == 0:
            print(f'verified {index}/{len(candidates)} metadata-only recipes', flush=True)
    if registry.read_bytes() != before:
        raise ValueError('registry changed')
    return plan, {'state': 'prepared_only', 'registry_sha256': pin, 'recipes': recipes}


def write_new(path, value):
    with path.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--comparison', type=Path, required=True)
    parser.add_argument('--audit', type=Path, required=True)
    parser.add_argument('--plan-output', type=Path, required=True)
    parser.add_argument('--recipes-output', type=Path, required=True)
    args = parser.parse_args()
    if args.plan_output.exists() or args.recipes_output.exists():
        raise ValueError('output already exists')
    plan, recipes = prepare(args.vault, args.report, args.comparison, args.audit)
    write_new(args.plan_output, plan)
    write_new(args.recipes_output, recipes)
    print(json.dumps({'state': plan['state'], 'candidates': len(plan['candidates'])}))
