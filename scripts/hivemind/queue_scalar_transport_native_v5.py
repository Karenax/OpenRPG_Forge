"""Test the actual scalar transport on F; retain logs, input hashes and exit codes."""
from pathlib import Path
import datetime,hashlib,json,os,re,subprocess,sys

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-graph-scalar-transport-native-v5'
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def save(name,value):
    t=ROOT/(name+'.tmp');t.write_text(json.dumps(value,indent=2),'utf-8');t.replace(ROOT/name)
def pins_ok(pins):
    for p,h in pins.items():assert sha(Path(p))==h,p

def prepare():
    assert not ROOT.exists()
    ROOT.mkdir();(ROOT/'temp').mkdir()
    files=[R/'Cargo.toml',R/'Cargo.lock',R/'crates/forge_assets/src/source_graph_bundle.rs',
        R/'crates/forge_assets/src/source_graph_bundle/scalar_parameters.rs',R/'crates/forge_assets/src/source_material_parameters.rs',
        R/'crates/forge_engine/src/source_graph_glb.rs',R/'crates/forge_engine/src/source_graph_scalars.rs',
        R/'crates/forge_engine/src/source_graph.rs',Path(__file__).resolve()]
    pins={str(p):sha(p) for p in files}
    save('PREPARED.v1.json',dict(input_pins=pins,actual_checkout=str(R),
        tests=['forge_assets::source_graph','forge_engine::source_graph'],full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=R,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,root=str(ROOT))))

def run():
    state=dict(status='testing_actual_scalar_transport',full_import=False,gpu_qualified=False)
    save('RUN-RESULT.v1.json',state)
    try:
        prepared=read(ROOT/'PREPARED.v1.json');pins_ok(prepared['input_pins'])
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),CARGO_INCREMENTAL='0',
            CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target',RUSTC=str(BIN/'rustc.exe'),
            PATH=str(BIN)+os.pathsep+env.get('PATH',''),
            OPENRPG_PRIVATE_SOURCE_FIXTURES='F:/OpenRPG_ImportWork/source-unlit-canonical-snapshot-v1')
        rows=[]
        for package in ['forge_assets','forge_engine']:
            args=[str(BIN/'cargo.exe'),'test','--manifest-path',str(R/'Cargo.toml'),'-p',package,'--lib','--locked','--offline','-j1']
            if package=='forge_engine':args+=['--features','private-source-fixtures']
            args+=['source_graph','--','--test-threads=1']
            with (ROOT/(package+'.stdout.log')).open('xb') as out,(ROOT/(package+'.stderr.log')).open('xb') as err:
                child=subprocess.Popen(args,cwd=R,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(status='testing_'+package,cargo_pid=child.pid);save('RUN-RESULT.v1.json',state)
                code=child.wait()
            text=(ROOT/(package+'.stdout.log')).read_text('utf-8-sig')
            passed=re.findall(r'^test (\S+) \.\.\. ok$',text,re.M)
            rows.append(dict(package=package,exit_code=code,passed_tests=passed,stdout_sha256=sha(ROOT/(package+'.stdout.log')),
                stderr_sha256=sha(ROOT/(package+'.stderr.log'))))
            state['tests']=rows;save('RUN-RESULT.v1.json',state)
            assert code==0 and passed,'Actual '+package+' tests refused; preserve logs'
            required=4 if package=='forge_assets' else 1
            assert sum('scalar_transport' in n or 'scalar_packet_reaches_registered' in n for n in passed)>=required
        pins_ok(prepared['input_pins'])
        state.update(status='actual_scalar_transport_and_previous_source_graph_tests_passed',source_pins_unchanged=True)
    except Exception as error:state.update(status='refused_or_unverified',error=repr(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
