from pathlib import Path
import datetime,json,hashlib,os,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'rocks-firepit-native-component-state-v1';PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
HELPER=PROJECT/'Assets/Editor/RocksFirePitComponentStateV1.cs'
CODE=r"""
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;
namespace OpenRPGRocksFirePitComponentStateV1 {
public static class Capture {
[Serializable]public class Identity{public string guid;public string file_id;public string path;public string type;}
[Serializable]public class RendererState{public Identity identity;public bool enabled;public bool force_rendering_off;public int shadow_casting_mode;public bool receive_shadows;}
[Serializable]public class Level{public float screen_relative_height;public float fade_transition_width;public Identity[] renderers;}
[Serializable]public class LODState{public Identity identity;public bool enabled;public float size;public float[] local_reference_point;public int fade_mode;public bool animate_cross_fading;public Level[] levels;}
[Serializable]public class SerializedValue{public string property_path;public string property_type;public double number;public bool boolean;public string text;public float[] vector;public Identity reference;}
[Serializable]public class ComponentState{public Identity identity;public SerializedValue[] serialized;}
[Serializable]public class Node{public string node_path;public bool active_self;public bool active_in_hierarchy;public int missing_script_components;public string[] component_types;public RendererState[] renderers;public LODState lod_group;public ComponentState[] lights;}
[Serializable]public class Prefab{public string asset_path;public Identity identity;public Node[] nodes;}
[Serializable]public class Result{public string unity_version;public float lod_bias;public int maximum_lod_level;public Prefab[] prefabs;public bool native_lod_camera_decisions_measured;}
static Identity Id(UnityEngine.Object obj){if(obj==null)return null;string guid;long file;bool ok=AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out guid,out file);return new Identity{guid=ok?guid:"",file_id=ok?file.ToString(System.Globalization.CultureInfo.InvariantCulture):"",path=AssetDatabase.GetAssetPath(obj),type=obj.GetType().FullName};}
static SerializedValue[] Props(Component c){var rows=new List<SerializedValue>();var serialized=new SerializedObject(c);var it=serialized.GetIterator();while(it.Next(true)){var p=new SerializedValue{property_path=it.propertyPath,property_type=it.propertyType.ToString()};switch(it.propertyType){case SerializedPropertyType.Boolean:p.boolean=it.boolValue;break;case SerializedPropertyType.Integer:case SerializedPropertyType.Enum:p.number=it.intValue;break;case SerializedPropertyType.Float:p.number=it.doubleValue;break;case SerializedPropertyType.String:p.text=it.stringValue;break;case SerializedPropertyType.ObjectReference:p.reference=Id(it.objectReferenceValue);break;case SerializedPropertyType.Color:var col=it.colorValue;p.vector=new[]{col.r,col.g,col.b,col.a};break;case SerializedPropertyType.Vector3:var v=it.vector3Value;p.vector=new[]{v.x,v.y,v.z};break;default:continue;}rows.Add(p);}return rows.ToArray();}
static void Walk(Transform t,string path,List<Node> nodes){
 var components=t.GetComponents<Component>();var group=t.GetComponent<LODGroup>();LODState lod=null;
 if(group!=null){lod=new LODState{identity=Id(group),enabled=group.enabled,size=group.size,local_reference_point=new[]{group.localReferencePoint.x,group.localReferencePoint.y,group.localReferencePoint.z},fade_mode=(int)group.fadeMode,animate_cross_fading=group.animateCrossFading,levels=group.GetLODs().Select(l=>new Level{screen_relative_height=l.screenRelativeTransitionHeight,fade_transition_width=l.fadeTransitionWidth,renderers=l.renderers.Select(r=>Id(r)).ToArray()}).ToArray()};}
 nodes.Add(new Node{node_path=path,active_self=t.gameObject.activeSelf,active_in_hierarchy=t.gameObject.activeInHierarchy,missing_script_components=components.Count(c=>c==null),component_types=components.Select(c=>c==null?"missing":c.GetType().FullName).ToArray(),lod_group=lod,renderers=t.GetComponents<Renderer>().Select(r=>new RendererState{identity=Id(r),enabled=r.enabled,force_rendering_off=r.forceRenderingOff,shadow_casting_mode=(int)r.shadowCastingMode,receive_shadows=r.receiveShadows}).ToArray(),lights=components.Where(c=>c!=null&&(c is Light||c.GetType().FullName=="UnityEngine.Rendering.HighDefinition.HDAdditionalLightData")).Select(c=>new ComponentState{identity=Id(c),serialized=Props(c)}).ToArray()});
 for(int i=0;i<t.childCount;i++)Walk(t.GetChild(i),path+"/"+i.ToString("D4"),nodes);
}
public static void Export(){
 string root=Environment.GetEnvironmentVariable("OPENRPG_COMPONENT_STATE_OUTPUT");if(string.IsNullOrEmpty(root))throw new Exception("Explicit output required");
 string[] paths={"Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_rocks_002.prefab","Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_FirePit_01.prefab"};var prefabs=new List<Prefab>();
 foreach(var path in paths){var obj=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(obj==null)throw new Exception("Original prefab not loaded: "+path);var nodes=new List<Node>();Walk(obj.transform,"root",nodes);prefabs.Add(new Prefab{asset_path=path,identity=Id(obj),nodes=nodes.ToArray()});}
 // Read prefab assets only: no Instantiate, Simulate, ForceLOD or purchased script execution.
 File.WriteAllText(Path.Combine(root,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(new Result{unity_version=Application.unityVersion,lod_bias=QualitySettings.lodBias,maximum_lod_level=QualitySettings.maximumLODLevel,prefabs=prefabs.ToArray(),native_lod_camera_decisions_measured=false},true));
}
}}
"""
def sha(p):return hashlib.file_digest(Path(p).open('rb'),'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def save(name,v):
 p=ROOT/(name+'.tmp');p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/name)
def prepare():
 assert not ROOT.exists() and not HELPER.exists()
 ps='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
 assert subprocess.check_output([ps,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()=='0'
 source=W/'hivemind-run/native-american-village/source';assembly=W/'rocks-firepit-scene-bindings-v1/RESULT.v1.json';files=[Path(__file__),assembly]
 for name in ['SM_rocks_002','SM_FirePit_01']:
  rel='Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/'+name+'.prefab'
  for suffix in ['', '.meta']:
   original=source/(rel+suffix);private=PROJECT/(rel+suffix);assert original.exists() and private.exists() and sha(original)==sha(private),(original,private);files += [original,private]
 # Bought script sources are absent from this native project. Only trusted helpers in Assets/Editor are compiled.
 purchased_scripts=[str(p) for p in (PROJECT/'Assets/Hivemind').rglob('*.cs')];assert not purchased_scripts,purchased_scripts
 ROOT.mkdir();HELPER.write_text(CODE,'utf-8');files.append(HELPER)
 files += [PROJECT/p for p in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json']]
 pins={str(p):sha(p) for p in files};save('PREPARED.v1.json',dict(input_pins=pins,source_scripts_executed=False,prefab_instantiated=False,full_import=False))
 with (ROOT/'controller.log').open('xb') as out:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid)))
def run():
 state=dict(status='capturing_prefab_lod_renderer_and_light_component_state',full_import=False)
 try:
  pins=read(ROOT/'PREPARED.v1.json')['input_pins']
  for p,h in pins.items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),OPENRPG_COMPONENT_STATE_OUTPUT=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
  with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
   child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-nographics','-quit','-projectPath',str(PROJECT),'-executeMethod','OpenRPGRocksFirePitComponentStateV1.Capture.Export','-logFile',str(ROOT/'unity.log')],env=env,cwd=ROOT,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_unity_exit_code']=child.wait()
  assert state['actual_unity_exit_code']==0,'Native component capture refused'
  actual=read(ROOT/'NATIVE-RESULT.v1.json');assert len(actual['prefabs'])==2
  expected=read(W/'rocks-firepit-scene-bindings-v1/RESULT.v1.json')['rows'];counts=[]
  for prefab,scene in zip(actual['prefabs'],expected):
   assert len(prefab['nodes'])==len(scene['nodes']);source_nodes={n['source_node']['path']:n['source_node'] for n in scene['nodes']};groups={g['node_path']:g for g in scene['original_lod_groups']}
   for node in prefab['nodes']:
    original=source_nodes[node['node_path']];assert node['active_self']==original['active_self'];old=original['renderers'];assert len(node['renderers'])==len(old)
    for renderer,prior in zip(node['renderers'],old):
     identity=prior['component'];assert renderer['identity']['guid']==identity['guid'] and renderer['identity']['file_id']==identity['file_id'];assert renderer['enabled']==prior['enabled'] and renderer['shadow_casting_mode']==prior['shadow_casting_mode'] and renderer['receive_shadows']==prior['receive_shadows']
    group=node.get('lod_group')
    if group:
     old=groups[node['node_path']];assert group['size']==old['size'] and group['local_reference_point']==old['local_reference_point'];assert group['fade_mode']==old['fade_mode'] and group['animate_cross_fading']==old['animate_cross_fading'];assert len(group['levels'])==len(old['levels'])
     for level,prior in zip(group['levels'],old['levels']):
      assert level['screen_relative_height']==prior['screen_relative_height'] and level['fade_transition_width']==prior['fade_transition_width'];assert [(x['guid'],x['file_id']) for x in level['renderers']]==[(x['guid'],x['file_id']) for x in prior['renderer_components']]
   counts.append(dict(asset_id=scene['asset_id'],nodes=len(prefab['nodes']),lod_groups=sum(n.get('lod_group') is not None for n in prefab['nodes']),missing_scripts=sum(n['missing_script_components'] for n in prefab['nodes']),light_components=sum(len(n['lights']) for n in prefab['nodes'])))
  for p,h in pins.items():assert sha(p)==h,p
  save('REOPEN.v1.json',dict(rows=counts,native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),input_pins_unchanged=True,source_scripts_executed=False,prefab_instantiated=False,native_camera_lod_parity_qualified=False,full_import=False))
  state.update(status='actual_native_lod_renderer_light_components_reopened',rows=counts,input_pins_unchanged=True)
 except Exception as e:state.update(status='refused_or_unverified',error=repr(e))
 state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
