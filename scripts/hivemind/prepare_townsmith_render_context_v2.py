"""Retain runtime sky metadata when Unity cannot supply a purchased asset GUID."""
from pathlib import Path
import subprocess, sys
S=Path(__file__).parent
target=S/'queue_townsmith_render_context_v2.py'
assert not target.exists()
text=(S/'queue_townsmith_render_context_v1.py').read_text(encoding='utf-8')
text=text.replace('townsmith-door-source-render-context-v1','townsmith-door-source-render-context-v2')
text=text.replace('SourceTownsmithRenderContextV1','SourceTownsmithRenderContextV2')
old='skybox_identity=RenderSettings.skybox==null?"null":Identity(RenderSettings.skybox),'
new='skybox_identity=RenderSettings.skybox==null?"null":"runtime-object:"+RenderSettings.skybox.name+";shader="+RenderSettings.skybox.shader.name,'
assert text.count(old)==1
text=text.replace(old,new)
old='tonemapping=item.id.EndsWith("-linear")?"None":"Neutral"},true));'
new=old+'\n    if(RenderSettings.skybox!=null)File.WriteAllText(Path.Combine(root,item.id+".skybox-runtime.json"),EditorJsonUtility.ToJson(RenderSettings.skybox,true));'
assert text.count(old)==1
text=text.replace(old,new)
target.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
