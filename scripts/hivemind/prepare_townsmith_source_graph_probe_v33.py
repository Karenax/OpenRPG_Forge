"""Extract exact generated graph blocks; capture TS normal and both blend weights."""
from pathlib import Path
import hashlib,json,subprocess,sys
s=Path(__file__).parent
w=Path('F:/OpenRPG_ImportWork')
generated=w/'townsmith-s-paint-sdk-generated-source-v2/S_Paint.generated.shader.txt'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
proof=json.loads((generated.parent/'PREPARED.v1.json').read_text(encoding='utf-8-sig'))
for p,h in proof['input_pins'].items():assert sha(Path(p))==h,p
original=generated.read_text(encoding='utf-8')
properties=original[original.index('Properties'):original.index('SubShader')]
# Restrict the extraction to the full graph META pass, not the reduced shadow graph.
meta=original[original.index('Name "META"'):original.index('Name "ScenePickingPass"')]
props=meta[meta.index('// -- Graph Properties'):meta.index('// -- Property used by ScenePickingPass')]
functions=meta[meta.index('// Graph Functions'):meta.index('// Graph Vertex')]
pixel=meta[meta.index('// Graph Pixel'):meta.index('// Build Graph Inputs')]
assert pixel.count('float3 NormalTS;')==1 and pixel.count('return surface;')==1
pixel=pixel.replace('float3 NormalTS;','float3 NormalTS;float4 ProbeBlend1;float4 ProbeBlend2;')
pixel=pixel.replace('return surface;','surface.ProbeBlend1=_Branch_f0070257ad7341d69f77b45153b5d674_Out_3_Vector4;surface.ProbeBlend2=_Branch_803da9c90eda4f52a2daef6e49a43b52_Out_3_Vector4;return surface;')
headers='''
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
struct SurfaceDescriptionInputs {float3 WorldSpaceNormal;float3 TangentSpaceNormal;float3 AbsoluteWorldSpacePosition;float4 uv0;float4 uv1;};
'''
draw='''
struct ProbeAttributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;};
struct ProbeVaryings {float4 positionCS:SV_POSITION;float3 positionRWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 uv0:TEXCOORD2;float4 uv1:TEXCOORD3;};
float _UvProbeMode;
ProbeVaryings Vert(ProbeAttributes i){ProbeVaryings o;o.positionRWS=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.positionRWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv0=i.uv0;o.uv1=i.uv1;return o;}
float4 Frag(ProbeVaryings i):SV_Target {
 SurfaceDescriptionInputs input=(SurfaceDescriptionInputs)0;input.WorldSpaceNormal=normalize(i.normalWS);input.TangentSpaceNormal=float3(0,0,1);input.AbsoluteWorldSpacePosition=GetAbsolutePositionWS(i.positionRWS);input.uv0=i.uv0;input.uv1=i.uv1;
 SurfaceDescription result=SurfaceDescriptionFunction(input);
 if(_UvProbeMode<0.5)return float4(result.NormalTS,1);if(_UvProbeMode<1.5)return result.ProbeBlend1;return result.ProbeBlend2;
}
'''
shader='Shader "Hidden/OpenRPG/OriginalGraphProbeV33" {\n'+properties+'\nSubShader {Tags {"RenderPipeline"="HDRenderPipeline"} Pass {Cull Back ZWrite Off ZTest Equal\nHLSLPROGRAM\n#pragma target 4.5\n#pragma vertex Vert\n#pragma fragment Frag\n'+headers+props+functions+pixel+draw+'\nENDHLSL\n}}}\n'
target=s/'queue_townsmith_source_graph_probe_v33.py';assert not target.exists()
q=(s/'queue_townsmith_fine_uv_source_v28.py').read_text(encoding='utf-8')
q=q.replace('v28','v33').replace('V28','V33').replace('townsmith-door-source-fine-uv-v33','townsmith-door-source-graph-probe-v33')
q=q.replace('.uv-control-','.graph-probe-')
start=q.index('    uv_shader.write_text(');end=q.index(",encoding='utf-8')",start)+len(",encoding='utf-8')")
q=q[:start]+'    uv_shader.write_text('+repr(shader)+",encoding='utf-8')"+q[end:]
anchor='CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceUvPass);'
assert q.count(anchor)==1
setup='''
    sourceUvPass.probeMaterial.CopyPropertiesFromMaterial(materials[0]);
    var originalShader=materials[0].shader;
    for(int property=0;property<originalShader.GetPropertyCount();property++){
     if(originalShader.GetPropertyType(property)!=ShaderPropertyType.Texture)continue;
     string name=originalShader.GetPropertyName(property);var texture=materials[0].GetTexture(name);
     if(sourceUvPass.probeMaterial.HasProperty(name))sourceUvPass.probeMaterial.SetTexture(name,texture);
    }
'''
q=q.replace(anchor,setup.replace('\n','\\n')+anchor)
q=q.replace('for p in [Path(__file__),CS,HELPER,compute_path,uv_shader,','for p in [Path(__file__),CS,HELPER,compute_path,uv_shader,Path('+repr(str(generated))+'),')
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
