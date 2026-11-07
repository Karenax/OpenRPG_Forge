"""Run native GLB/image/material admission in the real product workspace."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-paint-door-native-resource-admission-v2'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
EXAMPLE = R / 'crates/forge_engine/examples/verify_source_glb_resources.rs'
GLB = W / 'townsmith-paint-door-glb-v1/SM_Door-Single-B.source-material.candidate.glb'

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,value):
    temp=ROOT/(name+'.tmp'); temp.write_text(json.dumps(value,indent=2),encoding='utf-8');temp.replace(ROOT/name)

def prepare():
    assert not ROOT.exists()
    assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process cargo -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    assert sha(GLB)=='c9e500dd9c07e2b655441d4e748c03caa0f18b2ee38e114db5049f4859f09c49'
    subprocess.run([str(BIN/'rustfmt.exe'),'--edition','2024',str(EXAMPLE)],check=True)
    paths=[Path(__file__),EXAMPLE,GLB,R/'Cargo.toml',R/'Cargo.lock',R/'.cargo/config.toml',
        W/'townsmith-paint-door-glb-reopen-v1/RESULT.v1.json',W/'townsmith-s-paint-bevy-pipeline-reopen-v3/RESULT.v1.json']
    for crate in ['forge_engine','forge_assets','forge_navigation']:
        paths += [R/'crates'/crate/'Cargo.toml',*(R/'crates'/crate/'src').rglob('*.rs')]
    pins={str(p):sha(p) for p in paths}
    ROOT.mkdir()
    save('INPUT.v1.json',dict(glb=str(GLB),glb_sha256=sha(GLB)))
    pins[str(ROOT/'INPUT.v1.json')]=sha(ROOT/'INPUT.v1.json')
    save('PREPARED.v1.json',dict(input_pins=pins,lot='L03.TOWNSMITH.RESSOURCES',
        expected_textures=10,expected_mips=120,actual_drawing=False,full_import=False))
    save('RUN-RESULT.v1.json',dict(status='native_resource_admission_prepared',full_import=False,complete_assets_delivered_increment=0))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid,root=str(ROOT))))

def run():
    state=read(ROOT/'RUN-RESULT.v1.json')
    try:
        prep=read(ROOT/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items():assert sha(Path(p))==h,p
        env=os.environ.copy()
        env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),
            RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',
            CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        command=[str(BIN/'cargo.exe'),'run','--locked','--offline','-p','forge_engine','--example','verify_source_glb_resources','--',str(ROOT)]
        save('COMMAND.v1.json',dict(command=command,cwd=str(R)))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='actual_native_glb_resource_admission_running',cargo_pid=child.pid)
            save('RUN-RESULT.v1.json',state)
            state['actual_exit_code']=child.wait()
        assert child.returncode==0
        native=read(ROOT/'NATIVE-RESULT.v1.json')
        assert len(native['textures'])==10 and all(t['actual_binding_identity_verified'] for t in native['textures'])
        assert not native['gpu_validation_errors'] and not native['authored_texture_substitution']
        for p,h in prep['input_pins'].items():assert sha(Path(p))==h,p
        state.update(status=native['status'],native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),
            textures_gpu_resident=10,mips_gpu_resident=120,material_bind_group_created=True,
            source_uniform_bytes=native['source_uniform_bytes'],gpu_draw_executed=False,
            source_render_parity_qualified=False,forge_editor_verified=False)
    except Exception as exc:
        state.update(status='native_glb_resource_admission_refusal_retained',error=repr(exc),traceback=__import__('traceback').format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__': prepare() if len(sys.argv)==1 else run()
