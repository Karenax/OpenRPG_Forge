"""Compare explicit numeric raster coverage on the source D3D11 backend."""
import hashlib,json,os,subprocess,sys
from pathlib import Path
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-punctual-source-static-caster-depth-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
ASSET=PROJECT/'Assets/Editor/OpenRPGStaticCasterDepthControlV1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(n,v):(ROOT/n).write_text(json.dumps(v,indent=2,ensure_ascii=False),encoding='utf-8')
CS=r"""using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Experimental.Rendering;
public static class OpenRPGStaticCasterDepthControlV1 {
 [Serializable] public class Receipt {public string requests_json;}
 [Serializable] public class Viewport {public float x,y,width,height;}
 [Serializable] public class Row {public Viewport dynamicAtlasViewport;}
 [Serializable] public class Rows {public Row[] rows;}
 [Serializable] public class Submesh {public int[] indices;}
 [Serializable] public class Caster {public Vector3[] positions;public Submesh[] submeshes;}
 [Serializable] public class Geometry {public Caster[] renderers;}
 [Serializable] public class Split {public float[] clip_from_world;}
 [Serializable] public class Splits {public Split[] splits;}
 [Serializable] public class Result {public string gpu,backend,format;public int width,height,active_draws,inactive_draws;}
 public static void Export(){
 string root=Environment.GetEnvironmentVariable("OPENRPG_D16_CONTROL_OUT");
 var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText("F:/OpenRPG_ImportWork/firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-RASTER-0.json"));
 var rows=JsonUtility.FromJson<Rows>("{\"rows\":"+receipt.requests_json+"}").rows;if(rows.Length!=6)throw new Exception("Expected six viewports");
 AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
 var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/OpenRPGStaticCasterDepthControlV1/Control.shader");
 var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/OpenRPGStaticCasterDepthControlV1/ReadDepth.compute");
 if(shader==null||!shader.isSupported||compute==null)throw new Exception("Control shader unavailable");
 var desc=new RenderTextureDescriptor(4096,4096){graphicsFormat=GraphicsFormat.None,depthStencilFormat=GraphicsFormat.D16_UNorm,msaaSamples=1,useMipMap=false};
 var rt=new RenderTexture(desc);if(!rt.Create()||rt.depthStencilFormat!=GraphicsFormat.D16_UNorm)throw new Exception("Actual D16 required");
 var material=new Material(shader);var buffer=new ComputeBuffer(4096*4096,4);int kernel=compute.FindKernel("ReadDepth");
 var geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText("F:/OpenRPG_ImportWork/firepit-source-caster-geometry-v1/SOURCE-CASTER-GEOMETRY-0.json"));
 var splits=JsonUtility.FromJson<Splits>(File.ReadAllText("F:/OpenRPG_ImportWork/firepit-source-point-shadow-raster-v3/SOURCE-CASTER-SPLITS-0.json"));
 var caster=geometry.renderers[0];if(caster.positions.Length!=3381||splits.splits.Length!=6)throw new Exception("Original caster streams required");
 var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=caster.positions;mesh.subMeshCount=caster.submeshes.Length;
 for(int i=0;i<caster.submeshes.Length;i++)mesh.SetIndices(caster.submeshes[i].indices,MeshTopology.Triangles,i);mesh.RecalculateBounds();

 try {foreach(bool active in new[]{true,false}){
 var cmd=new CommandBuffer();cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,false,Color.clear,1f);
 cmd.SetGlobalDepthBias(1f,2.5f);int faceIndex=0;if(active)foreach(var row in rows){var v=row.dynamicAtlasViewport;var props=new MaterialPropertyBlock();var values=splits.splits[faceIndex++].clip_from_world;var matrix=new Matrix4x4();for(int col=0;col<4;col++)for(int r=0;r<4;r++)matrix[r,col]=values[col*4+r];props.SetMatrix("_SourceClip",matrix);props.SetVector("_SourceCamera",new Vector4(2.4390344619750977f,1.6617727279663086f,-2.422929286956787f,0));cmd.SetViewport(new Rect(v.x,v.y,v.width,v.height));for(int sm=0;sm<mesh.subMeshCount;sm++)cmd.DrawMesh(mesh,Matrix4x4.identity,material,sm,0,props);}
 cmd.SetGlobalDepthBias(0,0);
 cmd.SetComputeTextureParam(compute,kernel,"Depth",rt);cmd.SetComputeBufferParam(compute,kernel,"Values",buffer);cmd.DispatchCompute(compute,kernel,512,512,1);
 Graphics.ExecuteCommandBuffer(cmd);cmd.Release();var read=AsyncGPUReadback.Request(buffer);read.WaitForCompletion();if(read.hasError)throw new Exception("Readback failed");
 var floats=read.GetData<float>().ToArray();byte[] bytes=new byte[floats.Length*4];Buffer.BlockCopy(floats,0,bytes,0,bytes.Length);File.WriteAllBytes(Path.Combine(root,active?"active-depth.float32":"inactive-depth.float32"),bytes);
 }
 File.WriteAllText(Path.Combine(root,"SOURCE-GPU.v1.json"),JsonUtility.ToJson(new Result{gpu=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),format=rt.depthStencilFormat.ToString(),width=rt.width,height=rt.height,active_draws=6,inactive_draws=0},true));
 }finally{buffer.Release();rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(mesh);}
 }
}
"""
SHADER=r"""Shader "Hidden/OpenRPG/StaticCasterDepthControlV1" {SubShader {Pass {Cull Back ZWrite On ZTest LEqual ZClip On
 HLSLPROGRAM
 #pragma vertex Vertex
 #pragma fragment Fragment
 #pragma target 4.5
 float4x4 _SourceClip;float4 _SourceCamera;
 float4 Vertex(float3 position:POSITION):SV_POSITION {return mul(_SourceClip,float4(position-_SourceCamera.xyz,1));}
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
 for rel in ['firepit-source-caster-geometry-v1/SOURCE-CASTER-GEOMETRY-0.json','firepit-source-point-shadow-raster-v3/SOURCE-CASTER-SPLITS-0.json']:
  p=W/rel;pins[str(p)]=sha(p)
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
   child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGStaticCasterDepthControlV1.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  for p,h in pins.items():assert sha(p)==h,p
  gpu=json.loads((ROOT/'SOURCE-GPU.v1.json').read_text());assert gpu['backend']=='Direct3D11' and gpu['format']=='D16_UNorm'
  state.update(status='numeric_source_d16_coverage_captured_comparison_pending',input_pins_unchanged=True)
 except Exception as e:state.update(status='numeric_source_d16_coverage_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
