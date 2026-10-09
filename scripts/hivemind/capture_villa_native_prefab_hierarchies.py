"""Queue the trusted prefab snapshot behind the existing Unity controllers."""
import datetime
import json
from pathlib import Path
import subprocess
import sys
import time

from recover_native_scene_check_queue import wait_exact
from plan_villa_native_prefab_hierarchies import sha

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK/'villa-native-prefab-hierarchy-capture-v1'
PREPARED = WORK/'villa-native-prefab-hierarchy-project-v1/PREPARED.v1.json'
UNITY = Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
JOBS = [(43256,639269118471258673),(45688,639269203327956258),
        (43320,639269220609065830),(34288,639269228636277089),
        (17752,639269292678355524),(45700,639269316542014449),
        (42212,639269442801261812)]


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def save(name,value):
    target=ROOT/name
    temporary=target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value,indent=2),'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(target)
            return
        except PermissionError:
            if attempt==39:raise
            time.sleep(.25)


def original_and_copy_pins(prepared,after=False):
    changed_private_meta=[]
    for copy in prepared['copies']:
        assert sha(Path(copy['source']))==copy['source_sha256'],'Original source changed'
        actual=sha(Path(copy['private_copy']))
        if after and Path(copy['private_copy']).suffix.lower()=='.meta':
            if actual!=copy['private_copy_sha256']:
                changed_private_meta.append({'path':copy['private_copy'],
                    'original_sha256':copy['private_copy_sha256'],'after_import_sha256':actual})
        else:
            assert actual==copy['private_copy_sha256'],'Private asset copy changed'
    for path,expected in prepared['input_pins'].items():
        assert sha(Path(path))==expected,'Prepared helper/request/project setting changed'
    return changed_private_meta


def prepare():
    assert not ROOT.exists(),'Prior capture preserved'
    prepared=read(PREPARED)
    assert prepared['expected_native_prefab_roots']==76 and prepared['expected_logical_scene_ids']==226
    assert not prepared['purchased_scripts_copied_or_executed']
    original_and_copy_pins(prepared)
    assert UNITY.is_file()
    ROOT.mkdir()
    paths=[Path(__file__).resolve(),PREPARED,UNITY,
           Path(__file__).with_name('recover_native_scene_check_queue.py'),
           Path(__file__).with_name('plan_villa_native_prefab_hierarchies.py')]
    save('PREPARED.v1.json',{'wait_exact_jobs':JOBS,'input_pins':{str(p):sha(p) for p in paths},
         'project_preparation':str(PREPARED),'expected_prefab_roots':76,'logical_scene_ids':226})
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],
             cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,
             creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',{'pid':child.pid,'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid':child.pid,'root':str(ROOT),'native_captures_executed':False}))


def run():
    state={'status':'waiting_for_existing_unity_controllers','native_prefab_roots_captured':0,
           'independent_pose_and_binding_reopen_verified':False,'gpu_rendered':False,
           'studio_verified':False,'full_import':False}
    save('RUN-RESULT.v1.json',state)
    try:
        queue=read(ROOT/'PREPARED.v1.json')
        for pid,birth in queue['wait_exact_jobs']:wait_exact(pid,birth)
        # Also wait on any editor already open; never overlap or terminate it.
        while True:
            command="@(Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object { @{pid=$_.Id;birth=$_.StartTime.ToUniversalTime().Ticks} }) | ConvertTo-Json -AsArray -Compress"
            output=subprocess.check_output([PS,'-NoProfile','-Command',command],text=True).strip()
            editors=json.loads(output or '[]')
            if not editors:break
            state['status']='waiting_for_open_unity_editor'
            save('RUN-RESULT.v1.json',state)
            for editor in editors:wait_exact(editor['pid'],editor['birth'])
        prepared=read(PREPARED)
        for path,expected in queue['input_pins'].items():assert sha(Path(path))==expected
        original_and_copy_pins(prepared)
        project=Path(prepared['project'])
        owned=Path(prepared['trusted_editor_helper'])
        for path in (project/'Assets').rglob('*'):
            if path.is_file() and path.suffix.lower() in ('.cs','.dll','.exe','.asmdef','.asmref','.bat','.cmd'):
                assert path==owned,'Untrusted executable dependency excluded'
        capture=Path(prepared['capture_output'])
        assert not capture.exists(),'Existing native capture retained'
        state['status']='capturing_76_native_prefab_hierarchies'
        save('RUN-RESULT.v1.json',state)
        command=[str(UNITY),'-batchmode','-nographics','-noUpm','-quit','-projectPath',str(project),
            '-executeMethod','OpenRPGPrefabHierarchyProbe.PrefabHierarchySnapshot.Export',
            '--openrpg-manifest',prepared['requests'],'--openrpg-hierarchy-output',str(capture),
            '-logFile',str(ROOT/'unity.log')]
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen(command,stdout=out,stderr=err,cwd=ROOT,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
            save('UNITY-JOB.v1.json',{'pid':child.pid,'project':str(project)})
            code=child.wait()
        changes=original_and_copy_pins(prepared,after=True)
        save('PRIVATE-IMPORTER-META-OBSERVATIONS.v1.json',changes)
        assert code==0,'Native hierarchy capture failed; logs retained'
        result=read(capture)
        assert result['total']==result['captured']==len(result['cases'])==76 and result['failed']==0
        requests=read(Path(prepared['requests']))['cases']
        expected={r['asset_id']:r for r in requests}
        assert {r['asset_id'] for r in result['cases']}==set(expected)
        for case in result['cases']:
            assert not case['error'] and case['root']['exact_asset_identity_available']
            assert case['root']['guid']==expected[case['asset_id']]['expected_guid']
        for path,value in queue['input_pins'].items():assert sha(Path(path))==value
        state.update(status='76_native_prefab_roots_captured_independent_reopen_pending',
             native_prefab_roots_captured=76,capture=str(capture),capture_sha256=sha(capture),
             unity_exit_code=code,original_sources_and_asset_copies_unchanged=True,
             private_importer_metadata_changes=len(changes),
             finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        state.update(status='refused_or_unverified',error=str(error))
    save('RUN-RESULT.v1.json',state)


if __name__=='__main__':
    prepare() if len(sys.argv)==1 else run()
