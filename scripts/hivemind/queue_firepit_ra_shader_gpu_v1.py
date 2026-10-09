"""Compare real RA-textured source-shader fixtures on DX12 and native Unity.

Pinned historical texture/sampler modules; no current full-renderer qualification.
All pixels, half readback, explicit orientation hypotheses, unchanged tolerance.
"""
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

HERE = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-firepit-ra-shader-gpu-v1'
BASE = W / 'firepit-active-texture-gpu-run-v2'
NATIVE = W / 'unity-delta-firepit-native-ra-shader-v1'
WAIT = W / 'source-unlit-forward-native-check-v1/RUN-RESULT.v1.json'
BINDINGS = W / 'delta-firepit-legacy-material-bindings-v1'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')
NAME = 'openrpg-firepit-ra-shader-gpu-v1'
TOLERANCE = 1e-5

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for name, digest in pins.items(): assert sha(Path(name)) == digest, 'Changed input: ' + name
def save(name, value):
    temp = ROOT / (name + '.tmp'); temp.write_text(json.dumps(value, indent=2), 'utf-8'); temp.replace(ROOT / name)
def once(text, old, new):
    assert text.count(old) == 1, 'Pinned GPU helper shape differs: ' + old
    return text.replace(old, new)

def gpu_source():
    text = (HERE / 'candidates/legacy_particle_fixture_gpu_half.rs').read_text('utf-8')
    transport = (BASE / 'src/main.rs').read_text('utf-8')
    prefix = transport[:transport.index('use std::{')]
    # Shared source types/functions, without duplicate serde/core imports.
    prefix = prefix.replace('use forge_core::{ContentHash, bytes_sha256};', '').replace('use serde_json::Value;', '')
    text = prefix + text
    text = once(text, 'required_features: wgpu::Features::empty()', 'required_features: wgpu::Features::TEXTURE_COMPRESSION_BC')
    text = once(text, 'let (device, queue) =', 'assert!(adapter.features().contains(wgpu::Features::TEXTURE_COMPRESSION_BC), "Original BC storage unsupported");\n    let (device, queue) =')
    begin = transport.index('        let format = match r[')
    end = transport.index('        let directory = output.join(', begin)
    texture_code = transport[begin:end]
    text = once(text, '    let mut results = Vec::new();', '    let r = &value["texture"];\n' + texture_code + '\n    let mut results = Vec::new();')
    text = once(text, 'wgpu::BindGroupEntry {binding: 1, resource: vertex.as_entire_binding()}]', '''wgpu::BindGroupEntry {binding: 1, resource: vertex.as_entire_binding()},
                wgpu::BindGroupEntry {binding: 2, resource: wgpu::BindingResource::TextureView(&view)},
                wgpu::BindGroupEntry {binding: 3, resource: wgpu::BindingResource::Sampler(&sampler)}]''')
    text = once(text, 'width: 1, height: 1, depth_or_array_layers: 1', 'width: 16, height: 16, depth_or_array_layers: 1')
    text = once(text, 'label: None, size: 256,', 'label: None, size: 4096,')
    text = once(text, 'rows_per_image: Some(1)', 'rows_per_image: Some(16)')
    text = once(text, 'let pixels: Vec<f32> = mapped[..8].chunks_exact(2)', '''let raw_pixels: Vec<u8> = mapped.chunks_exact(256).flat_map(|row| row[..128].iter().copied()).collect();
        assert_eq!(raw_pixels.len(), 2048);
        let pixels: Vec<f32> = raw_pixels.chunks_exact(2)''')
    text = text.replace('&mapped[..8]', '&raw_pixels')
    text = once(text, '"white_linear_fixture_only":true', '"white_linear_fixture_only":false,"historical_texture_modules":true,"source_texture_guid":value["texture"]["unity_guid"],"texture_storage_sha256":value["texture"]["storage_sha256"],"width":16,"height":16')
    return text

def prepare():
    assert not ROOT.exists(), 'Preserve earlier evidence'
    ROOT.mkdir(); (ROOT / 'src').mkdir(); (ROOT / 'temp').mkdir()
    inputs = [Path(__file__).resolve(), HERE / 'candidates/legacy_particle_fixture_gpu_half.rs',
        BASE / 'src/main.rs', BASE / 'src/imported_texture.rs', BASE / 'src/sampler.rs',
        BASE / 'INPUT.v1.json', BASE / 'Cargo.toml', BASE / 'Cargo.lock',
        BINDINGS / 'legacy_particle_alpha_blended.wgsl', BINDINGS / 'RESULT.v1.json',
        BIN / 'cargo.exe', BIN / 'rustc.exe']
    pins = {str(p): sha(p) for p in inputs}
    texture_input = read(BASE / 'INPUT.v1.json')
    pins.update(texture_input['source_proof_pins'])
    check(pins)
    (ROOT / 'src/main.rs').write_text(gpu_source(), 'utf-8')
    for name in ['imported_texture.rs', 'sampler.rs']: shutil.copyfile(BASE / 'src' / name, ROOT / 'src' / name)
    wrapper = '''
struct OriginalMaterial { tint:vec4<f32>, st:vec4<f32>, inv_fade:f32, pad0:f32, pad1:f32, pad2:f32, }
struct Fixture { color:vec4<f32>, }
@group(0) @binding(0) var<uniform> material:OriginalMaterial;
@group(0) @binding(1) var<uniform> fixture:Fixture;
@group(0) @binding(2) var source_texture:texture_2d<f32>;
@group(0) @binding(3) var source_sampler:sampler;
struct VertexOutput { @builtin(position) position:vec4<f32>, @location(0) color:vec4<f32>, @location(1) uv:vec2<f32>, }
@vertex fn vertex(@builtin(vertex_index) index:u32) -> VertexOutput {
    let positions=array<vec2<f32>,3>(vec2<f32>(-1.0,-1.0),vec2<f32>(3.0,-1.0),vec2<f32>(-1.0,3.0));
    var output:VertexOutput;
    output.position=vec4<f32>(positions[index],0.0,1.0);
    output.uv=(positions[index]+vec2<f32>(1.0))*0.5*material.st.xy+material.st.zw;
    output.color=source_particle_alpha_vertex_color(fixture.color,material.tint);
    return output;
}
@fragment fn fragment(input:VertexOutput) -> @location(0) vec4<f32> {
    let texel=textureSample(source_texture,source_sampler,input.uv);
    return source_particle_alpha_surface(input.color,texel,false,
        material.inv_fade,0.0,0.0,false,1.0,vec3<f32>(0.0));
}
'''
    (ROOT / 'src/fixture.wgsl').write_text((BINDINGS / 'legacy_particle_alpha_blended.wgsl').read_text('utf-8') + wrapper, 'utf-8')
    for name in ['Cargo.toml', 'Cargo.lock']:
        text = (BASE / name).read_text('utf-8')
        (ROOT / name).write_text(once(text, 'openrpg-firepit-active-texture-gpu-run-v2', NAME), 'utf-8')
    for p in list((ROOT / 'src').iterdir()) + [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock']: pins[str(p)] = sha(p)
    check(pins)
    save('QUEUED.v1.json', dict(input_pins=pins, texture=texture_input['rows'][0],
        tolerance=TOLERANCE, orientation_hypotheses=['direct', 'vertical_flip'],
        expected_fixtures=16, expected_pixels=4096, renderer_admitted=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=out, stderr=out, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid, expected_pixels=4096)))

def run():
    state = dict(status='waiting_for_native_ra_capture_and_owned_material_tests', fixtures_verified=0,
        fixture_shader_actual_ra_gpu_parity_verified=False, renderer_admitted=False,
        whole_effect_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        while True:
            path = NATIVE / 'RUN-RESULT.v1.json'
            if path.is_file():
                native_state = read(path)
                if native_state.get('finished_utc'): break
            time.sleep(15)
        assert native_state.get('native_shader_fixture_gpu_verified'), 'Native real-texture capture unverified'
        while not read(WAIT).get('finished_utc'): time.sleep(15)
        queued = read(ROOT / 'QUEUED.v1.json'); pins = dict(queued['input_pins']); check(pins)
        proof = read(NATIVE / 'INDEPENDENT-REOPEN.v1.json')
        assert sha(NATIVE / 'INDEPENDENT-REOPEN.v1.json') == native_state['independent_receipt_sha256']
        assert sha(NATIVE / 'GPU.v1.json') == proof['capture_sha256']
        pins.update(proof['input_pins']); pins.update(proof['output_pins']); check(pins)
        for p in [path, NATIVE / 'GPU.v1.json', NATIVE / 'INDEPENDENT-REOPEN.v1.json', WAIT]: pins[str(p)] = sha(p)
        bindings = read(BINDINGS / 'RESULT.v1.json'); pins.update(bindings['input_pins']); pins.update(bindings['output_pins'])
        materials = {row['material_guid']: row for row in bindings['material_bindings']}
        fixtures = []
        for case in read(NATIVE / 'GPU.v1.json')['cases']:
            for row in case['rows']:
                fixtures.append(dict(material_guid=case['material_guid'], uniform_blob=materials[case['material_guid']]['uniform_blob'],
                    vertex_color=[row['baked_color'][k]/255 for k in 'rgba'], background=[row['background'][k] for k in 'rgba'],
                    native_pixels=row['pixels_path'], native_pixels_sha256=row['pixels_sha256']))
        assert len(fixtures) == 16; check(pins)
        save('INPUT.v1.json', dict(input_pins=pins.copy(), texture=queued['texture'], fixtures=fixtures, full_import=False))
        pins[str(ROOT / 'INPUT.v1.json')] = sha(ROOT / 'INPUT.v1.json')
        save('PREPARED.v1.json', dict(input_pins=pins, tolerance=TOLERANCE,
            orientation_hypotheses=queued['orientation_hypotheses'], historical_texture_modules=True,
            renderer_admitted=False, full_import=False))
        env = os.environ.copy(); env.update(RUSTC=str(BIN / 'rustc.exe'), CARGO_TARGET_DIR=str(TARGET), CARGO_BUILD_JOBS='1',
            TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), PATH=str(BIN)+os.pathsep+env.get('PATH',''))
        for label, args in [('build', [str(BIN / 'cargo.exe'), 'build', '--locked', '--offline', '-j', '1']),
            ('gpu', [str(TARGET / 'debug' / (NAME+'.exe')), str(ROOT / 'INPUT.v1.json'), str(ROOT / 'gpu-output')])]:
            state['status'] = label + '_real_ra_shader_fixtures'; save('RUN-RESULT.v1.json', state)
            with (ROOT / (label+'.stdout.log')).open('xb') as out, (ROOT / (label+'.stderr.log')).open('xb') as err:
                child = subprocess.Popen(args, cwd=ROOT, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                state[label+'_pid'] = child.pid; save('RUN-RESULT.v1.json', state); state[label+'_exit_code'] = child.wait()
            assert state[label+'_exit_code'] == 0, 'Real RA shader '+label+' refused'
            check(pins)
        report = read(ROOT / 'gpu-output/RESULT.v1.json')
        assert report['backend'] == 'Dx12' and report['render_target_format'] == 'Rgba16Float'
        assert report['width'] == report['height'] == 16 and not report['white_linear_fixture_only']
        assert report['source_texture_guid'] == queued['texture']['unity_guid']
        assert report['texture_storage_sha256'] == queued['texture']['storage_sha256']
        assert report['input_sha256'] == sha(ROOT / 'INPUT.v1.json') and len(report['rows']) == 16
        output_pins = {str(ROOT / 'gpu-output/RESULT.v1.json'): sha(ROOT / 'gpu-output/RESULT.v1.json')}
        errors = dict(direct=0.0, vertical_flip=0.0)
        for index, (row, fixture) in enumerate(zip(report['rows'], fixtures, strict=True)):
            assert row['index'] == index and row['material_guid'] == fixture['material_guid']
            raw_path = Path(row['gpu_rgba_f16']); decoded_path = Path(row['rgba_f32'])
            assert raw_path.resolve().is_relative_to((ROOT/'gpu-output').resolve()) and decoded_path.resolve().is_relative_to((ROOT/'gpu-output').resolve())
            raw = raw_path.read_bytes(); decoded = decoded_path.read_bytes()
            assert len(raw) == 2048 and len(decoded) == 4096
            assert sha(raw_path) == row['gpu_rgba_f16_sha256'] and sha(decoded_path) == row['sha256']
            gpu = list(struct.iter_unpack('<4e', raw)); floats = list(struct.unpack('<1024f', decoded))
            assert floats == [v for pixel in gpu for v in pixel] == row['rgba']
            assert all(all(math.isfinite(v) for v in pixel) and pixel[3] == fixture['background'][3] for pixel in gpu)
            native_path = Path(fixture['native_pixels']); assert sha(native_path) == fixture['native_pixels_sha256']
            native = list(struct.iter_unpack('<4e', native_path.read_bytes())); assert len(native) == 256
            for hypothesis in errors:
                for y in range(16):
                    for x in range(16):
                        n = native[(y if hypothesis == 'direct' else 15-y)*16+x]
                        errors[hypothesis] = max(errors[hypothesis], *(abs(a-b) for a,b in zip(gpu[y*16+x], n, strict=True)))
            output_pins[str(raw_path)] = sha(raw_path); output_pins[str(decoded_path)] = sha(decoded_path)
        matches = [name for name, error in errors.items() if error <= TOLERANCE]
        save('COMPARISON.v1.json', dict(input_pins=pins, output_pins=output_pins, maximum_errors=errors,
            matching_hypotheses=matches, tolerance=TOLERANCE, pixels_compared=4096,
            destination_alpha_preserved=True, historical_texture_modules=True, renderer_admitted=False, full_import=False))
        assert len(matches) == 1, 'Real RA shader parity/orientation not uniquely qualified; comparison retained'
        check(pins); check(output_pins)
        state.update(status='real_ra_shader_gpu_fixture_parity_verified', fixtures_verified=16, pixels_verified=4096,
            fixture_shader_actual_ra_gpu_parity_verified=True, fixture_coordinate_mapping=matches[0],
            maximum_rgba_error=errors[matches[0]], comparison_receipt_sha256=sha(ROOT/'COMPARISON.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__+': '+str(error), diagnostic_traceback=traceback.format_exc())
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json', state)

if __name__ == '__main__': run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
