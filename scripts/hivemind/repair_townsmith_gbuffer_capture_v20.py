"""Preserve the compile refusal and replace only the obsolete diagnostic ID."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
prior = W / 'townsmith-door-source-gbuffer-capture-v19'
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
state = read(prior / 'RUN-RESULT.v1.json')
assert state['actual_exit_code'] == 1 and state['status'] == 'source_render_context_refusal_retained'
assert 'CS0619' in (prior / 'Unity.log').read_text(encoding='utf-8-sig')
helper = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/Editor/SourceTownsmithGBufferCaptureV19.cs'
prepared = read(prior / 'PREPARED.v1.json')
with helper.open('rb') as stream:
    checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
assert prepared['input_pins'][str(helper)] == checksum
saved = prior / 'failed-helper.cs.saved'
assert not saved.exists()
helper.rename(saved)
(prior / 'HELPER-EPOCH.v1.json').write_text(json.dumps(dict(original_path=str(helper), saved_path=str(saved), sha256=checksum,
    reason='Preserve obsolete GetInstanceID compile refusal outside compiling Assets before the corrected helper.',
    saved_utc=datetime.now(timezone.utc).isoformat()), indent=2), encoding='utf-8')
target = S / 'queue_townsmith_gbuffer_capture_v20.py'
assert not target.exists()
text = (S / 'queue_townsmith_gbuffer_capture_v19.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-gbuffer-capture-v19', 'townsmith-door-source-gbuffer-capture-v20')
text = text.replace('SourceTownsmithGBufferCaptureV19', 'SourceTownsmithGBufferCaptureV20')
text = text.replace('SourceTownsmithFgdReadbackV19', 'SourceTownsmithFgdReadbackV20')
assert text.count('record.instance_id=actualTexture.GetInstanceID();') == 1
text = text.replace('public int width,height,instance_id,float_count;', 'public int width,height,float_count;public string entity_id;')
text = text.replace('record.instance_id=actualTexture.GetInstanceID();', 'record.entity_id=actualTexture.GetEntityId().ToString();')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
