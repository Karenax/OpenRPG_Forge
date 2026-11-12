"""Carry a native baseline to an exactly metadata-identical rebased stage.

No payload is read or removed. A published-registry native test is still
required before retiring any historical file.
"""
import argparse
import hashlib
import json
from pathlib import Path

from compose_variant_test_baselines import stage_data
from validate_no_legacy_reads import validate_export_baseline


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def rebase_baseline(original_stage, rebased_stage, baseline_path):
    old_raw, old, _ = stage_data(original_stage)
    new_raw, new, _ = stage_data(rebased_stage)
    baseline_raw = baseline_path.read_bytes()
    validate_export_baseline(baseline_raw, old_raw)
    baseline = json.loads(baseline_raw)
    if (baseline.get('provenance', {}).get('state') != 'metadata_equivalence_only'
            or new.get('rebased_from_stage_sha256') != sha(old_raw)
            or old['packs'] != new['packs']
            or old['aliases_requiring_project_migration'] != new['aliases_requiring_project_migration']):
        raise ValueError('rebased stage is not metadata-identical to tested source')
    baseline['stage_manifest_sha256'] = sha(new_raw)
    provenance = baseline['provenance']
    provenance.setdefault('rebases', []).append({'previous_manifest_sha256': sha(old_raw),
        'rebased_manifest_sha256': sha(new_raw), 'previous_baseline_sha256': sha(baseline_raw)})
    validate_export_baseline(json.dumps(baseline).encode('utf8'), new_raw)
    return baseline


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--original-stage', type=Path, required=True)
    p.add_argument('--rebased-stage', type=Path, required=True)
    p.add_argument('--baseline', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('output exists')
    result = rebase_baseline(args.original_stage, args.rebased_stage, args.baseline)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': 'metadata_equivalence_only', 'references': len(result['exports'])}))
