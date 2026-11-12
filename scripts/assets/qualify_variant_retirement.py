"""Prepare a pinned retirement receipt after published native verification."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import publish_variant_transition
import retire_consolidated_models
import validate_no_legacy_reads
import variant_legacy_recipe
import vault_storage
import audit_candidate_local_references


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def qualify(vault, plan, stage, proof, studio, studio_sha, importer):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    proof, studio = proof.resolve(strict=True), studio.resolve(strict=True)
    if digest(studio) != studio_sha:
        raise ValueError('observed deployed Studio changed')
    evidence = json.loads(proof.read_bytes())
    if (evidence.get('state') != 'passed'
            or evidence.get('exclusive_legacy_paths') != len(plan['candidates'])
            or evidence.get('plan_fingerprint') != validate_no_legacy_reads.plan_fingerprint(plan)
            or evidence.get('stage_manifest_sha256') != digest(stage / 'transition.json')):
        raise ValueError('exclusive proof incomplete or stale')
    registry, support, active = retire_consolidated_models.active_references(vault, plan['candidates'])
    if any(row['retired_id'] in active or row['retained_id'] not in active for row in plan['candidates']):
        raise ValueError('published identities are not consolidated')
    registry_sha = hashlib.sha256(registry).hexdigest()
    if registry_sha != evidence.get('registry_sha256'):
        raise ValueError('registry changed after exclusive proof')
    receipt = {'schema': 'openrpg.variant-retirement-qualification.v1', 'state': 'ready',
               'vault': str(vault), 'registry_sha256': registry_sha,
               'source_registry_sha256': hashlib.sha256(support).hexdigest(),
               'stage_manifest_sha256': digest(stage / 'transition.json'),
               'studio': str(studio), 'studio_sha256': studio_sha,
               'exclusive_read_proof': str(proof), 'exclusive_read_proof_sha256': digest(proof),
               'retirement_tool_sha256': digest(retire_consolidated_models.__file__),
               'storage_reader_sha256': digest(vault_storage.__file__),
               'publisher_tool_sha256': digest(publish_variant_transition.__file__),
               'recipe_tool_sha256': digest(variant_legacy_recipe.__file__),
               'proof_tool_sha256': digest(validate_no_legacy_reads.__file__),
               'candidate_reference_audit_sha256': digest(audit_candidate_local_references.__file__),
               'canonical_importer_sha256': digest(importer.__file__),
               'reference_scope': 'active details, profiles and source-support; external projects not exhaustively scanned'}
    retire_consolidated_models.preflight(vault, plan, stage, receipt)
    return receipt


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--proof', type=Path, required=True)
    p.add_argument('--studio', type=Path, required=True)
    p.add_argument('--studio-sha256', required=True)
    p.add_argument('--importer-scripts', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('output exists')
    sys.path.insert(0, str(args.importer_scripts.resolve(strict=True)))
    import asset_import
    plan = json.loads(args.plan.read_bytes())
    receipt = qualify(args.vault, plan, args.stage, args.proof, args.studio, args.studio_sha256, asset_import)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': 'ready', 'candidates': len(plan['candidates']), 'models_removed': 0}))
