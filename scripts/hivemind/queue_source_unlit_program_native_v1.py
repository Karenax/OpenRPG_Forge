"""Retain 21 material/Studio assertions and add three real program tests."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import tomllib

HERE=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-unlit-program-native-check-v1'
BASE=W/'source-unlit-forward-native-check-v2'
CORE=W/'source-graph-production-color-mask-snapshot-v3/crates/forge_core'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET=Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
CANDIDATE=HERE/'candidates/source_unlit_forward_program.rs'
TESTS=['source_unlit_program_registers_real_shader_and_preserves_source_phase',
    'source_unlit_program_missing_images_bad_mask_or_entry_refuse_before_registration',
    'source_unlit_program_wrong_full_sha_keeps_existing_shader_bytes']
def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Changed source: '+p
def save(name,data):
    p=ROOT/(name+'.tmp'); p.write_text(json.dumps(data,indent=2),'utf-8'); p.replace(ROOT/name)

def run():
    state=dict(status='waiting_for_actual_material_tests',actual_program_tests_passed=0,
        actual_material_tests_passed=0,actual_studio_tests_passed=0,installed=False,full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        result_path=BASE/'RUN-RESULT.v1.json'
        while not result_path.is_file() or not read(result_path).get('finished_utc'): time.sleep(15)
        result=read(result_path)
        assert result['actual_material_tests_passed']==4 and result['actual_studio_tests_passed']==17,'Actual material prerequisite unverified'
        prepared=read(BASE/'PREPARED.v1.json'); pins=dict(prepared['input_pins'])
        pins.update(read(ROOT/'QUEUED.v1.json')['input_pins']); pins[str(result_path)]=sha(result_path)
        for p in [BASE/'PREPARED.v1.json',BASE/'Cargo.toml',BASE/'Cargo.lock',BASE/'src/main.rs',BASE/'src/source_unlit_forward.rs']:
            pins[str(p)]=sha(p)
        check(pins)
        shutil.copyfile(BASE/'src/source_unlit_forward.rs',ROOT/'src/source_unlit_forward.rs')
        for name in ['Cargo.toml','Cargo.lock','src/main.rs']:
            text=(BASE/name).read_text('utf-8')
            text=text.replace('openrpg-source-unlit-forward-host-check','openrpg-source-unlit-program-host-check')
            if name=='Cargo.toml':
                assert text.count('[dependencies]')==1
                text=text.replace('[dependencies]', '[dependencies]\nforge_core={path="'+CORE.as_posix()+'"}')
            if name=='Cargo.lock':
                before=tomllib.loads((BASE/name).read_text('utf-8'))['package']
                marker='name = "openrpg-source-unlit-program-host-check"'
                assert text.count(marker)==1
                index=text.index(marker); end=text.find('[[package]]',index)
                end=len(text) if end<0 else end
                block=text[index:end]
                assert ' "forge_core",' not in block and block.count(' "forge_assets",')==1
                text=text[:index]+block.replace(' "forge_assets",',' "forge_assets",\n "forge_core",')+text[end:]
                after=tomllib.loads(text)['package']
                old=[x for x in before if x['name']!='openrpg-source-unlit-forward-host-check']
                new=[x for x in after if x['name']!='openrpg-source-unlit-program-host-check']
                assert old==new,'Transitive locked package changed'
                save('LOCK-ROOT-DEPENDENCY.v1.json',dict(transitive_packages_identical=True,
                    added_existing_transitive_root_dependency='forge_core',old_lock_sha256=sha(BASE/name),locked_verification=True))
            if name=='src/main.rs': text+='\nmod source_unlit_forward_program;\n'
            (ROOT/name).write_text(text,'utf-8'); pins[str(ROOT/name)]=sha(ROOT/name)
        pins[str(ROOT/'src/source_unlit_forward.rs')]=sha(ROOT/'src/source_unlit_forward.rs'); check(pins)
        save('PREPARED.v1.json',dict(input_pins=pins,expected_tests=24,old_studio_tests=17,old_material_tests=4,
            program_tests=TESTS,installed=False,gpu_qualified=False,full_import=False))
        env=os.environ.copy(); env.update(RUSTC=str(BIN/'rustc.exe'),CARGO_TARGET_DIR=str(TARGET),CARGO_BUILD_JOBS='1',
            TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),PATH=str(BIN)+os.pathsep+env.get('PATH',''))
        state['status']='testing_atomic_program_and_all_prior_material_studio_assertions'; save('RUN-RESULT.v1.json',state)
        with (ROOT/'testing.stdout.log').open('xb') as out,(ROOT/'testing.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(BIN/'cargo.exe'),'test','--locked','--offline','-j','1','--','--test-threads=1'],
                cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid']=child.pid; save('RUN-RESULT.v1.json',state); code=child.wait()
        state['testing_exit_code']=code; assert code==0,'Actual program build/test refused'
        output=(ROOT/'testing.stdout.log').read_text('utf-8-sig')
        assert 'test result: ok. 24 passed; 0 failed;' in output
        for name in TESTS: assert re.search(r'::'+name+r' \.\.\. ok',output),'Missing program test: '+name
        check(pins)
        state.update(status='atomic_forward_program_material_and_studio_tests_passed',actual_program_tests_passed=3,
            actual_material_tests_passed=4,actual_studio_tests_passed=17,source_pins_unchanged=True)
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json',state)

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='run': run()
    else:
        assert not ROOT.exists(); ROOT.mkdir(); (ROOT/'src').mkdir(); (ROOT/'temp').mkdir()
        shutil.copyfile(CANDIDATE,ROOT/'src/source_unlit_forward_program.rs')
        pins={str(p):sha(p) for p in [CANDIDATE,Path(__file__).resolve(),ROOT/'src/source_unlit_forward_program.rs',BIN/'cargo.exe',BIN/'rustc.exe']}
        save('QUEUED.v1.json',dict(input_pins=pins,expected_tests=24,full_import=False))
        with (ROOT/'controller.log').open('xb') as out:
            child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
                stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
        save('JOB.v1.json',dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid,expected_tests=24)))
