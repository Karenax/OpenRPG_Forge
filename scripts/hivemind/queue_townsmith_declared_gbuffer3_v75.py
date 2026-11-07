"""Capture the exact original door with explicit scene context and raw linear output."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-door-source-declared-gbuffer3-v75'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PRIOR=W/'townsmith-shadow-mode-controls-v1'
CS=S/'candidates/SourceTownsmithUvDerivativesV75.cs'
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
    compute_path=PROJECT/'Assets/Editor/SourceTownsmithFgdReadbackV75.compute'
    assert not compute_path.exists()
    compute_path.write_text('#pragma kernel ReadFgd\nTexture2D<float4> SourceFgd;\nRWStructuredBuffer<float4> Output;\nuint Width,Height;\n[numthreads(8,8,1)]\nvoid ReadFgd(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)Output[id.y*Width+id.x]=SourceFgd.Load(int3(id.xy,0));}\n',encoding='utf-8')
    compute_path.write_text(compute_path.read_text(encoding="utf-8")+'#pragma kernel ReadArray\nTexture2DArray<float4> SourceArray;\n[numthreads(8,8,1)]\nvoid ReadArray(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)Output[(id.z*Height+id.y)*Width+id.x]=SourceArray.Load(int4(id.xy,id.z,0));}\n',encoding="utf-8")
    compute_path.write_text(compute_path.read_text(encoding="utf-8")+'#pragma kernel ReadDepth\nTexture2DArray<float> SourceDepth;\nRWStructuredBuffer<float> DepthOutput;\n[numthreads(8,8,1)]\nvoid ReadDepth(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)DepthOutput[id.y*Width+id.x]=SourceDepth.Load(int4(id.xy,0,0));}\n',encoding="utf-8")
    uv_shader=PROJECT/'Assets/Editor/SourceUvControlV75.shader'
    assert not uv_shader.exists()
    uv_shader.write_text('Shader "Hidden/OpenRPG/PrivateUvControlV75" {\nSubShader { Tags {"RenderPipeline"="HDRenderPipeline"} Pass {\nCull Back ZWrite Off ZTest Equal\nHLSLPROGRAM\n#pragma target 4.5\n#pragma vertex Vert\n#pragma fragment Frag\n#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"\n#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"\nstruct Attributes {float3 positionOS:POSITION;float4 uv:TEXCOORD0;};\nstruct Varyings {float4 positionCS:SV_POSITION;float4 uv:TEXCOORD0;};\nfloat _UvProbeMode;\nVaryings Vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS);o.uv=input.uv;return o;}\nfloat4 Frag(Varyings input):SV_Target {if(_UvProbeMode<0.5)return float4(input.uv.xy,0,1);if(_UvProbeMode<1.5)return float4(ddx(input.uv.xy),ddy(input.uv.xy));return float4(ddx_fine(input.uv.xy),ddy_fine(input.uv.xy));}\nENDHLSL\n} } }\n',encoding='utf-8')
    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')
    text=text.replace('OpenRPGSourceTownsmithShadowModesV1','OpenRPGSourceTownsmithUvDerivativesV75')
    text=text.replace('request.cases.Length!=15','request.cases.Length!=3').replace('Fifteen shadow-mode controls required','Three original and background controls required')
    text=text.replace('tone.mode.Override(TonemappingMode.Neutral);','tone.mode.Override(item.id.EndsWith("-linear")?TonemappingMode.None:TonemappingMode.Neutral);')
    anchor=' [Serializable] public class Result'
    assert text.count(anchor)==1
    context=''' [Serializable] public class Context {
  public string id,anisotropic_filtering,ambient_mode,tonemapping,skybox_identity;
  public Vector3 bounds_center,bounds_size,camera_position; public Quaternion camera_rotation;
  public Matrix4x4 camera_world_to_view,camera_projection; public Color ambient_light,ambient_sky,ambient_equator,ambient_ground,light_color;
  public float radius,fov,near,far,exposure_ev,light_intensity,ambient_intensity; public int msaa_samples,width=512,height=512;
  public string hdrp_clear_color_mode; public int active_sky_type,target_samples; public float[] ambient_probe,render_settings_ambient_probe; public string actual_sky_reflection; public bool actual_hdrp_probe_captured; public string actual_camera_history; public bool actual_postprocess_enabled,actual_color_grading_enabled,actual_exposure_control_enabled,exposure_uses_fetched,exposure_uses_current_camera; public string actual_color_buffer_format; public float exposure_fetched; public float[] exposure_texture_rgba; public bool[] exposure_texture_present; public bool dark_source_control,actual_fog_enabled; public string actual_debug_lighting_mode; public float actual_ssao_intensity,actual_ssao_radius,actual_ssao_specular_blend,actual_indirect_diffuse_multiplier,actual_reflection_multiplier; public Vector4 actual_global_ao_parameters; public int actual_hdri_width,actual_hdri_mip_count; public float actual_hdri_exposure,actual_hdri_multiplier; public string actual_hdri_intensity_mode; public string actual_lit_shader_mode,actual_fgd_format,actual_hdri_source; public bool actual_ssao_enabled,actual_ssr_enabled; public Matrix4x4 actual_hdrp_gpu_projection; public int sky_cubemap_count,sky_mip_count; public string sky_source_format; public int[] sky_layer_counts,sky_layer_bytes; public float[] actual_gpu_ambient_coefficients; public int ambient_buffer_count,ambient_buffer_stride;
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
    foreach(var type in new[]{typeof(ColorAdjustments),typeof(Bloom),typeof(LiftGammaGain),typeof(WhiteBalance),typeof(SplitToning),typeof(ChannelMixer),typeof(FilmGrain),typeof(Vignette),typeof(ChromaticAberration),typeof(Tonemapping),typeof(Fog)}){
      var volumeComponent=sourceHdCamera.volumeStack.GetComponent(type);
      if(volumeComponent==null)throw new Exception("Actual post-processing volume component absent: "+type.Name);
      File.WriteAllText(Path.Combine(root,item.id+".active-"+type.Name+".json"),EditorJsonUtility.ToJson(volumeComponent,true));
    }

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
    var sourceCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV75.compute");if(sourceCompute==null)throw new Exception("Owned source BRDF readback compute absent");
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
    text=text.replace('id=item.id,bounds_center=', 'id=item.id,actual_postprocess_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.Postprocess),actual_color_grading_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.ColorGrading),actual_exposure_control_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.ExposureControl),actual_color_buffer_format=((HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).currentPlatformRenderPipelineSettings.colorBufferFormat.ToString(),exposure_uses_fetched=(bool)exposureStateType.GetField("useFetchedExposure").GetValue(exposureState),exposure_uses_current_camera=(bool)exposureStateType.GetField("useCurrentCamera").GetValue(exposureState),exposure_fetched=(float)exposureStateType.GetField("fetchedGpuExposure").GetValue(exposureState),exposure_texture_rgba=exposureValues,exposure_texture_present=exposurePresent,dark_source_control=item.id.StartsWith("door-emission-dark"),actual_fog_enabled=sourceHdCamera.volumeStack.GetComponent<Fog>().enabled.value,actual_debug_lighting_mode=sourceDebugSettings.GetDebugLightingMode().ToString(),actual_ssao_intensity=activeOcclusion.intensity.value,actual_ssao_radius=activeOcclusion.radius.value,actual_ssao_specular_blend=activeOcclusion.specularOcclusion.value,actual_indirect_diffuse_multiplier=activeIndirect.indirectDiffuseLightingMultiplier.value,actual_reflection_multiplier=activeIndirect.reflectionLightingMultiplier.value,actual_global_ao_parameters=Shader.GetGlobalVector("_AmbientOcclusionParam"),actual_hdri_width=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.width,actual_hdri_mip_count=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.mipmapCount,actual_hdri_exposure=activeHdri.exposure.value,actual_hdri_multiplier=activeHdri.multiplier.value,actual_hdri_intensity_mode=activeHdri.skyIntensityMode.value.ToString(),actual_lit_shader_mode=sourceHdCamera.frameSettings.litShaderMode.ToString(),actual_ssao_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSAO),actual_ssr_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR),actual_hdrp_gpu_projection=sourceHdCamera.mainViewConstants.projMatrix,actual_fgd_format=sourceFgd.graphicsFormat.ToString(),actual_hdri_source=activeHdri.hdriSky.value==null?"null":AssetDatabase.GetAssetPath(activeHdri.hdriSky.value),sky_cubemap_count=skyArray.cubemapCount,sky_mip_count=skyArray.mipmapCount,sky_source_format=skyArray.format.ToString(),sky_layer_counts=skyLayerCounts,sky_layer_bytes=skyLayerBytes,actual_gpu_ambient_coefficients=gpuCoefficients,ambient_buffer_count=gpuProbe.count,ambient_buffer_stride=gpuProbe.stride,ambient_probe=ambientCoefficients,render_settings_ambient_probe=renderSettingsCoefficients,actual_hdrp_probe_captured=true,actual_camera_history="CustomUserHistory0-existing",actual_sky_reflection=reflectionDescription,active_sky_type=activeVisual.skyType.value,target_samples=target.antiAliasing,hdrp_clear_color_mode=camera.GetComponent<HDAdditionalCameraData>().clearColorMode.ToString(),bounds_center=')
    assert text.count('    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);')==1
    text=text.replace('    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);','    for(int warm=0;warm<3;warm++){\n      RenderPipeline.SubmitRenderRequest(camera,render);\n      if(warm==0){\n        var sourceDebugField=typeof(HDRenderPipeline).GetField("m_DebugDisplaySettings",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);\n        if(sourceDebugField==null)throw new Exception("Pinned HDRP debug settings field absent");\n        sourceDebugSettings=sourceDebugField.GetValue(RenderPipelineManager.currentPipeline) as DebugDisplaySettings;\n        if(sourceDebugSettings==null)throw new Exception("Actual HDRP debug settings absent");\n        priorDebugLighting=sourceDebugSettings.GetDebugLightingMode();\n        if(priorDebugLighting!=DebugLightingMode.None)throw new Exception("Preserve unexpected preexisting debug lighting state");\n        var requested=DebugLightingMode.None;\n        if(item.id.StartsWith("door-debug-"))requested=(DebugLightingMode)Enum.Parse(typeof(DebugLightingMode),item.id.Substring(11).Replace("-nopost", "").Replace("-nobloom", "").Replace("-linear", ""));\n        sourceDebugSettings.SetDebugLightingMode(requested);\n        if(sourceDebugSettings.GetDebugLightingMode()!=requested)throw new Exception("Native requested HDRP debug mode was not applied");\n      }\n    }')
    text=text.replace('RenderTexture target=null;', 'RenderTexture target=null;DebugDisplaySettings sourceDebugSettings=null;DebugLightingMode priorDebugLighting=DebugLightingMode.None;')
    text=text.replace('}finally{if(target!=null)', '}finally{if(sourceDebugSettings!=null)sourceDebugSettings.SetDebugLightingMode(priorDebugLighting);if(target!=null)')
    assert text.count('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')==1
    text=text.replace('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);','    if(item.id.StartsWith("door-emission-dark")){\n      light.intensity=0;\n      var darkCamera=camera.GetComponent<HDAdditionalCameraData>();darkCamera.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;darkCamera.backgroundColorHDR=new Color(.025f,.025f,.025f,1);\n      var darkIndirect=profile.Add<IndirectLightingController>();darkIndirect.indirectDiffuseLightingMultiplier.Override(0);darkIndirect.reflectionLightingMultiplier.Override(0);darkIndirect.reflectionProbeIntensityMultiplier.Override(0);\n      var darkOcclusion=profile.Add<ScreenSpaceAmbientOcclusion>();darkOcclusion.intensity.Override(0);\n      var darkFog=profile.Add<Fog>();darkFog.enabled.Override(false);darkFog.enableVolumetricFog.Override(false);\n    }\n    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')
    assert text.count('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')==1
    text=text.replace('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);','    if(item.id.Contains("nopost")){\n      var postCamera=camera.GetComponent<HDAdditionalCameraData>();postCamera.customRenderingSettings=true;\n      postCamera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess,false);\n      postCamera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess]=true;\n    }\n    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')
    assert text.count('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')==1
    text=text.replace('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);','    if(item.id.Contains("nobloom")){\n      var sourceBloom=profile.Add<Bloom>();sourceBloom.intensity.Override(0);\n    }\n    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')
    assert text.count('    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",instanceFlags,null,new[]{typeof(HDCamera)},null);')==1
    text=text.replace('    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",instanceFlags,null,new[]{typeof(HDCamera)},null);','    File.WriteAllText(Path.Combine(root,item.id+".source-light.json"),EditorJsonUtility.ToJson(light,true));\n    var sourceHdLight=light.GetComponent<HDAdditionalLightData>();\n    if(sourceHdLight==null)throw new Exception("Actual additional light data absent");\n    File.WriteAllText(Path.Combine(root,item.id+".source-hdlight.json"),EditorJsonUtility.ToJson(sourceHdLight,true));\n    var sourceBuffers=new List<GlobalBufferCapture>();\n    foreach(var property in new[]{"_GBufferTexture0","_GBufferTexture1","_GBufferTexture2","_GBufferTexture3","_CameraDepthTexture","_AmbientOcclusionTexture"}){\n      var record=new GlobalBufferCapture{shader_property=property};sourceBuffers.Add(record);\n      var actualTexture=Shader.GetGlobalTexture(property);record.present=actualTexture!=null;\n      if(actualTexture==null){record.error="Global texture absent after request";continue;}\n      record.texture_name=actualTexture.name;record.entity_id=actualTexture.GetEntityId().ToString();\n      record.texture_dimension=actualTexture.dimension.ToString();record.graphics_format=actualTexture.graphicsFormat.ToString();\n      record.width=actualTexture.width;record.height=actualTexture.height;\n      if(actualTexture.dimension!=TextureDimension.Tex2D||actualTexture.width<1||actualTexture.height<1||actualTexture.width*actualTexture.height>2097152){record.error="Unsupported or unbounded global layout";continue;}\n      ComputeBuffer actualBuffer=null;\n      try{\n        actualBuffer=new ComputeBuffer(record.width*record.height,16);\n        sourceCompute.SetTexture(sourceKernel,"SourceFgd",actualTexture);sourceCompute.SetBuffer(sourceKernel,"Output",actualBuffer);\n        sourceCompute.SetInt("Width",record.width);sourceCompute.SetInt("Height",record.height);\n        sourceCompute.Dispatch(sourceKernel,(record.width+7)/8,(record.height+7)/8,1);\n        var bufferValues=new Vector4[record.width*record.height];actualBuffer.GetData(bufferValues);\n        using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+"."+property+".rgba32f"),FileMode.CreateNew))){\n          foreach(var vector in bufferValues)for(int ch=0;ch<4;ch++){var value=vector[ch];if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite source global readback");writer.Write(value);}\n        }\n        record.float_count=bufferValues.Length*4;record.gpu_loaded=true;\n      }catch(Exception captureError){record.error=captureError.ToString();}\n      finally{if(actualBuffer!=null)actualBuffer.Release();}\n    }\n    File.WriteAllText(Path.Combine(root,item.id+".global-buffers.json"),JsonUtility.ToJson(new GlobalBufferInventory{id=item.id,buffers=sourceBuffers.ToArray()},true));\n    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",instanceFlags,null,new[]{typeof(HDCamera)},null);')
    text=text.replace(" [Serializable] public class Context {",' [Serializable] public class GlobalBufferCapture {\n public string shader_property,texture_name,texture_dimension,graphics_format,error;\n public int width,height,float_count;public string entity_id;public bool present,gpu_loaded;\n public bool actual_lighting_binding_qualified=false;public string observation_scope="After actual SubmitRenderRequest; resource identity/lifetime requires separate verification.";\n }\n [Serializable] public class GlobalBufferInventory {public string id;public GlobalBufferCapture[] buffers;}\n [Serializable] public class Context {')

    text=text.replace('public int width,height,float_count;','public int width,height,float_count,layer_count;')
    text=text.replace('public string id;public GlobalBufferCapture[] buffers;', 'public string id;public int actual_viewport_width,actual_viewport_height;public GlobalBufferCapture[] buffers;')
    text=text.replace('record.width=actualTexture.width;record.height=actualTexture.height;', 'record.width=actualTexture.width;record.height=actualTexture.height;record.layer_count=actualTexture is RenderTexture actualRt?actualRt.volumeDepth:1;')
    text=text.replace('actualTexture.dimension!=TextureDimension.Tex2D||actualTexture.width<1||actualTexture.height<1||actualTexture.width*actualTexture.height>2097152', '(actualTexture.dimension!=TextureDimension.Tex2D&&actualTexture.dimension!=TextureDimension.Tex2DArray)||actualTexture.width<1||actualTexture.height<1||record.layer_count<1||record.layer_count>8||actualTexture.width*actualTexture.height*record.layer_count>4194304')
    text=text.replace('actualBuffer=new ComputeBuffer(record.width*record.height,16);', 'actualBuffer=new ComputeBuffer(record.width*record.height*record.layer_count,16);var captureKernel=actualTexture.dimension==TextureDimension.Tex2DArray?sourceCompute.FindKernel("ReadArray"):sourceKernel;')
    text=text.replace('sourceCompute.SetTexture(sourceKernel,"SourceFgd",actualTexture);sourceCompute.SetBuffer(sourceKernel,"Output",actualBuffer);', 'sourceCompute.SetTexture(captureKernel,actualTexture.dimension==TextureDimension.Tex2DArray?"SourceArray":"SourceFgd",actualTexture);sourceCompute.SetBuffer(captureKernel,"Output",actualBuffer);')
    text=text.replace('sourceCompute.Dispatch(sourceKernel,(record.width+7)/8,(record.height+7)/8,1);', 'sourceCompute.Dispatch(captureKernel,(record.width+7)/8,(record.height+7)/8,record.layer_count);')
    text=text.replace('var bufferValues=new Vector4[record.width*record.height];', 'var bufferValues=new Vector4[record.width*record.height*record.layer_count];')
    text=text.replace('new GlobalBufferInventory{id=item.id,buffers=', 'new GlobalBufferInventory{id=item.id,actual_viewport_width=sourceHdCamera.actualWidth,actual_viewport_height=sourceHdCamera.actualHeight,buffers=')

    text=text.replace('RenderTexture target=null;DebugDisplaySettings', 'RenderTexture target=null;SourceOwnedDepthCaptureV75 sourceDepthPass=null;DebugDisplaySettings')
    text=text.replace('    for(int warm=0;warm<3;warm++){', '    sourceDepthPass=new SourceOwnedDepthCaptureV75{sourceCamera=camera,compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV75.compute"),root=root,id=item.id};\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceDepthPass);\n    for(int warm=0;warm<3;warm++){')
    text=text.replace('    var historyType=typeof(HDCamera)', '    AsyncGPUReadback.WaitAllRequests();if(sourceDepthPass.submitted<1||sourceDepthPass.completed!=sourceDepthPass.submitted||sourceDepthPass.errors.Count!=0)throw new Exception("Actual depth capture incomplete: "+string.Join(";",sourceDepthPass.errors));\n    var historyType=typeof(HDCamera)')
    text=text.replace('}finally{if(sourceDebugSettings!=null)', '}finally{if(sourceDepthPass!=null)CustomPassVolume.UnregisterGlobalCustomPass(sourceDepthPass);if(sourceDebugSettings!=null)')
    text+='\npublic sealed class SourceOwnedDepthCaptureV75 : CustomPass {\n public Camera sourceCamera;public ComputeShader compute;public string root,id;\n public int submitted,completed;public List<string> errors=new List<string>();\n [Serializable] public class DepthRecord {public string id,texture_name,format,dimension;public int capture,width,height,physical_width,physical_height,layers;public Matrix4x4 projection;public bool captured_inside_actual_camera_pass=true,geometry_parity_qualified=false;}\n protected override void Execute(CustomPassContext ctx){\n  if(ctx.hdCamera.camera!=sourceCamera)return;\n  var actual=ctx.cameraDepthBuffer.rt;if(actual==null||actual.dimension!=TextureDimension.Tex2DArray)throw new Exception("Expected actual HDRP array depth");\n  int width=ctx.hdCamera.actualWidth,height=ctx.hdCamera.actualHeight,index=submitted++;\n  if(width!=512||height!=512||actual.volumeDepth!=1)throw new Exception("Depth viewport differs from pinned fixture");\n  var output=new ComputeBuffer(width*height,4);int kernel=compute.FindKernel("ReadDepth");\n  ctx.cmd.SetComputeTextureParam(compute,kernel,"SourceDepth",ctx.cameraDepthBuffer);\n  ctx.cmd.SetComputeBufferParam(compute,kernel,"DepthOutput",output);\n  ctx.cmd.SetComputeIntParam(compute,"Width",width);ctx.cmd.SetComputeIntParam(compute,"Height",height);\n  ctx.cmd.DispatchCompute(compute,kernel,(width+7)/8,(height+7)/8,1);\n  var path=Path.Combine(root,id+".depth-"+index+".r32f");\n  File.WriteAllText(Path.Combine(root,id+".depth-"+index+".json"),JsonUtility.ToJson(new DepthRecord{id=id,capture=index,width=width,height=height,physical_width=actual.width,physical_height=actual.height,layers=actual.volumeDepth,texture_name=actual.name,format=actual.graphicsFormat.ToString(),dimension=actual.dimension.ToString(),projection=ctx.hdCamera.mainViewConstants.projMatrix},true));\n  ctx.cmd.RequestAsyncReadback(output,request=>{\n   try{if(request.hasError)throw new Exception("Depth GPU readback failed");var values=request.GetData<float>();if(values.Length!=width*height)throw new Exception("Depth size differs");\n    using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(var value in values){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite depth");writer.Write(value);}completed++;\n   }catch(Exception error){errors.Add(error.ToString());}finally{output.Release();}\n  });\n }\n}\n'

    text=text.replace('var scene=EditorSceneManager.NewPreviewScene();', 'var sourcePriorAniso=QualitySettings.anisotropicFiltering;var scene=EditorSceneManager.NewPreviewScene();')
    text=text.replace('    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);', '    if(item.id.Contains("noaniso"))QualitySettings.anisotropicFiltering=AnisotropicFiltering.Disable;\n    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);')
    text=text.replace('}finally{if(sourceDepthPass!=null)', '}finally{QualitySettings.anisotropicFiltering=sourcePriorAniso;if(sourceDepthPass!=null)')

    text=text.replace('SourceOwnedDepthCaptureV75 sourceDepthPass=null;', 'SourceOwnedDepthCaptureV75 sourceDepthPass=null;SourceOwnedUvCaptureV75 sourceUvPass=null;SourceOwnedNormalCaptureV75 sourceNormalPass=null;')
    text=text.replace('    for(int warm=0;warm<3;warm++){', '    var sourceUvShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/SourceUvControlV75.shader");if(sourceUvShader==null||!sourceUvShader.isSupported||ShaderUtil.ShaderHasError(sourceUvShader))throw new Exception("Owned UV shader unresolved: "+(sourceUvShader==null?"null asset":string.Join(";",System.Array.ConvertAll(ShaderUtil.GetShaderMessages(sourceUvShader),message=>message.message))));\n    sourceUvPass=new SourceOwnedUvCaptureV75{sourceCamera=camera,sourceMesh=mesh,probeMaterial=new Material(sourceUvShader){hideFlags=HideFlags.HideAndDontSave},root=root,id=item.id};\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceUvPass);\n    sourceNormalPass=new SourceOwnedNormalCaptureV75{sourceCamera=camera,shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV75.compute"),root=root,id=item.id};\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.BeforePostProcess,sourceNormalPass);\n    for(int warm=0;warm<3;warm++){')
    text=text.replace('    var historyType=typeof(HDCamera)', '    if(sourceNormalPass.submitted<2||sourceNormalPass.completed!=sourceNormalPass.submitted||sourceNormalPass.errors.Count!=0)throw new Exception("Actual normal capture incomplete: "+string.Join(";",sourceNormalPass.errors));\n    if(sourceUvPass.submitted<2||sourceUvPass.completed!=sourceUvPass.submitted||sourceUvPass.errors.Count!=0)throw new Exception("Actual UV capture incomplete: "+string.Join(";",sourceUvPass.errors));\n    var historyType=typeof(HDCamera)')
    text=text.replace('}finally{QualitySettings.anisotropicFiltering=sourcePriorAniso;', '}finally{if(sourceNormalPass!=null){CustomPassVolume.UnregisterGlobalCustomPass(sourceNormalPass);AsyncGPUReadback.WaitAllRequests();}if(sourceUvPass!=null){CustomPassVolume.UnregisterGlobalCustomPass(sourceUvPass);AsyncGPUReadback.WaitAllRequests();sourceUvPass.ReleaseOwned();}QualitySettings.anisotropicFiltering=sourcePriorAniso;')
    text+='\npublic sealed class SourceOwnedUvCaptureV75 : CustomPass {\n public Camera sourceCamera;public Mesh sourceMesh;public Material probeMaterial;public string root,id;\n public int submitted,completed;public List<string> errors=new List<string>();\n private List<RenderTexture> targets=new List<RenderTexture>();\n [Serializable] public class UvRecord {public string id,format;public int capture,mode,width,height,viewportWidth,viewportHeight;public bool actual_opaque_depth_bound=true;public Matrix4x4 projection;public bool isolated_original_lod0_submesh0=true,full_import=false;}\n protected override void Execute(CustomPassContext ctx){\n  if(ctx.hdCamera.camera!=sourceCamera)return;\n  int viewportWidth=ctx.hdCamera.actualWidth,viewportHeight=ctx.hdCamera.actualHeight;int width=ctx.cameraDepthBuffer.rt.width,height=ctx.cameraDepthBuffer.rt.height;\n  if(viewportWidth!=512||viewportHeight!=512||ctx.cameraDepthBuffer.rt.dimension!=TextureDimension.Tex2DArray||ctx.cameraDepthBuffer.rt.volumeDepth!=1)throw new Exception("UV viewport/depth layout differs");\n  for(int mode=0;mode<3;mode++){\n   int index=submitted++;var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){hideFlags=HideFlags.HideAndDontSave,dimension=TextureDimension.Tex2DArray,volumeDepth=1};\n   if(!target.Create())throw new Exception("Private float32 UV target creation failed");targets.Add(target);\n   var properties=new MaterialPropertyBlock();properties.SetFloat("_UvProbeMode",mode);\n   ctx.cmd.SetRenderTarget(new RenderTargetIdentifier(target,0,CubemapFace.Unknown,0),ctx.cameraDepthBuffer.nameID);ctx.cmd.SetViewport(new Rect(0,0,viewportWidth,viewportHeight));ctx.cmd.ClearRenderTarget(false,true,Color.clear);\n   ctx.cmd.DrawMesh(sourceMesh,Matrix4x4.identity,probeMaterial,0,0,properties);\n   string path=Path.Combine(root,id+".uv-control-"+index+".rgba32f");\n   File.WriteAllText(Path.Combine(root,id+".uv-control-"+index+".json"),JsonUtility.ToJson(new UvRecord{id=id,capture=index,mode=mode,width=width,height=height,viewportWidth=viewportWidth,viewportHeight=viewportHeight,format=target.graphicsFormat.ToString(),projection=ctx.hdCamera.mainViewConstants.projMatrix},true));\n   ctx.cmd.RequestAsyncReadback(target,0,request=>{\n    try{if(request.hasError)throw new Exception("UV float32 GPU readback failed");var values=request.GetData<float>();if(values.Length!=width*height*4)throw new Exception("UV layout differs");\n     using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(var value in values){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite UV capture");writer.Write(value);}completed++;\n    }catch(Exception error){errors.Add(error.ToString());}\n   });\n  }\n }\n public void ReleaseOwned(){foreach(var target in targets){target.Release();UnityEngine.Object.DestroyImmediate(target);}targets.Clear();if(probeMaterial!=null)UnityEngine.Object.DestroyImmediate(probeMaterial);}\n}\n'
    text+='\npublic sealed class SourceOwnedNormalCaptureV75 : CustomPass {\n public Camera sourceCamera;public ComputeShader shader;public string root,id;\n public int submitted,completed;public List<string> errors=new List<string>();\n [Serializable] public class NormalRecord {public string id,format,textureName,injection="BeforePostProcess";public int capture,width,height,viewportWidth,viewportHeight;public bool actual_camera_normal_buffer=true,full_import=false;}\n protected override void Execute(CustomPassContext ctx){\n  if(ctx.hdCamera.camera!=sourceCamera)return;\n  var texture=ctx.cameraNormalBuffer.rt;\n  if(texture==null||texture.dimension!=TextureDimension.Tex2DArray||texture.volumeDepth!=1)throw new Exception("Actual normal target layout differs");\n  int width=texture.width,height=texture.height,index=submitted++;\n  int kernel=shader.FindKernel("ReadArray");var output=new ComputeBuffer(width*height,16);\n  ctx.cmd.SetComputeIntParam(shader,"Width",width);ctx.cmd.SetComputeIntParam(shader,"Height",height);\n  ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",ctx.cameraNormalBuffer);\n  ctx.cmd.SetComputeBufferParam(shader,kernel,"Output",output);\n  ctx.cmd.DispatchCompute(shader,kernel,(width+7)/8,(height+7)/8,1);\n  string path=Path.Combine(root,id+".inpass-normal-"+index+".rgba32f");\n  File.WriteAllText(Path.Combine(root,id+".inpass-normal-"+index+".json"),JsonUtility.ToJson(new NormalRecord{id=id,capture=index,width=width,height=height,viewportWidth=ctx.hdCamera.actualWidth,viewportHeight=ctx.hdCamera.actualHeight,format=texture.graphicsFormat.ToString(),textureName=texture.name},true));\n  ctx.cmd.RequestAsyncReadback(output,request=>{\n   try{if(request.hasError)throw new Exception("Actual camera normal GPU readback failed");var data=request.GetData<float>();if(data.Length!=width*height*4)throw new Exception("Actual normal readback size differs");\n    using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(float value in data){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite camera normal");writer.Write(value);}completed++;\n   }catch(Exception error){errors.Add(error.ToString());}finally{output.Release();}\n  });\n }\n}\n'
    text='using UnityEngine.Rendering.RenderGraphModule;\n'+text
    assert 'CustomPassInjectionPoint.BeforePostProcess,sourceNormalPass' in text
    text=text.replace('CustomPassInjectionPoint.BeforePostProcess,sourceNormalPass','CustomPassInjectionPoint.BeforePreRefraction,sourceNormalPass')
    assert 'injection="BeforePostProcess"' in text
    text=text.replace('injection="BeforePostProcess"','injection="BeforePreRefraction"')
    assert '.inpass-normal-' in text
    text=text.replace('.inpass-normal-','.inpass-gbuffer3-')
    assert 'actual_camera_normal_buffer=true' in text
    text=text.replace('actual_camera_normal_buffer=true','actual_rendergraph_gbuffer3_buffer=true')
    assert 'var texture=ctx.cameraNormalBuffer.rt;' in text
    text=text.replace('var texture=ctx.cameraNormalBuffer.rt;','var pipeline=RenderPipelineManager.currentPipeline;\n  if(!(pipeline is HDRenderPipeline))throw new Exception("Actual pipeline is not HDRP");\n  var field=typeof(HDRenderPipeline).GetField("m_GBufferOutput",BindingFlags.Instance|BindingFlags.NonPublic);\n  if(field==null)throw new Exception("Pinned GBuffer output field absent");\n  var outputState=field.GetValue(pipeline);var mrtField=outputState.GetType().GetField("mrt",BindingFlags.Public|BindingFlags.Instance);\n  if(mrtField==null)throw new Exception("Pinned MRT field absent");\n  var handles=(TextureHandle[])mrtField.GetValue(outputState);\n  if(handles==null||handles.Length<=3||!handles[3].IsValid())throw new Exception("Current GBuffer3 RenderGraph handle absent");\n  RTHandle actualBuffer=handles[3];var texture=actualBuffer==null?null:actualBuffer.rt;\n  if(texture==null||texture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32)throw new Exception("Actual GBuffer3 HDR format differs");')
    assert 'ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",ctx.cameraNormalBuffer);' in text
    text=text.replace('ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",ctx.cameraNormalBuffer);','ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",actualBuffer);')
    assert 'var pipeline=RenderPipelineManager.currentPipeline;\n  if(!(pipeline is HDRenderPipeline))throw new Exception("Actual pipeline is not HDRP");\n  var field=typeof(HDRenderPipeline).GetField("m_GBufferOutput",BindingFlags.Instance|BindingFlags.NonPublic);\n  if(field==null)throw new Exception("Pinned GBuffer output field absent");\n  var outputState=field.GetValue(pipeline);var mrtField=outputState.GetType().GetField("mrt",BindingFlags.Public|BindingFlags.Instance);\n  if(mrtField==null)throw new Exception("Pinned MRT field absent");\n  var handles=(TextureHandle[])mrtField.GetValue(outputState);\n  if(handles==null||handles.Length<=3||!handles[3].IsValid())throw new Exception("Current GBuffer3 RenderGraph handle absent");\n  RTHandle actualBuffer=handles[3];var texture=actualBuffer==null?null:actualBuffer.rt;\n  if(texture==null||texture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32)throw new Exception("Actual GBuffer3 HDR format differs");' in text
    text=text.replace('var pipeline=RenderPipelineManager.currentPipeline;\n  if(!(pipeline is HDRenderPipeline))throw new Exception("Actual pipeline is not HDRP");\n  var field=typeof(HDRenderPipeline).GetField("m_GBufferOutput",BindingFlags.Instance|BindingFlags.NonPublic);\n  if(field==null)throw new Exception("Pinned GBuffer output field absent");\n  var outputState=field.GetValue(pipeline);var mrtField=outputState.GetType().GetField("mrt",BindingFlags.Public|BindingFlags.Instance);\n  if(mrtField==null)throw new Exception("Pinned MRT field absent");\n  var handles=(TextureHandle[])mrtField.GetValue(outputState);\n  if(handles==null||handles.Length<=3||!handles[3].IsValid())throw new Exception("Current GBuffer3 RenderGraph handle absent");\n  RTHandle actualBuffer=handles[3];var texture=actualBuffer==null?null:actualBuffer.rt;\n  if(texture==null||texture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32)throw new Exception("Actual GBuffer3 HDR format differs");','RTHandle actualBuffer=retainedGbuffer3;var texture=actualBuffer==null?null:actualBuffer.rt;\n  if(texture==null||texture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32)throw new Exception("Declared GBuffer3 format differs");')
    assert text.count('public sealed class SourceOwnedNormalCaptureV75 : CustomPass {')==1
    text=text.replace('public sealed class SourceOwnedNormalCaptureV75 : CustomPass {','public sealed class SourceOwnedNormalCaptureV75 : CustomPass {\n public TextureHandle retainedGbuffer3;\n public TextureHandle OpenRpgDeclareReadTexture(HDCamera camera){\n  if(camera.camera!=sourceCamera)return default(TextureHandle);\n  var pipeline=RenderPipelineManager.currentPipeline;\n  if(!(pipeline is HDRenderPipeline))throw new Exception("Actual pipeline is not HDRP");\n  var field=typeof(HDRenderPipeline).GetField("m_GBufferOutput",BindingFlags.Instance|BindingFlags.NonPublic);\n  if(field==null)throw new Exception("Pinned GBuffer output field absent");\n  var outputState=field.GetValue(pipeline);var mrtField=outputState.GetType().GetField("mrt",BindingFlags.Public|BindingFlags.Instance);\n  if(mrtField==null)throw new Exception("Pinned MRT field absent");\n  var handles=(TextureHandle[])mrtField.GetValue(outputState);\n  if(handles==null||handles.Length<=3||!handles[3].IsValid())throw new Exception("Current GBuffer3 RenderGraph handle absent");\n\n  retainedGbuffer3=handles[3];return retainedGbuffer3;\n }\n')
    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())
    ROOT.mkdir()
    base=read(PRIOR/'REQUEST.v1.json')['cases'][0]
    cases=[dict(base,id='door-neutral'),dict(base,id='door-linear'),dict(base,id='door-fixedbackground-linear')]
    cases += [dict(base,id='door-debug-'+mode+'-linear') for mode in ['DirectDiffuseLighting','DirectSpecularLighting','IndirectDiffuseLighting','ReflectionLighting','IndirectDiffuseOcclusion','IndirectSpecularOcclusion','EmissiveLighting']]
    cases.append(dict(base,id='door-emission-dark-linear'))
    cases += [dict(base,id=name) for name in ['door-nopost-linear','door-emission-dark-nopost-linear','door-debug-EmissiveLighting-nopost-linear']]
    cases += [dict(base,id=name) for name in ['door-nobloom-linear','door-emission-dark-nobloom-linear','door-debug-EmissiveLighting-nobloom-linear']]
    cases += [dict(base,id='door-debug-'+mode+'-nopost-linear') for mode in ['DirectDiffuseLighting','DirectSpecularLighting','IndirectDiffuseLighting','ReflectionLighting']]
    cases=[dict(base,id=name) for name in ['door-neutral','door-linear','door-nopost-linear','door-debug-DirectDiffuseLighting-nopost-linear','door-debug-DirectSpecularLighting-nopost-linear']]
    cases=[dict(base,id=name) for name in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']]
    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))
    for p in [Path(__file__),CS,HELPER,compute_path,uv_shader,ROOT/'REQUEST.v1.json',PRIOR/'RUN-RESULT.v1.json']:
        pins[str(p)]=sha(p)
    save('PREPARED.v1.json',dict(input_pins=pins,private_before_pins=private,expected_cases=3,
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
                '-executeMethod','OpenRPGSourceTownsmithUvDerivativesV75.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,
                stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='source_original_door_neutral_and_linear_context_running',unity_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
        native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['backend']=='Direct3D11' and native['source_objects_unchanged']
        assert len(native['rows'])==3 and all(r['original_material_runtime_states_unchanged'] for r in native['rows'])
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        for p,h in prep['private_before_pins'].items():assert sha(p)==h,p
        assert sha(ROOT/'door-neutral.rgba16f')==sha(PRIOR/'case-00-source_default.rgba16f')
        state.update(status='original_door_neutral_reference_reproduced_and_linear_context_captured',
            neutral_reference_byte_equal=True,source_inputs_unchanged=True,cases_captured=3,
            native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),forge_editor_verified=False)
    except Exception as exc:state.update(status='source_render_context_refusal_retained',error=repr(exc),traceback=__import__('traceback').format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
