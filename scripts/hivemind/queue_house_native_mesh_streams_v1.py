"""Capture the 48 admitted House render meshes with the existing native adapter."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, os, shutil, subprocess, sys

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'house-native-mesh-streams-v1'
PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
HERE=Path(__file__).parent
BASE=HERE/'queue_rocks_native_mesh_streams_v1.py'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(name,value):
    p=ROOT/name;tmp=p.with_suffix(p.suffix+'.tmp');tmp.write_text(json.dumps(value,indent=2),'utf-8');tmp.replace(p)
def no_unity():assert subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=='0','Another Unity is live'

def prepare():
    assert not ROOT.exists();no_unity()
    assert read(W/'house-native-prefab-hierarchy-reopen-v1/RESULT.v1.json')['status']=='fifty_house_native_hierarchies_independently_reopened'
    prior=W/'unity-library-source-thumbnail-house-bench-v8';assert read(prior/'RUN-RESULT.v1.json')['source_inputs_unchanged']
    ROOT.mkdir();native=W/'house-native-prefab-hierarchy-v2/NATIVE-RESULT.v1.json';rows={}
    for case in read(native)['cases']:
        for node in case['nodes']:
            for renderer in node['renderers']:
                if not renderer['static_mesh_adapter_applicable']:continue
                m=renderer['mesh'];ident=m['source'];key=ident['guid']+':'+ident['file_id']
                row=dict(guid=ident['guid'],file_id=ident['file_id'],asset_path=ident['asset_path'],vertices=m['vertices'],original_vertex_formats=m['original_vertex_formats'])
                if key in rows:assert rows[key]==row
                rows[key]=row
    assert len(rows)==48
    request=dict(meshes=[rows[k] for k in sorted(rows)],output=str(ROOT));save('REQUEST.v1.json',request)
    tree=ast.parse(BASE.read_text('utf-8'));code=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='CODE' for t in n.targets))
    code=code.replace('OpenRPGRocksNativeMeshStreamsV1','OpenRPGHouseNativeMeshStreamsV1')
    code=code.replace('public static class Capture {','public static class Capture {\n[Serializable]public class MeshRequest{public string guid,file_id,asset_path;}\n[Serializable]public class Request{public MeshRequest[] meshes;public string output;}')
    start=code.index(' string output=Environment.GetEnvironmentVariable(');end=code.index('  string directory=Path.Combine(output,key);',start)
    code=code[:start]+''' var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--openrpg-manifest");if(arg<0)throw new Exception("Manifest required");var request=JsonUtility.FromJson<Request>(File.ReadAllText(args[arg+1]));string output=request.output;
 var rows=new List<Row>();var seen=new HashSet<string>();
 foreach(var wanted in request.meshes){
  if(AssetDatabase.AssetPathToGUID(wanted.asset_path)!=wanted.guid)throw new Exception("Source asset GUID mismatch");
  var meshes=AssetDatabase.LoadAllAssetsAtPath(wanted.asset_path).OfType<Mesh>().Where(m=>{string g;long f;return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out g,out f)&&g==wanted.guid&&f.ToString()==wanted.file_id;}).ToArray();
  if(meshes.Length!=1)throw new Exception("Exact native render mesh unresolved");var mesh=meshes[0];string guid=wanted.guid;long file=long.Parse(wanted.file_id);string key=guid+"-"+file.ToString();if(!seen.Add(key))throw new Exception("Duplicate requested mesh");
'''+code[end:]
    helper=ROOT/'HouseNativeMeshStreamsV1.cs';helper.write_text(code,'utf-8');dst=PROJECT/'Assets/Editor/HouseNativeMeshStreamsV1.cs';assert not dst.exists();shutil.copyfile(helper,dst)
    pins=read(prior/'PREPARED.v1.json')['input_pins'];pins.update({str(p):sha(p) for p in [Path(__file__).resolve(),BASE,helper,ROOT/'REQUEST.v1.json',native,W/'house-native-prefab-hierarchy-reopen-v1/RESULT.v1.json']})
    save('PREPARED.v1.json',dict(sub_batch='L03.HOUSE.GEOMETRIE.01',expected_meshes=48,input_pins=pins,reused_project=str(PROJECT),source_import_settings_changed=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,expected_meshes=48)))

def run():
    state=dict(status='capturing_48_house_native_render_meshes',full_import=False,geometry_converted=False)
    try:
        no_unity();pins=read(ROOT/'PREPARED.v1.json')['input_pins']
        for p,digest in pins.items():assert sha(p)==digest,p
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-nographics','-quit','-projectPath',str(PROJECT),'-executeMethod','OpenRPGHouseNativeMeshStreamsV1.Capture.Export','--openrpg-manifest',str(ROOT/'REQUEST.v1.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid']=proc.pid;save('RUN-RESULT.v1.json',state);state['actual_unity_exit_code']=proc.wait()
        assert state['actual_unity_exit_code']==0,'Native mesh capture refused'
        native=read(ROOT/'NATIVE-RESULT.v1.json');assert len(native['meshes'])==48
        assert not native['import_settings_changed'] and not native['prefab_instantiated']
        for p,digest in pins.items():assert sha(p)==digest,p
        state.update(status='48_house_native_mesh_streams_captured_reopen_pending',meshes_captured=48,input_pins_unchanged=True,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'))
    except Exception as exc:state.update(status='house_mesh_capture_refusal_retained',error=repr(exc))
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
