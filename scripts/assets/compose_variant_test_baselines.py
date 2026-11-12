"""Carry native-test outcomes across metadata-identical rebase and composition.

This does not test the deployed reader or authorize publication. Each source
must have an unmodified prepared stage, a complete native test log and a
matching baseline; each retained card must be byte-for-byte identical in the
composed stage. Published-reference checks remain mandatory before deletion.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

from prepare_variant_metadata_transition import _index
from validate_no_legacy_reads import validate_export_baseline


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def stage_data(folder):
    raw = (folder / 'transition.json').read_bytes()
    manifest = json.loads(raw)
    if manifest.get('state') != 'prepared_only' or not manifest.get('includes_legacy_recipes'):
        raise ValueError('invalid prepared stage')
    packs = {}
    for pack, pins in manifest['packs'].items():
        docs = {}
        for filename, key in [('details.v1.json', 'details'), ('profiles.v1.json', 'profiles')]:
            payload = (folder / pack / filename).read_bytes()
            if sha(payload) != pins[filename]:
                raise ValueError('stage metadata SHA changed')
            docs[key] = json.loads(payload)
        packs[pack] = docs
    return raw, manifest, _index(packs)


def compose_baselines(combined_stage, sources):
    if not sources:
        raise ValueError('no test sources')
    combined_raw, combined, combined_index = stage_data(combined_stage)
    exports, aliases, evidence = {}, [], []
    declared = {x['path']: x['manifest_sha256'] for x in combined.get('composed_sources', [])}
    if len(declared) != len(sources):
        raise ValueError('combined source count changed')
    for original, rebased, baseline_path, log_path in sources:
        original_raw, old, _ = stage_data(original)
        rebased_raw, new, source_index = stage_data(rebased)
        if (new.get('rebased_from_stage_sha256') != sha(original_raw)
                or old['packs'] != new['packs']
                or old['aliases_requiring_project_migration'] != new['aliases_requiring_project_migration']
                or declared.get(str(rebased.resolve(strict=True))) != sha(rebased_raw)):
            raise ValueError('source stage changed during rebase or composition')
        baseline_raw = baseline_path.read_bytes()
        validate_export_baseline(baseline_raw, original_raw)
        baseline = json.loads(baseline_raw)
        log_raw = log_path.read_bytes()
        log = log_raw.decode('utf8')
        result = re.search(r'Verified (\d+) real legacy references against staged snapshot; '
                           r'(\d+) exports succeeded, (\d+) reproduced existing failures', log)
        expected = old['aliases_requiring_project_migration']
        if not result or '2 passed; 0 failed' not in log:
            raise ValueError('native stage test did not complete successfully')
        success = sum(x['outcome'] == 'success' for x in baseline['exports'].values())
        failures = sum(x['outcome'] == 'error' for x in baseline['exports'].values())
        if tuple(map(int, result.groups())) != (len(expected), success, failures):
            raise ValueError('native outcomes differ from baseline')
        for alias in expected:
            ident = alias['retained_id']
            if source_index.get(ident) != combined_index.get(ident):
                raise ValueError('combined retained metadata differs from native-tested stage')
            retired = alias['retired_id']
            if retired in exports:
                raise ValueError('duplicate historical identity')
            exports[retired] = baseline['exports'][retired]
        aliases.extend(expected)
        evidence.append({'original_stage_sha256': sha(original_raw), 'rebased_stage_sha256': sha(rebased_raw),
                         'baseline_sha256': sha(baseline_raw), 'native_log_sha256': sha(log_raw),
                         'references': len(expected), 'successful_exports': success,
                         'matching_preexisting_failures': failures})
    if aliases != combined['aliases_requiring_project_migration']:
        raise ValueError('combined aliases differ from tested stages')
    result = {'schema': 'openrpg.variant-export-baseline.v1',
              'stage_manifest_sha256': sha(combined_raw), 'exports': exports,
              'provenance': {'state': 'metadata_equivalence_only', 'sources': evidence,
                             'limitations': 'Combined and published stages require separate qualification.'}}
    validate_export_baseline(json.dumps(result).encode('utf8'), combined_raw)
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--combined-stage', type=Path, required=True)
    parser.add_argument('--source', nargs=4, action='append', required=True,
                        metavar=('ORIGINAL_STAGE', 'REBASED_STAGE', 'BASELINE', 'NATIVE_LOG'))
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('output exists')
    result = compose_baselines(args.combined_stage, [tuple(map(Path, row)) for row in args.source])
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    outcomes = list(result['exports'].values())
    print(json.dumps({'state': 'metadata_equivalence_only', 'references': len(outcomes),
                      'successful_exports': sum(x['outcome'] == 'success' for x in outcomes),
                      'matching_preexisting_failures': sum(x['outcome'] == 'error' for x in outcomes)}))
