"""Export exactly composed material stages for subsequent native GPU admission."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-composed-wgsl-v1'
TOOLS = W / 'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'
DEPS = W / 'native-hdrp-nine-source-programs-v1/opacity-cutout-runtime-v1/native-build-v2/target/debug/deps'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    assert not ROOT.exists()
    stage_receipt = W / 'townsmith-s-paint-lit-fragments-v1/RESULT.v1.json'
    stages = read(stage_receipt)
    assert stages['compositions_passed'] == 7
    original_source = TOOLS / 'validate_real_bevy_closure.rs'
    original = read(TOOLS / 'RESULT.v1.json')
    assert sha(original_source) == original['source_sha256']
    oil = DEPS / 'libnaga_oil-fed2ca864a76f26a.rlib'
    naga = DEPS / 'libnaga-5984a4b0ea48d9e4.rlib'
    assert sha(oil) == original['rlib_sha256']
    pins = dict(stages['input_pins'])
    pins.update({str(p): sha(p) for p in (stage_receipt, original_source, oil, naga, BIN / 'rustc.exe', Path(__file__))})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    code = original_source.read_text(encoding='utf-8')
    assert code.count('args.iter().skip(3)') == 1
    code = code.replace('args.iter().skip(3)', 'args.iter().skip(4)')
    anchor = 'Ok(m)=>{println!'
    assert code.count(anchor) == 1
    code = code.replace(anchor, '''Ok(m)=>{
let info=naga::valid::Validator::new(naga::valid::ValidationFlags::all(),naga::valid::Capabilities::all()).validate(&m).expect("Composed IR validation failed");
let emitted=naga::back::wgsl::write_string(&m,&info,naga::back::wgsl::WriterFlags::EXPLICIT_TYPES).expect("WGSL export refused");
fs::write(&args[3],emitted).expect("Export write failed");
println!''')
    ROOT.mkdir()
    rust = ROOT / 'export_real_bevy_closure.rs'
    rust.write_text(code, encoding='utf-8')
    exe = ROOT / 'export_real_bevy_closure.exe'
    compiler = subprocess.run([str(BIN / 'rustc.exe'), '--edition=2024', str(rust), '--extern', 'naga_oil=' + str(oil), '--extern', 'naga=' + str(naga), '-L', 'dependency=' + str(DEPS), '-o', str(exe)], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT / 'rustc.stdout.log').write_bytes(compiler.stdout)
    (ROOT / 'rustc.stderr.log').write_bytes(compiler.stderr)
    cases = []
    if compiler.returncode == 0:
        for case in stages['cases']:
            label = case['permutation']
            output = ROOT / (label + '.wgsl')
            process = subprocess.run([str(exe), str(TOOLS / 'module-map.tsv'), stages[case['shader_stage']], str(output), *case['shader_defines']], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
            out, err = ROOT / (label + '.stdout.log'), ROOT / (label + '.stderr.log')
            out.write_bytes(process.stdout)
            err.write_bytes(process.stderr)
            pins.update({str(p): sha(p) for p in (out, err)})
            row = {**case, 'export_exit_code': process.returncode, 'export': str(output)}
            if output.exists():
                pins[str(output)] = sha(output)
                row['export_sha256'] = sha(output)
            cases.append(row)
    pins[str(rust)] = sha(rust)
    if exe.exists():
        pins[str(exe)] = sha(exe)
    for path, expected in pins.items():
        assert sha(path) == expected, path
    success = compiler.returncode == 0 and len(cases) == 7 and all(c['export_exit_code'] == 0 for c in cases)
    result = {'status': 'seven_real_bevy_compositions_exported_to_standalone_wgsl' if success else 'composed_wgsl_export_refusal_retained',
              'finished_utc': datetime.now(timezone.utc).isoformat(), 'rustc_exit_code': compiler.returncode, 'cases': cases,
              'input_pins': pins, 'gpu_pipeline_created': False, 'gpu_render_parity_qualified': False,
              'renderer_installed': False, 'forge_editor_verified': False, 'full_import': False,
              'complete_assets_delivered_increment': 0}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
