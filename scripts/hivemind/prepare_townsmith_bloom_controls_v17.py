"""Isolate native bloom while keeping post-processing and exposure enabled."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_bloom_controls_v17.py'
assert not target.exists()
text = (S / 'queue_townsmith_postprocess_controls_v16.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-postprocess-controls-v16', 'townsmith-door-source-bloom-controls-v17')
text = text.replace('SourceTownsmithPostprocessControlsV16', 'SourceTownsmithBloomControlsV17')
text = text.replace('SourceTownsmithFgdReadbackV16', 'SourceTownsmithFgdReadbackV17')
text = text.replace('request.cases.Length!=14', 'request.cases.Length!=17')
text = text.replace('expected_cases=14', 'expected_cases=17').replace("len(native['rows'])==14", "len(native['rows'])==17").replace('cases_captured=14', 'cases_captured=17')
text = text.replace('.Replace("-nopost", "").Replace("-linear", "")', '.Replace("-nopost", "").Replace("-nobloom", "").Replace("-linear", "")')
anchor = "    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))"
assert text.count(anchor) == 1
text = text.replace(anchor, "    cases += [dict(base,id=name) for name in ['door-nobloom-linear','door-emission-dark-nobloom-linear','door-debug-EmissiveLighting-nobloom-linear']]\n" + anchor)
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
source_anchor = '    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);'
insertion = '''    if(item.id.Contains("nobloom")){
      var sourceBloom=profile.Add<Bloom>();sourceBloom.intensity.Override(0);
    }
'''
text = text.replace(anchor, '    assert text.count(' + repr(source_anchor) + ')==1\n    text=text.replace(' + repr(source_anchor) + ',' + repr(insertion + source_anchor) + ')\n' + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
