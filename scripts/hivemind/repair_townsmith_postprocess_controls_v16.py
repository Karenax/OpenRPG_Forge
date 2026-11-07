"""Preserve failed owned helper and retry with HDRP's actual available components."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
OLD = W / 'townsmith-door-source-postprocess-controls-v15'
PROJECT = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
helper = PROJECT / 'Assets/Editor/SourceTownsmithPostprocessControlsV15.cs'
snapshot = OLD / 'failed-helper.cs.saved'
target = S / 'queue_townsmith_postprocess_controls_v16.py'
assert not target.exists() and not snapshot.exists()
state = json.loads((OLD / 'RUN-RESULT.v1.json').read_text(encoding='utf-8-sig'))
assert state['actual_exit_code'] == 1
assert 'CS0246' in (OLD / 'Unity.log').read_text(encoding='utf-8-sig')
assert subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process Unity -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'
assert helper.resolve().parent == (PROJECT / 'Assets/Editor').resolve()
assert snapshot.resolve().parent == OLD.resolve()
pins = json.loads((OLD / 'PREPARED.v1.json').read_text(encoding='utf-8-sig'))['input_pins']
with helper.open('rb') as stream:
    expected = hashlib.file_digest(stream, 'sha256').hexdigest()
assert expected == pins[str(helper)]
helper.rename(snapshot)
with snapshot.open('rb') as stream:
    assert hashlib.file_digest(stream, 'sha256').hexdigest() == expected
(OLD / 'HELPER-EPOCH.v1.json').write_text(json.dumps(dict(original_path=str(helper), sha256=expected, saved_path=str(snapshot), reason='Owned failed helper used an unavailable ColorLookup component; snapshot retained outside compiling Assets.'), indent=2), encoding='utf-8')
text = (S / 'queue_townsmith_postprocess_controls_v15.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-postprocess-controls-v15', 'townsmith-door-source-postprocess-controls-v16')
text = text.replace('SourceTownsmithPostprocessControlsV15', 'SourceTownsmithPostprocessControlsV16')
text = text.replace('SourceTownsmithFgdReadbackV15', 'SourceTownsmithFgdReadbackV16')
assert text.count('typeof(ColorLookup),') == 1
text = text.replace('typeof(ColorLookup),', '')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
