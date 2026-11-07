"""Complete native lighting witnesses with the original material emission."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_emission_control_v13.py'
assert not target.exists()
text = (S / 'queue_townsmith_lighting_controls_v12.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-lighting-controls-v12', 'townsmith-door-source-emission-control-v13')
text = text.replace('SourceTownsmithLightingControlsV12', 'SourceTownsmithEmissionControlV13')
text = text.replace('SourceTownsmithFgdReadbackV12', 'SourceTownsmithFgdReadbackV13')
text = text.replace('request.cases.Length!=9', 'request.cases.Length!=10')
text = text.replace('expected_cases=9', 'expected_cases=10').replace("len(native['rows'])==9", "len(native['rows'])==10").replace('cases_captured=9', 'cases_captured=10')
anchor = "'IndirectDiffuseOcclusion','IndirectSpecularOcclusion']"
assert text.count(anchor) == 1
text = text.replace(anchor, "'IndirectDiffuseOcclusion','IndirectSpecularOcclusion','EmissiveLighting']")
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
