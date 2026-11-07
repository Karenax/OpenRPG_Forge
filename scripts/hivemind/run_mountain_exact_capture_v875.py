"""Native HDRP capture with exact LOD and raw repeats; never admit images implicitly."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, os, subprocess, sys, traceback

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W/'mountain-exact-native-capture-v875'
PREPARED = W/'mountain-exact-source-capture-v873/PREPARED.v1.json'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
BROKER = R/'scripts/hivemind/queue_delta_firepit_particle_behavior_v4.py'
UNITY = Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def save(state):
    state['utc'] = datetime.now(timezone.utc).isoformat()
    (O/'RUN-RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8')

if len(sys.argv) == 1:
    assert not O.exists()
    prepared = read(PREPARED)
    assert prepared['status'] == 'exact_lod_source_capture_prepared_not_launched'
    assert prepared['source_cards'] == 47 and prepared['capture_cases'] == 141
    O.mkdir();(O/'temp').mkdir()
    pins = {str(p):sha(p) for p in [Path(__file__),BROKER,PREPARED,UNITY]}
    (O/'CONTROLLER-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
    with (O/'controller.log').open('xb') as log:
        process = subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],cwd=R,stdout=log,stderr=log,
                                   stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=process.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(status='native_capture_controller_started',pid=process.pid,root=str(O))))
else:
    assert sys.argv[1:] == ['run']
    state = dict(status='native_capture_preflight',images_admitted=0,full_import=False,
                 original_sdk_pipeline_equivalence=False,actual_studio_gui_verified=False)
    broker = None
    namespace = None
    try:
        prepared = read(PREPARED)
        pins = dict(prepared['input_pins']);pins.update(read(O/'CONTROLLER-PINS.v1.json'))
        for path,digest in pins.items():
            assert sha(path) == digest, path
        project = Path(prepared['project'])
        expected_helpers = {'SourceLibraryThumbnailBatchV1.cs','PirateWoodLodMigrationV819.cs','SourceLibraryExactThumbnailBatchV873.cs'}
        assert {p.name for p in (project/'Assets').rglob('*.cs')} == expected_helpers
        assert len(list((project/'Assets').rglob('*.cs'))) == 3
        assert not list((project/'Assets').rglob('*.dll'))
        count = subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()
        assert count == '0', 'Preserve other Unity processes'
        tree = ast.parse(BROKER.read_text('utf-8-sig'))
        selected = [n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','start_broker','stop_broker'}]
        namespace = dict(ROOT=O,W=W,UPM=UNITY.parent/'Data/Resources/PackageManager/Server/UnityPackageManager.exe',LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(BROKER)+'#owned-broker-only','exec'),namespace)
        broker,label = namespace['start_broker'](state)
        env = os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        command = [str(UNITY),'-batchmode','-quit','-force-d3d11','-job-worker-count','2','-upmIpcPath',label,
                   '-projectPath',str(project),'-executeMethod',prepared['execute_method'],
                   '--openrpg-manifest',prepared['manifest'],'-logFile',str(O/'unity.log')]
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            process = subprocess.Popen(command,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_exact_mountain_source_lods',unity_pid=process.pid,command=command)
            save(state)
            state['unity_exit_code'] = process.wait()
        state['input_changes'] = [dict(path=p,before=h,after=sha(p) if Path(p).exists() else None) for p,h in pins.items() if not Path(p).exists() or sha(p)!=h]
        batch = read(Path(prepared['manifest']).parent/'NATIVE-RESULT.v1.json')
        assert state['unity_exit_code'] == 0 and len(batch['cases']) == 141
        outputs = {};pairs = [];refusals = []
        for index in range(47):
            reports = []
            for expected,actual in zip(prepared['cases'][index*3:index*3+3],batch['cases'][index*3:index*3+3]):
                assert actual['id'] == expected['id']
                if not actual['captured']:
                    refusals.append(actual);reports.append(None);continue
                receipt = Path(actual['receipt']);native = read(receipt);outputs[str(receipt)] = sha(receipt)
                assert native['source_object_unchanged'] and native['gpu'] == 'Direct3D11'
                assert native['requested_lod'] == expected['requested_lod'] and native['requested_lod_selection_verified']
                assert native['source_lod_groups'] == expected['expected_lod_groups']
                assert native['source_empty_lod_slots'] == expected['expected_empty_lod_slots']
                assert len(native['images']) == 2
                for image in native['images']:
                    for field,digest in [('path','sha256'),('raw_path','raw_sha256')]:
                        assert sha(image[field]) == image[digest]
                        outputs[image[field]] = image[digest]
                    assert Path(image['raw_path']).stat().st_size == 512*512*8
                reports.append(native)
            if reports[1] is not None and reports[2] is not None:
                for view in range(2):
                    a,b = reports[1]['images'][view],reports[2]['images'][view]
                    pairs.append(dict(source_card_id=prepared['cases'][index*3]['source_card_id'],view=view,
                                      raw_repeat_exact=a['raw_sha256']==b['raw_sha256']))
        state.update(status='native_capture_finished_requires_migration_and_visual_review',
                     output_pins=outputs,refused_cases=refusals,repeat_comparisons=pairs,
                     all_views_repeat_exact=len(pairs)==94 and all(p['raw_repeat_exact'] for p in pairs),
                     sdk_migration_review_required=True,images_admitted=0)
    except Exception as exc:
        state.update(status='native_capture_refusal_preserved',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None:
            namespace['stop_broker'](broker,state)
        save(state)
