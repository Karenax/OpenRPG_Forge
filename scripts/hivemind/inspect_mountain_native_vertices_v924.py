"""Measure held source mesh colors and typed SpeedTree parameters in Unity."""
from pathlib import Path
import ast,hashlib,json,os,subprocess,sys,traceback
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-native-vertex-diagnostic-v924'
R=Path(__file__).resolve().parents[2];PROJECT=W/'mountain-hdrp-capture-stage-v869/project'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
BROKER=R/'scripts/hivemind/queue_delta_firepit_particle_behavior_v4.py'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(v):
    v['utc']=datetime.now(timezone.utc).isoformat();(O/'RUN-RESULT.v1.json').write_text(json.dumps(v,indent=2),'utf-8')
HELPER=r'''using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;
public static class OpenRPGMountainVertexDiagnosticV924 {
 [Serializable] public class Request { public string id,prefab; }
 [Serializable] public class Batch { public Request[] cards;public string output; }
 [Serializable] public class Property { public string name,type,value,texture_path; }
 [Serializable] public class MaterialRow { public string path,shader;public string[] keywords;public Property[] properties; }
 [Serializable] public class Slot { public int submesh,unique_vertices,color_count,zero_color_r,zero_color_rgb;public Vector4 color_min,color_max;public MaterialRow material; }
 [Serializable] public class RendererRow { public string mesh_path;public long mesh_file_id;public string mesh_guid;public int vertex_count;public bool readable;public Slot[] slots; }
 [Serializable] public class Card { public string id;public RendererRow[] renderers;public bool authored_objects_unchanged; }
 [Serializable] public class Result { public string unity_version;public Card[] cards;public bool source_mutations=false; }
 static MaterialRow InspectMaterial(Material m) {
  var rows=new List<Property>();var shader=m.shader;
  for(int i=0;i<shader.GetPropertyCount();i++) {
   var type=shader.GetPropertyType(i);string name=shader.GetPropertyName(i);string value=null,path=null;
   if(type==UnityEngine.Rendering.ShaderPropertyType.Float||type==UnityEngine.Rendering.ShaderPropertyType.Range||type==UnityEngine.Rendering.ShaderPropertyType.Int)value=m.GetFloat(name).ToString("R",System.Globalization.CultureInfo.InvariantCulture);
   else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)value=JsonUtility.ToJson(m.GetVector(name));
   else if(type==UnityEngine.Rendering.ShaderPropertyType.Color)value=JsonUtility.ToJson(m.GetColor(name));
   else if(type==UnityEngine.Rendering.ShaderPropertyType.Texture){var texture=m.GetTexture(name);path=texture==null?null:AssetDatabase.GetAssetPath(texture);value=texture==null?"null":texture.name;}
   rows.Add(new Property{name=name,type=type.ToString(),value=value,texture_path=path});
  }
  return new MaterialRow{path=AssetDatabase.GetAssetPath(m),shader=shader.name,keywords=m.shaderKeywords,properties=rows.ToArray()};
 }
 public static void Inspect() {
  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--openrpg-manifest");if(index<0)throw new Exception("Manifest required");
  var batch=JsonUtility.FromJson<Batch>(File.ReadAllText(args[index+1]));var cards=new List<Card>();
  foreach(var request in batch.cards) {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab);if(prefab==null)throw new Exception("Missing prefab");
   var guards=new Dictionary<UnityEngine.Object,string>();guards[prefab]=EditorJsonUtility.ToJson(prefab,true);var renderers=new List<RendererRow>();
   foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true)) {
    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;if(mesh==null)throw new Exception("Missing mesh");guards[mesh]=EditorJsonUtility.ToJson(mesh,true);
    var colors=new List<Color>();mesh.GetColors(colors);if(colors.Count!=0&&colors.Count!=mesh.vertexCount)throw new Exception("Invalid colors");
    var slots=new List<Slot>();
    for(int submesh=0;submesh<mesh.subMeshCount;submesh++) {
     var material=renderer.sharedMaterials[submesh];if(material==null)throw new Exception("Missing material");guards[material]=EditorJsonUtility.ToJson(material,true);
     var vertices=mesh.GetIndices(submesh).Distinct().ToArray();var values=colors.Count==0?new Color[0]:vertices.Select(v=>colors[v]).ToArray();
     var lo=new Vector4(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);var hi=new Vector4(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
     foreach(var color in values){lo=Vector4.Min(lo,(Vector4)color);hi=Vector4.Max(hi,(Vector4)color);}
     if(values.Length==0){lo=Vector4.zero;hi=Vector4.zero;}
     slots.Add(new Slot{submesh=submesh,unique_vertices=vertices.Length,color_count=values.Length,zero_color_r=values.Count(c=>c.r==0),zero_color_rgb=values.Count(c=>c.r==0&&c.g==0&&c.b==0),color_min=lo,color_max=hi,material=InspectMaterial(material)});
    }
    string guid;long fileid;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out guid,out fileid))throw new Exception("Mesh identity required");
    renderers.Add(new RendererRow{mesh_path=AssetDatabase.GetAssetPath(mesh),mesh_guid=guid,mesh_file_id=fileid,vertex_count=mesh.vertexCount,readable=mesh.isReadable,slots=slots.ToArray()});
   }
   foreach(var guard in guards)if(EditorJsonUtility.ToJson(guard.Key,true)!=guard.Value)throw new Exception("Authored object changed");
   cards.Add(new Card{id=request.id,renderers=renderers.ToArray(),authored_objects_unchanged=true});
  }
  if(File.Exists(batch.output))throw new Exception("Preserve evidence");File.WriteAllText(batch.output,JsonUtility.ToJson(new Result{unity_version=Application.unityVersion,cards=cards.ToArray()},true));
 }
}'''
if len(sys.argv)==1:
    assert not O.exists();assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    visual=read(W/'mountain-capture-agent-review-v898/RESULT.v1.json');proof=read(W/'mountain-exact-capture-review-v897/RESULT.v1.json');rows={r['id']:r for r in proof['rows']}
    O.mkdir();(O/'temp').mkdir();helper=O/'MountainVertexDiagnosticV924.cs';helper.write_text(HELPER,'utf-8');target=PROJECT/'Assets/Editor'/helper.name;assert not target.exists();target.write_bytes(helper.read_bytes())
    manifest=O/'manifest.json';manifest.write_text(json.dumps(dict(cards=[dict(id=h['id'],prefab=rows[h['id']]['prefab_path']) for h in visual['visual_holds']],output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),UNITY,BROKER,helper,manifest]}
    for p in (PROJECT/'Assets').rglob('*'):
        if p.is_file() and p.suffix!='.meta' or p.is_file() and p.with_suffix('').suffix!='.cs':pins[str(p)]=sha(p)
    helpers={p.name for p in (PROJECT/'Assets').rglob('*.cs')}
    assert helpers=={'SourceLibraryThumbnailBatchV1.cs','PirateWoodLodMigrationV819.cs','SourceLibraryExactThumbnailBatchV873.cs','MountainParameterComparisonV901.cs','MountainParameterComparisonV904.cs','MountainParameterComparisonV908.cs','MountainParameterComparisonV912.cs','MountainVertexDiagnosticV924.cs'}
    assert not list((PROJECT/'Assets').rglob('*.dll'))
    sdk_proof=W/'mountain-speedtree-binding-review-v923/RESULT.v1.json'
    for p,d in read(sdk_proof)['input_pins'].items():assert sha(p)==d,p;pins[p]=d
    pins[str(sdk_proof)]=sha(sdk_proof)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,helpers=sorted(helpers),source_mutations=False),indent=2),'utf-8')
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=R,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid)),'utf-8');print(json.dumps(dict(status='native_vertex_diagnostic_started',pid=proc.pid)))
else:
    assert sys.argv[1:]==['run'];state=dict(status='native_vertex_preflight',full_import=False);broker=None;namespace=None
    try:
        pins=read(O/'PREPARED.v1.json')['input_pins']
        for p,d in pins.items():assert sha(p)==d,p
        tree=ast.parse(BROKER.read_text('utf-8-sig'));selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','start_broker','stop_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=UNITY.parent/'Data/Resources/PackageManager/Server/UnityPackageManager.exe',LOG_HANDLES=[]);exec(compile(ast.Module(body=selected,type_ignores=[]),str(BROKER)+'#owned-broker-only','exec'),namespace)
        broker,label=namespace['start_broker'](state);env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        command=[str(UNITY),'-batchmode','-quit','-force-d3d11','-job-worker-count','2','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGMountainVertexDiagnosticV924.Inspect','--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')]
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(command,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='measuring_native_speedtree_vertices',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
        state['input_changes']=[p for p,d in pins.items() if sha(p)!=d];assert state['unity_exit_code']==0 and not state['input_changes']
        result=read(O/'NATIVE-RESULT.v1.json');assert len(result['cards'])==3 and all(c['authored_objects_unchanged'] for c in result['cards'])
        state.update(status='native_speedtree_vertices_and_material_properties_measured',native_result_sha256=sha(O/'NATIVE-RESULT.v1.json'),input_pins=pins)
    except Exception as e:state.update(status='native_vertex_diagnostic_refusal_preserved',error=repr(e),traceback=traceback.format_exc())
    finally:
        if broker is not None:namespace['stop_broker'](broker,state)
        save(state)
