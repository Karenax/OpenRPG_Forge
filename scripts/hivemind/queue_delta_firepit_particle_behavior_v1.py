"""Capture both effective inherited systems inside complete FirePit parent clones."""
from pathlib import Path
import datetime
import copy
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import struct

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-firepit-particle-behavior-v1'
PROJECT = W / 'hivemind-prefab-instance-source-closure-v1/projects/native-american-village'
UNITY = Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value, new=False):
    target = ROOT / name
    if new:
        with target.open('x', encoding='utf-8') as stream:
            json.dump(value, stream, indent=2, allow_nan=False)
    else:
        temporary = target.with_suffix('.tmp')
        temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
        os.replace(temporary, target)


def check(pins):
    for path,digest in pins.items():
        assert sha(path) == digest, 'Changed input: ' + path


def unity_idle():
    output = subprocess.run([PS, '-NoProfile', '-Command',
        'Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object {$_.Id}'],
        capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW).stdout
    assert not output.strip(), 'Existing Unity session preserved; wait for actual completion'


def effective_particle(row):
    original = next(d['data'] for d in row['original_complete_documents'] if d['type']=='ParticleSystem')
    effective = copy.deepcopy(original)
    applied = []
    for edit in row['complete_parent_modifications']['m_Modifications']:
        target = edit['target']
        assert target['guid'] == row['inherited_prefab_guid']
        assert target['fileID'] != row['particle_renderer_file_id'], 'Renderer override requires explicit native comparison'
        if target['fileID'] != row['particle_system_file_id']:
            continue
        assert edit['objectReference']['fileID']=='0'
        path = edit['propertyPath'].split('.')
        obj = effective
        for key in path[:-1]:
            assert isinstance(obj,dict) and key in obj, 'Unknown serialized particle override path'
            obj = obj[key]
        assert isinstance(obj,dict) and path[-1] in obj
        before = obj[path[-1]]
        value = edit['value']
        assert type(before) in {int,float} and type(value) in {int,float}
        # YAML may spell a Unity float as an integer (simulationSpeed: 1).
        # Retain the authored override scalar without inferring Unity type from spelling.
        obj[path[-1]] = value
        applied.append(dict(property_path=edit['propertyPath'], before=before, after=value))
    return effective,applied


def prepare():
    assert not ROOT.exists() and UNITY.is_file()
    unity_idle()
    source_root = W / 'delta-complete-prefab-source-reopen-v1'
    source = read(source_root / 'RESULT.v1.json')
    assert source['effect_sources_reopened'] == 2
    check(source['input_pins']);check(source['output_pins'])
    plan = read(W / 'nested-prefab-scene-bindings-v2/RESULT.v1.json')
    prefab = next(r for r in plan['rows'] if r['original_root']['guid'] == 'abc00000000014341674918090329798')
    cases = []
    for ordinal in range(2):
        row = read(source_root / f'{ordinal:02}-SOURCE-IR.v1.json')
        effective,applied = effective_particle(row)
        node = next(n['source_node'] for n in prefab['nodes'] if n['source_node']['path'] == row['native_node_path'])
        matrix = node['source_local_to_world_matrix_row_major']
        cases.append(dict(node_path=row['native_node_path'], renderer_file_id=row['native_hierarchy_renderer']['component']['file_id'],
            material_guid=row['material_guid'], inherited_prefab_guid=row['inherited_prefab_guid'],
            inherited_particle_file_id=row['particle_system_file_id'], inherited_renderer_file_id=row['particle_renderer_file_id'],
            source_local_to_world={f'e{r}{c}':matrix[4*r+c] for r in range(4) for c in range(4)},
            expected_particle_overrides=applied,
            original_source_ir=str(source_root / f'{ordinal:02}-SOURCE-IR.v1.json')))
    assert sum(len(c['expected_particle_overrides']) for c in cases)==5
    helper = Path(__file__).with_name('candidates') / 'FirePitParticleBehavior.cs'
    destination = PROJECT / 'Assets/Editor/OpenRPGFirePitParticleBehavior.cs'
    assert not destination.exists() and not Path(str(destination)+'.meta').exists()
    ROOT.mkdir()
    (ROOT/'temp').mkdir()
    destination.parent.mkdir(exist_ok=True)
    shutil.copyfile(helper,destination)
    verifier = W / 'unity-dungeon-particle-dust-behavior-v1/verify_behavior.v2.py'
    original_verifier = verifier.read_text('utf-8-sig')
    assert 'isolated clone; Random.InitState(1729);' in original_verifier
    (ROOT/'verify_frames.py').write_text(original_verifier.replace('isolated clone; Random.InitState(1729);',
        'isolated complete-parent clone; Random.InitState(1729);'), 'utf-8')
    pins = {str(p):sha(p) for p in [Path(__file__).resolve(), helper, destination, verifier,
        ROOT/'verify_frames.py', source_root/'RESULT.v1.json', UNITY]}
    pins.update(source['input_pins']);pins.update(source['output_pins'])
    # Fresh project pins keep previous private import normalizations distinct.
    for path in (PROJECT/'Assets').rglob('*'):
        if path.is_file():
            pins[str(path)] = sha(path)
    for folder in ['ProjectSettings','Packages']:
        for path in (PROJECT/folder).glob('*'):
            if path.is_file():pins[str(path)] = sha(path)
    manifest = dict(output=str(ROOT/'BEHAVIOR.v1.json'),prefab_path=prefab['original_root']['asset_path'],
        prefab_guid=prefab['original_root']['guid'],cases=cases,seeds=[1729,1234567],
        times=[0,0.05,0.1,0.25,0.5,1,2,3,4,5,6,8,10,12],repetitions=2)
    save('manifest.json',manifest,new=True)
    pins[str(ROOT/'manifest.json')] = sha(ROOT/'manifest.json')
    save('PREPARED.v1.json',dict(input_pins=pins, project=str(PROJECT), source_cases=cases,
        original_sources_preserved=True, temporal_behavior_verified=False, full_import=False),new=True)
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()),new=True)
    print(json.dumps(dict(controller_pid=child.pid,effect_systems=2,expected_seeded_frames=112,
        original_complete_parent_nodes=9,receipt_root=str(ROOT),full_import=False)))


def run():
    state=dict(status='preparing_two_effect_native_capture',native_behavior_verified=False,
        gpu_rendered=False,studio_verified=False,full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins']);unity_idle()
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),
            UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-nographics','-noUpm','-quit',
                '-projectPath',str(PROJECT),'-executeMethod','OpenRPGFirePitParticleBehavior.Export',
                '--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],
                cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_two_inherited_effects_inside_complete_parent',unity_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0,'Native capture refused; preserve source and logs'
        check(prepared['input_pins'])
        report=read(ROOT/'BEHAVIOR.v1.json');assert len(report['cases'])==2 and not report['full_import']
        request=read(ROOT/'manifest.json')
        verifier_path=ROOT/'verify_frames.py'
        spec=importlib.util.spec_from_file_location('firepit_frame_verifier',verifier_path)
        verifier=importlib.util.module_from_spec(spec);spec.loader.exec_module(verifier)
        controls=verifier.controls()
        summaries=[]
        for case in prepared['source_cases']:
            result=next(r for r in report['cases'] if r['node_path']==case['node_path'])
            source=read(case['original_source_ir'])
            particle,applied=effective_particle(source)
            assert applied==case['expected_particle_overrides']
            native=verifier.load_native(result['native_particle_json'],'ParticleSystem')
            for edit in applied:
                value=native
                for key in edit['property_path'].split('.'):
                    value=value[key]
                expected=edit['after']
                if type(expected) is float:
                    assert struct.pack('<f',value)==struct.pack('<f',expected), 'Native particle override differs'
                else:
                    assert value==expected, 'Native particle override differs'
            scoped=dict(request,particle_file_id=result['particle_file_id'],renderer_file_id=case['renderer_file_id'],material_guid=case['material_guid'])
            summary=verifier.check(result,scoped,particle)
            assert summary['rows']==56
            # Reopen the exact effective inherited source frame at every time.
            for row in result['rows']:
                for key,value in case['source_local_to_world'].items():
                    assert abs(row['local_to_world'][key]-value)<=1e-6
            summaries.append(dict(node_path=case['node_path'], native_particle_overrides_reopened=applied, **summary))
        check(prepared['input_pins'])
        save('INDEPENDENT-REOPEN.v1.json',dict(input_pins=prepared['input_pins'],behavior_sha256=sha(ROOT/'BEHAVIOR.v1.json'),
            summaries=summaries,controls=controls,frames_verified=112,effect_systems_verified=2,
            source_effective_overrides_and_transforms_retained=True,seeded_native_reference_verified=True,
            autonomous_play_or_prewarm_parity_qualified=False,gpu_rendered=False,runtime_converted=False,full_import=False),new=True)
        state.update(status='two_effects_and_112_seeded_native_frames_reopened',native_behavior_verified=True,
            effect_systems_verified=2,frames_verified=112,independent_receipt_sha256=sha(ROOT/'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)


if __name__=='__main__':
    run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
