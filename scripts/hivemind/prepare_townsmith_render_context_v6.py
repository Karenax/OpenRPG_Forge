"""Read the existing CustomUserHistory0 camera actually used by StandardRequest."""
from pathlib import Path
import subprocess,sys
S=Path(__file__).parent;target=S/'queue_townsmith_render_context_v6.py'
assert not target.exists()
text=(S/'queue_townsmith_render_context_v5.py').read_text(encoding='utf-8')
text=text.replace('townsmith-door-source-render-context-v5','townsmith-door-source-render-context-v6')
text=text.replace('SourceTownsmithRenderContextV5','SourceTownsmithRenderContextV6')
old='var sourceHdCamera=HDCamera.GetOrCreate(camera);'
new='''var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",BindingFlags.Public|BindingFlags.NonPublic);if(historyType==null)throw new Exception("Pinned HDRP request history type absent");
    var history=Enum.Parse(historyType,"CustomUserHistory0");
    var signature=new[]{typeof(Camera),typeof(int),historyType};
    var existsMethod=typeof(HDCamera).GetMethod("IsHistoryChannelExisting",flags,null,signature,null);if(existsMethod==null||!(bool)existsMethod.Invoke(null,new object[]{camera,0,history}))throw new Exception("Actual request camera history absent; do not create a substitute");
    var historyMethod=typeof(HDCamera).GetMethod("GetOrCreate",flags,null,signature,null);if(historyMethod==null)throw new Exception("Pinned HDRP request camera method absent");
    var sourceHdCamera=(HDCamera)historyMethod.Invoke(null,new object[]{camera,0,history});
'''
assert text.count(old)==1;text=text.replace(old,new)
text=text.replace('public bool actual_hdrp_probe_captured;', 'public bool actual_hdrp_probe_captured; public string actual_camera_history;')
text=text.replace('actual_hdrp_probe_captured=true,actual_sky_reflection=', 'actual_hdrp_probe_captured=true,actual_camera_history="CustomUserHistory0-existing",actual_sky_reflection=')
compile(text,str(target),'exec');target.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
