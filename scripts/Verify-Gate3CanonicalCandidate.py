"""Authenticate the common Studio for public Gate 3 proofs; never build or launch."""

from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import sys

sys.dont_write_bytecode = True


def validation_driver():
    path = Path(__file__).with_name('Validate-Gate3WorkingTree.py')
    specification = importlib.util.spec_from_file_location('forge_gate3_validator', path)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def verify_candidate(candidate, evidence, driver=None):
    driver = driver or validation_driver()
    for path in (candidate, evidence):
        driver.require(path.is_absolute(), f'Gate 3 input must be absolute: {path}')
        driver.physical_path(path, Path(path.anchor))
    candidate, evidence = candidate.resolve(), evidence.resolve()
    driver.require(evidence.is_dir(), f'Gate 3 evidence directory absent: {evidence}')
    source_files = driver.source_inputs()
    driver.physical_path(evidence / 'release-source-manifest.json', evidence)
    source_manifest = driver.read_json(evidence / 'release-source-manifest.json')
    target_dir = Path(source_manifest['target_dir'])
    driver.require(target_dir.is_absolute(), 'Release cache must be absolute')
    studio = driver.release_artifact_paths(target_dir)['studio-release']
    driver.require(candidate == studio, 'Candidate is not the Studio in the common release receipt')

    proof_paths = []
    for mode in ('wasm', 'release'):
        proof_paths.extend(evidence / f'{mode}-{suffix}.json' for suffix in (
            'source-manifest', 'result', 'working-tree-before', 'working-tree-after'))
        proof_paths.extend(evidence / f'{name}.log' for name, _, _ in driver.STAGES[mode])

    def proof_pins():
        pins = {}
        for path in proof_paths:
            driver.physical_path(path, evidence)
            driver.require(path.is_file(), f'Gate 3 proof absent: {path}')
            pins[path.name] = {'sha256': driver.sha(path), 'bytes': path.stat().st_size}
        return pins

    before = proof_pins()
    # This verifies all current sources, logs, release artifacts and the completed
    # WASM/worker/static-assets chain through the one canonical validation API.
    manifest, result = driver.completed_receipt(evidence, 'release', source_files, target_dir)
    pin = driver.release_artifact_pin(candidate, target_dir)
    driver.require(pin == result['artifacts'][1], 'Studio differs from its release artifact pin')
    frontend = driver.SOURCE / 'apps/forge_studio/dist'
    closed = driver.validate_frontend(
        frontend, source_files, require_assets=True,
        expected_asset_manifest_sha256=driver.LOCAL_ASSET_MANIFEST_SHA256)
    driver.require(closed == result['frontend'], 'Canonical frontend changed during input validation')
    driver.require(driver.source_inputs() == source_files, 'Common sources changed during input validation')
    driver.require(driver.release_artifact_pin(candidate, target_dir) == pin,
                   'Studio changed during input validation')
    driver.require(proof_pins() == before, 'Gate 3 receipts or logs changed during input validation')
    governed = {name: entry for name, entry in closed['files'].items() if name in (
        'forge-kit/build.json', 'forge-imports/buildings.json') or (
            name.startswith('forge-imports/') and name.endswith('/package.json'))}
    return {
        'schema': 'forge.gate3.canonical-candidate.v1', 'status': 'VALIDATED_INPUTS',
        'source': str(driver.SOURCE), 'source_fingerprint_sha256': manifest['fingerprint_sha256'],
        'candidate': pin, 'frontend': str(frontend),
        'frontend_fingerprint_sha256': hashlib.sha256(json.dumps(
            closed['files'], sort_keys=True).encode()).hexdigest(),
        'worker_manifest_sha256': closed['worker_manifest_sha256'],
        'local_asset_manifest_sha256': closed['local_asset_manifest_sha256'],
        'asset_receipt_sha256': closed['asset_receipt_sha256'],
        'governed_input_pins': governed, 'evidence': str(evidence), 'evidence_proofs': before,
        'validation_driver_sha256': result['driver_sha256'],
        'verification_helper_sha256': driver.sha(Path(__file__)),
        'runtime_verified': False, 'gate_qualified': False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    try:
        verified = verify_candidate(args.candidate, args.evidence)
    except (ValueError, KeyError, OSError, TypeError) as error:
        print(f'Canonical Gate 3 candidate refused: {error}', file=sys.stderr)
        return 1
    print(json.dumps(verified, sort_keys=True))
    return 0


if __name__ == '__main__':
    sys.exit(main())
