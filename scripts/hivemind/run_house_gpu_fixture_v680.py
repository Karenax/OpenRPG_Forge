"""Run the prepared controlled House GPU fixture and retain terminal evidence."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys
import traceback

R=Path(__file__).resolve().parents[2];W=Path('F:/OpenRPG_ImportWork');O=W/'house-controlled-gpu-fixture-v679'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(state):
    state.update(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),full_import=False,studio_verified=False,unity_render_parity=False)
    temp=O/'RUN-RESULT.tmp';temp.write_text(json.dumps(state,indent=2),'utf-8');temp.replace(O/'RUN-RESULT.v1.json')
def run():
    state=dict(status='controlled_house_gpu_build_running')
    try:
        prep=read(O/'PREPARED.v1.json');pins=prep['input_pins']
        for p,h in pins.items():assert sha(p)==h,p
        pins[str(Path(__file__))]=sha(Path(__file__))
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
        command=[str(BIN/'cargo.exe'),'run','--locked','--offline','-j','1','-p','forge_engine','--example',prep['example'],'--',str(O)]
        (O/'COMMAND.v1.json').write_text(json.dumps(dict(command=command,cwd=str(R)),indent=2),'utf-8')
        with (O/'stdout.log').open('xb') as out,(O/'stderr.log').open('xb') as err:
            child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid']=child.pid;save(state);state['actual_exit_code']=child.wait()
        assert child.returncode==0,'See stderr.log'
        native=read(O/'NATIVE-RESULT.v1.json')
        assert native['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3
        assert native['native_images']==3 and native['native_mips']==36
        image=O/'FORGE-LINEAR.rgba16f';assert image.stat().st_size==512*512*8
        for p,h in pins.items():assert sha(p)==h,p
        state.update(status='controlled_house_gpu_draw_and_native_bc_bindings_verified',input_pins=pins,native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'),image_sha256=sha(image),gpu_rendered=True)
    except Exception as e:
        state.update(status='controlled_house_gpu_refusal_retained',error=repr(e),traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save(state)
    print(json.dumps({k:state.get(k) for k in ['status','actual_exit_code','error']}))
if __name__=='__main__':
    if sys.argv[1:]==['run']:run()
    else:
        assert not (O/'RUN-RESULT.v1.json').exists()
        save(dict(status='controlled_house_gpu_prepared'))
        with (O/'controller.log').open('xb') as log:
            child=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid,one_shot=True)),'utf-8')
        print(json.dumps(dict(controller_pid=child.pid,root=str(O))))
