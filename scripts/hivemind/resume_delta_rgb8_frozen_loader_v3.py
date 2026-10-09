"""Retain the resolver refusal and pin Hermit to the actual application lock."""
from pathlib import Path
import hashlib

BASE = Path(__file__).with_name('resume_delta_rgb8_frozen_loader_v2.py')
script = BASE.read_text('utf-8-sig')
script = script.replace("'delta-rgb8-frozen-canonical-loader-v2'", "'delta-rgb8-frozen-canonical-loader-v3'")
needle = '    birth = native.identity(28340)'
assert script.count(needle) == 1
script = script.replace(needle, '''    prior_terminal = native.read(native.HOST / 'RUN-RESULT.v2.json')
    if prior_terminal.get('finished_utc'):
        assert prior_terminal['actual_exit_code'] == 0
        assert prior_terminal['status'] == 'refused_or_unverified' and prior_terminal['error'].startswith('Native input changed:')
        assert 'test result: ok. 17 passed; 0 failed;' in (native.HOST / 'native.stdout.log').read_text('utf-8-sig')
        birth = '0'  # Terminal job; do not wait for an unrelated reused process ID.
    else:
        birth = native.identity(28340)''')
needle = "    assert result.returncode == 0, 'Moved harness dependency resolution refused; preserve log'"
assert script.count(needle) == 1
script = script.replace(needle, needle + '''
    root_lock = tomllib.loads((SNAPSHOT / 'Cargo.lock').read_text('utf-8-sig'))
    hermit = [p for p in root_lock['package'] if p['name'] == 'hermit-abi']
    assert len(hermit) == 1
    with (ROOT / 'hermit-lock-repair.log').open('xb') as log:
        code = subprocess.run([str(native.BIN / 'cargo.exe'), 'update', '-p', 'hermit-abi',
            '--precise', hermit[0]['version'], '--offline'], cwd=ROOT, env=env,
            stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
    assert code == 0, 'Exact original Hermit pin unavailable; retain repair log'
''')
needle = "    prior_pins = {str(p): native.sha(p) for p in PRIOR.rglob('*') if p.is_file()}"
assert script.count(needle) == 1
script = script.replace(needle, needle + '''
    previous_resolver = PRIOR.parent / 'delta-rgb8-frozen-canonical-loader-v2'
    old_lock = tomllib.loads((previous_resolver / 'Cargo.lock').read_text('utf-8-sig'))
    assert any(p['name']=='hermit-abi' and p['version']=='0.5.3' for p in old_lock['package'])
    assert not (previous_resolver / 'JOB.v1.json').exists()
    for path in [HERE / 'resume_delta_rgb8_frozen_loader_v2.py', previous_resolver / 'Cargo.lock', previous_resolver / 'lock.log']:
        prior_pins[str(path)] = native.sha(path)
''')
needle = "        repair='harness moved outside snapshot workspace; all copied source bytes retained',"
assert script.count(needle) == 1
script = script.replace(needle, "        repair='external harness plus exact original Hermit version; all copied source bytes retained',")
namespace = {'__name__': '__main__', '__file__': str(Path(__file__).resolve()), 'BASE': BASE}
exec(compile(script, str(BASE), 'exec'), namespace)
