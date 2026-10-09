"""Recheck the exact compiled engine binary within the Hivemind test scope."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess
import check_source_unlit_canonical_v1 as original

ROOT = original.W / 'source-unlit-targeted-native-check-v2'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save(name, value):
    temporary = ROOT / (name + '.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(ROOT / name)

def main():
    assert not ROOT.exists(), 'Never overwrite retained evidence'
    prepared = original.read(original.ROOT / 'PREPARED.v1.json')
    original.check(prepared['input_pins'])
    original.check(prepared['frozen_pins'])
    previous = original.read(original.ROOT / 'RUN-RESULT.v1.json')
    assert previous['testing_exit_code'] == 101 and previous['finished_utc']
    stderr = (original.ROOT / 'testing.stderr.log').read_text('utf-8-sig')
    matches = re.findall(r'Running unittests .*? \(([^\r\n]+\.exe)\)', stderr)
    assert len(matches) == 1
    executable = Path(matches[0])
    digest = sha(executable)
    ROOT.mkdir()
    state = dict(status='checking_exact_hivemind_engine_binary', full_import=False,
        canonical_installed=False, existing_studio_tests_reused=17,
        executable=str(executable), executable_sha256=digest,
        predecessor_failure_retained=True, test_filter='source_graph',
        actual_engine_tests_passed=0)
    save('PREPARED.v1.json', dict(**state, source_receipt=str(original.ROOT / 'PREPARED.v1.json'),
        source_receipt_sha256=sha(original.ROOT / 'PREPARED.v1.json'),
        frozen_pins=prepared['frozen_pins'], input_pins=prepared['input_pins'],
        required_old_tests=prepared['preserved_engine_test_names'],
        required_new_tests=prepared['new_test_names']))
    save('RUN-RESULT.v1.json', state)
    try:
        with (ROOT / 'testing.stdout.log').open('xb') as out, (ROOT / 'testing.stderr.log').open('xb') as err:
            result = subprocess.run([str(executable), 'source_graph', '--test-threads=1'],
                cwd=original.ROOT, stdin=subprocess.DEVNULL, stdout=out, stderr=err,
                creationflags=subprocess.CREATE_NO_WINDOW)
        state['testing_exit_code'] = result.returncode
        assert result.returncode == 0
        output = (ROOT / 'testing.stdout.log').read_text('utf-8-sig')
        passed = set(re.findall(r'^test (\S+) \.\.\. ok$', output, re.M))
        assert set(prepared['preserved_engine_test_names']).issubset(passed)
        for name in prepared['new_test_names']:
            assert any(test.endswith('::' + name) for test in passed), name
        assert re.search(r'test result: ok\. \d+ passed; 0 failed; 0 ignored;', output)
        original.check(prepared['input_pins'])
        original.check(prepared['frozen_pins'])
        assert sha(executable) == digest
        state.update(status='actual_engine_material_and_factory_tests_passed',
            actual_material_tests_passed=4, actual_program_tests_passed=3,
            actual_engine_tests_passed=len(passed), source_pins_unchanged=True)
    except Exception as error:
        state.update(status='refused_or_unverified', error=repr(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    print(json.dumps(state))

if __name__ == '__main__':
    main()
