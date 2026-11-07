"""Capture original native lighting components with post-processing disabled."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_direct_controls_v18.py'
assert not target.exists()
text = (S / 'queue_townsmith_bloom_controls_v17.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-bloom-controls-v17', 'townsmith-door-source-direct-controls-v18')
text = text.replace('SourceTownsmithBloomControlsV17', 'SourceTownsmithDirectControlsV18')
text = text.replace('SourceTownsmithFgdReadbackV17', 'SourceTownsmithFgdReadbackV18')
text = text.replace('request.cases.Length!=17', 'request.cases.Length!=21')
text = text.replace('expected_cases=17', 'expected_cases=21')
text = text.replace("len(native['rows'])==17", "len(native['rows'])==21")
text = text.replace('cases_captured=17', 'cases_captured=21')
anchor = "    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))"
assert text.count(anchor) == 1
text = text.replace(anchor, "    cases += [dict(base,id='door-debug-'+mode+'-nopost-linear') for mode in ['DirectDiffuseLighting','DirectSpecularLighting','IndirectDiffuseLighting','ReflectionLighting']]\n" + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
