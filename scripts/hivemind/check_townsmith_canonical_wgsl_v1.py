"""Validate the assembled arithmetic shader; this is not runtime qualification."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-canonical-wgsl-check-v1'
SOURCE = W / 'townsmith-s-paint-texture-set-v1/material-arithmetic-canonical-bindings.wgsl'
TOOLS = W / 'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'


def sha(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def main():
    assert not ROOT.exists(), 'Retain the previous receipt; use a new run.'
    original = json.loads((TOOLS / 'RESULT.v1.json').read_text(encoding='utf-8-sig'))
    tool = TOOLS / 'validate_real_bevy_closure.exe'
    modules = TOOLS / 'module-map.tsv'
    assert sha(tool) == original['exe_sha256']
    assert sha(modules) == original['module_map_sha256']
    pins = {str(p): sha(p) for p in (SOURCE, tool, modules, Path(__file__))}
    ROOT.mkdir()
    probe = ROOT / 'fragment-type-admission.wgsl'
    probe.write_text(SOURCE.read_text(encoding='utf-8') + '''
@fragment fn canonical_type_admission(@location(0) uv:vec4<f32>)->@location(0) vec4<f32> {
    let m=mat4x4<f32>(vec4<f32>(1.0,0.0,0.0,0.0),vec4<f32>(0.0,1.0,0.0,0.0),vec4<f32>(0.0,0.0,1.0,0.0),vec4<f32>(0.0,0.0,0.0,1.0));
    let v=evaluate_source_material(uv,vec3<f32>(0.0),vec3<f32>(0.0,1.0,0.0),m,vec3<u32>(2u),vec3<f32>(0.0,0.0,1.0));
    return vec4<f32>(v.base_color,v.alpha);
}
''', encoding='utf-8')
    pins[str(probe)] = sha(probe)
    cases = []
    for group in (0, 3):
        process = subprocess.run([str(tool), str(modules), str(probe), f'MATERIAL_BIND_GROUP={group}'], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        (ROOT / f'group-{group}.stdout.log').write_bytes(process.stdout)
        (ROOT / f'group-{group}.stderr.log').write_bytes(process.stderr)
        cases.append({'material_bind_group': group, 'actual_exit_code': process.returncode, 'stdout': process.stdout.decode('utf-8', errors='replace')})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    success = all(c['actual_exit_code'] == 0 and 'PASS_REAL_BEVY0191_NAGA_OIL_COMPOSITION' in c['stdout'] for c in cases)
    result = {
        'status': 'canonical_arithmetic_wgsl_composition_passed' if success else 'canonical_arithmetic_wgsl_composition_refusal_retained',
        'finished_utc': datetime.now(timezone.utc).isoformat(),
        'cases': cases, 'input_pins': pins,
        'complete_render_stages_qualified': False,
        'actual_engine_uniform_binding_qualified': False,
        'forge_material_render_qualified': False,
        'studio_qualified': False, 'full_import': False,
        'complete_assets_delivered_increment': 0,
        'limits': ['Fragment type probe composed, not executed.', 'Probe constants are not runtime defaults.', 'Forward/prepass/vertex stages and implicit derivative sampling remain open.'],
    }
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
