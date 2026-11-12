"""Qualify a composed private stage against native source-test evidence.

This only prepares a receipt. It does not publish or delete models. The
publication still rechecks every payload; retirement requires a separate
published-registry test while old paths are locked.
"""
import argparse
import datetime
import hashlib
import json
import re
import sys
from pathlib import Path

import prepare_variant_metadata_transition
import publish_variant_transition
import validate_no_legacy_reads
import variant_legacy_recipe
import vault_storage


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def qualify(vault, stage, plan, recipe_doc, baseline_path, studio, expected_studio_sha, importer, observation,
            defer_payload_recheck=False, native_log=None):
    vault = vault.resolve(strict=True)
    stage = stage.resolve(strict=True)
    studio = studio.resolve(strict=True)
    if digest(studio) != expected_studio_sha:
        raise ValueError('deployed Studio differs from observed reader')
    if recipe_doc.get('state') != 'prepared_only' or recipe_doc.get('registry_sha256') != plan['registry_sha256']:
        raise ValueError('recipe document is stale')
    manifest_raw = (stage / 'transition.json').read_bytes()
    baseline_raw = baseline_path.read_bytes()
    baseline_sha = validate_no_legacy_reads.validate_export_baseline(baseline_raw, manifest_raw)
    baseline = json.loads(baseline_raw)
    provenance = baseline.get('provenance')
    if not isinstance(provenance, dict) or provenance.get('state') != 'metadata_equivalence_only':
        if native_log is None:
            raise ValueError('native stage evidence missing')
        log_raw = Path(native_log).read_bytes()
        log = log_raw.decode('utf8')
        matched = re.search(r'Verified (\d+) real legacy references against staged snapshot; '
                            r'(\d+) exports succeeded, (\d+) reproduced existing failures', log)
        outcomes = list(baseline['exports'].values())
        counts = (len(outcomes), sum(x['outcome'] == 'success' for x in outcomes),
                  sum(x['outcome'] == 'error' for x in outcomes))
        if not matched or tuple(map(int, matched.groups())) != counts or '2 passed; 0 failed' not in log:
            raise ValueError('native stage log incomplete or mismatched')
        provenance = {'state': 'direct_native_test', 'native_log_sha256': hashlib.sha256(log_raw).hexdigest()}
    if digest(vault / 'metadata/asset-import-registry.v1.json') != plan['registry_sha256']:
        raise ValueError('registry changed')
    if defer_payload_recheck:
        current = prepare_variant_metadata_transition.load_packs(vault, plan)
        prepare_variant_metadata_transition.verify_stage(stage, current, plan, recipe_doc['recipes'])
        manifest = json.loads(manifest_raw)
        for row in plan['candidates']:
            keep = vault_storage.confined(vault, Path(row['retained_path']))
            old = vault_storage.confined(vault, Path(row['retired_path']))
            if keep == old:
                raise ValueError('historical and retained paths coincide')
            keep_descriptor = vault_storage.read_descriptor(vault, keep, variant_legacy_recipe.MAX_CONSOLIDATION_MODEL_BYTES)
            old_descriptor = vault_storage.read_descriptor(vault, old, variant_legacy_recipe.MAX_CONSOLIDATION_MODEL_BYTES)
            recipe = recipe_doc['recipes'][row['retired_id']]
            if (keep_descriptor is None or keep_descriptor['sha256'] != row['retained_sha256']
                    or old_descriptor is None or old_descriptor['sha256'] != row['retired_sha256']
                    or recipe != {'schema': 'openrpg.legacy-vault-descriptor.v1', 'descriptor': old_descriptor,
                                  'logical_sha256': row['retired_sha256'], 'logical_bytes': old_descriptor['bytes']}):
                raise ValueError('current descriptor differs from qualified recipe')
        if digest(vault / 'metadata/asset-import-registry.v1.json') != plan['registry_sha256']:
            raise ValueError('registry changed during qualification')
    else:
        _, manifest = publish_variant_transition.prepare_entries(vault, stage, plan, recipe_doc['recipes'], importer)
    if len(manifest['aliases_requiring_project_migration']) != plan['candidate_count']:
        raise ValueError('stage scope changed')
    outcomes = list(baseline['exports'].values())
    success = sum(x['outcome'] == 'success' for x in outcomes)
    failures = sum(x['outcome'] == 'error' for x in outcomes)
    if len(outcomes) != success + failures or not observation.strip():
        raise ValueError('incomplete outcome or Studio observation')
    receipt = {'schema': 'openrpg.variant-reader-qualification.v1', 'state': 'ready',
        'created_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'vault': str(vault), 'registry_sha256': plan['registry_sha256'],
        'stage_manifest_sha256': digest(stage / 'transition.json'),
        'studio': str(studio), 'studio_sha256': digest(studio),
        'publisher_sha256': digest(publish_variant_transition.__file__),
        'storage_reader_sha256': digest(vault_storage.__file__),
        'transition_tool_sha256': digest(prepare_variant_metadata_transition.__file__),
        'recipe_tool_sha256': digest(variant_legacy_recipe.__file__),
        'canonical_importer_sha256': digest(importer.__file__),
        'checks': {'native_legacy_reference': 'passed',
                   'native_legacy_export': 'passed' if failures == 0 else 'baseline_matched',
                   'real_recipes': 'passed', 'studio_library': 'passed',
                   'stage_snapshot_integrity': 'passed', 'recovery': 'passed'},
        'evidence': {'native_source_stages': provenance,
                     'native_source_baseline_sha256': baseline_sha,
                     'references': len(outcomes), 'successful_exports': success,
                     'matching_preexisting_failures': failures,
                     'studio_observation': observation,
                     'current_payload_recheck': 'deferred_to_transactional_publisher' if defer_payload_recheck else 'completed'},
        'limitations': ('Source native tests are carried over after exact per-card metadata comparison; '
                        'the combined stage has not itself been fully native-tested. '
                        if provenance['state'] == 'metadata_equivalence_only' else
                        'The prepared stage was native-tested; the published registry requires separate verification. ')
                       + 'Unique historical material bytes remain in shared storage; no visual equivalence is claimed.'}
    if failures:
        receipt.update(export_baseline=str(baseline_path.resolve(strict=True)),
                       export_baseline_sha256=baseline_sha,
                       baseline_validator_sha256=digest(validate_no_legacy_reads.__file__))
    return receipt


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--recipes', type=Path, required=True)
    p.add_argument('--baseline', type=Path, required=True)
    p.add_argument('--studio', type=Path, required=True)
    p.add_argument('--studio-sha256', required=True)
    p.add_argument('--importer-scripts', type=Path, required=True)
    p.add_argument('--observation', required=True)
    p.add_argument('--defer-payload-recheck-to-publisher', action='store_true')
    p.add_argument('--native-stage-log', type=Path)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('output exists')
    sys.path.insert(0, str(args.importer_scripts.resolve(strict=True)))
    import asset_import
    receipt = qualify(args.vault, args.stage, json.loads(args.plan.read_bytes()),
                      json.loads(args.recipes.read_bytes()), args.baseline, args.studio, args.studio_sha256,
                      asset_import, args.observation, args.defer_payload_recheck_to_publisher, args.native_stage_log)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': 'ready', 'references': receipt['evidence']['references'],
                      'successful_exports': receipt['evidence']['successful_exports'],
                      'matching_preexisting_failures': receipt['evidence']['matching_preexisting_failures']}))
