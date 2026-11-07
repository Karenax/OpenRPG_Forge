"""Capture all 24 static House prefabs through the existing native mesh exporter."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys,shutil
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-native-prefab-geometry-v648';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists();assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    package=W/'house-source-material-package-v647/RESULT.v1.json';proof=read(package);pins={}
    for key in ['input_pins','output_pins']:
        for p,h in proof[key].items():assert sha(p)==h,p;pins[p]=h
    ap=W/'house-native-sampling-policy-v644/HELPER-OWNERSHIP.v1.json';audit=read(ap);assert len(audit['helpers'])==32
    assert not any((PROJECT/'Assets').rglob('*.dll')) and {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    qp=W/'queue/house-on-a-hill/prefabs.json';q=read(qp);lp=W/'house-main-material-lot-v603/RESULT.v1.json';lot=read(lp);selected=[r for r in q['jobs'] if r['id'] in lot['card_ids']];assert len(selected)==24
    source_root=Path(q['source_root']);copies=read(W/'house-native-sampling-policy-v644/PREPARED.v1.json')['private_copies'];known={r['copy']:r['private_before_sha256'] for r in copies}
    for r in copies:assert sha(r['copy'])==r['private_before_sha256']
    O.mkdir();(O/'temp').mkdir();(O/'cases').mkdir();files=set()
    for row in selected:
        assert sha(source_root/row['source'])==row['source_sha256'];files.add(row['source'])
        for c in row['components']:assert sha(source_root/c['mesh_source'])==c['mesh_sha256'];files.add(c['mesh_source'])
    for rel in sorted(files):
        for suffix in ['', '.meta']:
            source=source_root/(rel+suffix);target=PROJECT/(rel+suffix);assert source.is_file() and target.resolve().is_relative_to(PROJECT.resolve());h=sha(source);pins[str(source)]=h
            if target.exists():assert sha(target)==h or known.get(str(target))==sha(target),str(target)
            else:target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target);assert sha(target)==h
            if str(target) not in known:copies.append(dict(source=str(source),copy=str(target),source_sha256=h,private_before_sha256=sha(target)));known[str(target)]=sha(target)
    parent=W/'gladiator-native-geometry-v380/project/Assets/Editor/GladiatorGeometryV380.cs';text=parent.read_text('utf-8-sig').replace('OpenRPGGladiatorGeometryV380','OpenRPGHousePrefabGeometryV648')
    text=text.replace('public string output,prefab_path,prefab_guid;}','public string output,prefab_path,prefab_guid,card_id;public int expected_mesh_filters;}')
    text=text.replace('public string unity_version,prefab_guid;','public string unity_version,prefab_guid,card_id;public string[] component_snapshots;public Node[] nodes;')
    start=text.index(' public static void Export(){');body=text.index('if(File.Exists(r.output))',start);text=text[:start]+' static Report Capture(Request r){'+text[body:]
    old='if(bindings.Count(b=>b.role=="mesh_filter")!=4||bindings.Count(b=>b.role=="mesh_collider")!=0||lods.Count!=1)throw new Exception("Geometry hierarchy mismatch");';assert text.count(old)==1;text=text.replace(old,'if(bindings.Count(b=>b.role=="mesh_filter")!=r.expected_mesh_filters)throw new Exception("Source mesh filter count differs");')
    needle='File.WriteAllText(r.output,JsonUtility.ToJson(new Report{';assert text.count(needle)==1;text=text.replace(needle,'return new Report{')
    needle='source_components_unchanged=true},true));}';assert text.count(needle)==1;text=text.replace(needle,'source_components_unchanged=true,card_id=r.card_id,component_snapshots=before,nodes=Nodes(p.transform,"root","").ToArray()};}')
    addition='''
 [Serializable]public class Node{public string path,parent,name;public Vector3 local_position,local_scale;public Quaternion local_rotation;public string[] component_types;}
 static IEnumerable<Node> Nodes(Transform t,string path,string parent){yield return new Node{path=path,parent=parent,name=t.name,local_position=t.localPosition,local_scale=t.localScale,local_rotation=t.localRotation,component_types=t.GetComponents<Component>().Select(c=>c.GetType().FullName).ToArray()};for(int i=0;i<t.childCount;i++)foreach(var n in Nodes(t.GetChild(i),path+"/"+i,path))yield return n;}
 [Serializable]public class BatchRequest{public string output;public Request[] cases;}
 [Serializable]public class BatchReport{public Report[] cases;public bool full_import=false;}
 public static void Export(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--openrpg-manifest");if(i<0)throw new Exception("Manifest absent");var r=JsonUtility.FromJson<BatchRequest>(File.ReadAllText(args[i+1]));if(File.Exists(r.output)||r.cases.Length!=24)throw new Exception("Preserve evidence;24 cases required");var cases=new List<Report>();foreach(var c in r.cases)cases.Add(Capture(c));File.WriteAllText(r.output,JsonUtility.ToJson(new BatchReport{cases=cases.ToArray()},true));}
'''
    text=text.rstrip();assert text.endswith('}');text=text[:-1]+addition+'\n}\n';base=O/'HousePrefabGeometryV648.cs';base.write_text(text,'utf-8');helper=PROJECT/'Assets/Editor'/base.name;assert not helper.exists();helper.write_bytes(base.read_bytes());audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(base),sha256=sha(helper)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat());owned=O/'HELPER-OWNERSHIP.v1.json';owned.write_text(json.dumps(audit,indent=2),'utf-8')
    cases=[dict(card_id=r['id'],prefab_path=r['source'],prefab_guid=r['guid'],expected_mesh_filters=len(r['components']),output=str(O/'cases'/(r['guid']+'.json'))) for r in selected];request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),cases=cases),indent=2),'utf-8')
    for p in [Path(__file__),package,ap,qp,lp,parent,base,helper,owned,request,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,prefab_cards=24,cases=cases,purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8');save(dict(status='house_native_prefab_geometry_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,prefab_cards=24)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHousePrefabGeometryV648.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1;code=code.replace(old,"assert len(native['cases'])==24 and all(c['source_components_unchanged'] and len(c['meshes'])>0 and len(c['nodes'])>0 for c in native['cases'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_native_prefab_geometry_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_native_prefab_geometry_captured_review_pending').replace('remaining_geometry_capture_refusal_retained','house_native_prefab_geometry_refusal_retained').replace('new_meshes=13','prefab_cards=24')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-prefab-native-geometry','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
