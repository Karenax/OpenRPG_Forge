"""Measure the two original built-in effect shaders; no fallback or render claim."""
from pathlib import Path
import ast
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-delta-firepit-effect-materials-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
SOURCE=W/'hivemind-run/native-american-village/source'
HERE=Path(__file__).resolve().parent
HELPER=HERE/'candidates/FirePitEffectMaterialIdentity.cs'
BROKER_HELPER=HERE/'queue_delta_firepit_particle_behavior_v4.py'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')

def sha(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def read(path):return json.loads(Path(path).read_text('utf-8-sig'))
def save(name,value):
    temp=ROOT/(name+'.tmp');temp.write_text(json.dumps(value,indent=2),'utf-8');os.replace(temp,ROOT/name)
def idle():
    result=subprocess.check_output([PS,'-NoProfile','-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True,
        creationflags=subprocess.CREATE_NO_WINDOW)
    assert result.strip()=='0','Preserve active Unity process'
def check(pins):
    for p,d in pins.items():assert sha(p)==d,'Changed input: '+p

def prepare():
    assert not ROOT.exists();idle()
    behavior=W/'unity-delta-firepit-particle-behavior-v7'
    result=read(behavior/'RUN-RESULT.v1.json');assert result['frames_verified']==112 and result['native_behavior_verified']
    proof=read(behavior/'INDEPENDENT-REOPEN.v1.json')
    assert sha(behavior/'INDEPENDENT-REOPEN.v1.json')==result['independent_receipt_sha256']
    check(proof['input_pins'])
    closure=W/'delta-complete-prefab-source-reopen-v1'
    sourceproof=read(closure/'RESULT.v1.json');check(sourceproof['input_pins']);check(sourceproof['output_pins'])
    destination=PROJECT/'Assets/Editor/FirePitEffectMaterialIdentity.cs'
    assert not destination.exists() and not Path(str(destination)+'.meta').exists()
    ROOT.mkdir();(ROOT/'temp').mkdir();shutil.copyfile(HELPER,destination)
    cases=[]
    for irpath in sorted(closure.glob('*-SOURCE-IR.v1.json')):
        ir=read(irpath);original=Path(ir['material_source']);private=PROJECT/original.relative_to(SOURCE)
        assert sha(original)==sha(private),'Preserve exact original material bytes'
        cases.append(dict(asset_path=private.relative_to(PROJECT).as_posix(),material_guid=ir['material_guid']))
    assert len(cases)==2
    pins={str(p):sha(p) for p in (PROJECT/'Assets').rglob('*') if p.is_file()}
    pins.update(sourceproof['input_pins']);pins.update(sourceproof['output_pins'])
    for p in [Path(__file__).resolve(),HELPER,BROKER_HELPER,behavior/'RUN-RESULT.v1.json',
              behavior/'INDEPENDENT-REOPEN.v1.json',PROJECT/'Packages/manifest.json',
              PROJECT/'Packages/packages-lock.json',PROJECT/'ProjectSettings/ProjectSettings.asset']:
        pins[str(p)]=sha(p)
    save('manifest.json',dict(output=str(ROOT/'MATERIALS.v1.json'),cases=cases))
    pins[str(ROOT/'manifest.json')]=sha(ROOT/'manifest.json')
    save('PREPARED.v1.json',dict(input_pins=pins,cases=cases,original_sources_preserved=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid,original_effect_materials=2,full_import=False)))

def run():
    state=dict(status='preparing_original_effect_material_identity_capture',native_material_identity_verified=False,
        gpu_rendered=False,studio_verified=False,full_import=False)
    save('RUN-RESULT.v1.json',state);broker=None;broker_globals=None
    try:
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins']);idle()
        # Reuse only the owned broker API, not the previous job's top-level
        # execution or preparation. Its exact helper bytes are pinned above.
        tree=ast.parse(BROKER_HELPER.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or
            isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        broker_globals=dict(ROOT=ROOT,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(BROKER_HELPER),'exec'),broker_globals)
        broker,label=broker_globals['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-nographics','-quit','-upmIpcPath',label,
                '-projectPath',str(PROJECT),'-executeMethod','OpenRPGFirePitEffectMaterialIdentity.Export',
                '--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,
                env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_two_original_effect_shader_identities',unity_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0,'Native identity capture refused'
        check(prepared['input_pins'])
        report=read(ROOT/'MATERIALS.v1.json');assert report['unity_version']=='6000.5.8f1' and len(report['cases'])==2
        assert not report['gpu_rendered'] and not report['runtime_converted'] and not report['full_import']
        for request in prepared['cases']:
            row=next(r for r in report['cases'] if r['material_guid']==request['material_guid'])
            assert row['material_file_id']=='2100000' and row['shader_file_id']=='203'
            assert row['shader_guid']=='0000000000000000f000000000000000'
            assert row['shader_name'] and not row['rendered']
            assert len({p['name'] for p in row['properties']})==len(row['properties'])
        save('INDEPENDENT-REOPEN.v1.json',dict(input_pins=prepared['input_pins'],
            material_capture_sha256=sha(ROOT/'MATERIALS.v1.json'),materials_verified=2,
            properties_and_source_identities_reopened=True,shader_passes_compiled=False,
            gpu_rendered=False,runtime_converted=False,full_import=False))
        state.update(status='two_original_effect_shader_identities_reopened',native_material_identity_verified=True,
            materials_verified=2,independent_receipt_sha256=sha(ROOT/'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    finally:
        if broker is not None:broker_globals['stop_broker'](broker,state)
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
