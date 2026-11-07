"""Measure a provenance-pinned decoder repair without editing any source GLB."""
from pathlib import Path
from datetime import datetime, timezone
import json
import struct
import subprocess

from compare_villa_builtin_normal_gpu_v1006 import function, read, sha

W = Path('F:/OpenRPG_ImportWork')
PRIOR = W / 'villa-builtin-normal-gpu-comparison-v1009'
DONOR = W / 'source-graph-imported-normal-decoder-gpu-probe-v1'
O = W / 'villa-builtin-decoder-repair-plan-v1010'

def main():
    assert not O.exists()
    prior_path = PRIOR / 'RESULT.v1.json'
    prior = read(prior_path)
    assert prior['status'] == 'EXACT_VILLA_DECODER_GPU_PARITY_REFUSED'
    donor_path = DONOR / 'src/sample.wgsl'
    donor_prepared_path = DONOR / 'PREPARED.v1.json'
    donor_prepared = read(donor_prepared_path)
    assert sha(donor_path) == donor_prepared['owned_files']['src\\sample.wgsl']
    kernel_path = PRIOR / 'kernel.wgsl'
    assert sha(kernel_path) == prior['input_pins'][str(kernel_path)]
    kernel = kernel_path.read_text('utf-8')
    original = function(kernel, 'source_graph_normal_sample')
    replacement = function(donor_path.read_text('utf-8'), 'source_graph_normal_sample')
    assert 'if mode==2.0' not in original and 'if mode==2.0' in replacement
    assert kernel.count(original) == 1
    candidate = kernel.replace(original, replacement, 1)
    exe = next(Path(p) for p in prior['input_pins'] if p.endswith('forge_gpu_kernel_check.exe'))
    assert sha(exe) == prior['input_pins'][str(exe)]
    native_path = W / 'unity-builtin-normal-constant-reference-v1/NATIVE-RESULT.v1.json'
    assert sha(native_path) == prior['input_pins'][str(native_path)]
    input_path = PRIOR / 'input.f32'
    assert sha(input_path) == prior['input_pins'][str(input_path)]
    pins = {str(p): sha(p) for p in (prior_path, donor_path, donor_prepared_path, kernel_path, exe, native_path, input_path, Path(__file__).resolve())}
    O.mkdir()
    candidate_path = O / 'candidate-kernel.wgsl'
    candidate_path.write_text(candidate, 'utf-8')
    pins[str(candidate_path)] = sha(candidate_path)
    with (O / 'gpu.stdout.log').open('wb') as out, (O / 'gpu.stderr.log').open('wb') as err:
        args = [str(exe), str(candidate_path), str(input_path), str(O / 'output.f32'), str(O / 'GPU-RESULT.v1.json')]
        process = subprocess.Popen(args, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
        (O / 'JOB.v1.json').write_text(json.dumps({'pid': process.pid, 'command': args}), 'utf-8')
        assert process.wait() == 0, 'Candidate GPU execution refused'
    output = (O / 'output.f32').read_bytes()
    prior_output = (PRIOR / 'output.f32').read_bytes()
    assert len(output) == len(prior_output) == 192
    assert output[:128] == prior_output[:128], 'Modes 0 and 1 changed'
    expected = [[float(row[k]) for k in ('x', 'y', 'z', 'w')] for row in read(native_path)['decoded_normals']]
    actual = list(struct.iter_unpack('<4f', output[128:]))
    errors = [max(abs(a-b) for a,b in zip(row, reference)) for row,reference in zip(actual, expected)]
    assert len(errors) == 4 and max(errors) <= 1e-6, 'Candidate decoder differs from Unity constants'
    assert all(sha(Path(p)) == h for p,h in pins.items())
    prepared = read(PRIOR / 'PREPARED.v1.json')
    result = dict(status='MEASURED_DECODER_REPAIR_CANDIDATE_GPU_PASS', finished_utc=datetime.now(timezone.utc).isoformat(),
                  affected_source_scenes=19, source_rows=prepared['source_rows'], mode2_max_abs_errors=errors,
                  other_modes_bitwise_preserved=True, input_pins=pins,
                  source_glbs_modified=False, derived_glbs_created=False, registry_modified=False,
                  source_binding_qualified=False, source_material_render_parity_qualified=False,
                  next_actions=['Bind explicit builtin decoder slot with source default provenance',
                                'Create derivatives preserving original geometry, textures, metadata and old shader bytes',
                                'Reopen independently and test current canonical reader before any Vault admission',
                                'Qualify actual source material draw, scene placement and runtime'],
                  full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({'status': result['status'], 'affected_scenes': 19, 'mode2_errors': errors, 'glbs_modified': False}))

if __name__ == '__main__':
    main()
