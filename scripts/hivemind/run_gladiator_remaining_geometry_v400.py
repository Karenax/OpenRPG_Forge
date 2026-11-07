"""Capture the other thirteen meshes, plus root/material metadata of all five prefabs."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,os,shutil,subprocess,sys,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-remaining-native-geometry-v400'
PROJECT=W/'gladiator-native-geometry-v380/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    bp=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json';cp=W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json'
    sp=W/'gladiator-material-serialization-seal-v401/RESULT.v1.json';gp=W/'gladiator-converted-geometry-v396/RESULT.v1.json'
    bridge,closure,seal,geometry=[read(p) for p in [bp,cp,sp,gp]]
    assert len(geometry['meshes'])==4 and geometry['all_vertex_bytes_reopened_and_reverse_compared']
    assert bridge['prefabs']==5 and closure['file_count']==60 and seal['source_render_properties_unchanged']
    for proof in [bridge,closure,seal,geometry]:
        for p,h in proof['input_pins'].items():assert sha(p)==h,p
    for p,h in geometry['output_pins'].items():assert sha(p)==h,p
    allowed={r['private_imported']:seal['input_pins'][r['private_imported']] for r in seal['rows']}
    O.mkdir();(O/'temp').mkdir();pins={str(p):sha(p) for p in [bp,cp,sp,gp,Path(__file__)]};copies=[]
    for row in closure['files']:
        source=Path(row['source']);assert sha(source)==row['sha256'];pins[str(source)]=row['sha256']
        target=PROJECT/row['relative_project_path'];target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():assert sha(target)==row['sha256'] or str(target) in allowed and sha(target)==allowed[str(target)],target
        else:shutil.copyfile(source,target);assert sha(target)==row['sha256']
        copies.append(dict(source=str(source),copy=str(target),source_sha256=row['sha256'],private_before_sha256=sha(target)))
    base=PROJECT/'Assets/Editor/GladiatorGeometryV380.cs';code=base.read_text('utf-8-sig');pins[str(base)]=sha(base)
    code=code.replace('OpenRPGGladiatorGeometryV380','OpenRPGGladiatorLotGeometryV400')
    code=code.replace('public string output,prefab_path,prefab_guid;}','public string output,prefab_path,prefab_guid;public bool include_geometry;public int expected_mesh_filters;}')
    code=code.replace('public string unity_version,prefab_guid;','public string unity_version,prefab_guid;public Vector3 root_position,root_scale;public Quaternion root_rotation;public string[] root_components;public MaterialSnapshot[] materials;')
    start=code.index(' public static void Export(){');body=code.index('if(File.Exists(r.output))',start)
    code=code[:start]+' static Report Capture(Request r){'+code[body:]
    code=code.replace('b.role=="mesh_filter")!=4','b.role=="mesh_filter")!=r.expected_mesh_filters')
    needle='var exported=meshes.OrderBy(k=>k.Key).Select(k=>ExportMesh(k.Value)).ToArray();';assert code.count(needle)==1
    code=code.replace(needle,'var exported=r.include_geometry?meshes.OrderBy(k=>k.Key).Select(k=>ExportMesh(k.Value)).ToArray():new Geometry[0];')
    needle='File.WriteAllText(r.output,JsonUtility.ToJson(new Report{';assert code.count(needle)==1;code=code.replace(needle,'return new Report{')
    needle='source_components_unchanged=true},true));}';assert code.count(needle)==1
    code=code.replace(needle,'source_components_unchanged=true,root_position=p.transform.localPosition,root_scale=p.transform.localScale,root_rotation=p.transform.localRotation,root_components=p.GetComponents<Component>().Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray(),materials=p.GetComponentsInChildren<MeshRenderer>(true).SelectMany(m=>m.sharedMaterials).Distinct().Select(Snapshot).ToArray()};}')
    addition=r'''
 [Serializable] public class BatchRequest {public Request[] cases;public string output;}
 [Serializable] public class BatchReport {public Report[] cases;public bool converted=false,full_import=false;}
 [Serializable] public class FloatValue {public string property;public float value;}
 [Serializable] public class VectorValue {public string property,kind;public Vector4 value;}
 [Serializable] public class TextureValue {public string property,builtin_name,format,graphics_format,filter_mode;public Identity identity;public bool explicitly_null,readable;public int width,height,mips,aniso_level;}
 [Serializable] public class MaterialSnapshot {public Identity identity,shader;public string serialized;public FloatValue[] floats;public VectorValue[] vectors;public TextureValue[] textures;}
 static MaterialSnapshot Snapshot(Material m){
  var floats=new List<FloatValue>();var vectors=new List<VectorValue>();var textures=new List<TextureValue>();
  for(int i=0;i<m.shader.GetPropertyCount();i++){
   string name=m.shader.GetPropertyName(i);var kind=m.shader.GetPropertyType(i);
   if(kind==ShaderPropertyType.Float||kind==ShaderPropertyType.Range)floats.Add(new FloatValue{property=name,value=m.GetFloat(name)});
   else if(kind==ShaderPropertyType.Color||kind==ShaderPropertyType.Vector){Vector4 value=kind==ShaderPropertyType.Color?(Vector4)m.GetColor(name):m.GetVector(name);vectors.Add(new VectorValue{property=name,kind=kind.ToString(),value=value});}
   else if(kind==ShaderPropertyType.Texture){var t=m.GetTexture(name);var row=new TextureValue{property=name,explicitly_null=t==null};if(t!=null){string g;long f;if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(t,out g,out f)&&!String.IsNullOrEmpty(g))row.identity=Id(t);else row.builtin_name=t.name;row.width=t.width;row.height=t.height;row.filter_mode=t.filterMode.ToString();row.aniso_level=t.anisoLevel;if(t is Texture2D){var image=(Texture2D)t;row.format=image.format.ToString();row.graphics_format=image.graphicsFormat.ToString();row.mips=image.mipmapCount;row.readable=image.isReadable;}}textures.Add(row);}
  }
  return new MaterialSnapshot{identity=Id(m),shader=Id(m.shader),serialized=EditorJsonUtility.ToJson(m,true),floats=floats.ToArray(),vectors=vectors.ToArray(),textures=textures.ToArray()};
 }
 public static void Export(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--openrpg-manifest");if(i<0)throw new Exception("Manifest absent");var request=JsonUtility.FromJson<BatchRequest>(File.ReadAllText(args[i+1]));if(File.Exists(request.output))throw new Exception("Receipt already exists");var rows=new List<Report>();foreach(var item in request.cases){var report=Capture(item);File.WriteAllText(item.output,JsonUtility.ToJson(report,true));rows.Add(report);}File.WriteAllText(request.output,JsonUtility.ToJson(new BatchReport{cases=rows.ToArray()},true));}
'''
    code=code.rstrip();assert code.endswith('}');code=code[:-1]+addition+'\n}\n'
    helper=PROJECT/'Assets/Editor/GladiatorLotGeometryV400.cs';assert not helper.exists();helper.write_text(code,'utf-8');pins[str(helper)]=sha(helper)
    cases=[];first_guid=geometry['meshes'][0]['binding']['materials'][0]['guid'] # Only source identity below selects the old prefab.
    old_prefab=read(W/'gladiator-native-geometry-v380/NATIVE-RESULT.v1.json')['prefab_guid']
    for i,row in enumerate(sorted(bridge['rows'],key=lambda r:r['source_path'])):
        folder=O/f'case-{i:02d}';folder.mkdir();source=Path(row['source_path'])
        relative=next(r['relative_project_path'] for r in closure['files'] if r['source']==str(source))
        cases.append(dict(prefab_path=relative,prefab_guid=row['prefab_guid'],expected_mesh_filters=row['renderer_count'],include_geometry=row['prefab_guid']!=old_prefab,output=str(folder/'NATIVE-RESULT.v1.json')))
    assert len(cases)==5 and sum(c['expected_mesh_filters'] for c in cases if c['include_geometry'])==13
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8');pins[str(request)]=sha(request)
    recipe=S/'queue_delta_firepit_particle_behavior_v4.py';pins[str(recipe)]=sha(recipe)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(cases=cases,input_pins=pins,private_copies=copies,expected_new_meshes=13,prior_four_meshes_reused=True,purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8')
    save(dict(status='remaining_thirteen_meshes_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,new_meshes=13,metadata_cases=5)))
def run():
    state=read(O/'RUN-RESULT.v1.json');broker=None;namespace=None
    try:
        assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve live Unity'
        prep=read(O/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        for row in prep['private_copies']:assert sha(row['copy'])==row['private_before_sha256']
        recipe=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[]);exec(compile(ast.Module(body=nodes,type_ignores=[]),str(recipe),'exec'),namespace);broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-job-worker-count','2','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGGladiatorLotGeometryV400.Export','--openrpg-manifest',str(O/'REQUEST.v1.json'),'-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='remaining_thirteen_meshes_native_capture_running',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
        state['source_changes']=[p for p,h in prep['input_pins'].items() if sha(p)!=h];state['private_importer_changes']=[row['copy'] for row in prep['private_copies'] if sha(row['copy'])!=row['private_before_sha256']]
        assert not state['source_changes'] and state['unity_exit_code']==0
        native=read(O/'NATIVE-RESULT.v1.json');assert len(native['cases'])==5 and sum(len(c['meshes']) for c in native['cases'])==13 and all(c['source_components_unchanged'] for c in native['cases'])
        state.update(status='remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending',native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'),new_meshes=13,source_inputs_unchanged=True)
    except Exception as exc:state.update(status='remaining_geometry_capture_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None:namespace['stop_broker'](broker,state)
        state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
