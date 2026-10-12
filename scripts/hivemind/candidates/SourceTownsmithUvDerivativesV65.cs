using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Original mesh assets and material slot arrays; isolated diagnostic instances.
public static class OpenRPGSourceTownsmithUvDerivativesV65 {
 [Serializable] public class Ref {public string guid,file_id,asset_path;}
 [Serializable] public class Case {public string id,control_mode;public Ref mesh;public Ref[] materials;public int expected_submeshes,affected_bindings;}
 [Serializable] public class Request {public string output;public Case[] cases;}
 [Serializable] public class Pass {public string material,shader,name;public int index;public bool compiled;}
 [Serializable] public class Draw {public int event_index;public string mesh_subset,pass_name,original_shader,real_shader,component,index_count;}
 [Serializable] public class Row {public string id,mesh_identity,image,raw_image;public int vertices,submeshes,material_slots,event_count,matching_mesh_events;public bool native_draw_mapping_observed;public string[] material_identities;public Pass[] compiled_passes;public Draw[] draws;public string frame_debugger_error;public string[] material_before_paths,material_after_compile_paths,material_after_render_paths;public int source_material_runtime_states_changed;public bool source_material_runtime_states_restored;public bool original_material_runtime_states_unchanged;public bool private_material_instances_used=true;public float max_linear_channel;}
 [Serializable] public class GlobalBufferCapture {
 public string shader_property,texture_name,texture_dimension,graphics_format,error;
 public int width,height,float_count,layer_count;public string entity_id;public bool present,gpu_loaded;
 public bool actual_lighting_binding_qualified=false;public string observation_scope="After actual SubmitRenderRequest; resource identity/lifetime requires separate verification.";
 }
 [Serializable] public class GlobalBufferInventory {public string id;public int actual_viewport_width,actual_viewport_height;public GlobalBufferCapture[] buffers;}
 [Serializable] public class Context {
  public string id,anisotropic_filtering,ambient_mode,tonemapping,skybox_identity;
  public Vector3 bounds_center,bounds_size,camera_position; public Quaternion camera_rotation;
  public Matrix4x4 camera_world_to_view,camera_projection; public Color ambient_light,ambient_sky,ambient_equator,ambient_ground,light_color;
  public float radius,fov,near,far,exposure_ev,light_intensity,ambient_intensity; public int msaa_samples,width=512,height=512;
  public string hdrp_clear_color_mode; public int active_sky_type,target_samples; public float[] ambient_probe,render_settings_ambient_probe; public string actual_sky_reflection; public bool actual_hdrp_probe_captured; public string actual_camera_history; public bool actual_postprocess_enabled,actual_color_grading_enabled,actual_exposure_control_enabled,exposure_uses_fetched,exposure_uses_current_camera; public string actual_color_buffer_format; public float exposure_fetched; public float[] exposure_texture_rgba; public bool[] exposure_texture_present; public bool dark_source_control,actual_fog_enabled; public string actual_debug_lighting_mode; public float actual_ssao_intensity,actual_ssao_radius,actual_ssao_specular_blend,actual_indirect_diffuse_multiplier,actual_reflection_multiplier; public Vector4 actual_global_ao_parameters; public int actual_hdri_width,actual_hdri_mip_count; public float actual_hdri_exposure,actual_hdri_multiplier; public string actual_hdri_intensity_mode; public string actual_lit_shader_mode,actual_fgd_format,actual_hdri_source; public bool actual_ssao_enabled,actual_ssr_enabled; public Matrix4x4 actual_hdrp_gpu_projection; public int sky_cubemap_count,sky_mip_count; public string sky_source_format; public int[] sky_layer_counts,sky_layer_bytes; public float[] actual_gpu_ambient_coefficients; public int ambient_buffer_count,ambient_buffer_stride;
  public bool source_prefab_transform_context=false,source_original_material=true,full_import=false;
 }
 [Serializable] public class Result {public string gpu,backend,pipeline;public bool private_material_instances_used=true;public bool frame_debugger_locally_supported,source_objects_unchanged,original_materials_preserved=true,isolated_mesh_renderer_context=true,original_prefab_transform_context=false,full_import=false,forge_editor_verified=false;public Row[] rows;}
 static BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static string Identity(UnityEngine.Object obj) {if(obj==null||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id))throw new Exception("Source identity absent");return guid+":"+id;}
 static T Load<T>(Ref r) where T:UnityEngine.Object {if(AssetDatabase.AssetPathToGUID(r.asset_path)!=r.guid)throw new Exception("Source path/GUID changed: "+r.asset_path);return AssetDatabase.LoadAllAssetsAtPath(r.asset_path).OfType<T>().Single(x=>Identity(x)==r.guid+":"+r.file_id);}
 static string Field(object value,string name) {return value.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(value)?.ToString()??"";}
 static bool LocalSupported(Type utility) {return (bool)(utility.GetProperty("locallySupported",flags)?.GetValue(null)??false);}
 public static void Export() {
  string root=Environment.GetEnvironmentVariable("OPENRPG_TOWNSMITH_DRAW_OUT");if(string.IsNullOrEmpty(root))throw new Exception("Output absent");
  var request=JsonUtility.FromJson<Request>(File.ReadAllText(Path.Combine(root,"REQUEST.v1.json")));if(request.cases.Length!=3)throw new Exception("Three original and background controls required");
  if(File.Exists(request.output))throw new Exception("Preserve evidence");
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  if(PlayerSettings.colorSpace!=ColorSpace.Linear||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("Linear native GPU required");
  var pipeline=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;if(pipeline==null)throw new Exception("HDRP required");
  var prepare=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",flags,null,new[]{typeof(RenderPipelineAsset)},null);
  if(prepare==null||!(bool)prepare.Invoke(null,new object[]{pipeline}))throw new Exception("HDRP initialization failed");
  var utility=typeof(Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility",true);
  bool supported=LocalSupported(utility);var rows=new List<Row>();var ownedMaterials=new List<Material>();var ownedMeshes=new List<Mesh>();bool priorAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
  try {foreach(var item in request.cases) {
   var mesh=Load<Mesh>(item.mesh);var sourceMaterials=item.materials.Select(Load<Material>).ToArray();
   var sourceBefore=sourceMaterials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();
   var instances=sourceMaterials.Distinct().ToDictionary(x=>x,x=>new Material(x){hideFlags=HideFlags.HideAndDontSave});ownedMaterials.AddRange(instances.Values);
   var materials=sourceMaterials.Select(x=>instances[x]).ToArray();
   if(mesh.subMeshCount!=item.expected_submeshes)throw new Exception("Source submesh count changed");
   var before=materials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();var passes=new List<Pass>();
   foreach(var material in materials.Distinct()) {
    if(material.shader==null||!material.shader.isSupported||ShaderUtil.ShaderHasError(material.shader)||material.shader.name=="Hidden/InternalErrorShader")throw new Exception("Original shader unresolved: "+Identity(material));
    for(int i=0;i<material.passCount;i++) {
     string name=material.GetPassName(i);if(!material.GetShaderPassEnabled(name)||name.IndexOf("RayTracing",StringComparison.OrdinalIgnoreCase)>=0)continue;
     ShaderUtil.CompilePass(material,i,true);
     if(!ShaderUtil.IsPassCompiled(material,i)||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Source pass compilation refused: "+name);
     passes.Add(new Pass{material=Identity(sourceMaterials[Array.IndexOf(materials,material)]),shader=Identity(material.shader),index=i,name=name,compiled=true});
    }
   }
   var afterCompile=materials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();
   var sourcePriorAniso=QualitySettings.anisotropicFiltering;var scene=EditorSceneManager.NewPreviewScene();GameObject fixture=null,co=null,lo=null,vo=null;VolumeProfile profile=null;RenderTexture target=null;SourceOwnedDepthCaptureV65 sourceDepthPass=null;SourceOwnedUvCaptureV65 sourceUvPass=null;DebugDisplaySettings sourceDebugSettings=null;DebugLightingMode priorDebugLighting=DebugLightingMode.None;
   try {
    fixture=new GameObject("original-mesh-slot-control");fixture.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture,scene);
    fixture.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=fixture.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
    var bounds=renderer.bounds;float radius=bounds.extents.magnitude;if(!float.IsFinite(radius)||radius<=0)throw new Exception("Source bounds invalid");
    bool sourceRenderer=item.control_mode.StartsWith("source_");
    if(!new[]{"source_default","visible_default","visible_cast_off","source_shadowed","visible_shadowed"}.Contains(item.control_mode))throw new Exception("Unknown shadow-mode control");
    MeshRenderer actualRenderer=renderer;
    if(!sourceRenderer) {
     renderer.enabled=false;
     var part=UnityEngine.Object.Instantiate(mesh);part.hideFlags=HideFlags.HideAndDontSave;ownedMeshes.Add(part);
     for(int sub=1;sub<part.subMeshCount;sub++)part.SetSubMesh(sub,new SubMeshDescriptor(0,0,mesh.GetTopology(sub)),MeshUpdateFlags.DontRecalculateBounds);
     part.bounds=mesh.bounds;
     if(!part.GetIndices(0,true).SequenceEqual(mesh.GetIndices(0,true))||!part.vertices.SequenceEqual(mesh.vertices)||!part.normals.SequenceEqual(mesh.normals)||!part.tangents.SequenceEqual(mesh.tangents)||!part.uv.SequenceEqual(mesh.uv)||!part.GetVertexAttributes().SequenceEqual(mesh.GetVertexAttributes()))throw new Exception("Original selected geometry changed");
     var child=new GameObject("visible-original-submesh");child.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(fixture.transform,false);
     child.AddComponent<MeshFilter>().sharedMesh=part;actualRenderer=child.AddComponent<MeshRenderer>();actualRenderer.sharedMaterials=materials;
    }
    actualRenderer.shadowCastingMode=item.control_mode=="visible_cast_off"?ShadowCastingMode.Off:ShadowCastingMode.On;
    co=new GameObject("source-camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(co,scene);var camera=co.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.025f);camera.fieldOfView=30;camera.nearClipPlane=Mathf.Max(.0001f,radius*.01f);camera.farClipPlane=radius*12;camera.transform.position=bounds.center+new Vector3(1.4f,.8f,1.8f).normalized*radius*4;camera.transform.LookAt(bounds.center);camera.gameObject.AddComponent<HDAdditionalCameraData>();if(item.id.Contains("fixedbackground")){var extra=camera.GetComponent<HDAdditionalCameraData>();extra.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;extra.backgroundColorHDR=new Color(.025f,.025f,.025f,1);}
    lo=new GameObject("source-sun");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lo,scene);var light=lo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=15000;light.transform.rotation=camera.transform.rotation;var hdLight=lo.AddComponent<HDAdditionalLightData>();
    if(item.control_mode.EndsWith("_shadowed"))light.shadows=LightShadows.Soft;
    File.WriteAllText(Path.Combine(root,item.id+".light.json"),EditorJsonUtility.ToJson(light,true));
    File.WriteAllText(Path.Combine(root,item.id+".hdlight.json"),EditorJsonUtility.ToJson(hdLight,true));
    File.WriteAllText(Path.Combine(root,item.id+".control.txt"),"mode="+item.control_mode+"; light shadows="+light.shadows+"; renderer shadow casting="+actualRenderer.shadowCastingMode+"; source renderer="+sourceRenderer+"; original selected geometry exact");
    vo=new GameObject("source-volume");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(vo,scene);var volume=vo.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;var exposure=profile.Add<Exposure>();exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);var tone=profile.Add<Tonemapping>();tone.mode.Override(item.id.EndsWith("-linear")?TonemappingMode.None:TonemappingMode.Neutral);
    if(item.id.StartsWith("door-emission-dark")){
      light.intensity=0;
      var darkCamera=camera.GetComponent<HDAdditionalCameraData>();darkCamera.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;darkCamera.backgroundColorHDR=new Color(.025f,.025f,.025f,1);
      var darkIndirect=profile.Add<IndirectLightingController>();darkIndirect.indirectDiffuseLightingMultiplier.Override(0);darkIndirect.reflectionLightingMultiplier.Override(0);darkIndirect.reflectionProbeIntensityMultiplier.Override(0);
      var darkOcclusion=profile.Add<ScreenSpaceAmbientOcclusion>();darkOcclusion.intensity.Override(0);
      var darkFog=profile.Add<Fog>();darkFog.enabled.Override(false);darkFog.enableVolumetricFog.Override(false);
    }
    if(item.id.Contains("nopost")){
      var postCamera=camera.GetComponent<HDAdditionalCameraData>();postCamera.customRenderingSettings=true;
      postCamera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess,false);
      postCamera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess]=true;
    }
    if(item.id.Contains("nobloom")){
      var sourceBloom=profile.Add<Bloom>();sourceBloom.intensity.Override(0);
    }
    if(item.id.Contains("noaniso"))QualitySettings.anisotropicFiltering=AnisotropicFiltering.Disable;
    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);if(!target.Create())throw new Exception("Target creation failed");
    var render=new RenderPipeline.StandardRequest{destination=target};if(!RenderPipeline.SupportsRenderRequest(camera,render))throw new Exception("HDRP request unavailable");
    sourceDepthPass=new SourceOwnedDepthCaptureV65{sourceCamera=camera,compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV65.compute"),root=root,id=item.id};
    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceDepthPass);
    var sourceUvShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/SourceUvControlV65.shader");if(sourceUvShader==null||!sourceUvShader.isSupported||ShaderUtil.ShaderHasError(sourceUvShader))throw new Exception("Owned UV shader unresolved: "+(sourceUvShader==null?"null asset":string.Join(";",System.Array.ConvertAll(ShaderUtil.GetShaderMessages(sourceUvShader),message=>message.message))));
    sourceUvPass=new SourceOwnedUvCaptureV65{sourceCamera=camera,sourceMesh=mesh,probeMaterial=new Material(sourceUvShader){hideFlags=HideFlags.HideAndDontSave},root=root,id=item.id};
    
    var normalGuids=new string[]{"abc00000000004864883221994049297","abc00000000005543024739457366141"};
    for(int normalIndex=0;normalIndex<2;normalIndex++){
     string path=AssetDatabase.GUIDToAssetPath(normalGuids[normalIndex]);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
     if(texture==null||AssetDatabase.AssetPathToGUID(path)!=normalGuids[normalIndex])throw new Exception("Original normal texture identity missing");
     sourceUvPass.probeMaterial.SetTexture(normalIndex==0?"_ProbeNormal6":"_ProbeNormal7",texture);
     File.WriteAllText(Path.Combine(root,item.id+".normal-resource-"+normalIndex+".json"),JsonUtility.ToJson(new SourceNormalResourceV65{guid=normalGuids[normalIndex],path=path,width=texture.width,height=texture.height,mips=texture.mipmapCount,aniso=texture.anisoLevel,format=texture.graphicsFormat.ToString(),filter=texture.filterMode.ToString(),wrapU=texture.wrapModeU.ToString(),wrapV=texture.wrapModeV.ToString(),mipBias=texture.mipMapBias},true));
    }
CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceUvPass);
    for(int warm=0;warm<3;warm++){
      RenderPipeline.SubmitRenderRequest(camera,render);
      if(warm==0){
        var sourceDebugField=typeof(HDRenderPipeline).GetField("m_DebugDisplaySettings",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(sourceDebugField==null)throw new Exception("Pinned HDRP debug settings field absent");
        sourceDebugSettings=sourceDebugField.GetValue(RenderPipelineManager.currentPipeline) as DebugDisplaySettings;
        if(sourceDebugSettings==null)throw new Exception("Actual HDRP debug settings absent");
        priorDebugLighting=sourceDebugSettings.GetDebugLightingMode();
        if(priorDebugLighting!=DebugLightingMode.None)throw new Exception("Preserve unexpected preexisting debug lighting state");
        var requested=DebugLightingMode.None;
        if(item.id.StartsWith("door-debug-"))requested=(DebugLightingMode)Enum.Parse(typeof(DebugLightingMode),item.id.Substring(11).Replace("-nopost", "").Replace("-nobloom", "").Replace("-linear", ""));
        sourceDebugSettings.SetDebugLightingMode(requested);
        if(sourceDebugSettings.GetDebugLightingMode()!=requested)throw new Exception("Native requested HDRP debug mode was not applied");
      }
    }
    var row=new Row{id=item.id,mesh_identity=Identity(mesh),vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,material_slots=materials.Length,material_identities=sourceMaterials.Select(Identity).ToArray(),compiled_passes=passes.ToArray()};var draws=new List<Draw>();
    if(supported) {
     try {
      var enable=utility.GetMethod("SetEnabled",flags,null,new[]{typeof(bool),typeof(int)},null);if(enable==null)throw new Exception("Enable signature absent");enable.Invoke(null,new object[]{true,0});
      RenderPipeline.SubmitRenderRequest(camera,render);
      var events=utility.GetMethod("GetFrameEvents",flags)?.Invoke(null,null) as Array;row.event_count=events?.Length??0;
      var dataMethod=utility.GetMethods(flags).SingleOrDefault(m=>m.Name=="GetFrameEventData"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(int));
      if(dataMethod==null)throw new Exception("Event data signature absent");
      for(int index=0;index<row.event_count;index++) {
       var parameter=dataMethod.GetParameters()[1].ParameterType;if(parameter.IsByRef)parameter=parameter.GetElementType();
       var data=Activator.CreateInstance(parameter,true);object[] args={index,data};var ok=dataMethod.Invoke(null,args);if(ok is bool valid&&!valid)continue;data=args[1];
       var actualMesh=data.GetType().GetField("m_Mesh",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(data) as Mesh;if(actualMesh!=mesh)continue;
       draws.Add(new Draw{event_index=index,mesh_subset=Field(data,"m_MeshSubset"),pass_name=Field(data,"m_PassName"),original_shader=Field(data,"m_OriginalShaderName"),real_shader=Field(data,"m_RealShaderName"),component=Field(data,"m_ComponentEntityId"),index_count=Field(data,"m_IndexCount")});
      }
      row.native_draw_mapping_observed=draws.Count>0;
     }catch(Exception e){row.frame_debugger_error=e.ToString();}
     finally{utility.GetMethod("SetEnabled",flags,null,new[]{typeof(bool),typeof(int)},null)?.Invoke(null,new object[]{false,0});}
    } else row.frame_debugger_error="Installed native FrameDebugger locallySupported=false; no draw observation claimed";
    row.matching_mesh_events=draws.Count;row.draws=draws.ToArray();
    RenderPipeline.SubmitRenderRequest(camera,render);
    AsyncGPUReadback.WaitAllRequests();if(sourceDepthPass.submitted<1||sourceDepthPass.completed!=sourceDepthPass.submitted||sourceDepthPass.errors.Count!=0)throw new Exception("Actual depth capture incomplete: "+string.Join(";",sourceDepthPass.errors));
    if(sourceUvPass.submitted<2||sourceUvPass.completed!=sourceUvPass.submitted||sourceUvPass.errors.Count!=0)throw new Exception("Actual UV capture incomplete: "+string.Join(";",sourceUvPass.errors));
    var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",BindingFlags.Public|BindingFlags.NonPublic);if(historyType==null)throw new Exception("Pinned HDRP request history type absent");
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
    var sourceCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV65.compute");if(sourceCompute==null)throw new Exception("Owned source BRDF readback compute absent");
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
    File.WriteAllText(Path.Combine(root,item.id+".source-light.json"),EditorJsonUtility.ToJson(light,true));
    var sourceHdLight=light.GetComponent<HDAdditionalLightData>();
    if(sourceHdLight==null)throw new Exception("Actual additional light data absent");
    File.WriteAllText(Path.Combine(root,item.id+".source-hdlight.json"),EditorJsonUtility.ToJson(sourceHdLight,true));
    var sourceBuffers=new List<GlobalBufferCapture>();
    foreach(var property in new[]{"_GBufferTexture0","_GBufferTexture1","_GBufferTexture2","_GBufferTexture3","_CameraDepthTexture","_AmbientOcclusionTexture"}){
      var record=new GlobalBufferCapture{shader_property=property};sourceBuffers.Add(record);
      var actualTexture=Shader.GetGlobalTexture(property);record.present=actualTexture!=null;
      if(actualTexture==null){record.error="Global texture absent after request";continue;}
      record.texture_name=actualTexture.name;record.entity_id=actualTexture.GetEntityId().ToString();
      record.texture_dimension=actualTexture.dimension.ToString();record.graphics_format=actualTexture.graphicsFormat.ToString();
      record.width=actualTexture.width;record.height=actualTexture.height;record.layer_count=actualTexture is RenderTexture actualRt?actualRt.volumeDepth:1;
      if((actualTexture.dimension!=TextureDimension.Tex2D&&actualTexture.dimension!=TextureDimension.Tex2DArray)||actualTexture.width<1||actualTexture.height<1||record.layer_count<1||record.layer_count>8||actualTexture.width*actualTexture.height*record.layer_count>4194304){record.error="Unsupported or unbounded global layout";continue;}
      ComputeBuffer actualBuffer=null;
      try{
        actualBuffer=new ComputeBuffer(record.width*record.height*record.layer_count,16);var captureKernel=actualTexture.dimension==TextureDimension.Tex2DArray?sourceCompute.FindKernel("ReadArray"):sourceKernel;
        sourceCompute.SetTexture(captureKernel,actualTexture.dimension==TextureDimension.Tex2DArray?"SourceArray":"SourceFgd",actualTexture);sourceCompute.SetBuffer(captureKernel,"Output",actualBuffer);
        sourceCompute.SetInt("Width",record.width);sourceCompute.SetInt("Height",record.height);
        sourceCompute.Dispatch(captureKernel,(record.width+7)/8,(record.height+7)/8,record.layer_count);
        var bufferValues=new Vector4[record.width*record.height*record.layer_count];actualBuffer.GetData(bufferValues);
        using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+"."+property+".rgba32f"),FileMode.CreateNew))){
          foreach(var vector in bufferValues)for(int ch=0;ch<4;ch++){var value=vector[ch];if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite source global readback");writer.Write(value);}
        }
        record.float_count=bufferValues.Length*4;record.gpu_loaded=true;
      }catch(Exception captureError){record.error=captureError.ToString();}
      finally{if(actualBuffer!=null)actualBuffer.Release();}
    }
    File.WriteAllText(Path.Combine(root,item.id+".global-buffers.json"),JsonUtility.ToJson(new GlobalBufferInventory{id=item.id,actual_viewport_width=sourceHdCamera.actualWidth,actual_viewport_height=sourceHdCamera.actualHeight,buffers=sourceBuffers.ToArray()},true));
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
    File.WriteAllText(Path.Combine(root,item.id+".context.json"),JsonUtility.ToJson(new Context{
      id=item.id,actual_postprocess_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.Postprocess),actual_color_grading_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.ColorGrading),actual_exposure_control_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.ExposureControl),actual_color_buffer_format=((HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).currentPlatformRenderPipelineSettings.colorBufferFormat.ToString(),exposure_uses_fetched=(bool)exposureStateType.GetField("useFetchedExposure").GetValue(exposureState),exposure_uses_current_camera=(bool)exposureStateType.GetField("useCurrentCamera").GetValue(exposureState),exposure_fetched=(float)exposureStateType.GetField("fetchedGpuExposure").GetValue(exposureState),exposure_texture_rgba=exposureValues,exposure_texture_present=exposurePresent,dark_source_control=item.id.StartsWith("door-emission-dark"),actual_fog_enabled=sourceHdCamera.volumeStack.GetComponent<Fog>().enabled.value,actual_debug_lighting_mode=sourceDebugSettings.GetDebugLightingMode().ToString(),actual_ssao_intensity=activeOcclusion.intensity.value,actual_ssao_radius=activeOcclusion.radius.value,actual_ssao_specular_blend=activeOcclusion.specularOcclusion.value,actual_indirect_diffuse_multiplier=activeIndirect.indirectDiffuseLightingMultiplier.value,actual_reflection_multiplier=activeIndirect.reflectionLightingMultiplier.value,actual_global_ao_parameters=Shader.GetGlobalVector("_AmbientOcclusionParam"),actual_hdri_width=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.width,actual_hdri_mip_count=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.mipmapCount,actual_hdri_exposure=activeHdri.exposure.value,actual_hdri_multiplier=activeHdri.multiplier.value,actual_hdri_intensity_mode=activeHdri.skyIntensityMode.value.ToString(),actual_lit_shader_mode=sourceHdCamera.frameSettings.litShaderMode.ToString(),actual_ssao_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSAO),actual_ssr_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR),actual_hdrp_gpu_projection=sourceHdCamera.mainViewConstants.projMatrix,actual_fgd_format=sourceFgd.graphicsFormat.ToString(),actual_hdri_source=activeHdri.hdriSky.value==null?"null":AssetDatabase.GetAssetPath(activeHdri.hdriSky.value),sky_cubemap_count=skyArray.cubemapCount,sky_mip_count=skyArray.mipmapCount,sky_source_format=skyArray.format.ToString(),sky_layer_counts=skyLayerCounts,sky_layer_bytes=skyLayerBytes,actual_gpu_ambient_coefficients=gpuCoefficients,ambient_buffer_count=gpuProbe.count,ambient_buffer_stride=gpuProbe.stride,ambient_probe=ambientCoefficients,render_settings_ambient_probe=renderSettingsCoefficients,actual_hdrp_probe_captured=true,actual_camera_history="CustomUserHistory0-existing",actual_sky_reflection=reflectionDescription,active_sky_type=activeVisual.skyType.value,target_samples=target.antiAliasing,hdrp_clear_color_mode=camera.GetComponent<HDAdditionalCameraData>().clearColorMode.ToString(),bounds_center=bounds.center,bounds_size=bounds.size,radius=radius,
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
    var prior=RenderTexture.active;Texture2D raw=null,png=null;
    try {
     RenderTexture.active=target;raw=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);raw.ReadPixels(new Rect(0,0,512,512),0,0);raw.Apply();var colors=raw.GetPixels();foreach(var c in colors)if(!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b))throw new Exception("Nonfinite pixels");
     row.max_linear_channel=colors.Max(c=>Math.Max(c.r,Math.Max(c.g,c.b)));row.raw_image=Path.Combine(root,item.id+".rgba16f");File.WriteAllBytes(row.raw_image,raw.GetRawTextureData<byte>().ToArray());
     for(int i=0;i<colors.Length;i++)colors[i]=new Color(Mathf.LinearToGammaSpace(colors[i].r),Mathf.LinearToGammaSpace(colors[i].g),Mathf.LinearToGammaSpace(colors[i].b),1);
     png=new Texture2D(512,512,TextureFormat.RGBA32,false,false);png.SetPixels(colors);png.Apply();row.image=Path.Combine(root,item.id+".png");File.WriteAllBytes(row.image,png.EncodeToPNG());
    }finally{RenderTexture.active=prior;if(raw!=null)UnityEngine.Object.DestroyImmediate(raw);if(png!=null)UnityEngine.Object.DestroyImmediate(png);}
    var afterRender=materials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();
    row.material_before_paths=new string[materials.Length];row.material_after_compile_paths=new string[materials.Length];row.material_after_render_paths=new string[materials.Length];
    for(int i=0;i<materials.Length;i++) {
     row.material_before_paths[i]=Path.Combine(root,item.id+".material-"+i+".before.json");File.WriteAllText(row.material_before_paths[i],before[i]);
     row.material_after_compile_paths[i]=Path.Combine(root,item.id+".material-"+i+".after-compile.json");File.WriteAllText(row.material_after_compile_paths[i],afterCompile[i]);
     row.material_after_render_paths[i]=Path.Combine(root,item.id+".material-"+i+".after-render.json");File.WriteAllText(row.material_after_render_paths[i],afterRender[i]);
     if(before[i]!=afterRender[i])row.source_material_runtime_states_changed++;
     if(sourceBefore[i]!=EditorJsonUtility.ToJson(sourceMaterials[i],true))throw new Exception("Original source material runtime changed despite private instance");
    }
    row.source_material_runtime_states_restored=false;row.original_material_runtime_states_unchanged=true;
    rows.Add(row);File.WriteAllText(Path.Combine(root,"PROGRESS.v1.json"),JsonUtility.ToJson(new Result{gpu=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline.GetType().FullName,frame_debugger_locally_supported=supported,source_objects_unchanged=true,rows=rows.ToArray()},true));
   }finally{if(sourceUvPass!=null){CustomPassVolume.UnregisterGlobalCustomPass(sourceUvPass);AsyncGPUReadback.WaitAllRequests();sourceUvPass.ReleaseOwned();}QualitySettings.anisotropicFiltering=sourcePriorAniso;if(sourceDepthPass!=null)CustomPassVolume.UnregisterGlobalCustomPass(sourceDepthPass);if(sourceDebugSettings!=null)sourceDebugSettings.SetDebugLightingMode(priorDebugLighting);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}foreach(var obj in new[]{fixture,co,lo,vo})if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);if(profile!=null)UnityEngine.Object.DestroyImmediate(profile);EditorSceneManager.ClosePreviewScene(scene);}
  }
  File.WriteAllText(request.output,JsonUtility.ToJson(new Result{gpu=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline.GetType().FullName,frame_debugger_locally_supported=supported,source_objects_unchanged=true,rows=rows.ToArray()},true));
  }finally{foreach(var owned in ownedMeshes)UnityEngine.Object.DestroyImmediate(owned);foreach(var owned in ownedMaterials)UnityEngine.Object.DestroyImmediate(owned);ShaderUtil.allowAsyncCompilation=priorAsync;}
 }
}

public sealed class SourceOwnedDepthCaptureV65 : CustomPass {
 public Camera sourceCamera;public ComputeShader compute;public string root,id;
 public int submitted,completed;public List<string> errors=new List<string>();
 [Serializable] public class DepthRecord {public string id,texture_name,format,dimension;public int capture,width,height,physical_width,physical_height,layers;public Matrix4x4 projection;public bool captured_inside_actual_camera_pass=true,geometry_parity_qualified=false;}
 protected override void Execute(CustomPassContext ctx){
  if(ctx.hdCamera.camera!=sourceCamera)return;
  var actual=ctx.cameraDepthBuffer.rt;if(actual==null||actual.dimension!=TextureDimension.Tex2DArray)throw new Exception("Expected actual HDRP array depth");
  int width=ctx.hdCamera.actualWidth,height=ctx.hdCamera.actualHeight,index=submitted++;
  if(width!=512||height!=512||actual.volumeDepth!=1)throw new Exception("Depth viewport differs from pinned fixture");
  var output=new ComputeBuffer(width*height,4);int kernel=compute.FindKernel("ReadDepth");
  ctx.cmd.SetComputeTextureParam(compute,kernel,"SourceDepth",ctx.cameraDepthBuffer);
  ctx.cmd.SetComputeBufferParam(compute,kernel,"DepthOutput",output);
  ctx.cmd.SetComputeIntParam(compute,"Width",width);ctx.cmd.SetComputeIntParam(compute,"Height",height);
  ctx.cmd.DispatchCompute(compute,kernel,(width+7)/8,(height+7)/8,1);
  var path=Path.Combine(root,id+".depth-"+index+".r32f");
  File.WriteAllText(Path.Combine(root,id+".depth-"+index+".json"),JsonUtility.ToJson(new DepthRecord{id=id,capture=index,width=width,height=height,physical_width=actual.width,physical_height=actual.height,layers=actual.volumeDepth,texture_name=actual.name,format=actual.graphicsFormat.ToString(),dimension=actual.dimension.ToString(),projection=ctx.hdCamera.mainViewConstants.projMatrix},true));
  ctx.cmd.RequestAsyncReadback(output,request=>{
   try{if(request.hasError)throw new Exception("Depth GPU readback failed");var values=request.GetData<float>();if(values.Length!=width*height)throw new Exception("Depth size differs");
    using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(var value in values){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite depth");writer.Write(value);}completed++;
   }catch(Exception error){errors.Add(error.ToString());}finally{output.Release();}
  });
 }
}

public sealed class SourceOwnedUvCaptureV65 : CustomPass {
 public Camera sourceCamera;public Mesh sourceMesh;public Material probeMaterial;public string root,id;
 public int submitted,completed;public List<string> errors=new List<string>();
 private List<RenderTexture> targets=new List<RenderTexture>();
 [Serializable] public class UvRecord {public string id,format;public int capture,mode,width,height,viewportWidth,viewportHeight;public bool actual_opaque_depth_bound=true;public Matrix4x4 projection;public bool isolated_original_lod0_submesh0=true,full_import=false;}
 protected override void Execute(CustomPassContext ctx){
  if(ctx.hdCamera.camera!=sourceCamera)return;
  int viewportWidth=ctx.hdCamera.actualWidth,viewportHeight=ctx.hdCamera.actualHeight;int width=ctx.cameraDepthBuffer.rt.width,height=ctx.cameraDepthBuffer.rt.height;
  if(viewportWidth!=512||viewportHeight!=512||ctx.cameraDepthBuffer.rt.dimension!=TextureDimension.Tex2DArray||ctx.cameraDepthBuffer.rt.volumeDepth!=1)throw new Exception("UV viewport/depth layout differs");
  for(int mode=0;mode<6;mode++){
   int index=submitted++;var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){hideFlags=HideFlags.HideAndDontSave,dimension=TextureDimension.Tex2DArray,volumeDepth=1};
   if(!target.Create())throw new Exception("Private float32 UV target creation failed");targets.Add(target);
   var properties=new MaterialPropertyBlock();properties.SetFloat("_UvProbeMode",mode);
   ctx.cmd.SetRenderTarget(new RenderTargetIdentifier(target,0,CubemapFace.Unknown,0),ctx.cameraDepthBuffer.nameID);ctx.cmd.SetViewport(new Rect(0,0,viewportWidth,viewportHeight));ctx.cmd.ClearRenderTarget(false,true,Color.clear);
   ctx.cmd.DrawMesh(sourceMesh,Matrix4x4.identity,probeMaterial,0,0,properties);
   string path=Path.Combine(root,id+".gradient-mode-"+index+".rgba32f");
   File.WriteAllText(Path.Combine(root,id+".gradient-mode-"+index+".json"),JsonUtility.ToJson(new UvRecord{id=id,capture=index,mode=mode,width=width,height=height,viewportWidth=viewportWidth,viewportHeight=viewportHeight,format=target.graphicsFormat.ToString(),projection=ctx.hdCamera.mainViewConstants.projMatrix},true));
   ctx.cmd.RequestAsyncReadback(target,0,request=>{
    try{if(request.hasError)throw new Exception("UV float32 GPU readback failed");var values=request.GetData<float>();if(values.Length!=width*height*4)throw new Exception("UV layout differs");
     using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(var value in values){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite UV capture");writer.Write(value);}completed++;
    }catch(Exception error){errors.Add(error.ToString());}
   });
  }
 }
 public void ReleaseOwned(){foreach(var target in targets){target.Release();UnityEngine.Object.DestroyImmediate(target);}targets.Clear();if(probeMaterial!=null)UnityEngine.Object.DestroyImmediate(probeMaterial);}
}

[Serializable] public class SourceNormalResourceV65 {public string guid,path,format,filter,wrapU,wrapV;public int width,height,mips,aniso;public float mipBias;public bool original_resource=true,full_import=false;}
