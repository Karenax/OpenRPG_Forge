"""Test the forward material and factory inside the actual engine workspace.

Retain 68 source_graph tests, seven new material/program tests and the separate
17-test Studio receipt. No external host graph or production installation.
"""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-unlit-canonical-native-check-v1'
SNAP=W/'source-unlit-canonical-snapshot-v1'
OLD=W/'source-graph-production-color-mask-snapshot-v4'
PROOF=W/'source-graph-production-color-mask-check-v4'
HERE=Path(__file__).resolve().parent
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET=Path('F:/OpenRPG_Forge/relocated-work/Rust-target')


def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for path,digest in pins.items():assert sha(Path(path))==digest,'Pinned byte change: '+path
def save(name,data):
    p=ROOT/(name+'.tmp');p.write_text(json.dumps(data,indent=2),'utf-8');p.replace(ROOT/name)


def prepare():
    assert not ROOT.exists() and not SNAP.exists()
    native=read(PROOF/'RUN-RESULT.v1.json');assert native['actual_renderer_tests_passed']==68 and native['host_tests_passed']==17
    assert read(W/'source-unlit-forward-native-check-v2/RUN-RESULT.v1.json').get('finished_utc')
    assert read(W/'source-unlit-program-native-check-v1/RUN-RESULT.v1.json').get('finished_utc')
    frozen=read(PROOF/'PREPARED.v1.json')['frozen_pins'];check(frozen)
    ROOT.mkdir();SNAP.mkdir();(ROOT/'temp').mkdir()
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),PROOF/'PREPARED.v1.json',PROOF/'RUN-RESULT.v1.json',
        W/'source-unlit-forward-native-check-v2/RUN-RESULT.v1.json',
        W/'source-graph-production-color-mask-install-v1/INSTALL.v1.json']}
    pins.update(frozen);outputs={}
    for path,digest in frozen.items():
        original=Path(path)
        if not original.is_relative_to(OLD):continue
        copied=SNAP/original.relative_to(OLD);copied.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(original,copied);assert sha(copied)==digest
        outputs[str(copied)]=digest
    new_tests=[]
    for name in ['source_unlit_forward.rs','source_unlit_forward_program.rs']:
        candidate=HERE/'candidates'/name;pins[str(candidate)]=sha(candidate)
        text=candidate.read_text('utf-8-sig').replace('forge_engine::source_graph::','crate::source_graph::')
        new_tests.extend(re.findall(r'#\[test\]\s*fn\s+(\w+)',text))
        destination=SNAP/'crates/forge_engine/src'/name;destination.write_text(text,'utf-8')
        outputs[str(destination)]=sha(destination)
    assert len(new_tests)==7
    bridge=SNAP/'crates/forge_engine/src/source_graph.rs';text=bridge.read_text('utf-8-sig')
    text='''#[path="source_unlit_forward.rs"]
mod source_unlit_forward;
pub use source_unlit_forward::{SourceUnlitForwardExtension,SourceUnlitForwardKey,SourceUnlitForwardMaterial,SourceUnlitForwardPlugin};
#[path="source_unlit_forward_program.rs"]
mod source_unlit_forward_program;
pub use source_unlit_forward_program::{SourceUnlitForwardProgram,register_source_unlit_forward_material};
'''+text
    marker='app.add_plugins(MaterialPlugin::<SourceGraphMaterial>::default());'
    assert text.count(marker)==1
    text=text.replace(marker,marker+'\n        app.add_plugins(SourceUnlitForwardPlugin);')
    bridge.write_text(text,'utf-8');outputs[str(bridge)]=sha(bridge)
    old_output=(PROOF/'checking_actual_renderer_source_graph_tests.stdout.log').read_text('utf-8-sig')
    old_tests=re.findall(r'^test (\S+) \.\.\. ok$',old_output,re.M);assert len(old_tests)==68
    pins[str(PROOF/'checking_actual_renderer_source_graph_tests.stdout.log')]=sha(PROOF/'checking_actual_renderer_source_graph_tests.stdout.log')
    check(pins);check(outputs)
    save('PREPARED.v1.json',dict(input_pins=pins,frozen_pins=outputs,new_test_names=new_tests,
        preserved_engine_test_names=old_tests,existing_studio_tests_reused=17,
        canonical_module_layout=True,external_host_serde_failure_retained=True,
        canonical_installed=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,expected_engine_tests_at_least=75)))


def run():
    state=dict(status='checking_material_and_program_inside_actual_engine',actual_material_tests_passed=0,
        actual_program_tests_passed=0,actual_engine_tests_passed=0,existing_studio_tests_reused=17,
        canonical_installed=False,full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins']);check(prepared['frozen_pins'])
        env=os.environ.copy();env.update(RUSTC=str(BIN/'rustc.exe'),CARGO_TARGET_DIR=str(TARGET),CARGO_BUILD_JOBS='1',
            TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),PATH=str(BIN)+os.pathsep+env.get('PATH',''))
        with (ROOT/'testing.stdout.log').open('xb') as out,(ROOT/'testing.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(BIN/'cargo.exe'),'test','--manifest-path',str(SNAP/'Cargo.toml'),
                '-p','forge_engine','--lib','--locked','--offline','-j','1','source_','--','--test-threads=1'],
                cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid']=child.pid;save('RUN-RESULT.v1.json',state);code=child.wait()
        state['testing_exit_code']=code;assert code==0,'Actual canonical engine build/test refused'
        output=(ROOT/'testing.stdout.log').read_text('utf-8-sig')
        passed=set(re.findall(r'^test (\S+) \.\.\. ok$',output,re.M))
        assert set(prepared['preserved_engine_test_names']).issubset(passed)
        for name in prepared['new_test_names']:assert any(test.endswith('::'+name) for test in passed),name
        assert re.search(r'test result: ok\. \d+ passed; 0 failed;',output)
        check(prepared['input_pins']);check(prepared['frozen_pins'])
        state.update(status='actual_engine_material_and_factory_tests_passed',actual_material_tests_passed=4,
            actual_program_tests_passed=3,actual_engine_tests_passed=len(passed),source_pins_unchanged=True)
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=repr(error),diagnostic_traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)


if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
