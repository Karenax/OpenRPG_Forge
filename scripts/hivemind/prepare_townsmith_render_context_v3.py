"""Isolate HDRP background; keep the full original reference and capture sky/SH."""
from pathlib import Path
import subprocess, sys
S=Path(__file__).parent
target=S/'queue_townsmith_render_context_v3.py'
assert not target.exists()
text=(S/'queue_townsmith_render_context_v2.py').read_text(encoding='utf-8')
text=text.replace('townsmith-door-source-render-context-v2','townsmith-door-source-render-context-v3')
text=text.replace('SourceTownsmithRenderContextV2','SourceTownsmithRenderContextV3')
text=text.replace("text=text.replace('request.cases.Length!=15','request.cases.Length!=2')", "text=text.replace('request.cases.Length!=15','request.cases.Length!=3')")
text=text.replace('Two fixed neutral/raw-linear reference controls required','Three original and background controls required')
text=text.replace('public bool source_prefab_transform_context=false', 'public string hdrp_clear_color_mode; public int active_sky_type,target_samples; public float[] ambient_probe;\n  public bool source_prefab_transform_context=false')
anchor="    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor)==1
instrument='''    camera_anchor='camera.gameObject.AddComponent<HDAdditionalCameraData>();'
    assert text.count(camera_anchor)==1
    text=text.replace(camera_anchor,camera_anchor+'if(item.id.Contains("fixedbackground")){var extra=camera.GetComponent<HDAdditionalCameraData>();extra.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;extra.backgroundColorHDR=new Color(.025f,.025f,.025f,1);}')
    context_anchor='    File.WriteAllText(Path.Combine(root,item.id+".context.json"),'
    assert text.count(context_anchor)==1
    extra_context=\'''    var sourceHdCamera=HDCamera.GetOrCreate(camera);
    var activeVisual=sourceHdCamera.volumeStack.GetComponent<VisualEnvironment>();
    var activeHdri=sourceHdCamera.volumeStack.GetComponent<HDRISky>();
    var activeGradient=sourceHdCamera.volumeStack.GetComponent<GradientSky>();
    var ambientCoefficients=new float[27];var probe=RenderSettings.ambientProbe;
    for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)ambientCoefficients[channel*9+coefficient]=probe[channel,coefficient];
    File.WriteAllText(Path.Combine(root,item.id+".hdcamera.json"),EditorJsonUtility.ToJson(camera.GetComponent<HDAdditionalCameraData>(),true));
    File.WriteAllText(Path.Combine(root,item.id+".active-visual.json"),EditorJsonUtility.ToJson(activeVisual,true));
    File.WriteAllText(Path.Combine(root,item.id+".active-hdri.json"),EditorJsonUtility.ToJson(activeHdri,true));
    File.WriteAllText(Path.Combine(root,item.id+".active-gradient.json"),EditorJsonUtility.ToJson(activeGradient,true));
\'''
    text=text.replace(context_anchor,extra_context+context_anchor)
    text=text.replace('id=item.id,bounds_center=', 'id=item.id,ambient_probe=ambientCoefficients,active_sky_type=activeVisual.skyType.value,target_samples=target.antiAliasing,hdrp_clear_color_mode=camera.GetComponent<HDAdditionalCameraData>().clearColorMode.ToString(),bounds_center=')
'''
text=text.replace(anchor,instrument+anchor)
text=text.replace("cases=[dict(base,id='door-neutral'),dict(base,id='door-linear')]", "cases=[dict(base,id='door-neutral'),dict(base,id='door-linear'),dict(base,id='door-fixedbackground-linear')]")
text=text.replace('expected_cases=2','expected_cases=3').replace("len(native['rows'])==2", "len(native['rows'])==3").replace('cases_captured=2','cases_captured=3')
compile(text,str(target),'exec')
target.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
