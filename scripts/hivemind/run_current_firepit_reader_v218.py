"""Rebuild the actual product GLB reader against the current pinned source tree."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess,sys,traceback
S=Path(__file__).resolve().parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork')
O=W/'firepit-current-product-reader-v218';OLD=W/'firepit-static-glb-product-check-v2'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(state):
    state.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False,gpu_rendered=False,studio_verified=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
    for name in ['Cargo.toml','Cargo.lock']:(O/name).write_bytes((OLD/name).read_bytes())
    text=(OLD/'src/main.rs').read_text('utf-8-sig')
    marker=' println!("{}",json!('
    assert text.count(marker)==1
    negative=''' let json_len=u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
 let view_index=doc["materials"][0]["extensions"]["OPENRPG_source_graph"]["forward"]["buffer_view"].as_u64().unwrap() as usize;
 let at=doc["bufferViews"][view_index]["byteOffset"].as_u64().unwrap() as usize;
 let mut wrong_shader=raw.clone();wrong_shader[28+json_len+at]^=1;
 assert!(forge_assets::source_graph_glb::read_source_graph_glb(&wrong_shader).is_err(),"corrupt source shader must refuse");
'''
    text=text.replace(marker,negative+marker).replace('"header_negative_control_passed":true','"header_negative_control_passed":true,"shader_integrity_negative_control_passed":true')
    (O/'src/main.rs').write_text(text,'utf-8')
    assembly=W/'firepit-complete-static-glb-v3/RESULT.v1.json';row=read(assembly)['rows'][0];candidate=Path(row['glb']);assert sha(candidate)==row['sha256']
    pins={str(p):sha(p) for crate in ['forge_assets','forge_core'] for p in (R/'crates'/crate).rglob('*') if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml')}
    for p in [Path(__file__),O/'Cargo.toml',O/'Cargo.lock',O/'src/main.rs',OLD/'src/main.rs',R/'Cargo.toml',R/'Cargo.lock',R/'rust-toolchain.toml',assembly,candidate]:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,candidate=row),indent=2),'utf-8')
    save(dict(status='current_firepit_product_reader_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,candidate=str(candidate),root=str(O))))
def run():
    state=read(O/'RUN-RESULT.v1.json')
    try:
        prep=read(O/'PREPARED.v1.json');pins=prep['input_pins']
        for p,h in pins.items():assert sha(p)==h,p
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(OLD/'target'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'))
        with (O/'cargo.log').open('xb') as out:
            proc=subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml')],env=env,cwd=O,stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='building_current_firepit_product_reader',cargo_pid=proc.pid);save(state);state['cargo_exit_code']=proc.wait()
        assert state['cargo_exit_code']==0,'Current product reader build refused; see cargo.log'
        for p,h in pins.items():assert sha(p)==h,p
        exe=OLD/'target/debug/firepit-static-glb-product-check.exe'
        with (O/'checker.stdout.log').open('xb') as out,(O/'checker.stderr.log').open('xb') as err:
            proc=subprocess.Popen([str(exe),prep['candidate']['glb']],env=env,cwd=O,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='executing_current_product_glb_reader',checker_pid=proc.pid);save(state);state['checker_exit_code']=proc.wait()
        assert state['checker_exit_code']==0,(O/'checker.stderr.log').read_text('utf-8',errors='replace')[-1800:]
        result=read(O/'checker.stdout.log');assert result['sha256']==prep['candidate']['sha256']
        assert result['nodes']==9 and result['material_contracts']==1 and result['unique_mesh_primitives']==4
        assert result['header_negative_control_passed'] and result['shader_integrity_negative_control_passed']
        for p,h in pins.items():assert sha(p)==h,p
        state.update(status='current_product_firepit_glb_reader_and_integrity_controls_passed',result=result,
            source_inputs_unchanged=True,input_pins=pins,checker_sha256=sha(exe),result_sha256=sha(O/'checker.stdout.log'))
    except Exception as exc:state.update(status='current_product_firepit_glb_reader_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
