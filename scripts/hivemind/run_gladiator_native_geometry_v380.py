"""Data-only isolated native extraction of the audited Gladiator flag."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, os, re, shutil, subprocess, sys, traceback
S=Path(__file__).resolve().parent; W=Path('F:/OpenRPG_ImportWork')
O=W/'gladiator-native-geometry-v380'; PROJECT=O/'project'
BASE=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PACKAGE=Path('F:/OpenRPG_AssetVault/originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
overlay=os.environ.get('OPENRPG_UNITY_OVERLAY_MANIFEST')
sys.path.insert(0,str(S.parent/'assets'))
from unity_shared_overlay import UnitySourceResolver,configured_resolver
resolver=None
def configure_resolver():
    global resolver
    if overlay:
        resolver=UnitySourceResolver(Path('F:/OpenRPG_AssetVault'),Path(overlay),os.environ.get('OPENRPG_UNITY_OVERLAY_SHA256',''))
    else:resolver=configured_resolver(Path('F:/OpenRPG_AssetVault'),PACKAGE/'source')
def actual(p):
    path=Path(p).resolve(strict=False)
    return resolver.resolve_absolute(path) if resolver else path
def exists(p):
    path=Path(p).resolve(strict=False)
    return resolver.contains_absolute(path) if resolver else path.is_file()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with actual(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def copy_audited_sources(audit, package, project):
    """Copy verified logical source paths while preserving their Unity layout."""
    pins={};copies=[];source_root=(package/'source').resolve(strict=True)
    for row in audit['resolved_files']:
        source=Path(row['path']).resolve(strict=False)
        if not source.is_relative_to(source_root) or source.suffix not in {'.prefab','.fbx','.mat','.shadergraph','.png'}:
            raise ValueError('source outside package or unsupported format')
        for p in [source,Path(str(source)+'.meta')]:
            if not exists(p) or sha(p)!=audit['input_pins'][str(p)]:
                raise ValueError('source missing or changed: '+str(p))
            target=project/p.relative_to(source_root);target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(actual(p),target)
            if sha(target)!=sha(p):raise ValueError('copied source differs: '+str(p))
            pins[str(p)]=sha(p);copies.append(dict(source=str(p),copy=str(target),sha256=sha(target)))
    return pins,copies
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False,converted=False)
    p=O/'RUN-RESULT.tmp'; p.write_text(json.dumps(v,indent=2),'utf-8'); p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    configure_resolver()
    assert not O.exists(), 'Never replace an existing proof'
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    auditpath=W/'gladiator-mirror-source-case-v379/RESULT.v1.json'; audit=read(auditpath)
    assert audit['status']=='real_flag_source_dependency_closure_rehashed'
    assert not audit['mono_behaviour_present'] and not audit['skinned_renderer_present']
    for p,h in audit['input_pins'].items(): assert sha(p)==h,p
    O.mkdir(); (O/'temp').mkdir(); (PROJECT/'Assets/Editor').mkdir(parents=True)
    pins={str(auditpath):sha(auditpath),str(Path(__file__)):sha(__file__)}
    source_pins,copies=copy_audited_sources(audit,PACKAGE,PROJECT)
    pins.update(source_pins)
    for name in ['Packages','ProjectSettings']:
        shutil.copytree(BASE/name,PROJECT/name)
        for p in (BASE/name).rglob('*'):
            if p.is_file(): pins[str(p)]=sha(p)
    original=S/'candidates/TorchGeometryV150.cs'; code=original.read_text('utf-8-sig')
    code=code.replace('OpenRPGTorchGeometryV150','OpenRPGGladiatorGeometryV380')
    code=code.replace('public Identity mesh;}','public Identity mesh;public Vector3 local_position,local_scale;public Quaternion local_rotation;public Identity[] materials;}')
    code=code.replace('mesh=id});','mesh=id,local_position=t.localPosition,local_scale=t.localScale,local_rotation=t.localRotation,materials=t.GetComponent<MeshRenderer>()==null?new Identity[0]:t.GetComponent<MeshRenderer>().sharedMaterials.Select(Id).ToArray()});')
    code=code.replace('b.role=="mesh_collider")!=1','b.role=="mesh_collider")!=0')
    code=code.replace('var before=components.Select', 'if(components.Any(c=>c is MonoBehaviour||c is SkinnedMeshRenderer))throw new Exception("Non-static source component");var before=components.Select')
    helper=PROJECT/'Assets/Editor/GladiatorGeometryV380.cs';helper.write_text(code,'utf-8')
    pins[str(original)]=sha(original);pins[str(helper)]=sha(helper)
    source=Path(audit['resolved_files'][0]['path']); assert source.suffix=='.prefab'
    guid=re.search(r'^guid: ([a-f0-9]{32})$',actual(Path(str(source)+'.meta')).read_text('utf-8-sig'),re.M)[1]
    request=dict(prefab_path=source.relative_to(PACKAGE/'source').as_posix(),prefab_guid=guid,output=str(O/'NATIVE-RESULT.v1.json'))
    (O/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    pins[str(O/'REQUEST.v1.json')]=sha(O/'REQUEST.v1.json')
    recipe=S/'queue_delta_firepit_particle_behavior_v4.py';pins[str(recipe)]=sha(recipe)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(asset_id=audit['asset_id'],input_pins=pins,private_copies=copies,purchased_code_copied=False,source_version_parity_qualified=False,full_import=False),indent=2),'utf-8')
    save(dict(status='isolated_flag_geometry_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,private_files=len(copies),project=str(PROJECT))))
def run():
    configure_resolver()
    state=read(O/'RUN-RESULT.v1.json'); broker=None; namespace=None
    try:
        assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve live Unity'
        prep=read(O/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items(): assert sha(p)==h,p
        for row in prep['private_copies']: assert sha(row['copy'])==row['sha256']
        recipe=S/'queue_delta_firepit_particle_behavior_v4.py'; tree=ast.parse(recipe.read_text('utf-8-sig'))
        nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=nodes,type_ignores=[]),str(recipe),'exec'),namespace)
        broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-job-worker-count','2','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGGladiatorGeometryV380.Export','--openrpg-manifest',str(O/'REQUEST.v1.json'),'-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='native_flag_geometry_running',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
        state['source_changes']=[p for p,h in prep['input_pins'].items() if sha(p)!=h]
        state['private_importer_changes']=[row['copy'] for row in prep['private_copies'] if sha(row['copy'])!=row['sha256']]
        assert not state['source_changes']
        assert state['unity_exit_code']==0
        native=read(O/'NATIVE-RESULT.v1.json');assert native['source_components_unchanged'] and len(native['meshes'])==4 and len(native['bindings'])==4
        state.update(status='native_flag_geometry_extracted_review_pending',native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'),mesh_count=4,source_inputs_unchanged=True)
    except Exception as exc: state.update(status='native_flag_geometry_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None: namespace['stop_broker'](broker,state)
        state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':
    if len(sys.argv)==1: prepare()
    else: assert sys.argv[1:]==['run'];run()
