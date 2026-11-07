"""Capture source prefab renderer, LOD and collider states without instantiation."""
from pathlib import Path
from datetime import datetime, timezone
import ast
import hashlib
import json
import os
import subprocess
import traceback

S=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
O=W/'gladiator-native-component-state-v447'
PROJECT=W/'gladiator-native-geometry-v380/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
assert not O.exists()
assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve live Unity'
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def save(state):
    state.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
parent=S/'queue_rocks_firepit_native_component_state_v1.py'
tree=ast.parse(parent.read_text('utf-8'))
assignment=next(n for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='CODE' for t in n.targets))
code=ast.literal_eval(assignment.value)
bridge_path=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json'
closure_path=W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json'
bridge,closure=read(bridge_path),read(closure_path)
assert len(bridge['rows'])==5
relative_by_source={r['source']:r['relative_project_path'] for r in closure['files']}
paths=[relative_by_source[r['source_path']] for r in bridge['rows']]
old='string[] paths={"Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_rocks_002.prefab","Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_FirePit_01.prefab"};'
assert code.count(old)==1
code=code.replace(old,'string[] paths={'+','.join(json.dumps(p) for p in paths)+'};')
code=code.replace('OpenRPGRocksFirePitComponentStateV1','OpenRPGGladiatorComponentStateV447')
code=code.replace('i.ToString("D4")','i.ToString()')
code=code.replace('public ComponentState[] lights;','public ComponentState[] lights;public ComponentState[] colliders;')
anchor='lights=components.Where(c=>c!=null&&(c is Light||c.GetType().FullName=="UnityEngine.Rendering.HighDefinition.HDAdditionalLightData")).Select(c=>new ComponentState{identity=Id(c),serialized=Props(c)}).ToArray()'
assert code.count(anchor)==1
code=code.replace(anchor,anchor+',colliders=components.Where(c=>c is Collider).Select(c=>new ComponentState{identity=Id(c),serialized=Props(c)}).ToArray()')
anchor='var nodes=new List<Node>();Walk(obj.transform,"root",nodes);'
assert code.count(anchor)==1
code=code.replace(anchor,'''var sourceComponents=obj.GetComponentsInChildren<Component>(true);if(sourceComponents.Any(c=>c==null||c is MonoBehaviour))throw new Exception("Missing or purchased script component");var snapshots=sourceComponents.Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray();var nodes=new List<Node>();Walk(obj.transform,"root",nodes);for(int q=0;q<sourceComponents.Length;q++)if(snapshots[q]!=EditorJsonUtility.ToJson(sourceComponents[q],true))throw new Exception("Source component changed during readback");''')
helper=PROJECT/'Assets/Editor/GladiatorComponentStateV447.cs'
assert not helper.exists()
assert not list((PROJECT/'Assets/Hivemind').rglob('*.cs'))
assert not list((PROJECT/'Assets/Hivemind').rglob('*.dll'))
pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path]}
private=[]
for row in closure['files']:
    assert sha(row['source'])==row['sha256']
    pins[row['source']]=row['sha256']
    if row['relative_project_path'].endswith('.prefab') or row['relative_project_path'].endswith('.prefab.meta'):
        p=PROJECT/row['relative_project_path']
        assert sha(p)==row['sha256']
        private.append(dict(path=str(p),sha256=row['sha256']))
for rel in ['Packages/manifest.json','ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset']:
    p=PROJECT/rel;pins[str(p)]=sha(p)
O.mkdir();(O/'temp').mkdir();helper.write_text(code,'utf-8');pins[str(helper)]=sha(helper)
recipe=S/'queue_delta_firepit_particle_behavior_v4.py';pins[str(recipe)]=sha(recipe)
(O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_prefab_pins=private,prefab_paths=paths,purchased_scripts_executed=False,prefabs_instantiated=False),indent=2),'utf-8')
state=dict(status='gladiator_component_capture_prepared')
save(state)
broker=None;namespace=None
try:
    tree=ast.parse(recipe.read_text('utf-8-sig'))
    nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
    namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
    exec(compile(ast.Module(body=nodes,type_ignores=[]),str(recipe),'exec'),namespace)
    broker,label=namespace['start_broker'](state)
    env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),OPENRPG_COMPONENT_STATE_OUTPUT=str(O),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
    with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
        process=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-nographics','-quit','-job-worker-count','2','-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGGladiatorComponentStateV447.Capture.Export','-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='gladiator_native_component_capture_running',unity_pid=process.pid);save(state)
        state['unity_exit_code']=process.wait()
    state['source_changes']=[p for p,h in pins.items() if sha(p)!=h]
    state['private_prefab_changes']=[r['path'] for r in private if sha(r['path'])!=r['sha256']]
    assert state['unity_exit_code']==0 and not state['source_changes'] and not state['private_prefab_changes']
    native=read(O/'NATIVE-RESULT.v1.json')
    assert len(native['prefabs'])==5 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==17
    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==5
    state.update(status='five_prefab_renderer_lod_and_collider_states_captured_review_pending',native_sha256=sha(O/'NATIVE-RESULT.v1.json'),input_pins=pins)
except Exception as e:
    state.update(status='gladiator_component_state_refusal_retained',error=repr(e),traceback=traceback.format_exc())
finally:
    if broker is not None:namespace['stop_broker'](broker,state)
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
print(json.dumps(dict(status=state['status'],error=state.get('error'))))
