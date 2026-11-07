"""Capture the exact original door with explicit scene context and raw linear output."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-door-source-lighting-controls-v12'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PRIOR=W/'townsmith-shadow-mode-controls-v1'
CS=S/'candidates/SourceTownsmithLightingControlsV12.cs'
HELPER=PROJECT/'Assets/Editor'/CS.name
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,value):
    p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(value,indent=2),encoding='utf-8');t.replace(p)

def prepare():
    assert not ROOT.exists() and not CS.exists() and not HELPER.exists()
    assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    prior=read(PRIOR/'PREPARED.v1.json')
    pins=dict(prior['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    private={p:sha(p) for p in prior['private_before_pins']}
    compute_path=PROJECT/'Assets/Editor/SourceTownsmithFgdReadbackV12.compute'
    assert not compute_path.exists()
    compute_path.write_text('#pragma kernel ReadFgd\nTexture2D<float4> SourceFgd;\nRWStructuredBuffer<float4> Output;\nuint Width,Height;\n[numthreads(8,8,1)]\nvoid ReadFgd(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)Output[id.y*Width+id.x]=SourceFgd.Load(int3(id.xy,0));}\n',encoding='utf-8')
    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')
    text=text.replace('OpenRPGSourceTownsmithShadowModesV1','OpenRPGSourceTownsmithLightingControlsV12')
    text=text.replace('request.cases.Length!=15','request.cases.Length!=9').replace('Fifteen shadow-mode controls required','Three original and background controls required')
    text=text.replace('tone.mode.Override(TonemappingMode.Neutral);','tone.mode.Override(item.id.EndsWith("-linear")?TonemappingMode.None:TonemappingMode.Neutral);')
    anchor=' [Serializable] public class Result'
    assert text.count(anchor)==1
    context=''' [Serializable] public class Context {
  public string id,anisotropic_filtering,ambient_mode,tonemapping,skybox_identity;
  public Vector3 bounds_center,bounds_size,camera_position; public Quaternion camera_rotation;
  public Matrix4x4 camera_world_to_view,camera_projection; public Color ambient_light,ambient_sky,ambient_equator,ambient_ground,light_color;
  public float radius,fov,near,far,exposure_ev,light_intensity,ambient_intensity; public int msaa_samples,width=512,height=512;
  public string hdrp_clear_color_mode; public int active_sky_type,target_samples; public float[] ambient_probe,render_settings_ambient_probe; public string actual_sky_reflection; public bool actual_hdrp_probe_captured; public string actual_camera_history; public string actual_debug_lighting_mode; public float actual_ssao_intensity,actual_ssao_radius,actual_ssao_specular_blend,actual_indirect_diffuse_multiplier,actual_reflection_multiplier; public Vector4 actual_global_ao_parameters; public int actual_hdri_width,actual_hdri_mip_count; public float actual_hdri_exposure,actual_hdri_multiplier; public string actual_hdri_intensity_mode; public string actual_lit_shader_mode,actual_fgd_format,actual_hdri_source; public bool actual_ssao_enabled,actual_ssr_enabled; public Matrix4x4 actual_hdrp_gpu_projection; public int sky_cubemap_count,sky_mip_count; public string sky_source_format; public int[] sky_layer_counts,sky_layer_bytes; public float[] actual_gpu_ambient_coefficients; public int ambient_buffer_count,ambient_buffer_stride;
  public bool source_prefab_transform_context=false,source_original_material=true,full_import=false;
 }
'''
    text=text.replace(anchor,context+anchor)
    anchor='    var prior=RenderTexture.active;Texture2D raw=null,png=null;'
    assert text.count(anchor)==1
    context='''    File.WriteAllText(Path.Combine(root,item.id+".context.json"),JsonUtility.ToJson(new Context{
      id=item.id,bounds_center=bounds.center,bounds_size=bounds.size,radius=radius,
      camera_position=camera.transform.position,camera_rotation=camera.transform.rotation,
      camera_world_to_view=camera.worldToCameraMatrix,camera_projection=camera.projectionMatrix,
      fov=camera.fieldOfView,near=camera.nearClipPlane,far=camera.farClipPlane,exposure_ev=8,
      light_intensity=light.intensity,light_color=light.color,
      anisotropic_filtering=QualitySettings.anisotropicFiltering.ToString(),msaa_samples=QualitySettings.antiAliasing,
      ambient_mode=RenderSettings.ambientMode.ToString(),ambient_intensity=RenderSettings.ambientIntensity,
      ambient_light=RenderSettings.ambientLight,ambient_sky=RenderSettings.ambientSkyColor,
      ambient_equator=RenderSettings.ambientEquatorColor,ambient_ground=RenderSettings.ambientGroundColor,
      skybox_identity=RenderSettings.skybox==null?"null":"runtime-object:"+RenderSettings.skybox.name+";shader="+RenderSettings.skybox.shader.name,
      tonemapping=item.id.EndsWith("-linear")?"None":"Neutral"},true));
    if(RenderSettings.skybox!=null)File.WriteAllText(Path.Combine(root,item.id+".skybox-runtime.json"),EditorJsonUtility.ToJson(RenderSettings.skybox,true));
'''
    text=text.replace(anchor,context+anchor)
    camera_anchor='camera.gameObject.AddComponent<HDAdditionalCameraData>();'
    assert text.count(camera_anchor)==1
    text=text.replace(camera_anchor,camera_anchor+'if(item.id.Contains("fixedbackground")){var extra=camera.GetComponent<HDAdditionalCameraData>();extra.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;extra.backgroundColorHDR=new Color(.025f,.025f,.025f,1);}')
    context_anchor='    File.WriteAllText(Path.Combine(root,item.id+".context.json"),'
    assert text.count(context_anchor)==1
    extra_context='''    var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",BindingFlags.Public|BindingFlags.NonPublic);if(historyType==null)throw new Exception("Pinned HDRP request history type absent");
    var history=Enum.Parse(historyType,"CustomUserHistory0");
    var signature=new[]{typeof(Camera),typeof(int),historyType};
    var existsMethod=typeof(HDCamera).GetMethod("IsHistoryChannelExisting",flags,null,signature,null);if(existsMethod==null||!(bool)existsMethod.Invoke(null,new object[]{camera,0,history}))throw new Exception("Actual request camera history absent; do not create a substitute");
    var historyMethod=typeof(HDCamera).GetMethod("GetOrCreate",flags,null,signature,null);if(historyMethod==null)throw new Exception("Pinned HDRP request camera method absent");
    var sourceHdCamera=(HDCamera)historyMethod.Invoke(null,new object[]{camera,0,history});

    var activeVisual=sourceHdCamera.volumeStack.GetComponent<VisualEnvironment>();
    var activeHdri=sourceHdCamera.volumeStack.GetComponent<HDRISky>();
    var activeGradient=sourceHdCamera.volumeStack.GetComponent<GradientSky>();
    var activeOcclusion=sourceHdCamera.volumeStack.GetComponent<ScreenSpaceAmbientOcclusion>();
    var activeIndirect=sourceHdCamera.volumeStack.GetComponent<IndirectLightingController>();
    if(activeOcclusion==null||activeIndirect==null)throw new Exception("Actual HDRP indirect-light controls absent");
    File.WriteAllText(Path.Combine(root,item.id+".active-ssao.json"),EditorJsonUtility.ToJson(activeOcclusion,true));
    File.WriteAllText(Path.Combine(root,item.id+".active-indirect.json"),EditorJsonUtility.ToJson(activeIndirect,true));

    var ambientCoefficients=new float[27];var renderSettingsCoefficients=new float[27];var declaredProbe=RenderSettings.ambientProbe;
    for(int ch=0;ch<3;ch++)for(int k=0;k<9;k++)renderSettingsCoefficients[ch*9+k]=declaredProbe[ch,k];
    var instanceFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    var skyManagerField=typeof(HDRenderPipeline).GetField("m_SkyManager",instanceFlags);if(skyManagerField==null)throw new Exception("Pinned HDRP sky manager absent");
    var manager=skyManagerField.GetValue(RenderPipelineManager.currentPipeline);
    var probeMethod=manager.GetType().GetMethod("GetAmbientProbe",instanceFlags,null,new[]{typeof(HDCamera)},null);if(probeMethod==null)throw new Exception("Pinned HDRP camera probe method absent");
    var probe=(SphericalHarmonicsL2)probeMethod.Invoke(manager,new object[]{sourceHdCamera});
    var gpuProbeMethod=manager.GetType().GetMethod("GetDiffuseAmbientProbeBuffer",instanceFlags,null,new[]{typeof(HDCamera)},null);if(gpuProbeMethod==null)throw new Exception("Pinned HDRP diffuse GPU buffer method absent");
    var gpuProbe=gpuProbeMethod.Invoke(manager,new object[]{sourceHdCamera}) as GraphicsBuffer;
    if(gpuProbe==null||!gpuProbe.IsValid()||gpuProbe.count!=7||gpuProbe.stride!=16)throw new Exception("Actual HDRP GPU ambient buffer absent or unexpected layout");
    var gpuVectors=new Vector4[7];gpuProbe.GetData(gpuVectors);
    var gpuCoefficients=new float[28];for(int i=0;i<7;i++)for(int ch=0;ch<4;ch++)gpuCoefficients[i*4+ch]=gpuVectors[i][ch];
    var sourceFgd=Shader.GetGlobalTexture("_PreIntegratedFGD_GGXDisneyDiffuse");
    if(sourceFgd==null||sourceFgd.dimension!=TextureDimension.Tex2D||sourceFgd.width!=64||sourceFgd.height!=64)throw new Exception("Actual HDRP FGD missing or unexpected dimensions");
    var sourceCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV12.compute");if(sourceCompute==null)throw new Exception("Owned source BRDF readback compute absent");
    var sourceKernel=sourceCompute.FindKernel("ReadFgd");var fgdBuffer=new ComputeBuffer(64*64,16);
    try{
      sourceCompute.SetTexture(sourceKernel,"SourceFgd",sourceFgd);sourceCompute.SetBuffer(sourceKernel,"Output",fgdBuffer);
      sourceCompute.SetInt("Width",64);sourceCompute.SetInt("Height",64);sourceCompute.Dispatch(sourceKernel,8,8,1);
      var fgdValues=new Vector4[64*64];fgdBuffer.GetData(fgdValues);
      if(!BitConverter.IsLittleEndian)throw new Exception("FGD capture requires declared little endian");
      using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+".fgd.rgba32f"),FileMode.CreateNew))){
        for(int i=0;i<fgdValues.Length;i++)for(int ch=0;ch<4;ch++){var v=fgdValues[i][ch];if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Nonfinite FGD GPU load");writer.Write(v);}
      }
    }finally{fgdBuffer.Release();}
    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",instanceFlags,null,new[]{typeof(HDCamera)},null);if(reflectionMethod==null)throw new Exception("Pinned HDRP reflection method absent");
    var skyReflection=reflectionMethod.Invoke(manager,new object[]{sourceHdCamera}) as Texture;
    if(!(skyReflection is CubemapArray skyArray))throw new Exception("Actual sky reflection is not the captured CubemapArray type");
    if(skyArray.width!=256||skyArray.height!=256||skyArray.cubemapCount<=0)throw new Exception("Actual sky reflection dimensions absent or unexpected");
    var skyLayerCounts=new int[skyArray.mipmapCount];var skyLayerBytes=new int[skyArray.mipmapCount];
    for(int mip=0;mip<skyArray.mipmapCount;mip++){
      var skyReadback=AsyncGPUReadback.Request(skyArray,mip,TextureFormat.RGBAFloat);skyReadback.WaitForCompletion();
      if(skyReadback.hasError)throw new Exception("Actual HDRP sky GPU readback failed mip "+mip);
      skyLayerCounts[mip]=skyReadback.layerCount;skyLayerBytes[mip]=skyReadback.layerDataSize;
      var side=Math.Max(1,skyArray.width>>mip);
      if(skyReadback.layerCount!=skyArray.cubemapCount*6||skyReadback.layerDataSize!=side*side*16)throw new Exception("Sky GPU readback layer layout differs from actual texture");
      using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+".sky-mip-"+mip+".rgba32f"),FileMode.CreateNew))){
        for(int layer=0;layer<skyReadback.layerCount;layer++){
          var values=skyReadback.GetData<float>(layer);
          if(values.Length!=side*side*4)throw new Exception("Sky GPU float count differs");
          for(int i=0;i<values.Length;i++){var value=values[i];if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite sky capture");writer.Write(value);}
        }
      }
    }
    var reflectionDescription=skyReflection==null?"null":skyReflection.name+";dimension="+skyReflection.dimension+";width="+skyReflection.width+";height="+skyReflection.height;
    if(skyReflection is RenderTexture reflectionTarget)reflectionDescription+=";volumeDepth="+reflectionTarget.volumeDepth+";format="+reflectionTarget.graphicsFormat+";mips="+reflectionTarget.mipmapCount;

    for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)ambientCoefficients[channel*9+coefficient]=probe[channel,coefficient];
    File.WriteAllText(Path.Combine(root,item.id+".hdcamera.json"),EditorJsonUtility.ToJson(camera.GetComponent<HDAdditionalCameraData>(),true));
    File.WriteAllText(Path.Combine(root,item.id+".active-visual.json"),EditorJsonUtility.ToJson(activeVisual,true));
    File.WriteAllText(Path.Combine(root,item.id+".active-hdri.json"),EditorJsonUtility.ToJson(activeHdri,true));
    File.WriteAllText(Path.Combine(root,item.id+".active-gradient.json"),EditorJsonUtility.ToJson(activeGradient,true));
'''
    text=text.replace(context_anchor,extra_context+context_anchor)
    text=text.replace('id=item.id,bounds_center=', 'id=item.id,actual_debug_lighting_mode=sourceDebugSettings.GetDebugLightingMode().ToString(),actual_ssao_intensity=activeOcclusion.intensity.value,actual_ssao_radius=activeOcclusion.radius.value,actual_ssao_specular_blend=activeOcclusion.specularOcclusion.value,actual_indirect_diffuse_multiplier=activeIndirect.indirectDiffuseLightingMultiplier.value,actual_reflection_multiplier=activeIndirect.reflectionLightingMultiplier.value,actual_global_ao_parameters=Shader.GetGlobalVector("_AmbientOcclusionParam"),actual_hdri_width=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.width,actual_hdri_mip_count=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.mipmapCount,actual_hdri_exposure=activeHdri.exposure.value,actual_hdri_multiplier=activeHdri.multiplier.value,actual_hdri_intensity_mode=activeHdri.skyIntensityMode.value.ToString(),actual_lit_shader_mode=sourceHdCamera.frameSettings.litShaderMode.ToString(),actual_ssao_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSAO),actual_ssr_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR),actual_hdrp_gpu_projection=sourceHdCamera.mainViewConstants.projMatrix,actual_fgd_format=sourceFgd.graphicsFormat.ToString(),actual_hdri_source=activeHdri.hdriSky.value==null?"null":AssetDatabase.GetAssetPath(activeHdri.hdriSky.value),sky_cubemap_count=skyArray.cubemapCount,sky_mip_count=skyArray.mipmapCount,sky_source_format=skyArray.format.ToString(),sky_layer_counts=skyLayerCounts,sky_layer_bytes=skyLayerBytes,actual_gpu_ambient_coefficients=gpuCoefficients,ambient_buffer_count=gpuProbe.count,ambient_buffer_stride=gpuProbe.stride,ambient_probe=ambientCoefficients,render_settings_ambient_probe=renderSettingsCoefficients,actual_hdrp_probe_captured=true,actual_camera_history="CustomUserHistory0-existing",actual_sky_reflection=reflectionDescription,active_sky_type=activeVisual.skyType.value,target_samples=target.antiAliasing,hdrp_clear_color_mode=camera.GetComponent<HDAdditionalCameraData>().clearColorMode.ToString(),bounds_center=')
    assert text.count('    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);')==1
    text=text.replace('    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);','    for(int warm=0;warm<3;warm++){\n      RenderPipeline.SubmitRenderRequest(camera,render);\n      if(warm==0){\n        var sourceDebugField=typeof(HDRenderPipeline).GetField("m_DebugDisplaySettings",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);\n        if(sourceDebugField==null)throw new Exception("Pinned HDRP debug settings field absent");\n        sourceDebugSettings=sourceDebugField.GetValue(RenderPipelineManager.currentPipeline) as DebugDisplaySettings;\n        if(sourceDebugSettings==null)throw new Exception("Actual HDRP debug settings absent");\n        priorDebugLighting=sourceDebugSettings.GetDebugLightingMode();\n        if(priorDebugLighting!=DebugLightingMode.None)throw new Exception("Preserve unexpected preexisting debug lighting state");\n        var requested=DebugLightingMode.None;\n        if(item.id.StartsWith("door-debug-"))requested=(DebugLightingMode)Enum.Parse(typeof(DebugLightingMode),item.id.Substring(11,item.id.Length-18));\n        sourceDebugSettings.SetDebugLightingMode(requested);\n        if(sourceDebugSettings.GetDebugLightingMode()!=requested)throw new Exception("Native requested HDRP debug mode was not applied");\n      }\n    }')
    text=text.replace('RenderTexture target=null;', 'RenderTexture target=null;DebugDisplaySettings sourceDebugSettings=null;DebugLightingMode priorDebugLighting=DebugLightingMode.None;')
    text=text.replace('}finally{if(target!=null)', '}finally{if(sourceDebugSettings!=null)sourceDebugSettings.SetDebugLightingMode(priorDebugLighting);if(target!=null)')
    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())
    ROOT.mkdir()
    base=read(PRIOR/'REQUEST.v1.json')['cases'][0]
    cases=[dict(base,id='door-neutral'),dict(base,id='door-linear'),dict(base,id='door-fixedbackground-linear')]
    cases += [dict(base,id='door-debug-'+mode+'-linear') for mode in ['DirectDiffuseLighting','DirectSpecularLighting','IndirectDiffuseLighting','ReflectionLighting','IndirectDiffuseOcclusion','IndirectSpecularOcclusion']]
    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))
    for p in [Path(__file__),CS,HELPER,compute_path,ROOT/'REQUEST.v1.json',PRIOR/'RUN-RESULT.v1.json']:
        pins[str(p)]=sha(p)
    save('PREPARED.v1.json',dict(input_pins=pins,private_before_pins=private,expected_cases=9,
        source_objects_preserved=True,purchased_scripts_executed=False,full_import=False))
    save('RUN-RESULT.v1.json',dict(status='source_render_context_prepared',full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,
            stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,root=str(ROOT))))

def run():
    state=read(ROOT/'RUN-RESULT.v1.json')
    try:
        prep=read(ROOT/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_TOWNSMITH_DRAW_OUT=str(ROOT))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),
                '-executeMethod','OpenRPGSourceTownsmithLightingControlsV12.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,
                stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='source_original_door_neutral_and_linear_context_running',unity_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
        native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['backend']=='Direct3D11' and native['source_objects_unchanged']
        assert len(native['rows'])==9 and all(r['original_material_runtime_states_unchanged'] for r in native['rows'])
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        for p,h in prep['private_before_pins'].items():assert sha(p)==h,p
        assert sha(ROOT/'door-neutral.rgba16f')==sha(PRIOR/'case-00-source_default.rgba16f')
        state.update(status='original_door_neutral_reference_reproduced_and_linear_context_captured',
            neutral_reference_byte_equal=True,source_inputs_unchanged=True,cases_captured=9,
            native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),forge_editor_verified=False)
    except Exception as exc:state.update(status='source_render_context_refusal_retained',error=repr(exc),traceback=__import__('traceback').format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
