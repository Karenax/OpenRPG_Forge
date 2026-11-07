"""Execute actual source-material drawing, using the measured Unity context."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys
S=Path(__file__).parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork')
LAYER=int(os.environ['OPENRPG_PRIVATE_LIGHT_LAYER']);assert 0<=LAYER<4
ROOT=W/f'townsmith-native-material-quad-difference-layer-{LAYER}-v68'
SOURCE=W/'townsmith-door-source-camera-brdf-v10'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
EXAMPLE=R/'crates/forge_engine/examples/verify_source_glb_hdr_buffer_v56.rs'
GLB=W/'townsmith-material-quad-difference-carrier-v68/SM_Door-Single-B.material-quad-difference.candidate.glb'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,value):
    temp=ROOT/(name+'.tmp');temp.write_text(json.dumps(value,indent=2),encoding='utf-8');temp.replace(ROOT/name)
def prepare():
    assert not ROOT.exists()
    source=read(SOURCE/'RUN-RESULT.v1.json')
    assert source['neutral_reference_byte_equal'] and source['source_inputs_unchanged'] and source['actual_exit_code']==0
    assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process cargo -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    assert sha(GLB)=='71e19d6df06f6cbf17db617615ef1be17291e1f5e90f7abaae7b35370e608484'
    subprocess.run([str(BIN/'rustfmt.exe'),'--edition','2024',str(EXAMPLE)],check=True)
    paths=[Path(__file__),EXAMPLE,GLB,R/'Cargo.toml',R/'Cargo.lock',R/'.cargo/config.toml',
        SOURCE/'RUN-RESULT.v1.json',SOURCE/'NATIVE-RESULT.v1.json',SOURCE/'PREPARED.v1.json',
        SOURCE/'door-linear.context.json',SOURCE/'door-linear.rgba16f',SOURCE/'door-neutral.rgba16f',
        SOURCE/'door-linear.skybox-runtime.json',W/'townsmith-paint-door-native-resource-admission-reopen-v3/RESULT.v1.json']
    for crate in ['forge_engine','forge_assets','forge_navigation']:
        paths += [R/'crates'/crate/'Cargo.toml',*(R/'crates'/crate/'src').rglob('*.rs'),*(R/'crates'/crate/'src').rglob('*.wgsl')]
    paths += [Path('F:\\OpenRPG_ImportWork\\firepit-source-fgd-reference-v1\\source-fgd.rgba32f'),W/'townsmith-door-source-gpu-ambient-reopen-v1/RESULT.v1.json',W/'townsmith-paint-door-ambient-carrier-v1/RESULT.v1.json']
    paths += [Path('F:\\OpenRPG_ImportWork\\townsmith-door-source-sky-capture-reopen-v8\\actual-source-sky.mip-major.rgba16f'),SOURCE/'door-linear.fgd.rgba32f',W/'townsmith-material-quad-difference-carrier-v68/RESULT.v1.json',W/'townsmith-door-source-sky-repeat-reopen-v9/RESULT.v1.json']
    pins={str(p):sha(p) for p in paths}
    paths += [W/'townsmith-source-microshadow-inputs-reopen-v55/RESULT.v1.json']
    pins={str(p):sha(p) for p in paths}
    ROOT.mkdir()
    epoch=ROOT/'source-input-epoch';epoch.mkdir();snapshots=[]
    for path in paths:
        if path.is_relative_to(R):
            target=epoch/path.relative_to(R);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(path.read_bytes());assert sha(target)==pins[str(path)];snapshots.append(dict(live_path=str(path),snapshot=str(target),sha256=pins[str(path)]));pins[str(target)]=sha(target)
    save('SOURCE-EPOCH.v1.json',dict(snapshots=snapshots));pins[str(ROOT/'SOURCE-EPOCH.v1.json')]=sha(ROOT/'SOURCE-EPOCH.v1.json')
    save('INPUT.v1.json',dict(diagnostic_layer=LAYER,glb=str(GLB),glb_sha256=sha(GLB),source_context=str(SOURCE/'door-linear.context.json'),
        source_reference=str(SOURCE/'door-linear.rgba16f'),brdf_path='F:\\OpenRPG_ImportWork\\firepit-source-fgd-reference-v1\\source-fgd.rgba32f',brdf_sha256='d962fd1333474b433e12b951ee7f492d7f21307dc28f33d1b9ff6d4a51cd769c',sky_path='F:\\OpenRPG_ImportWork\\townsmith-door-source-sky-capture-reopen-v8\\actual-source-sky.mip-major.rgba16f',sky_sha256='90e75d0d344fe70f8c9d5cce0c1f38c4fddc16cbcce0ae5a854c0b575df58e70',source_context_sha256=sha(SOURCE/'door-linear.context.json')))
    pins[str(ROOT/'INPUT.v1.json')]=sha(ROOT/'INPUT.v1.json')
    save('PREPARED.v1.json',dict(input_pins=pins,lot='L03.TOWNSMITH.DESSIN',source_scene_parameters_measured=True,
        full_import=False,source_lighting_parity_qualified=False))
    save('RUN-RESULT.v1.json',dict(status='actual_source_glb_draw_prepared',full_import=False,complete_assets_delivered_increment=0))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,
            stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,root=str(ROOT))))
def run():
    state=read(ROOT/'RUN-RESULT.v1.json')
    try:
        prep=read(ROOT/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items():assert sha(Path(p))==h,p
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),
            RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='1',
            CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        command=[str(BIN/'cargo.exe'),'run','--locked','--offline','-p','forge_engine','--example','verify_source_glb_hdr_buffer_v56','--',str(ROOT)]
        save('COMMAND.v1.json',dict(command=command,cwd=str(R)))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='actual_source_glb_draw_build_and_camera_running',cargo_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait()
        assert child.returncode==0
        native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3
        for p,h in prep['input_pins'].items():assert sha(Path(p))==h,p
        state.update(status=native['status'],gpu_draw_executed=True,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),
            forge_packed_image_sha256=sha(ROOT/'FORGE-PACKED.rg11b10'),source_render_parity_qualified=False,forge_editor_verified=False)
    except Exception as exc:state.update(status='actual_source_glb_draw_refusal_retained',error=repr(exc),traceback=__import__('traceback').format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
