Shader "Hidden/OpenRPG/PrivateSourceBranchV95" {
SubShader { Tags {"RenderPipeline"="HDRenderPipeline"} Pass {
Cull Off ZWrite Off ZTest Always
HLSLPROGRAM
#pragma target 4.5
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
float4 Vert(uint id:SV_VertexID):SV_POSITION {
 return float4(id==2?3.0:-1.0,id==1?3.0:-1.0,0.0,1.0);
}
float4 Frag():SV_Target {
 float variant=0.0;
#if defined(PROBE_VOLUMES_L2)
 variant=2.0;
#elif defined(PROBE_VOLUMES_L1)
 variant=1.0;
#endif
 return float4(float(_EnableProbeVolumes),float(_IndirectDiffuseMode),variant,1.0);
}
ENDHLSL
} }
}
