"""Measure raw and decoded normals using copies of current product resource adapters."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,shutil,subprocess,sys,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-normal-gpu-v417';BASE=W/'delta-foliage-texture-gpu-v2'
ENGINE=S.parents[1]/'crates/forge_engine/src';TARGET=W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'
RUST=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def check(pins):
    for p,h in pins.items():assert sha(p)==h,p
def function(text,name):
    start=text.index('fn '+name+'(');opening=text.index('{',start);depth=1
    for i in range(opening+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:return text[start:i+1]
    raise ValueError('Unclosed function')
def prepare():
    assert not O.exists()
    kernel=W/'gladiator-material-kernels-v414/RESULT.v1.json';k=read(kernel)
    reference=W/'gladiator-normal-reference-v415/GPU-REFERENCE-REOPEN.v1.json';assert read(reference)['verified_textures']==4
    storage=W/'gladiator-native-texture-storage-v412/NATIVE-RESULT.v1.json';seal=W/'gladiator-native-texture-seal-v413/RESULT.v1.json'
    check(read(seal)['output_pins'])
    guids={b['guid'] for r in k['rows'] for b in r['source_texture_bindings'] if b['requires_normal_decode']};assert len(guids)==4
    helpers=[];pins={str(p):sha(p) for p in [Path(__file__),kernel,reference,storage,seal,BASE/'src/main.rs',BASE/'src/sample.wgsl',BASE/'Cargo.toml',BASE/'Cargo.lock']}
    for r in k['rows']:
        p=Path(r['wgsl']);assert sha(p)==r['wgsl_sha256'];pins[str(p)]=r['wgsl_sha256'];text=p.read_text('utf-8-sig')
        helpers.append(function(text,'source_decode_normal')+'\n'+function(text,'source_graph_normal_sample'))
    assert len(set(helpers))==1
    O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
    text=(BASE/'src/main.rs').read_text('utf-8-sig')
    edits={'assert_eq!(results.len(), 18);':'assert_eq!(results.len(), 4);','assert_eq!(total_mips, 225);':'assert_eq!(total_mips, 52);','"gpu_sampled_textures":18':'"gpu_sampled_textures":4','(-0.000001..=1.000001)':'(-1.000001..=1.000001)','let parameters = [level as f32, 64.0, 0.0, 0.0];':'let parameters = [level as f32, 64.0, decode_mode, 0.0];','let input_path = std::env::args().nth(1).unwrap();':'let decode_mode: f32 = std::env::args().nth(3).unwrap().parse().unwrap();\n    assert!(decode_mode == 0.0 || decode_mode == 2.0);\n    let input_path = std::env::args().nth(1).unwrap();'}
    for a,b in edits.items():assert a in text,a;text=text.replace(a,b)
    (O/'src/main.rs').write_text(text,'utf-8')
    shader=(BASE/'src/sample.wgsl').read_text('utf-8-sig');expr='textureSampleLevel(source_image,source_sampler,uv,parameters.x)';assert shader.count(expr)==1
    shader=helpers[0]+'\n'+shader.replace('output[gid.x+gid.y*grid]='+expr+';','let raw='+expr+';\n if parameters.z==2.0 {output[gid.x+gid.y*grid]=source_graph_normal_sample(raw,2.0);} else {output[gid.x+gid.y*grid]=raw;}')
    (O/'src/sample.wgsl').write_text(shader,'utf-8')
    for source,name in [(ENGINE/'source_graph_imported_texture.rs','imported_texture.rs'),(ENGINE/'source_graph_sampler.rs','sampler.rs')]:
        pins[str(source)]=sha(source);shutil.copyfile(source,O/'src'/name)
    for name in ['Cargo.toml','Cargo.lock']:
        text=(BASE/name).read_text('utf-8-sig');assert text.count('openrpg-delta-foliage-texture-gpu-v1')==1
        (O/name).write_text(text.replace('openrpg-delta-foliage-texture-gpu-v1','openrpg-gladiator-normal-gpu-v417'),'utf-8')
    for p in (S.parents[1]/'crates/forge_core').rglob('*.rs'):pins[str(p)]=sha(p)
    rows=[]
    for r in read(storage)['rows']:
        if r['guid'] not in guids:continue
        assert r['graphics_format']=='RGBA_DXT5_UNorm';offset=0;mips=[]
        for m in r['mips']:
            mips.append(dict(level=m['level'],width=m['width'],height=m['height'],offset=offset,bytes=m['bytes'],sha256=sha(m['file'])));offset+=m['bytes']
        pins[r['whole_file']]=sha(r['whole_file'])
        rows.append(dict(asset_id=r['guid'],unity_guid=r['guid'],unity_file_id='2800000',gpu_format='Bc3RgbaUnorm',storage=r['whole_file'],storage_sha256=sha(r['whole_file']),storage_bytes=r['whole_bytes'],width=r['width'],height=r['height'],mips=mips,source_mipmaps_enabled=True,resolved_sampler=dict(filter_mode=r['filter_mode'],wrap_u=r['wrap_u'],wrap_v=r['wrap_v'],wrap_w=r['wrap_w'],anisotropy=r['aniso'],mip_bias=r['mip_bias'])))
    assert len(rows)==4
    for p in list((O/'src').iterdir())+[O/'Cargo.toml',O/'Cargo.lock']:pins[str(p)]=sha(p)
    check(pins);(O/'INPUT.v1.json').write_text(json.dumps(dict(rows=rows,source_proof_pins=pins),indent=2),'utf-8');pins[str(O/'INPUT.v1.json')]=sha(O/'INPUT.v1.json')
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,current_product_adapters_copied_verbatim=True,normal_textures=4,mips=52),indent=2),'utf-8');save(dict(status='four_current_adapter_normal_gpu_probes_prepared'))
    with (O/'controller.log').open('xb') as log:p=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=p.pid)))
def run():
    state=dict(status='building_current_adapter_normal_gpu_probe')
    try:
        pins=read(O/'PREPARED.v1.json')['input_pins'];check(pins)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),CARGO_TARGET_DIR=str(TARGET),CARGO_INCREMENTAL='0',RUSTC=str(RUST/'rustc.exe'),RUSTDOC=str(RUST/'rustdoc.exe'));env['PATH']=str(RUST)+os.pathsep+env['PATH'];save(state)
        with (O/'build.log').open('xb') as log:state['build_exit_code']=subprocess.run([str(RUST/'cargo.exe'),'build','--locked','--offline','-j1'],cwd=O,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert state['build_exit_code']==0,'Inspect build.log';check(pins)
        exe=O/'gpu-probe.immutable.v1.exe';shutil.copyfile(TARGET/'debug/openrpg-gladiator-normal-gpu-v417.exe',exe);state['executable_sha256']=sha(exe)
        for name,mode in [('raw',0),('decoded',2)]:
            state['status']='sampling_four_normal_textures_'+name;save(state)
            with (O/(name+'.stdout.log')).open('xb') as out,(O/(name+'.stderr.log')).open('xb') as err:
                p=subprocess.Popen([str(exe),str(O/'INPUT.v1.json'),str(O/name),str(mode)],cwd=O,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state['gpu_process_pid']=p.pid;save(state);state[name+'_exit_code']=p.wait()
            assert state[name+'_exit_code']==0,name;check(pins)
        state['status']='four_normals_raw_and_decoded_gpu_samples_captured_comparison_pending'
    except Exception as e:state.update(status='gpu_probe_refusal_retained',error=repr(e),traceback=traceback.format_exc())
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
