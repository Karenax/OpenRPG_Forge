"""Use real native ARGBHalf references and actual GPU half-format readback.

No tolerance relaxation, CPU replacement of rendering, or source shader change.
"""
from pathlib import Path
import ast
import json
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
BASE_SCRIPT = HERE / 'queue_firepit_alpha_port_gpu_v1.py'
NATIVE = W / 'unity-delta-firepit-legacy-shader-gpu-half-v3'
ROOT = W / 'delta-firepit-alpha-port-gpu-half-v2'
QUEUE = W / 'delta-firepit-alpha-port-gpu-half-queue-v2'

def once(text, before, after, count=1):
    assert text.count(before)==count, 'Source shape differs: '+before
    return text.replace(before, after)

def configure():
    source=(HERE/'candidates/legacy_particle_fixture_gpu.rs').read_text('utf-8')
    source=once(source,'assert!(adapter.features().contains(wgpu::Features::FLOAT32_BLENDABLE), "Exact native float blend unavailable");',
        'assert!(adapter.get_texture_format_features(wgpu::TextureFormat::Rgba16Float).flags.contains(wgpu::TextureFormatFeatureFlags::BLENDABLE), "Native HDR half blending unavailable");')
    source=once(source,'required_features: wgpu::Features::FLOAT32_BLENDABLE','required_features: wgpu::Features::empty()')
    source=once(source,'wgpu::TextureFormat::Rgba32Float','wgpu::TextureFormat::Rgba16Float',count=2)
    source=once(source,'fn main() {','''fn half_to_float(value: u16) -> f32 {
    let sign = u32::from(value & 0x8000) << 16;
    let exponent = (value >> 10) & 31;
    let mantissa = value & 1023;
    if exponent == 0 {
        if mantissa == 0 { return f32::from_bits(sign); }
        let magnitude = f32::from(mantissa) * (1.0 / 16777216.0);
        return if sign == 0 { magnitude } else { -magnitude };
    }
    let output_exponent = if exponent == 31 { 255 } else { u32::from(exponent) + 112 };
    f32::from_bits(sign | (output_exponent << 23) | (u32::from(mantissa) << 13))
}
fn main() {''')
    source=once(source,'mapped[..16].chunks_exact(4).map(|chunk| f32::from_le_bytes(chunk.try_into().unwrap())).collect()',
        'mapped[..8].chunks_exact(2).map(|chunk| half_to_float(u16::from_le_bytes(chunk.try_into().unwrap()))).collect()')
    source=once(source,'let path = output.join(format!("fixture-{index:02}.rgba.f32")); write_new(&path, &mapped[..16]);',
        '''let raw_path = output.join(format!("fixture-{index:02}.rgba.f16")); write_new(&raw_path, &mapped[..8]);
        let decoded: Vec<u8> = pixels.iter().flat_map(|v| v.to_le_bytes()).collect();
        let path = output.join(format!("fixture-{index:02}.rgba.f32")); write_new(&path, &decoded);''')
    source=once(source,'"sha256":bytes_sha256(&mapped[..16]).to_string()',
        '"sha256":bytes_sha256(&decoded).to_string(),"gpu_rgba_f16":raw_path,"gpu_rgba_f16_sha256":bytes_sha256(&mapped[..8]).to_string()')
    source=once(source,'"float32_blendable_enabled":true','"float32_blendable_enabled":false,"render_target_format":"Rgba16Float"')
    rs=HERE/'candidates/legacy_particle_fixture_gpu_half.rs'
    if rs.exists(): assert rs.read_text('utf-8')==source, 'Preserve existing half candidate'
    else: rs.write_text(source,'utf-8')
    native=json.loads((NATIVE/'GPU.v1.json').read_text('utf-8-sig'))
    assert native['render_target_format']=='ARGBHalf' and native['pipeline_settings_restored']
    comparisons=[dict(material_guid=case['material_guid'],native=row) for case in native['cases'] for row in case['rows']]
    assert len(comparisons)==16
    script=BASE_SCRIPT.read_text('utf-8')
    script=once(script,"rs = HERE/'candidates/legacy_particle_fixture_gpu.rs'", "rs = HERE/'candidates/legacy_particle_fixture_gpu_half.rs'")
    script=once(script,"for comparison in tint['comparisons']:", 'for comparison in native_half_comparisons:')
    script=once(script,'check(pins); ROOT.mkdir();', '''for p in [BASE_SCRIPT, NATIVE/'RUN-RESULT.v1.json', NATIVE/'GPU.v1.json', NATIVE/'INDEPENDENT-REOPEN.v1.json', HERE/'candidates/legacy_particle_fixture_gpu.rs', W/'delta-firepit-alpha-port-gpu-v1/RUN-RESULT.v1.json']:
        pins[str(p)]=sha(p)
    native_proof=read(NATIVE/'INDEPENDENT-REOPEN.v1.json'); pins.update(native_proof['input_pins'])
    assert sha(NATIVE/'GPU.v1.json')==native_proof['capture_sha256']
    check(pins); ROOT.mkdir();''')
    script=once(script,"assert report['backend']=='Dx12' and report['float32_blendable_enabled']", "assert report['backend']=='Dx12' and report['render_target_format']=='Rgba16Float'")
    script=once(script,"error=max(abs(a-b) for a,b in zip(rgba,fixture['native_result'],strict=True))", '''raw=Path(row['gpu_rgba_f16']); assert raw.resolve().is_relative_to((ROOT/'gpu-output').resolve())
            raw_bytes=raw.read_bytes(); assert len(raw_bytes)==8 and sha(raw)==row['gpu_rgba_f16_sha256']
            assert struct.unpack('<4e',raw_bytes)==rgba, 'GPU half readback/decoded floats differ'
            output_pins[str(raw)]=sha(raw)
            error=max(abs(a-b) for a,b in zip(rgba,fixture['native_result'],strict=True))''')
    nodes=[n for n in ast.parse(script).body if isinstance(n,(ast.Import,ast.ImportFrom,ast.FunctionDef))]
    namespace=dict(__file__=str(Path(__file__).resolve()),W=W,HERE=HERE,ROOT=ROOT,
        BASE=W/'firepit-active-texture-gpu-run-v2',BINDINGS=W/'delta-firepit-legacy-material-bindings-v1',
        TINT=W/'delta-firepit-native-tint-gpu-v3/RESULT.v1.json',
        WAIT=W/'source-graph-production-color-mask-check-v2/RUN-RESULT.v1.json',
        BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin'),
        TARGET=Path('F:/OpenRPG_Forge/relocated-work/Rust-target'),NAME='openrpg-firepit-alpha-port-gpu-half-v2',
        TOLERANCE=1e-5,BASE_SCRIPT=BASE_SCRIPT,NATIVE=NATIVE,native_half_comparisons=comparisons)
    exec(compile(ast.Module(body=nodes,type_ignores=[]),str(BASE_SCRIPT),'exec'),namespace)
    return namespace

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='run': configure()['run']()
    elif len(sys.argv)>1 and sys.argv[1]=='prepare_after_native':
        while not json.loads((NATIVE/'RUN-RESULT.v1.json').read_text('utf-8-sig')).get('finished_utc'): time.sleep(15)
        status=json.loads((NATIVE/'RUN-RESULT.v1.json').read_text('utf-8-sig'))
        assert status['native_shader_fixture_gpu_verified'], 'Actual native half reference is unqualified'
        configure()['prepare']()
    else:
        assert not QUEUE.exists() and not ROOT.exists(); QUEUE.mkdir()
        with (QUEUE/'controller.log').open('xb') as out:
            child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'prepare_after_native'],cwd=QUEUE,
                stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        (QUEUE/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid)),'utf-8')
        print(json.dumps(dict(controller_pid=child.pid,reference_format='ARGBHalf',tolerance=1e-5)))
