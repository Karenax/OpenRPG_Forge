"""Retain V1 refusal; queue a new probe after existing shared-cache Cargo jobs."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
source = S / 'queue_townsmith_bevy_pipeline_v1.py'
target = S / 'queue_townsmith_bevy_pipeline_v2.py'
assert not target.exists()
text = source.read_text(encoding='utf-8')
text = text.replace("ROOT = W / 'townsmith-s-paint-bevy-pipeline-v1'", "ROOT = W / 'townsmith-s-paint-bevy-pipeline-v2'")
old = "        assert subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'"
new = '''        while subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue).Count'], text=True).strip() != '0':
            state.update(status='actual_bevy_pipeline_waiting_for_existing_cargo', shared_cache_not_modified=True)
            save('RUN-RESULT.v1.json', state)
            time.sleep(10)
        for path, expected in prep['input_pins'].items():
            assert sha(path) == expected, path'''
assert text.count(old) == 1
text = text.replace(old, new)
text = text.replace("error=repr(error))", "error=repr(error), traceback=__import__('traceback').format_exc())")
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
