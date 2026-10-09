"""Retry only the pre-export UPM startup failure; retain its logs and source pins."""
from pathlib import Path
import json
import hashlib

BASE = Path(__file__).with_name('queue_delta_remaining_native_materials_v1.py')
OLD = Path('F:/OpenRPG_ImportWork/unity-delta-remaining-native-materials-v1')
run = json.loads((OLD / 'RUN-RESULT.v1.json').read_text('utf-8-sig'))
assert run['actual_unity_exit_code'] == 1 and run['native_materials_verified'] == 0 and run.get('finished_utc')
assert not (OLD / 'NATIVE-RESULT.v1.json').exists()
log = (OLD / 'unity.log').read_text('utf-8-sig', errors='replace')
assert 'Could not establish a connection with the Unity Package Manager local server process' in log
script = BASE.read_text('utf-8-sig')
assert script.count("ROOT = common.W / 'unity-delta-remaining-native-materials-v1'") == 1
script = script.replace("ROOT = common.W / 'unity-delta-remaining-native-materials-v1'",
                        "ROOT = common.W / 'unity-delta-remaining-native-materials-v2'")
needle = '    unity_idle()\n    source_path'
assert script.count(needle) == 1
script = script.replace(needle, '''    unity_idle()
    common.check(common.read(OLD / 'PREPARED.v1.json')['input_pins'])
    source_path''')
needle = "    common.save('PREPARED.v1.json', dict(project=str(project), input_pins=pins, copies=copies,"
assert script.count(needle) == 1
script = script.replace(needle, '''    for path in [BASE, OLD / 'RUN-RESULT.v1.json', OLD / 'manifest.v1.json', OLD / 'unity.log']:
        pins[str(path)] = common.sha(path)
    common.save('PREVIOUS-STARTUP-REFUSAL.v1.json', dict(actual_previous_unity_exit_code=1,
         previous_receipt_sha256=common.sha(OLD / 'RUN-RESULT.v1.json'),
         previous_log_sha256=common.sha(OLD / 'unity.log'), native_export_previously_executed=False,
         reason='Unity Package Manager local IPC startup connection failed',
         previous_sources_and_logs_preserved=True, full_import=False))
''' + needle)
namespace = {'__name__': '__main__', '__file__': str(Path(__file__).resolve()), 'BASE': BASE, 'OLD': OLD}
exec(compile(script, str(BASE), 'exec'), namespace)
