"""Close native rig/morph and draw cardinalities before static conversion."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,shutil,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'house-native-mesh-metadata-v1';CAP=W/'house-native-mesh-streams-v1'
PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
CODE=r'''
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;
public static class OpenRPGHouseMeshMetadataV1 {
 [Serializable]public class Wanted{public string guid,file_id,asset_path;}
 [Serializable]public class Request{public Wanted[] meshes;public string output;}
 [Serializable]public class Row{public string guid,file_id,asset_path;public int vertices,submeshes,bind_poses,blend_shapes;public long[] index_counts;public string[] topologies,vertex_formats;public Vector3 bounds_center,bounds_extents;}
 [Serializable]public class Result{public string unity_version;public Row[] meshes;public bool importer_changed=false,prefab_instantiated=false;}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--openrpg-manifest");if(arg<0)throw new Exception("Manifest required");var request=JsonUtility.FromJson<Request>(File.ReadAllText(args[arg+1]));var rows=new List<Row>();
  foreach(var wanted in request.meshes){
   if(AssetDatabase.AssetPathToGUID(wanted.asset_path)!=wanted.guid)throw new Exception("Source asset GUID mismatch");
   var meshes=AssetDatabase.LoadAllAssetsAtPath(wanted.asset_path).OfType<Mesh>().Where(m=>{string g;long f;return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out g,out f)&&g==wanted.guid&&f.ToString()==wanted.file_id;}).ToArray();if(meshes.Length!=1)throw new Exception("Exact native mesh absent");var mesh=meshes[0];
   var counts=new long[mesh.subMeshCount];var topologies=new string[mesh.subMeshCount];for(int i=0;i<counts.Length;i++){counts[i]=(long)mesh.GetIndexCount(i);topologies[i]=mesh.GetTopology(i).ToString();}
   rows.Add(new Row{guid=wanted.guid,file_id=wanted.file_id,asset_path=wanted.asset_path,vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,bind_poses=mesh.bindposeCount,blend_shapes=mesh.blendShapeCount,index_counts=counts,topologies=topologies,vertex_formats=mesh.GetVertexAttributes().Select(v=>v.attribute+":"+v.format+":"+v.dimension+":"+v.stream).ToArray(),bounds_center=mesh.bounds.center,bounds_extents=mesh.bounds.extents});
  }
  var path=Path.Combine(request.output,"NATIVE-RESULT.v1.json");if(File.Exists(path))throw new Exception("Preserve existing evidence");File.WriteAllText(path,JsonUtility.ToJson(new Result{unity_version=Application.unityVersion,meshes=rows.ToArray()},true));
 }
}
'''
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(name,data):
 p=ROOT/name;t=p.with_suffix(p.suffix+'.tmp');t.write_text(json.dumps(data,indent=2),'utf-8');t.replace(p)
def no_unity():assert subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=='0','Another Unity is live'
def prepare():
 assert not ROOT.exists();no_unity();assert read(W/'house-native-mesh-streams-reopen-v1/RESULT.v1.json')['meshes_verified']==48
 ROOT.mkdir();request=read(CAP/'REQUEST.v1.json');request['output']=str(ROOT);save('REQUEST.v1.json',request)
 helper=ROOT/'HouseMeshMetadataV1.cs';helper.write_text(CODE,'utf-8');dst=PROJECT/'Assets/Editor/HouseMeshMetadataV1.cs';assert not dst.exists();shutil.copyfile(helper,dst)
 pins=read(CAP/'PREPARED.v1.json')['input_pins'];pins.update({str(p):sha(p) for p in [Path(__file__).resolve(),helper,ROOT/'REQUEST.v1.json',CAP/'NATIVE-RESULT.v1.json',W/'house-native-mesh-streams-reopen-v1/RESULT.v1.json']});save('PREPARED.v1.json',dict(input_pins=pins,expected_meshes=48,full_import=False))
 with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,expected_meshes=48)))
def run():
 state=dict(status='capturing_48_house_mesh_rig_morph_draw_metadata',meshes_verified=0,full_import=False,geometry_converted=False)
 try:
  no_unity();pins=read(ROOT/'PREPARED.v1.json')['input_pins']
  for p,d in pins.items():assert sha(p)==d,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-nographics','-quit','-projectPath',str(PROJECT),'-executeMethod','OpenRPGHouseMeshMetadataV1.Export','--openrpg-manifest',str(ROOT/'REQUEST.v1.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state['unity_pid']=proc.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=proc.wait();assert proc.returncode==0
  native=read(ROOT/'NATIVE-RESULT.v1.json');assert not native['importer_changed'] and not native['prefab_instantiated']
  expected={(r['guid'],r['file_id']):r for r in read(ROOT/'REQUEST.v1.json')['meshes']};streams={(r['mesh_guid'],r['mesh_file_id']):r for r in read(CAP/'NATIVE-RESULT.v1.json')['meshes']};seen=set()
  for row in native['meshes']:
   key=(row['guid'],row['file_id']);assert key in expected and key not in seen;seen.add(key);e=expected[key];s=streams[key]
   assert row['vertices']==e['vertices']==s['vertices'] and row['submeshes']==s['submeshes'] and row['asset_path']==e['asset_path']
   assert row['vertex_formats']==e['original_vertex_formats']
   assert row['bind_poses']>=0 and row['blend_shapes']>=0
   assert len(row['index_counts'])==len(row['topologies'])==row['submeshes']
   for i,count in enumerate(row['index_counts']):assert row['topologies'][i]=='Triangles' and count*4==(Path(s['directory'])/f'submesh-{i}.i32').stat().st_size
  assert len(seen)==48
  for p,d in pins.items():assert sha(p)==d,p
  state.update(status='48_house_native_mesh_metadata_reopened_against_verified_streams',meshes_verified=48,static_adapter_candidates=sum(r['bind_poses']==0 and r['blend_shapes']==0 for r in native['meshes']),rig_or_morph_candidates=sum(r['bind_poses']>0 or r['blend_shapes']>0 for r in native['meshes']),input_pins=pins,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),source_inputs_unchanged=True)
 except Exception as exc:state.update(status='house_mesh_metadata_refusal_retained',error=repr(exc))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
