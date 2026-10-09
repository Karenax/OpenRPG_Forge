"""One isolated native test job for canonical layout and atomic frame preflight."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,os,re,subprocess,sys

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-sky-cube-binding-check-v1'
NAME='openrpg-source-sky-cube-binding-check-v1'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')

def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def save(name,value):
    p=ROOT/name;t=p.with_suffix(p.suffix+'.tmp')
    t.write_text(json.dumps(value,indent=2),'utf-8');t.replace(p)

def prepare():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir();(ROOT/'temp').mkdir()
    old=W/'studio-source-host-shadows-v2'
    manifest=(old/'Cargo.toml').read_text('utf-8').replace('openrpg-source-host-shadows-v2',NAME)
    manifest+='forge_core={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_core"}\nserde={version="=1.0.228",features=["derive"]}\n'
    (ROOT/'Cargo.toml').write_text(manifest,'utf-8')
    code=(old/'src/main.rs').read_text('utf-8')
    code+='\n#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_assets/src/source_graph_bundle.rs"] mod source_bundle;\n'
    (ROOT/'src/main.rs').write_text(code,'utf-8')
    (ROOT/'Cargo.lock.before').write_bytes((old/'Cargo.lock').read_bytes())
    (ROOT/'Cargo.lock').write_bytes((ROOT/'Cargo.lock.before').read_bytes())
    paths=[Path(__file__).resolve(),ROOT/'Cargo.toml',ROOT/'src/main.rs',ROOT/'Cargo.lock.before',R/'Cargo.lock']
    for directory in [R/'crates',R/'apps/forge_studio/src/authoring_assets']:
        paths.extend(p for p in directory.rglob('*') if p.is_file() and (p.suffix in ['.rs','.wgsl'] or p.name=='Cargo.toml'))
    pins={str(p):sha(p) for p in sorted(set(paths))}
    save('PREPARED.v1.json',dict(input_pins=pins,expected_new_sky_contract_tests=3,
        expected_frame_tests=7,full_import=False,gpu_rendered=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(pid=child.pid,created_utc=datetime.now(timezone.utc).isoformat()))
    print(json.dumps(dict(pid=child.pid,root=str(ROOT))))

def run():
    state=dict(status='source_sky_binding_contract_and_frame_tests_running',full_import=False,
        gpu_rendered=False,editor_visual_verified=False)
    try:
        pins=json.loads((ROOT/'PREPARED.v1.json').read_text('utf-8'))['input_pins']
        assert all(sha(p)==h for p,h in pins.items()),'Source changed before the test job'
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),
            RUSTC=str(BIN/'rustc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',
            CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),
            TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'))
        commands=[[str(BIN/'cargo.exe'),'test','--offline','--bin',NAME,scope,'--','--nocapture']
            for scope in ['source_bundle::','source_graph_frame::tests::']]
        save('COMMANDS.v1.json',dict(commands=commands,cwd=str(ROOT)))
        results=[]
        for i,command in enumerate(commands):
            with (ROOT/f'stdout-{i}.log').open('xb') as out,(ROOT/f'stderr-{i}.log').open('xb') as err:
                child=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,
                    stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(cargo_pid=child.pid,command_index=i);save('RUN-RESULT.v1.json',state)
                code=child.wait()
            output=(ROOT/f'stdout-{i}.log').read_text('utf-8')
            match=re.search(r'test result: ok\. (\d+) passed; 0 failed;',output)
            results.append(dict(scope=command[-3],actual_exit_code=code,
                tests_verified=int(match.group(1)) if match and code==0 else 0,
                stdout_sha256=sha(ROOT/f'stdout-{i}.log'),stderr_sha256=sha(ROOT/f'stderr-{i}.log')))
            if code!=0:break
        changed=[p for p,h in pins.items() if sha(p)!=h]
        expected=['scene_cube_and_brdf_preserve_all_seven_authored_slots',
            'cube_sampler_must_be_paired_exactly_once','changed_cube_type_binding_name_and_sampler_refused']
        layout=(ROOT/'stdout-0.log').read_text('utf-8')
        frame=(ROOT/'stdout-1.log').read_text('utf-8') if (ROOT/'stdout-1.log').exists() else ''
        passed=len(results)==2 and all(r['actual_exit_code']==0 for r in results) and not changed
        passed=passed and all(name+' ... ok' in layout for name in expected)
        passed=passed and results[1]['tests_verified']==7 and 'required_sky_missing_cleared_or_evicted_refuses_camera_and_shadow_atomically ... ok' in frame
        state.update(status='canonical_sky_binding_and_atomic_scene_preflight_verified' if passed else 'source_sky_binding_test_refusal_retained',
            command_results=results,input_pins=pins,changed_source_files=changed,
            contract_tests_verified=results[0]['tests_verified'],
            frame_tests_verified=results[1]['tests_verified'] if len(results)>1 else 0,
            new_sky_contract_tests_verified=3 if passed else 0,
            actual_exit_code=0 if passed else (results[-1]['actual_exit_code'] or 1),
            lock_sha256=sha(ROOT/'Cargo.lock'))
    except Exception as error:state.update(status='source_sky_binding_tests_refused',error=repr(error))
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
