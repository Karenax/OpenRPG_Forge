"""Read UV and screen derivatives into private float32 targets in the real camera."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_uv_derivatives_v24.py'
assert not target.exists()
text = (S / 'queue_townsmith_anisotropy_controls_v23.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-anisotropy-controls-v23', 'townsmith-door-source-uv-derivatives-v24')
text = text.replace('SourceTownsmithAnisotropyControlsV23', 'SourceTownsmithUvDerivativesV24')
text = text.replace('SourceTownsmithFgdReadbackV23', 'SourceTownsmithFgdReadbackV24')
text = text.replace('SourceOwnedDepthCaptureV23', 'SourceOwnedDepthCaptureV24')
shader = '''Shader "Hidden/OpenRPG/PrivateUvControlV24" {
SubShader { Tags {"RenderPipeline"="HDRenderPipeline"} Pass {
Cull Back ZWrite Off ZTest Always
HLSLPROGRAM
#pragma target 4.5
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
struct Attributes {float3 positionOS:POSITION;float4 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float4 uv:TEXCOORD0;};
float _UvProbeMode;
Varyings Vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS);o.uv=input.uv;return o;}
float4 Frag(Varyings input):SV_Target {if(_UvProbeMode<0.5)return float4(input.uv.xy,0,1);return float4(ddx(input.uv.xy),ddy(input.uv.xy));}
ENDHLSL
} } }
'''
custom = '''
public sealed class SourceOwnedUvCaptureV24 : CustomPass {
 public Camera sourceCamera;public Mesh sourceMesh;public Material probeMaterial;public string root,id;
 public int submitted,completed;public List<string> errors=new List<string>();
 private List<RenderTexture> targets=new List<RenderTexture>();
 [Serializable] public class UvRecord {public string id,format;public int capture,mode,width,height;public Matrix4x4 projection;public bool isolated_original_lod0_submesh0=true,full_import=false;}
 protected override void Execute(CustomPassContext ctx){
  if(ctx.hdCamera.camera!=sourceCamera)return;
  int width=ctx.hdCamera.actualWidth,height=ctx.hdCamera.actualHeight;
  if(width!=512||height!=512)throw new Exception("UV viewport differs");
  for(int mode=0;mode<2;mode++){
   int index=submitted++;var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){hideFlags=HideFlags.HideAndDontSave};
   if(!target.Create())throw new Exception("Private float32 UV target creation failed");targets.Add(target);
   var properties=new MaterialPropertyBlock();properties.SetFloat("_UvProbeMode",mode);
   ctx.cmd.SetRenderTarget(target);ctx.cmd.SetViewport(new Rect(0,0,width,height));ctx.cmd.ClearRenderTarget(false,true,Color.clear);
   ctx.cmd.DrawMesh(sourceMesh,Matrix4x4.identity,probeMaterial,0,0,properties);
   string path=Path.Combine(root,id+".uv-control-"+index+".rgba32f");
   File.WriteAllText(Path.Combine(root,id+".uv-control-"+index+".json"),JsonUtility.ToJson(new UvRecord{id=id,capture=index,mode=mode,width=width,height=height,format=target.graphicsFormat.ToString(),projection=ctx.hdCamera.mainViewConstants.projMatrix},true));
   ctx.cmd.RequestAsyncReadback(target,0,request=>{
    try{if(request.hasError)throw new Exception("UV float32 GPU readback failed");var values=request.GetData<float>();if(values.Length!=width*height*4)throw new Exception("UV layout differs");
     using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(var value in values){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite UV capture");writer.Write(value);}completed++;
    }catch(Exception error){errors.Add(error.ToString());}
   });
  }
 }
 public void ReleaseOwned(){foreach(var target in targets){target.Release();UnityEngine.Object.DestroyImmediate(target);}targets.Clear();if(probeMaterial!=null)UnityEngine.Object.DestroyImmediate(probeMaterial);}
}
'''
anchor = "    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')"
assert text.count(anchor) == 1
text = text.replace(anchor, "    uv_shader=PROJECT/'Assets/Editor/SourceUvControlV24.shader'\n    assert not uv_shader.exists()\n    uv_shader.write_text(" + repr(shader) + ",encoding='utf-8')\n" + anchor)
patch = '''
    text=text.replace('SourceOwnedDepthCaptureV24 sourceDepthPass=null;', 'SourceOwnedDepthCaptureV24 sourceDepthPass=null;SourceOwnedUvCaptureV24 sourceUvPass=null;')
    text=text.replace('    for(int warm=0;warm<3;warm++){', '    var sourceUvShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/SourceUvControlV24.shader");if(sourceUvShader==null||!sourceUvShader.isSupported||ShaderUtil.ShaderHasError(sourceUvShader))throw new Exception("Owned UV shader unresolved");\\n    sourceUvPass=new SourceOwnedUvCaptureV24{sourceCamera=camera,sourceMesh=mesh,probeMaterial=new Material(sourceUvShader){hideFlags=HideFlags.HideAndDontSave},root=root,id=item.id};\\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceUvPass);\\n    for(int warm=0;warm<3;warm++){')
    text=text.replace('    var historyType=typeof(HDCamera)', '    if(sourceUvPass.submitted<2||sourceUvPass.completed!=sourceUvPass.submitted||sourceUvPass.errors.Count!=0)throw new Exception("Actual UV capture incomplete: "+string.Join(";",sourceUvPass.errors));\\n    var historyType=typeof(HDCamera)')
    text=text.replace('}finally{QualitySettings.anisotropicFiltering=sourcePriorAniso;', '}finally{if(sourceUvPass!=null){CustomPassVolume.UnregisterGlobalCustomPass(sourceUvPass);AsyncGPUReadback.WaitAllRequests();sourceUvPass.ReleaseOwned();}QualitySettings.anisotropicFiltering=sourcePriorAniso;')
'''
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
text = text.replace(anchor, patch + '    text+=' + repr(custom) + '\n' + anchor)
text = text.replace('for p in [Path(__file__),CS,HELPER,compute_path,', 'for p in [Path(__file__),CS,HELPER,compute_path,uv_shader,')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
