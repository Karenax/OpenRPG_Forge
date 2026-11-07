"""Capture HDRP's actual per-camera preview probe and reflection descriptor."""
from pathlib import Path
import subprocess,sys
S=Path(__file__).parent;target=S/'queue_townsmith_render_context_v4.py'
assert not target.exists()
text=(S/'queue_townsmith_render_context_v3.py').read_text(encoding='utf-8')
text=text.replace('townsmith-door-source-render-context-v3','townsmith-door-source-render-context-v4')
text=text.replace('SourceTownsmithRenderContextV3','SourceTownsmithRenderContextV4')
text=text.replace('public float[] ambient_probe;', 'public float[] ambient_probe,render_settings_ambient_probe; public string actual_sky_reflection; public bool actual_hdrp_probe_captured;')
old='var ambientCoefficients=new float[27];var probe=RenderSettings.ambientProbe;'
new='''var ambientCoefficients=new float[27];var renderSettingsCoefficients=new float[27];var declaredProbe=RenderSettings.ambientProbe;
    for(int ch=0;ch<3;ch++)for(int k=0;k<9;k++)renderSettingsCoefficients[ch*9+k]=declaredProbe[ch,k];
    var instanceFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    var skyManagerField=typeof(HDRenderPipeline).GetField("m_SkyManager",instanceFlags);if(skyManagerField==null)throw new Exception("Pinned HDRP sky manager absent");
    var manager=skyManagerField.GetValue(HDRenderPipeline.currentPipeline);
    var probeMethod=manager.GetType().GetMethod("GetAmbientProbe",instanceFlags,null,new[]{typeof(HDCamera)},null);if(probeMethod==null)throw new Exception("Pinned HDRP camera probe method absent");
    var probe=(SphericalHarmonicsL2)probeMethod.Invoke(manager,new object[]{sourceHdCamera});
    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",instanceFlags,null,new[]{typeof(HDCamera)},null);if(reflectionMethod==null)throw new Exception("Pinned HDRP reflection method absent");
    var skyReflection=reflectionMethod.Invoke(manager,new object[]{sourceHdCamera}) as Texture;
    var reflectionDescription=skyReflection==null?"null":skyReflection.name+";dimension="+skyReflection.dimension+";width="+skyReflection.width+";height="+skyReflection.height;
    if(skyReflection is RenderTexture reflectionTarget)reflectionDescription+=";volumeDepth="+reflectionTarget.volumeDepth+";format="+reflectionTarget.graphicsFormat+";mips="+reflectionTarget.mipmapCount;
'''
assert text.count(old)==1;text=text.replace(old,new)
text=text.replace('id=item.id,ambient_probe=ambientCoefficients,', 'id=item.id,ambient_probe=ambientCoefficients,render_settings_ambient_probe=renderSettingsCoefficients,actual_hdrp_probe_captured=true,actual_sky_reflection=reflectionDescription,')
compile(text,str(target),'exec');target.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
