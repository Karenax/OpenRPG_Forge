"""Measure source scene AO while preserving original neutral/no-post references."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
out=S/'queue_townsmith_source_ao_controls_v40.py'
assert not out.exists()
q=(S/'queue_townsmith_inpass_normal_v31.py').read_text(encoding='utf-8')
q=q.replace('v31','v40').replace('V31','V40')
q=q.replace('townsmith-door-source-inpass-normal-v40','townsmith-door-source-ao-controls-v40')
a="['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']"
assert q.count(a)==1
q=q.replace(a,"['door-neutral','door-nopost-linear','door-noao-nopost-linear']")
# The original no-anisotropy condition is left untouched: all new cases retain
# source ForceEnable sampling. Only the owned volume in the AO control changes.
a="    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert q.count(a)==1
injection='''
    anchor='target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);'
    assert text.count(anchor)==1
    text=text.replace(anchor,'if(item.id.StartsWith("door-noao")){var ownedAo=profile.Add<ScreenSpaceAmbientOcclusion>();ownedAo.intensity.Override(0);}'+'\\n    '+anchor)
    anchor='if(activeOcclusion==null||activeIndirect==null)'
    assert text.count(anchor)==1
    text=text.replace(anchor,'float expectedAoIntensity=item.id.StartsWith("door-noao")?0.0f:0.5f; if(activeOcclusion!=null && activeOcclusion.intensity.value!=expectedAoIntensity)throw new Exception("Source AO control intensity differs");\\n    '+anchor)
'''
q=q.replace(a,injection+'\n'+a)
compile(q,str(out),'exec')
out.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(out)],check=True)
