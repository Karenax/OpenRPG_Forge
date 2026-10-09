"""Move only the test harness outside the frozen workspace; reuse every copied source."""
from pathlib import Path
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tomllib

HERE = Path(__file__).resolve().parent
BASE = HERE / 'check_delta_rgb8_frozen_loader_v1.py'
spec = importlib.util.spec_from_file_location('frozen_loader', BASE)
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)
PRIOR = native.ROOT
ROOT = PRIOR.parent / 'delta-rgb8-frozen-canonical-loader-v2'
SNAPSHOT = PRIOR / 'snapshot'
native.ROOT = ROOT


def prepare():
    assert not ROOT.exists()
    refusal_log = PRIOR / 'lock.log'
    assert '`workspace.package.edition` was not defined' in refusal_log.read_text('utf-8-sig')
    assert not (PRIOR / 'JOB.v1.json').exists()
    prior_pins = {str(p): native.sha(p) for p in PRIOR.rglob('*') if p.is_file()}
    frozen_files = sorted(p for p in SNAPSHOT.rglob('*') if p.is_file())
    assert len(frozen_files) > 800
    birth = native.identity(28340)
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    for name in ['Cargo.toml', 'src/main.rs']:
        shutil.copyfile(PRIOR / name, ROOT / name)
    shutil.copyfile(SNAPSHOT / 'Cargo.lock', ROOT / 'Cargo.lock')
    env = os.environ.copy()
    env.update(TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
    with (ROOT / 'lock.log').open('xb') as log:
        result = subprocess.run([str(native.BIN / 'cargo.exe'), 'generate-lockfile', '--offline'],
            cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    assert result.returncode == 0, 'Moved harness dependency resolution refused; preserve log'
    before = tomllib.loads((SNAPSHOT / 'Cargo.lock').read_text('utf-8-sig'))
    actual = tomllib.loads((ROOT / 'Cargo.lock').read_text('utf-8-sig'))
    identities = {(p['name'], p['version'], p.get('source'), p.get('checksum')) for p in before['package']}
    added = [p for p in actual['package'] if p.get('source') and
             (p['name'], p['version'], p.get('source'), p.get('checksum')) not in identities]
    assert not added, 'Registry dependency differs from actual application lock: ' + repr(added)
    candidate = native.read(native.CANDIDATE / 'PREPARED.v1.json')
    transitions = []
    for path, row in candidate['candidate_files'].items():
        copied = SNAPSHOT / Path(path).relative_to(native.REPO)
        original = copied.with_suffix(copied.suffix + '.original')
        assert native.sha(copied) == row['candidate_sha256']
        assert native.sha(original) == row['original_sha256'] == native.sha(path)
        transitions.append(dict(original=path, frozen=str(copied), original_copy=str(original), **row))
    # Validate exact copied Studio code; only main.rs paths belong to the harness.
    host = native.read(native.HOST / 'PREPARED.v2.json')
    for file in (SNAPSHOT / 'apps/forge_studio/src/authoring_assets').glob('*.rs'):
        original = native.REPO / file.relative_to(SNAPSHOT)
        assert native.sha(file) == host['input_pins'][str(original)]
    native.check(prior_pins)
    pins = dict(prior_pins)
    for path in [BASE, Path(__file__).resolve(), ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs']:
        pins[str(path)] = native.sha(path)
    source_rows = []
    for file in frozen_files:
        if file.name.endswith('.original'):
            continue
        original = native.REPO / file.relative_to(SNAPSHOT)
        source_rows.append(dict(original=str(original), copied=str(file), copied_sha256=native.sha(file),
            currently_observed_original_sha256=native.sha(original) if original.is_file() else None,
            comparison_is_snapshot_provenance_not_live_source_lock=True))
    native.save('SNAPSHOT.v1.json', dict(original_sources=source_rows, frozen_input_pins=pins,
        candidate_transitions=transitions, partial_v1_preparation_preserved=prior_pins,
        preparation_error='nested test workspace intercepted inherited package edition',
        repair='harness moved outside snapshot workspace; all copied source bytes retained',
        local_rerecast_fork_preserved=True, registry_dependencies_match_actual_application_lock=True,
        original_sources_not_recopied=True, expected_host_tests=17,
        canonical_checkout_unchanged=True, full_import=False), new=True)
    native.save('PREPARED.v1.json', dict(input_pins=pins, wait_pid=28340, wait_birth_ticks=birth,
        target=host['target'], actual_source_files_copied=len(source_rows), expected_tests=17,
        canonical_checkout_unchanged=True, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    native.save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=__import__('datetime').datetime.now(
        __import__('datetime').timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, frozen_source_files=len(source_rows),
        previous_copies_reused=True, expected_tests=17, canonical_checkout_unchanged=True)))


if __name__ == '__main__':
    native.run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
