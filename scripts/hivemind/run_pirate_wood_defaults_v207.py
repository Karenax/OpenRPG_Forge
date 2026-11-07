"""Observe the actual Pirate source shader defaults; retain every input change."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, os, shutil, subprocess, sys, traceback

S = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
O = W/'pirate-material-native-defaults-v207'
PROJECT = W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PRIVATE = PROJECT/'Assets/Hivemind/PirateIsland'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'

def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
def save(state):
    state.update(updated_utc=datetime.now(timezone.utc).isoformat(), full_import=False,
                 source_equivalence_qualified=False, studio_verified=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')

def prepare():
    assert not O.exists(), 'Preserve prior diagnostic'
    audit=read(W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json')
    for p,h in audit['input_pins'].items(): assert sha(p)==h, p
    assert len(audit['changes'])==1
    assert audit['changes'][0]['after_sha256']=='1075f052d0fafa2640549b324bc31821f21383ae10c1598f045dddb601e473b6'
    O.mkdir();(O/'temp').mkdir()
    original=S/'candidates/TorchMaterialExportV171.cs'
    text=original.read_text('utf-8-sig')
    replacements={
        'OpenRPGTorchMaterialExportV171':'OpenRPGPirateMaterialDefaultsV207',
        'r.material_guids.Length!=3||r.material_guids.Distinct().Count()!=3':'r.material_guids.Length!=1||r.material_guids.Distinct().Count()!=1',
        'Exactly three original private materials required':'Exactly one retained Pirate material required',
        'Assets/OpenRPGTorchOriginalVariantV134/':'Assets/Hivemind/PirateIsland/',
        ' OpenRPGTorchCompressedStorageV159.Export();':' // Material-only diagnostic; no texture importer mutations.'}
    for before,after in replacements.items():
        assert text.count(before)==1, before
        text=text.replace(before,after)
    helper=O/'PirateMaterialDefaultsV207.cs';helper.write_text(text,'utf-8')
    request=dict(output=str(O/'MATERIAL-NATIVE.v1.json'),generated_shader=str(O/'source-generated.shader'),
        graph_path='Assets/Hivemind/PirateIsland/HDRP(Default)/Art/ShaderGraphs/S_WoodPlanks_PaintBlend.shadergraph',
        graph_guid='815df3162f8ce2c4d99e8f55addb4979',graph_file_id='-6465566751694194690',
        material_guids=['abc00000000004592200770135964921'])
    (O/'manifest.json').write_text(json.dumps(request,indent=2),'utf-8')
    pins={str(p):sha(p) for p in PRIVATE.rglob('*') if p.is_file()}
    for p in [Path(__file__),original,helper,O/'manifest.json',W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json',
              PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectSettings.asset']:
        pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,adaptations=replacements),indent=2),'utf-8')
    save(dict(status='pirate_native_defaults_prepared',native_defaults_observed=False))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,
            stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,root=str(O),materials=1)))

def run():
    state=read(O/'RUN-RESULT.v1.json');broker=None;namespace=None
    try:
        assert subprocess.check_output([PS,'-NoProfile','-Command',
            '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0', 'Preserve live Unity'
        pins=read(O/'PREPARED.v1.json')['input_pins']
        for p,h in pins.items(): assert sha(p)==h,p
        target=PROJECT/'Assets/Editor/PirateMaterialDefaultsV207.cs'
        assert not target.exists() and not Path(str(target)+'.meta').exists()
        shutil.copyfile(O/target.name,target)
        recipe=S/'queue_delta_firepit_particle_behavior_v4.py'
        tree=ast.parse(recipe.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or
            isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(recipe),'exec'),namespace)
        broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11',
                '-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGPirateMaterialDefaultsV207.Export',
                '--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')],
                env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='observing_pirate_native_material_defaults',unity_pid=proc.pid);save(state)
            state['unity_exit_code']=proc.wait()
        changes=[dict(path=p,before_sha256=h,after_sha256=sha(p)) for p,h in pins.items() if sha(p)!=h]
        state['input_changes']=changes
        assert not changes,'Retain unexpected source changes'
        assert sha(target)==sha(O/target.name)
        assert state['unity_exit_code']==0
        native=read(O/'MATERIAL-NATIVE.v1.json')
        assert native['source_materials_unchanged'] and len(native['materials'])==1
        state.update(status='pirate_native_shader_defaults_observed_inputs_unchanged',native_defaults_observed=True,
            output_pins={str(p):sha(p) for p in [O/'MATERIAL-NATIVE.v1.json',O/'source-generated.shader']})
    except Exception as exc:
        state.update(status='pirate_native_defaults_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None: namespace['stop_broker'](broker,state)
        state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)

if __name__=='__main__':
    if len(sys.argv)==1: prepare()
    else:
        assert sys.argv[1:]==['run'];run()
