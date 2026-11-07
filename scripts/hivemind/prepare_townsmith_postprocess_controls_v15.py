"""Measure post-processing, exposure history and controls with post-processing disabled."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_postprocess_controls_v15.py'
assert not target.exists()
text = (S / 'queue_townsmith_dark_control_v14.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-dark-control-v14', 'townsmith-door-source-postprocess-controls-v15')
text = text.replace('SourceTownsmithDarkControlV14', 'SourceTownsmithPostprocessControlsV15')
text = text.replace('SourceTownsmithFgdReadbackV14', 'SourceTownsmithFgdReadbackV15')
text = text.replace('request.cases.Length!=11', 'request.cases.Length!=14')
text = text.replace('expected_cases=11', 'expected_cases=14').replace("len(native['rows'])==11", "len(native['rows'])==14").replace('cases_captured=11', 'cases_captured=14')
text = text.replace('item.id=="door-emission-dark-linear"', 'item.id.StartsWith("door-emission-dark")')
text = text.replace('item.id.Substring(11,item.id.Length-18)', 'item.id.Substring(11).Replace("-nopost", "").Replace("-linear", "")')
anchor = "    cases.append(dict(base,id='door-emission-dark-linear'))"
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + "\n    cases += [dict(base,id=name) for name in ['door-nopost-linear','door-emission-dark-nopost-linear','door-debug-EmissiveLighting-nopost-linear']]")
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public bool actual_postprocess_enabled,actual_color_grading_enabled,actual_exposure_control_enabled,exposure_uses_fetched,exposure_uses_current_camera; public string actual_color_buffer_format; public float exposure_fetched; public float[] exposure_texture_rgba; public bool[] exposure_texture_present;')
anchor = '    var activeGradient=sourceHdCamera.volumeStack.GetComponent<GradientSky>();'
assert text.count(anchor) == 1
addition = '''
    var exposureProperty=typeof(HDCamera).GetProperty("currentExposureTextures",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    if(exposureProperty==null)throw new Exception("Pinned HDRP exposure history property absent");
    var exposureState=exposureProperty.GetValue(sourceHdCamera);var exposureStateType=exposureState.GetType();
    var exposureValues=new float[12];var exposurePresent=new bool[3];var exposureNames=new[]{"parent","current","previous"};
    for(int i=0;i<3;i++){
      var exposureField=exposureStateType.GetField(exposureNames[i]);if(exposureField==null)throw new Exception("Pinned HDRP exposure texture field absent");
      var exposureHandle=exposureField.GetValue(exposureState) as RTHandle;
      if(exposureHandle==null||exposureHandle.rt==null)continue;
      if(exposureHandle.rt.width!=1||exposureHandle.rt.height!=1)throw new Exception("Unexpected actual GPU exposure dimensions");
      var exposureReadback=AsyncGPUReadback.Request(exposureHandle.rt,0,TextureFormat.RGBAFloat);exposureReadback.WaitForCompletion();
      if(exposureReadback.hasError)throw new Exception("Actual HDRP exposure GPU readback failed");
      var exposureData=exposureReadback.GetData<float>();if(exposureData.Length!=4)throw new Exception("GPU exposure texel layout differs");
      exposurePresent[i]=true;for(int ch=0;ch<4;ch++)exposureValues[i*4+ch]=exposureData[ch];
    }
    foreach(var type in new[]{typeof(ColorAdjustments),typeof(Bloom),typeof(LiftGammaGain),typeof(WhiteBalance),typeof(SplitToning),typeof(ChannelMixer),typeof(FilmGrain),typeof(Vignette),typeof(ChromaticAberration),typeof(ColorLookup),typeof(Tonemapping),typeof(Fog)}){
      var volumeComponent=sourceHdCamera.volumeStack.GetComponent(type);
      if(volumeComponent==null)throw new Exception("Actual post-processing volume component absent: "+type.Name);
      File.WriteAllText(Path.Combine(root,item.id+".active-"+type.Name+".json"),EditorJsonUtility.ToJson(volumeComponent,true));
    }
'''
text = text.replace(anchor, anchor + addition)
anchor = 'id=item.id,dark_source_control='
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,actual_postprocess_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.Postprocess),actual_color_grading_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.ColorGrading),actual_exposure_control_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.ExposureControl),actual_color_buffer_format=((HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).currentPlatformRenderPipelineSettings.colorBufferFormat.ToString(),exposure_uses_fetched=(bool)exposureStateType.GetField("useFetchedExposure").GetValue(exposureState),exposure_uses_current_camera=(bool)exposureStateType.GetField("useCurrentCamera").GetValue(exposureState),exposure_fetched=(float)exposureStateType.GetField("fetchedGpuExposure").GetValue(exposureState),exposure_texture_rgba=exposureValues,exposure_texture_present=exposurePresent,dark_source_control=')
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
source_anchor = '    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);'
insertion = '''    if(item.id.Contains("nopost")){
      var postCamera=camera.GetComponent<HDAdditionalCameraData>();postCamera.customRenderingSettings=true;
      postCamera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess,false);
      postCamera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess]=true;
    }
'''
text = text.replace(anchor, '    assert text.count(' + repr(source_anchor) + ')==1\n    text=text.replace(' + repr(source_anchor) + ',' + repr(insertion + source_anchor) + ')\n' + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
