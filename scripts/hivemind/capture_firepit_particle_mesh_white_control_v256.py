"""White-texture GPU control for the identical acquired mesh streams."""
from pathlib import Path
import json,hashlib,shutil,subprocess,os,datetime
W=Path('F:/OpenRPG_ImportWork');B=W/'firepit-particle-mesh-gpu-v244';N=W/'firepit-particle-native-mesh-white-control-v255';O=W/'firepit-particle-mesh-white-control-v256';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');NAME='openrpg-firepit-particle-mesh-white-control-v256'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(n,d):(O/n).write_text(json.dumps(d,indent=2),'utf-8')
def main():
 native=read(N/'RUN-RESULT.v1.json');assert native.get('unity_exit_code')==0 and native['native_geometry_verified'],'Native control must finish before GPU'
 assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
 for p in (B/'src').iterdir():shutil.copyfile(p,O/'src'/p.name)
 shader=O/'src/fixture.wgsl';text=shader.read_text('utf-8');a='let texel=textureSample(source_texture,source_sampler,input.uv);';assert text.count(a)==1;text=text.replace(a,'let texel=select(textureSample(source_texture,source_sampler,input.uv),vec4<f32>(1.0),fixture.color.a>0.0);');shader.write_text(text,'utf-8')
 source=O/'src/main.rs';text=source.read_text('utf-8');a='"rgba":pixels';assert text.count(a)==1;text=text.replace(a,'"pixel_count":pixels.len()/4');source.write_text(text,'utf-8')
 for n in ['Cargo.toml','Cargo.lock']:(O/n).write_text((B/n).read_text('utf-8').replace('openrpg-firepit-particle-mesh-gpu-v244',NAME),'utf-8')
 inp=read(B/'INPUT.v1.json');pins=inp['input_pins'];pins.update({str(p):sha(p) for p in list((O/'src').iterdir())+[O/'Cargo.toml',O/'Cargo.lock',Path(__file__),N/'RUN-RESULT.v1.json',N/'INDEPENDENT-REOPEN.v1.json']});inp.update(input_pins=pins,white_texture_control=True,actual_source_texture_sampling_qualified=False);save('INPUT.v1.json',inp)
 env=os.environ.copy();env.update(RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'),PATH=str(BIN)+os.pathsep+env['PATH'])
 state=dict(status='building_white_texture_mesh_control',expected_meshes=336,white_texture_control=True,full_import=False);save('RUN-RESULT.v1.json',state)
 with (O/'build.log').open('xb') as log:r=subprocess.run([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml')],env=env,stdout=log,stderr=log)
 state['build_exit_code']=r.returncode;save('RUN-RESULT.v1.json',state);assert r.returncode==0
 exe=Path(env['CARGO_TARGET_DIR'])/'debug'/f'{NAME}.exe';state.update(executable_sha256=sha(exe),status='rendering_white_texture_mesh_control');save('RUN-RESULT.v1.json',state)
 with (O/'gpu.log').open('xb') as log:r=subprocess.run([str(exe),str(O/'INPUT.v1.json'),str(O/'gpu')],env=env,stdout=log,stderr=log)
 state.update(gpu_exit_code=r.returncode,status='white_texture_mesh_control_terminal',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat());save('RUN-RESULT.v1.json',state);assert r.returncode==0;print(json.dumps(state))
if __name__=='__main__':main()
