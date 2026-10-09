"""Measure the actual SDK's Deferred storage and complete standard sky specular."""
from pathlib import Path
import itertools,json,math,runpy,sys

loaded=runpy.run_path(str(Path(__file__).with_name('queue_firepit_source_sky_reference_v1.py')),run_name='specular_sdk_probe')
g=loaded['g'];original_helper=loaded['original_helper'];popen=loaded['popen']
NORMALS=[[0,1,0],[1,1,1],[1,0,-1]]
VIEWS=[[0,1,0],[1,0.1,0.3],[0,-1,0]]
ROUGHNESS=[-1.0,0.0,0.25,0.5,0.75,1.0,2.0]
VISIBILITY=[0.25,0.6,1.0]
F0=[[0.04,0.04,0.04],[0.8,0.2,0.05]]
MATERIAL_AO=[0.25,1.0]
CASES=[]
for normal,view,rough,visibility,f0,ao in itertools.product(NORMALS,VIEWS,ROUGHNESS,VISIBILITY,F0,MATERIAL_AO):
    CASES.append(dict(case_id=f'specular-sdk-{len(CASES):04d}',normal=normal,view=view,
        authored_perceptual_roughness=rough,scene_visibility=visibility,fresnel0=f0,material_ao=ao))
assert len(CASES)==756

COMPUTE=r'''#pragma kernel EncodeCases
#pragma kernel EvaluateCases
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#define TEXTURE2D_X TEXTURE2D
#define LOAD_TEXTURE2D_X LOAD_TEXTURE2D
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"
SamplerState s_linear_clamp_sampler;
SamplerState s_trilinear_clamp_sampler;
float _SpecularFade;
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/PreIntegratedFGD/PreIntegratedFGD.hlsl"
StructuredBuffer<float4> InputNormalRoughness,InputViewVisibility,InputFresnelMaterialAo;
RWTexture2D<float4> PackedNormalOutput,PackedMaterialOutput;
Texture2D<float4> PackedNormalInput,PackedMaterialInput;
TextureCubeArray<float4> SourceSky;
RWStructuredBuffer<float4> Output;
uint CaseCount;
float SourceSpecularOcclusionBlend;
[numthreads(64,1,1)]
void EncodeCases(uint3 id:SV_DispatchThreadID) {
 if(id.x>=CaseCount)return;
 float4 nr=InputNormalRoughness[id.x],vv=InputViewVisibility[id.x],fa=InputFresnelMaterialAo[id.x];
 NormalData normal;normal.normalWS=normalize(nr.xyz);normal.perceptualRoughness=nr.w;
 float4 packed;EncodeIntoNormalBuffer(normal,packed);PackedNormalOutput[uint2(id.x,0)]=packed;
 float authoredNdV=ClampNdotV(dot(normalize(nr.xyz),normalize(vv.xyz)));
 float originalSpecularAo=GetSpecularOcclusionFromAmbientOcclusion(authoredNdV,fa.w,nr.w*nr.w);
 // Standard Lit: F0 in manually encoded sRGB RT2, source AO in packed RT0 alpha.
 PackedMaterialOutput[uint2(id.x,0)]=float4(FastLinearToSRGB(fa.xyz),PackFloatInt8bit(originalSpecularAo,0,2));
}
[numthreads(64,1,1)]
void EvaluateCases(uint3 id:SV_DispatchThreadID) {
 if(id.x>=CaseCount)return;
 float4 nr=InputNormalRoughness[id.x],vv=InputViewVisibility[id.x];
 float4 packed=PackedNormalInput.Load(int3(id.x,0,0)),material=PackedMaterialInput.Load(int3(id.x,0,0));
 NormalData normal;DecodeFromNormalBuffer(packed,normal);
 float3 f0=FastSRGBToLinear(material.xyz);float materialSpecularAo;uint unused;
 UnpackFloatInt8bit(material.w,2,materialSpecularAo,unused);
 float3 V=normalize(vv.xyz);float ndv=ClampNdotV(dot(normal.normalWS,V));
 float3 originalR=reflect(-V,normal.normalWS);
 float3 dominantR=GetSpecularDominantDir(normal.normalWS,originalR,normal.perceptualRoughness,ndv);
 float rough=PerceptualRoughnessToRoughness(normal.perceptualRoughness);
 float3 R=lerp(dominantR,originalR,saturate(smoothstep(0,1,rough*rough)));
 float lod=PerceptualRoughnessToMipmapLevel(normal.perceptualRoughness);
 float3 fgd;float diffuseFgd,reflectivity;float f90=ComputeF90(f0);
 GetPreIntegratedFGDGGXAndDisneyDiffuse(ndv,normal.perceptualRoughness,f0,f90,fgd,diffuseFgd,reflectivity);
 float screenAo=lerp(1.0,GetSpecularOcclusionFromAmbientOcclusion(ndv,vv.w,rough),SourceSpecularOcclusionBlend);
 float3 ao=GTAOMultiBounce(min(materialSpecularAo,screenAo),f0);
 float3 energy=1.0+f0*(1.0/reflectivity-1.0);
 float4 sky=SourceSky.SampleLevel(s_trilinear_clamp_sampler,float4(normalize(R),0),lod);
 uint start=id.x*9;
 Output[start]=packed;
 Output[start+1]=float4(normal.normalWS,normal.perceptualRoughness);
 Output[start+2]=float4(f0,materialSpecularAo);
 Output[start+3]=float4(R,lod);
 Output[start+4]=float4(fgd,reflectivity);
 Output[start+5]=float4(ao,diffuseFgd);
 Output[start+6]=float4(energy,ndv);
 Output[start+7]=sky;
 Output[start+8]=float4(fgd*sky.xyz*ao*energy,f90);
}
'''
META=r'''
[Serializable] public class SourceSpecularSdkInputV1 { public Vector4[] normalRoughness,viewVisibility,fresnelMaterialAo; }
[Serializable] public class SourceSpecularSdkMetadataV1 {
 public int view,cases,vectors_per_case,bytes;public string raw_sha256,source_history_channel,case_storage_format,actual_normal_buffer_format,actual_normal_buffer_name;
 public float source_specular_occlusion_blend,source_specular_fade;
 public bool sdk_functions_gpu_executed,normal_material_hardware_unorm_roundtrip,preexposure_applied,full_import;
}
'''
CAPTURE=r'''
    var caseRoot=Path.GetDirectoryName(request.output);
    var sdkInput=JsonUtility.FromJson<SourceSpecularSdkInputV1>(File.ReadAllText(Path.Combine(caseRoot,"SDK-INPUT.v1.json")));
    int sdkCount=sdkInput.normalRoughness.Length;
    if(sdkCount!=756||sdkInput.viewVisibility.Length!=sdkCount||sdkInput.fresnelMaterialAo.Length!=sdkCount)throw new Exception("Specular SDK case layout changed");
    var skyMethod=skyManager.GetType().GetMethod("GetSkyReflection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public,null,new[]{typeof(HDCamera)},null);
    var sdkSky=skyMethod?.Invoke(skyManager,new object[]{hc}) as CubemapArray;
    var sdkFgd=Shader.GetGlobalTexture("_PreIntegratedFGD_GGXDisneyDiffuse");
    if(sdkSky==null||sdkFgd==null||sdkFgd.width!=64||sdkFgd.height!=64)throw new Exception("Source SDK lighting resources absent");
    var sdkCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceSpecularSdkV1.compute");if(sdkCompute==null)throw new Exception("Source specular compute absent");
    var sdkNormal=new RenderTexture(sdkCount,1,0){graphicsFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,enableRandomWrite=true};
    var sdkMaterial=new RenderTexture(sdkCount,1,0){graphicsFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,enableRandomWrite=true};
    var sdkNr=new ComputeBuffer(sdkCount,16);var sdkVv=new ComputeBuffer(sdkCount,16);var sdkFa=new ComputeBuffer(sdkCount,16);var sdkOutput=new ComputeBuffer(sdkCount*9,16);
    try {
     if(!sdkNormal.Create()||!sdkMaterial.Create())throw new Exception("Source case UNORM storage unavailable");
     sdkNr.SetData(sdkInput.normalRoughness);sdkVv.SetData(sdkInput.viewVisibility);sdkFa.SetData(sdkInput.fresnelMaterialAo);
     int encode=sdkCompute.FindKernel("EncodeCases"),evaluate=sdkCompute.FindKernel("EvaluateCases");
     foreach(int kernel in new[]{encode,evaluate}){sdkCompute.SetBuffer(kernel,"InputNormalRoughness",sdkNr);sdkCompute.SetBuffer(kernel,"InputViewVisibility",sdkVv);sdkCompute.SetBuffer(kernel,"InputFresnelMaterialAo",sdkFa);}
     sdkCompute.SetInt("CaseCount",sdkCount);sdkCompute.SetTexture(encode,"PackedNormalOutput",sdkNormal);sdkCompute.SetTexture(encode,"PackedMaterialOutput",sdkMaterial);
     sdkCompute.Dispatch(encode,(sdkCount+63)/64,1,1);
     sdkCompute.SetTexture(evaluate,"PackedNormalInput",sdkNormal);sdkCompute.SetTexture(evaluate,"PackedMaterialInput",sdkMaterial);sdkCompute.SetTexture(evaluate,"_PreIntegratedFGD_GGXDisneyDiffuse",sdkFgd);sdkCompute.SetTexture(evaluate,"SourceSky",sdkSky);sdkCompute.SetBuffer(evaluate,"Output",sdkOutput);
     float sdkBlend=Shader.GetGlobalFloat("_SpecularOcclusionBlend"),sdkFade=Shader.GetGlobalFloat("_SpecularFade");
     sdkCompute.SetFloat("SourceSpecularOcclusionBlend",sdkBlend);sdkCompute.SetFloat("_SpecularFade",sdkFade);sdkCompute.Dispatch(evaluate,(sdkCount+63)/64,1,1);
     var sdkValues=new Vector4[sdkCount*9];sdkOutput.GetData(sdkValues);var sdkBytes=new byte[sdkValues.Length*16];
     for(int i=0;i<sdkValues.Length;i++){var v=sdkValues[i];if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||!float.IsFinite(v.w))throw new Exception("Nonfinite source SDK output at "+i);Buffer.BlockCopy(BitConverter.GetBytes(v.x),0,sdkBytes,i*16,4);Buffer.BlockCopy(BitConverter.GetBytes(v.y),0,sdkBytes,i*16+4,4);Buffer.BlockCopy(BitConverter.GetBytes(v.z),0,sdkBytes,i*16+8,4);Buffer.BlockCopy(BitConverter.GetBytes(v.w),0,sdkBytes,i*16+12,4);}
     var sdkPath=Path.Combine(caseRoot,"source-specular-sdk-"+view+".rgba32f");if(File.Exists(sdkPath))throw new Exception("Preserve previous SDK witness");File.WriteAllBytes(sdkPath,sdkBytes);
     var actualNormal=Shader.GetGlobalTexture("_NormalBufferTexture");
     var sdkMeta=new SourceSpecularSdkMetadataV1{view=view,cases=sdkCount,vectors_per_case=9,bytes=sdkBytes.Length,raw_sha256=Hash(sdkBytes),source_history_channel="CustomUserHistory0",case_storage_format=sdkNormal.graphicsFormat.ToString(),actual_normal_buffer_format=actualNormal==null?"unavailable":actualNormal.graphicsFormat.ToString(),actual_normal_buffer_name=actualNormal==null?"unavailable":actualNormal.name,source_specular_occlusion_blend=sdkBlend,source_specular_fade=sdkFade,sdk_functions_gpu_executed=true,normal_material_hardware_unorm_roundtrip=true,preexposure_applied=false,full_import=false};
     File.WriteAllText(sdkPath+".json",JsonUtility.ToJson(sdkMeta,true));
    }finally{sdkNr.Release();sdkVv.Release();sdkFa.Release();sdkOutput.Release();sdkNormal.Release();sdkMaterial.Release();UnityEngine.Object.DestroyImmediate(sdkNormal);UnityEngine.Object.DestroyImmediate(sdkMaterial);}
'''

def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceSpecularSdkV1')
    compute=g['PROJECT']/'Assets/Editor/SourceSpecularSdkV1.compute';assert not compute.exists();compute.write_text(COMPUTE,'utf-8')
    def vector(values):return dict(zip(['x','y','z','w'],values,strict=True))
    inputs=dict(normalRoughness=[vector([*c['normal'],c['authored_perceptual_roughness']]) for c in CASES],
        viewVisibility=[vector([*c['view'],c['scene_visibility']]) for c in CASES],
        fresnelMaterialAo=[vector([*c['fresnel0'],c['material_ao']]) for c in CASES])
    (g['ROOT']/'SDK-INPUT.v1.json').write_text(json.dumps(inputs,indent=2),'utf-8')
    (g['ROOT']/'SDK-CASES.v1.json').write_text(json.dumps(dict(cases=CASES,output_vectors_per_case=9,
        output_fields=['packed_normal','decoded_normal_roughness','decoded_fresnel_material_specular_ao','source_reflection_direction_lod','specular_fgd_reflectivity','multibounce_specular_ao_diffuse_fgd','energy_factor_ndotv','sampled_sky','reflected_light_f90'],
        source_authored_values_preserved=True,not_original_scene_pixel_measurements=True),indent=2),'utf-8')
    package=g['PROJECT']/'Library/PackageCache'
    sdk=[package/'com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary'/n for n in ['Common.hlsl','CommonLighting.hlsl','CommonMaterial.hlsl','Packing.hlsl','Color.hlsl','ImageBasedLighting.hlsl']]
    sdk += [package/'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material'/n for n in ['NormalBuffer.hlsl','MaterialEvaluation.hlsl','Lit/Lit.hlsl','PreIntegratedFGD/PreIntegratedFGD.hlsl']]
    paths=[compute,g['ROOT']/'SDK-INPUT.v1.json',g['ROOT']/'SDK-CASES.v1.json',*sdk]
    g['save']('SDK-PREPARED.v1.json',dict(input_pins={str(p):g['sha'](p) for p in paths},expected_cases=756))
    marker='var packedProbe=new Vector4[7];actualBuffer.GetData(packedProbe);';assert text.count(marker)==1
    return text.replace(marker,marker+'\n'+CAPTURE)+'\n'+META

def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceSpecularSdkV1.Export'
    return popen(args,*a,**kw)

g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-specular-sdk-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceSpecularSdkV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        prepared=g['read'](g['ROOT']/'SDK-PREPARED.v1.json')
        assert all(g['sha'](p)==h for p,h in prepared['input_pins'].items())
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=[g['ROOT']/f'source-specular-sdk-{view}.rgba32f{suffix}' for view in range(2) for suffix in ['', '.json']]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in files):
            state.update(status='native_source_deferred_specular_sdk_756_cases_gpu_captured',sdk_files={str(p):g['sha'](p) for p in files},sdk_inputs=prepared)
        else:state.update(status='source_deferred_specular_sdk_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
