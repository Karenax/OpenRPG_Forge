"""Run authenticated private replays with the common native suite's binaries."""

from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import os
import re
import subprocess

SOURCE = Path(__file__).resolve().parents[1]
EXPECTED_TOTAL = 34

# Acquisition pins only: no historical executable or qualification is reused.
ACQUISITIONS = (
    ('rehearsal', 'integrated-rehearsal-acquired-inputs-001',
     'b8657993d1ce0da799311562746d1c45fa840c681977f5ba3972c3ee8589612c', 259),
    ('debris', 'integrated-debris-full-cpu-acquired-inputs-003',
     'd6a93188b4b08899cd34527489aeae3059b972e4b6f0eb6dca50acde36a28c60', 4997),
    ('game-fish', 'integrated-source063-runtime-fish-inputs-001',
     '0fced24e084d51123235d3b2ec240369f09f7ee13b729bdc020a302e60d1b0d8', 500),
)
DEBRIS_TESTS = (
    ('forge_engine', 'tidewater_debris::nature::tests::acquired_program_accepts_shared_receivers_without_duplicate_sun_shadow'),
    ('forge_engine', 'tidewater_debris::pebble_material::tests::acquired_program_accepts_shared_receivers_without_duplicate_sun_shadow'),
    ('forge_engine', 'tidewater_debris::pebble_mesh::tests::acquired_patch_preserves_graphics_resources'),
    ('forge_engine', 'tidewater_debris::scanned_draws::tests::edited_duplicates_keep_variation_and_transition_records_separate'),
    ('forge_engine', 'tidewater_debris::scanned_lod::tests::matches_acquired_source_bands_and_shadow_population'),
    ('forge_engine', 'tidewater_debris::scanned_placements::tests::source_frames_enter_common_documents_without_losing_variation'),
    ('forge_engine', 'tidewater_debris::scanned_shadow::tests::export_exact_source_depth_entries'),
    ('forge_engine', 'tidewater_debris::scanned_shadow_runtime::tests::near_casters_share_four_prototypes_and_retain_draw_coverage'),
    ('forge_engine', 'tidewater_debris::scene::tests::common_instances_reject_bad_revision_then_preserve_deletion_and_casters'),
    ('forge_assets', 'tidewater_debris::collisions::tests::real_frames_preserve_ownership_and_refuse_altered_data'),
    ('forge_assets', 'tidewater_debris::native::tests::acquired_native_input_survives_both_transports_and_refuses_shader_tampering'),
    ('forge_assets', 'tidewater_debris::pebble_program::tests::acquired_transports_preserve_source_and_refuse_tampering'),
    ('forge_assets', 'tidewater_debris::scanned::tests::acquired_package_loads_and_corrupt_texture_is_refused'),
    ('forge_assets', 'tidewater_debris::scanned_input::tests::acquired_scans_match_in_both_transports_and_refuse_tampering'),
    ('forge_assets', 'tidewater_debris::scanned::atlases::tests::acquired_mips_keep_source_colour_formats_and_sampler'),
    ('forge_assets', 'tidewater_debris::scanned::geometry::tests::decoded_prototypes_preserve_all_source_lods'),
)
GAME_FISH_TESTS = (
    ('forge_assets', 'tidewater_game_fish::tests::async_transport_preserves_the_same_verified_payloads'),
    ('forge_assets', 'tidewater_game_fish::tests::captured_package_loads_and_catalog_excludes_parked_slots'),
    ('forge_assets', 'tidewater_game_fish::tests::changed_manifest_does_not_start_payload_acquisition'),
    ('forge_assets', 'tidewater_game_fish::tests::changed_surface_payload_is_refused'),
    ('forge_engine', 'tidewater_fish_props::tests::game_stand_excludes_pool_and_preserves_three_source_lods'),
)


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def input_path(path, root, validation):
    require(path.is_absolute() and '..' not in path.parts and path.is_relative_to(root),
            f'Private input must stay inside its acquisition: {path}')
    validation.physical_path(path, root)


def load_acquisition(name, directory, expected_sha, expected_files, validation):
    proof_path = SOURCE.parent / '.gate3-evidence/20261002' / directory / 'proof.json'
    validation.physical_path(proof_path, proof_path.parent)
    proof_bytes = proof_path.read_bytes()
    require(hashlib.sha256(proof_bytes).hexdigest() == expected_sha,
            f'Private acquisition proof does not match its pinned source: {proof_path}')
    proof = json.loads(proof_bytes)
    rows = proof['files']
    require(len(rows) == expected_files and len({row['copy'] for row in rows}) == len(rows),
            f'Incomplete or duplicated acquired input manifest: {name}')
    for row in rows:
        input_path(Path(row['copy']), proof_path.parent, validation)
        require(isinstance(row['bytes'], int) and row['bytes'] >= 0
                and re.fullmatch(r'[0-9a-f]{64}', row['sha256']), 'Invalid private input pin')
    return {'name': name, 'proof': str(proof_path), 'proof_sha256': expected_sha,
            'files': len(rows), 'pins': rows}, proof['child_environment']


def validate_test_output(content, returncode, expected, argv):
    summaries = re.findall(r'^test result: (ok|FAILED)\. (\d+) passed; (\d+) failed; (\d+) ignored;.*$',
                           content, re.MULTILINE)
    require(len(summaries) == 1, 'Exactly one native test summary is required')
    state, passed, failed, ignored = summaries[0]
    passed, failed, ignored = int(passed), int(failed), int(ignored)
    require(returncode == 0 and state == 'ok' and passed == expected and failed == 0 and ignored == 0,
            f'Expected exactly {expected} executed successful tests; got {passed}/{failed}/{ignored}, exit {returncode}')
    tests = re.findall(r'^test ([A-Za-z0-9_:]+) \.\.\. (ok|FAILED|ignored)\s*$', content, re.MULTILINE)
    require(len(tests) == expected and len({name for name, _ in tests}) == expected
            and all(status == 'ok' for _, status in tests), 'Missing, duplicate or unsuccessful named test')
    if '--exact' in argv:
        require(expected == 1 and tests[0][0] == argv[0], 'The exact selected test was not executed')
    else:
        require(all(name.startswith(argv[0]) for name, _ in tests), 'Unexpected test outside selected module')
        if '--skip' in argv:
            excluded = argv[argv.index('--skip') + 1]
            require(all(excluded not in name for name, _ in tests), 'Excluded test was executed')
    return passed, failed, [name for name, _ in tests]


def replay_groups():
    groups = [
        ('assets', 'forge_assets', 1, ['tidewater_fish_props::tests::real_package_and_refusals', '--ignored', '--exact'], 'rehearsal', None),
        ('fish', 'forge_engine', 9, ['tidewater_fish_props::', '--ignored', '--skip', GAME_FISH_TESTS[-1][1]], 'rehearsal', None),
        ('scanned', 'forge_engine', 3, ['tidewater_debris::scanned_scene::tests::', '--ignored'], 'scanned', None),
    ]
    for family, tests in (('debris', DEBRIS_TESTS), ('game-fish', GAME_FISH_TESTS)):
        for index, (crate, exact) in enumerate(tests, 1):
            artifact = {DEBRIS_TESTS[0][1]: 'nature', DEBRIS_TESTS[1][1]: 'pebble',
                        DEBRIS_TESTS[6][1]: 'scanned-depth'}.get(exact)
            groups.append((f'{family}-{index:02d}', crate, 1,
                           [exact, '--ignored', '--exact', '--color', 'never'], family, artifact))
    require(len(DEBRIS_TESTS) == 16 and len(GAME_FISH_TESTS) == 5
            and len(set(DEBRIS_TESTS + GAME_FISH_TESTS)) == 21, 'Private exact selector inventory changed')
    require(len({group[0] for group in groups}) == len(groups)
            and sum(group[2] for group in groups) == EXPECTED_TOTAL, 'Private replay group inventory changed')
    return groups


def verify_private_inputs(receipts, validation):
    for receipt in receipts:
        proof = Path(receipt['proof'])
        validation.physical_path(proof, proof.parent)
        require(sha(proof) == receipt['proof_sha256'], f'Private acquisition proof changed: {proof}')
        for row in receipt['pins']:
            path = Path(row['copy'])
            input_path(path, proof.parent, validation)
            require(path.is_file() and path.stat().st_size == row['bytes'] and sha(path) == row['sha256'],
                    f'Private input changed: {path}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    evidence = args.evidence.resolve()
    groups = replay_groups()
    output = evidence / "private-replays-current.json"
    owned = [output, evidence / 'private-inputs-current.json']
    owned.extend(evidence / f'private-{group[0]}.log' for group in groups)
    artifacts = {
        'nature': evidence / 'private-debris-nature-prepared/nature.wgsl',
        'pebble': evidence / 'private-debris-pebble-prepared/pebbles.wgsl',
        'scanned-depth': evidence / 'private-debris-scanned-depth.wgsl',
    }
    owned.extend([artifacts['nature'].parent, artifacts['pebble'].parent, artifacts['scanned-depth']])
    for path in owned:
        if path.exists():
            parser.error(f"Previous evidence preserved: {path}")
    spec = importlib.util.spec_from_file_location("gate3_validation", SOURCE / "scripts/Validate-Gate3WorkingTree.py")
    validation = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validation)
    validation.physical_path(evidence, evidence)
    current_sources = validation.source_inputs()
    manifest, native = validation.completed_receipt(evidence, 'native', current_sources)
    manifest_path = evidence / 'native-source-manifest.json'
    native_path = evidence / 'native-result.json'
    native_sha, manifest_sha = sha(native_path), sha(manifest_path)
    target_dir = Path(manifest['target_dir']).resolve()
    test_row = next(row for row in native['stages'] if row['stage'] == 'tests')
    test_log = evidence / 'tests.log'
    log_sha = sha(test_log)
    pins = test_row.get('test_binaries', [])
    pin_map = {pin['path']: pin for pin in pins}
    paths = {str(path) for path in validation.executed_paths(test_log.read_text(encoding='utf-8', errors='replace'), target_dir)}
    require(len(pins) == len(pin_map) and set(pin_map) == paths,
            'Native test receipt does not close the binaries executed in tests.log')
    binaries = {}
    for crate in ('forge_assets', 'forge_engine'):
        matches = [pin for pin in pins if re.fullmatch(crate + r'-[0-9a-f]+(?:\.exe)?', Path(pin['path']).name)]
        require(len(matches) == 1, f'Native suite must pin exactly one {crate} test binary')
        binaries[crate] = matches[0]

    def verify_native_inputs():
        require(sha(native_path) == native_sha and sha(manifest_path) == manifest_sha and sha(test_log) == log_sha,
                'Native receipt, source manifest or test log changed during replays')
        require(validation.source_inputs() == current_sources, 'Common sources changed since the native suite')
        for pin in binaries.values():
            validation.verify_binary(pin, target_dir)

    verify_native_inputs()
    receipts = []
    environments = {}
    for acquisition in ACQUISITIONS:
        receipt, child_env = load_acquisition(*acquisition, validation)
        receipts.append(receipt)
        for value in child_env.values():
            path = Path(value)
            input_path(path, Path(receipt['proof']).parent, validation)
            require(path.exists() and (any(Path(row['copy']).is_relative_to(path) for row in receipt['pins'])),
                    f'Private environment does not point to authenticated inputs: {path}')
        environments[receipt['name']] = {**os.environ, **child_env}
    debris = receipts[1]
    package = Path(environments['debris']['FORGE_DEBRIS_SCANNED_FIXTURE'])
    scanned_rows = [row for row in debris['pins'] if Path(row['copy']).is_relative_to(package)]
    require(len(scanned_rows) == 83, 'The existing scanned replay must retain its 83 acquired inputs')
    receipts.append({**debris, 'name': 'scanned', 'files': len(scanned_rows), 'pins': scanned_rows})
    environments['scanned'] = {**os.environ, 'FORGE_DEBRIS_SCANNED_FIXTURE': str(package)}
    environments['debris'].update({
        'FORGE_DEBRIS_NATURE_PREPARED': str(artifacts['nature'].parent),
        'FORGE_DEBRIS_PEBBLE_PREPARED': str(artifacts['pebble'].parent),
        'FORGE_DEBRIS_SCANNED_DEPTH_SHADER_OUTPUT': str(artifacts['scanned-depth']),
    })
    environments['game-fish'].update({
        'FORGE_TIDEWATER_GAME_FISH_PACKAGE': environments['game-fish']['FORGE_TIDEWATER_GAME_FISH_ROOT'],
        'FORGE_TIDEWATER_FISH_PROP_PACKAGE': environments['game-fish']['FORGE_TIDEWATER_FISH_PROP_ROOT'],
    })
    verify_private_inputs(receipts, validation)
    private_manifest = evidence / 'private-inputs-current.json'
    private_manifest.write_text(json.dumps(receipts, indent=2), encoding="utf-8")
    private_manifest_sha = sha(private_manifest)
    results = []
    for name, crate, expected, argv, family, artifact in groups:
        log = evidence / f"private-{name}.log"
        selected_receipts = [receipt for receipt in receipts if receipt['name'] == family]
        require(len(selected_receipts) == 1, 'Private group must have exactly one authenticated acquisition')
        require(sha(private_manifest) == private_manifest_sha, 'Private replay input receipt changed')
        verify_native_inputs()
        verify_private_inputs(selected_receipts, validation)
        command = [binaries[crate]['path'], *argv]
        with log.open("xb") as stream:
            result = subprocess.run(command, cwd=SOURCE, env=environments[family], stdout=stream, stderr=subprocess.STDOUT)
        content = log.read_text(encoding='utf-8', errors='replace')
        passed, failed, executed = 0, 0, []
        status = 'PASS'
        error = None
        cpu_artifacts = []
        try:
            passed, failed, executed = validate_test_output(content, result.returncode, expected, argv)
            require(sha(private_manifest) == private_manifest_sha, 'Private replay input receipt changed')
            verify_native_inputs()
            verify_private_inputs(selected_receipts, validation)
            if artifact:
                path = artifacts[artifact]
                validation.physical_path(path, evidence)
                require(path.is_file() and path.stat().st_size > 0, f'Missing fresh CPU shader artifact: {path}')
                cpu_artifacts.append({'path': str(path), 'sha256': sha(path), 'bytes': path.stat().st_size})
        except Exception as failure:
            status, error = 'FAIL', str(failure)
        results.append({"group": name, "status": status, "passed": passed, "expected": expected,
                        "failed": failed, "exit_code": result.returncode, "command": command,
                         "binary_sha256": binaries[crate]['sha256'], "log_sha256": sha(log),
                         'executed_tests': executed, 'acquisition': family,
                         'environment': {key: value for key, value in environments[family].items() if key.startswith('FORGE_')},
                         'cpu_artifacts': cpu_artifacts,
                         'inputs_unchanged': error is None, 'validation_error': error})
        complete = len(results) == len(groups) and sum(row['passed'] for row in results) == EXPECTED_TOTAL
        if complete and status == 'PASS':
            try:
                verify_private_inputs(receipts, validation)
                verify_native_inputs()
                require(sha(private_manifest) == private_manifest_sha, 'Private replay input receipt changed')
                for row in results:
                    require(sha(evidence / f"private-{row['group']}.log") == row['log_sha256'], 'Private test log changed')
                    for pin in row['cpu_artifacts']:
                        path = Path(pin['path'])
                        validation.physical_path(path, evidence)
                        require(path.stat().st_size == pin['bytes'] and sha(path) == pin['sha256'], 'CPU shader artifact changed')
            except Exception as failure:
                status, error = 'FAIL', str(failure)
                results[-1].update(status=status, inputs_unchanged=False, validation_error=error)
        output.write_text(json.dumps({'schema': 'forge.gate3.private-replays.v1',
                                     'status': 'FAIL' if status == 'FAIL' else 'PASS' if complete else 'IN_PROGRESS',
                                     "source": str(SOURCE), "source_manifest_sha256": manifest_sha,
                                     'native_result_sha256': native_sha, 'native_test_log_sha256': log_sha,
                                     'driver_sha256': sha(Path(__file__)),
                                     'private_input_receipt_sha256': private_manifest_sha,
                                     "source_unchanged": error is None, "groups": results,
                                     'expected_total': EXPECTED_TOTAL, 'passed_total': sum(row['passed'] for row in results),
                                     "new_gpu_execution": False, 'gate_qualified': False}, indent=2), encoding="utf-8")
        print(f"{status} private-{name}: {passed}/{expected}", flush=True)
        require(status == 'PASS', error or log.read_text(encoding="utf-8", errors="replace")[-6500:])
    require(sum(row['passed'] for row in results) == EXPECTED_TOTAL, 'Private replay total must be exactly 34')


if __name__ == "__main__":
    main()
