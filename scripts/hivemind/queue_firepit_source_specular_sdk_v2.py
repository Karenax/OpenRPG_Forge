"""Read the real HDRP camera CBuffer rather than unbound global scalar properties."""
from pathlib import Path
BASE=Path(__file__).with_name('queue_firepit_source_specular_sdk_v1.py')
source=BASE.read_text('utf-8').replace('firepit-source-specular-sdk-v1','firepit-source-specular-sdk-v2').replace('SourceSpecularSdkV1','SourceSpecularSdkV2').replace('SourceSpecularSdkInputV1','SourceSpecularSdkInputV2').replace('SourceSpecularSdkMetadataV1','SourceSpecularSdkMetadataV2')
old='float sdkBlend=Shader.GetGlobalFloat("_SpecularOcclusionBlend"),sdkFade=Shader.GetGlobalFloat("_SpecularFade");'
new='''var sdkPipeline=RenderPipelineManager.currentPipeline;
     var sdkCbField=sdkPipeline.GetType().GetField("m_ShaderVariablesGlobalCB",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
     if(sdkCbField==null)throw new Exception("Source live ShaderVariablesGlobal field absent");
     var sdkCb=sdkCbField.GetValue(sdkPipeline);if(sdkCb==null)throw new Exception("Source live ShaderVariablesGlobal value absent");
     var sdkBlendField=sdkCb.GetType().GetField("_SpecularOcclusionBlend");var sdkFadeField=sdkCb.GetType().GetField("_SpecularFade");
     if(sdkBlendField==null||sdkFadeField==null)throw new Exception("Source specular fields absent from live CBuffer");
     float sdkBlend=(float)sdkBlendField.GetValue(sdkCb),sdkFade=(int)sdkFadeField.GetValue(sdkCb);
     if(!float.IsFinite(sdkBlend)||sdkBlend<0||sdkBlend>1||(sdkFade!=0&&sdkFade!=1))throw new Exception("Unexpected live source specular parameters");'''
assert source.count(old)==1;source=source.replace(old,new)
source=source.replace('public bool sdk_functions_gpu_executed,','public bool source_parameters_from_live_pipeline_cbuffer,sdk_functions_gpu_executed,')
source=source.replace('sdk_functions_gpu_executed=true,','source_parameters_from_live_pipeline_cbuffer=true,sdk_functions_gpu_executed=true,')
source=source.replace("'PreIntegratedFGD/PreIntegratedFGD.hlsl']","'PreIntegratedFGD/PreIntegratedFGD.hlsl']")
exec(compile(source,str(BASE),'exec'),globals())
