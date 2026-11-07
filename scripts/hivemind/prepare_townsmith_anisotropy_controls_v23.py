"""Disable only scene anisotropy in a source control, restoring prior state."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_anisotropy_controls_v23.py'
assert not target.exists()
text = (S / 'queue_townsmith_depth_capture_v22.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-depth-capture-v22', 'townsmith-door-source-anisotropy-controls-v23')
text = text.replace('SourceTownsmithDepthCaptureV22', 'SourceTownsmithAnisotropyControlsV23')
text = text.replace('SourceTownsmithFgdReadbackV22', 'SourceTownsmithFgdReadbackV23')
text = text.replace('SourceOwnedDepthCaptureV22', 'SourceOwnedDepthCaptureV23')
text = text.replace('request.cases.Length!=5', 'request.cases.Length!=3')
text = text.replace('expected_cases=5', 'expected_cases=3').replace("len(native['rows'])==5", "len(native['rows'])==3").replace('cases_captured=5', 'cases_captured=3')
anchor = "    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))"
assert text.count(anchor) == 1
text = text.replace(anchor, "    cases=[dict(base,id=name) for name in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']]\n" + anchor)
patch = '''
    text=text.replace('var scene=EditorSceneManager.NewPreviewScene();', 'var sourcePriorAniso=QualitySettings.anisotropicFiltering;var scene=EditorSceneManager.NewPreviewScene();')
    text=text.replace('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);', '    if(item.id.Contains("noaniso"))QualitySettings.anisotropicFiltering=AnisotropicFiltering.Disable;\\n    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')
    text=text.replace('}finally{if(sourceDepthPass!=null)', '}finally{QualitySettings.anisotropicFiltering=sourcePriorAniso;if(sourceDepthPass!=null)')
'''
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
text = text.replace(anchor, patch + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
