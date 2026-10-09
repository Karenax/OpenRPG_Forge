"""Compare explicit numeric raster coverage on the source D3D11 backend."""
import hashlib,json,os,subprocess,sys
from pathlib import Path
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-punctual-atlas-source-coverage-v2'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
ASSET=PROJECT/'Assets/Editor/OpenRPGD16ViewportControlV2'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(n,v):(ROOT/n).write_text(json.dumps(v,indent=2,ensure_ascii=False),encoding='utf-8')
CS=r"""using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Experimental.Rendering;
public static class OpenRPGD16ViewportControlV2 {
 [Serializable] public class Receipt {public string requests_json;}
 [Serializable] public class Viewport {public float x,y,width,height;}
 [Serializable] public class Row {public Viewport dynamicAtlasViewport;}
 [Serializable] public class Rows {public Row[] rows;}
 [Serializable] public class Result {public string gpu,backend,format;public int width,height,active_draws,inactive_draws;}
 public static void Export(){
 string root=Environment.GetEnvironmentVariable("OPENRPG_D16_CONTROL_OUT");
 var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText("F:/OpenRPG_ImportWork/firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-RASTER-0.json"));
 var rows=JsonUtility.FromJson<Rows>("{\"rows\":"+receipt.requests_json+"}").rows;if(rows.Length!=6)throw new Exception("Expected six viewports");
 AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
 var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/OpenRPGD16ViewportControlV2/Control.shader");
 var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/OpenRPGD16ViewportControlV2/ReadDepth.compute");
 if(shader==null||!shader.isSupported||compute==null)throw new Exception("Control shader unavailable");
 var desc=new RenderTextureDescriptor(4096,4096){graphicsFormat=GraphicsFormat.None,depthStencilFormat=GraphicsFormat.D16_UNorm,msaaSamples=1,useMipMap=false};
 var rt=new RenderTexture(desc);if(!rt.Create()||rt.depthStencilFormat!=GraphicsFormat.D16_UNorm)throw new Exception("Actual D16 required");
 var material=new Material(shader);var buffer=new ComputeBuffer(4096*4096,4);int kernel=compute.FindKernel("ReadDepth");
 try {foreach(bool active in new[]{true,false}){
 var cmd=new CommandBuffer();cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,false,Color.clear,1f);
 if(active)foreach(var row in rows){var v=row.dynamicAtlasViewport;cmd.SetViewport(new Rect(v.x,v.y,v.width,v.height));cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3,1);}
 cmd.SetComputeTextureParam(compute,kernel,"Depth",rt);cmd.SetComputeBufferParam(compute,kernel,"Values",buffer);cmd.DispatchCompute(compute,kernel,512,512,1);
 Graphics.ExecuteCommandBuffer(cmd);cmd.Release();var read=AsyncGPUReadback.Request(buffer);read.WaitForCompletion();if(read.hasError)throw new Exception("Readback failed");
 var floats=read.GetData<float>().ToArray();byte[] bytes=new byte[floats.Length*4];Buffer.BlockCopy(floats,0,bytes,0,bytes.Length);File.WriteAllBytes(Path.Combine(root,active?"active-depth.float32":"inactive-depth.float32"),bytes);
 }
 File.WriteAllText(Path.Combine(root,"SOURCE-GPU.v1.json"),JsonUtility.ToJson(new Result{gpu=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),format=rt.depthStencilFormat.ToString(),width=rt.width,height=rt.height,active_draws=6,inactive_draws=0},true));
 }finally{buffer.Release();rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(material);}
 }
}
"""
SHADER=r"""Shader "Hidden/OpenRPG/D16ViewportControlV2" {SubShader {Pass {Cull Off ZWrite On ZTest LEqual ZClip On
 HLSLPROGRAM
 #pragma vertex Vertex
 #pragma fragment Fragment
 #pragma target 4.5
 float4 Vertex(uint id:SV_VertexID):SV_POSITION {float2 p[3]={float2(-1,-1),float2(3,-1),float2(-1,3)};return float4(p[id],0.25,1);}
 float4 Fragment():SV_TARGET{return 0;}
 ENDHLSL
}}}"""
COMPUTE=r"""#pragma kernel ReadDepth
Texture2D<float> Depth;RWStructuredBuffer<float> Values;
[numthreads(8,8,1)] void ReadDepth(uint3 id:SV_DispatchThreadID){Values[id.y*4096+id.x]=Depth.Load(int3(id.xy,0));}
"""
def prepare():
 assert not ROOT.exists() and not ASSET.exists();assert UNITY.is_file()
 assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 assert not list((PROJECT/'Assets/Hivemind').rglob('*.cs'))
 prev=json.loads((W/'firepit-source-point-shadow-raster-v3/PREPARED.v1.json').read_text(encoding='utf-8'))
 pins={**prev['input_pins'],**prev['private_before_pins']}
 for p,h in pins.items():assert sha(p)==h,p
 ROOT.mkdir();ASSET.mkdir()
 for name,content in [('Control.cs',CS),('Control.shader',SHADER),('ReadDepth.compute',COMPUTE)]:
  p=ASSET/name;p.write_text(content,encoding='utf-8');pins[str(p)]=sha(p)
 pins[str(Path(__file__).resolve())]=sha(Path(__file__).resolve())
 save('PREPARED.v1.json',{'input_pins':pins,'source_scripts_executed':False,'full_import':False})
 with (ROOT/'controller.log').open('xb') as out:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
 state={'status':'numeric_source_coverage_running','full_import':False}
 try:
  pins=json.loads((ROOT/'PREPARED.v1.json').read_text(encoding='utf-8'))['input_pins']
  for p,h in pins.items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_D16_CONTROL_OUT=str(ROOT))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGD16ViewportControlV2.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  for p,h in pins.items():assert sha(p)==h,p
  gpu=json.loads((ROOT/'SOURCE-GPU.v1.json').read_text());assert gpu['backend']=='Direct3D11' and gpu['format']=='D16_UNorm'
  state.update(status='numeric_source_d16_coverage_captured_comparison_pending',input_pins_unchanged=True)
 except Exception as e:state.update(status='numeric_source_d16_coverage_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
