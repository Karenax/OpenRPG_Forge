"""Execute the exact WGSL port against the 16 retained native GPU fixtures."""
from pathlib import Path
import datetime
import hashlib
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
HERE = Path(__file__).resolve().parent
ROOT = W / 'delta-firepit-alpha-port-gpu-v1'
BASE = W / 'firepit-active-texture-gpu-run-v2'
BINDINGS = W / 'delta-firepit-legacy-material-bindings-v1'
TINT = W / 'delta-firepit-native-tint-gpu-v3/RESULT.v1.json'
WAIT = W / 'source-graph-production-color-mask-check-v1/RUN-RESULT.v1.json'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
NAME = 'openrpg-firepit-alpha-port-gpu-v1'
TOLERANCE = 1e-5

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for p,digest in pins.items(): assert sha(Path(p)) == digest, 'Changed pinned input: ' + p
def save(name, value):
    p=ROOT/(name+'.tmp'); p.write_text(json.dumps(value,indent=2),'utf-8'); p.replace(ROOT/name)

def prepare():
    assert not ROOT.exists()
    bindings = read(BINDINGS/'RESULT.v1.json'); tint = read(TINT)
    assert tint['matching_hypotheses'] == ['linearized_get_color']
    pins = dict(bindings['input_pins']); pins.update(bindings['output_pins']); pins.update(tint['input_pins'])
    # Reuse the same frozen core and pinned dependencies, not a mutable checkout.
    pins.update(read(BASE/'PREPARED.v1.json')['input_pins'])
    rs = HERE/'candidates/legacy_particle_fixture_gpu.rs'; library=BINDINGS/'legacy_particle_alpha_blended.wgsl'
    for p in [Path(__file__).resolve(),rs,library,BINDINGS/'RESULT.v1.json',TINT,BASE/'Cargo.toml',BASE/'Cargo.lock',BIN/'cargo.exe',BIN/'rustc.exe']:
        pins[str(p)] = sha(p)
    check(pins); ROOT.mkdir(); (ROOT/'src').mkdir(); (ROOT/'temp').mkdir()
    shutil.copyfile(rs,ROOT/'src/main.rs')
    wrapper='''
struct OriginalMaterial { tint:vec4<f32>, st:vec4<f32>, inv_fade:f32, pad0:f32, pad1:f32, pad2:f32, }
struct Fixture { color:vec4<f32>, }
@group(0) @binding(0) var<uniform> material:OriginalMaterial;
@group(0) @binding(1) var<uniform> fixture:Fixture;
struct VertexOutput { @builtin(position) position:vec4<f32>, @location(0) color:vec4<f32>, }
@vertex fn vertex(@builtin(vertex_index) index:u32) -> VertexOutput {
    let positions=array<vec2<f32>,3>(vec2<f32>(-1.0,-1.0),vec2<f32>(3.0,-1.0),vec2<f32>(-1.0,3.0));
    var output:VertexOutput;
    output.position=vec4<f32>(positions[index],0.0,1.0);
    output.color=source_particle_alpha_vertex_color(fixture.color,material.tint);
    return output;
}
@fragment fn fragment(input:VertexOutput) -> @location(0) vec4<f32> {
    // Same explicit white-linear fixture as the native GPU proof. Disabled
    // soft/fog arguments do not qualify either variant or the actual RA image.
    return source_particle_alpha_surface(input.color,vec4<f32>(1.0),false,
        material.inv_fade,0.0,0.0,false,1.0,vec3<f32>(0.0));
}
'''
    (ROOT/'src/fixture.wgsl').write_text(library.read_text('utf-8')+wrapper,'utf-8')
    for name in ['Cargo.toml','Cargo.lock']:
        text=(BASE/name).read_text('utf-8'); assert text.count('openrpg-firepit-active-texture-gpu-run-v2') == 1
        (ROOT/name).write_text(text.replace('openrpg-firepit-active-texture-gpu-run-v2',NAME),'utf-8')
    materials={row['material_guid']:row for row in bindings['material_bindings']}
    fixtures=[]
    for comparison in tint['comparisons']:
        row=comparison['native']; guid=comparison['material_guid']; material=materials[guid]
        assert row['alpha_channel_preserved']
        fixtures.append(dict(material_guid=guid,uniform_blob=material['uniform_blob'],
            vertex_color=[row['baked_color'][k]/255 for k in 'rgba'],
            background=[row['background'][k] for k in 'rgba'], native_result=[row['result'][k] for k in 'rgba']))
    assert len(fixtures)==16
    for p in list((ROOT/'src').iterdir())+[ROOT/'Cargo.toml',ROOT/'Cargo.lock']: pins[str(p)]=sha(p)
    save('INPUT.v1.json',dict(input_pins=pins.copy(),fixtures=fixtures,fixture_texture_is_white_and_linear=True,
        comparison_tolerance=TOLERANCE,source_shader_soft_fog_variants_qualified=False,full_import=False))
    pins[str(ROOT/'INPUT.v1.json')]=sha(ROOT/'INPUT.v1.json'); check(pins)
    save('PREPARED.v1.json',dict(input_pins=pins,expected_fixtures=16,tolerance=TOLERANCE,
        actual_material_uniforms=True,source_library_bytes_preserved=True,white_linear_fixture_only=True,
        source_shader_gpu_verified=False,renderer_admitted=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid,expected_fixtures=16)))

def run():
    state=dict(status='waiting_for_owned_production_mask_checks',fixtures_verified=0,
        actual_source_texture_shader_qualified=False,soft_fog_variants_qualified=False,
        renderer_admitted=False,whole_effect_qualified=False,full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        while not read(WAIT).get('finished_utc'): time.sleep(15)
        prepared=read(ROOT/'PREPARED.v1.json'); pins=prepared['input_pins']; check(pins)
        env=os.environ.copy(); env.update(RUSTC=str(BIN/'rustc.exe'),TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),
            CARGO_TARGET_DIR=str(TARGET),CARGO_BUILD_JOBS='1',PATH=str(BIN)+os.pathsep+env.get('PATH',''))
        state['status']='compiling_actual_shader_render_probe'; save('RUN-RESULT.v1.json',state)
        with (ROOT/'build.stdout.log').open('xb') as out,(ROOT/'build.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(BIN/'cargo.exe'),'build','--locked','--offline','-j','1'],cwd=ROOT,
                env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid']=child.pid; save('RUN-RESULT.v1.json',state); state['build_exit_code']=child.wait()
        assert state['build_exit_code']==0,'Actual shader probe build refused'; check(pins)
        exe=TARGET/'debug'/(NAME+'.exe'); state['executable_sha256']=sha(exe)
        state['status']='rendering_sixteen_original_source_fixtures'; save('RUN-RESULT.v1.json',state)
        with (ROOT/'gpu.stdout.log').open('xb') as out,(ROOT/'gpu.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(exe),str(ROOT/'INPUT.v1.json'),str(ROOT/'gpu-output')],cwd=ROOT,
                env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state['gpu_pid']=child.pid; save('RUN-RESULT.v1.json',state); state['gpu_exit_code']=child.wait()
        assert state['gpu_exit_code']==0,'Actual GPU render refused'; check(pins)
        report=read(ROOT/'gpu-output/RESULT.v1.json'); source=read(ROOT/'INPUT.v1.json')
        assert report['backend']=='Dx12' and report['float32_blendable_enabled']
        assert report['input_sha256']==sha(ROOT/'INPUT.v1.json') and len(report['rows'])==16
        comparisons=[]; output_pins={str(ROOT/'gpu-output/RESULT.v1.json'):sha(ROOT/'gpu-output/RESULT.v1.json')}
        for index,(row,fixture) in enumerate(zip(report['rows'],source['fixtures'],strict=True)):
            assert row['index']==index and row['material_guid']==fixture['material_guid']
            p=Path(row['rgba_f32']); assert p.resolve().is_relative_to((ROOT/'gpu-output').resolve())
            assert sha(p)==row['sha256']; data=p.read_bytes(); assert len(data)==16
            rgba=struct.unpack('<4f',data); assert all(math.isfinite(v) for v in rgba) and list(rgba)==row['rgba']
            assert rgba[3]==fixture['background'][3]==fixture['native_result'][3],'Original destination alpha changed'
            error=max(abs(a-b) for a,b in zip(rgba,fixture['native_result'],strict=True))
            comparisons.append(dict(index=index,maximum_rgba_error=error)); output_pins[str(p)]=sha(p)
        maximum=max(row['maximum_rgba_error'] for row in comparisons)
        save('COMPARISON.v1.json',dict(input_pins=pins,output_pins=output_pins,comparisons=comparisons,
            maximum_rgba_error=maximum,tolerance=TOLERANCE,actual_adapter=report['adapter_name'],
            destination_alpha_preserved=True,white_linear_fixture_only=True,full_import=False))
        assert maximum<=TOLERANCE,'Port differs from original GPU; comparison retained'
        check(pins); check(output_pins)
        state.update(status='sixteen_original_shader_and_port_gpu_fixtures_compared',fixtures_verified=16,
            maximum_rgba_error=maximum,destination_alpha_preserved=True,
            comparison_receipt_sha256=sha(ROOT/'COMPARISON.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json',state)

if __name__=='__main__': run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
