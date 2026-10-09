"""Pin shared source BRDF admission and GPU sampling cases before implementation."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import struct

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-brdf-lookup-case-v1'
CAP = W / 'firepit-source-fgd-reference-v1'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def main():
    proof_path = W / 'firepit-source-fgd-reopen-v1/RESULT.v1.json'
    proof = read(proof_path)
    assert proof['status'] == 'native_hdrp_fgd_gpu_readback_and_sdk_contract_reopened'
    assert proof['cpu_cases_count'] == 35 and proof['texels_verified'] == 4096
    for path, digest in proof['input_pins'].items():
        assert sha(path) == digest, path
    raw_path = CAP / 'source-fgd.rgba32f'
    data = raw_path.read_bytes()
    assert len(data) == 64 * 64 * 16
    assert all(math.isfinite(v) and 0 <= v <= 1 for p in struct.iter_unpack('<4f', data) for v in p)
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(), proof_path, raw_path,
        raw_path.with_suffix(raw_path.suffix + '.json')]}
    case = dict(
        schema='openrpg.source-brdf-gpu-case.v1',
        sub_lot='L01.ECLAIRAGE.BRDF',
        created_utc=datetime.now(timezone.utc).isoformat(),
        status='protocol_prepared_before_target_gpu_measurement',
        inputs=dict(rgba32f=str(raw_path), sha256=sha(raw_path), width=64, height=64,
            components=4, little_endian=True, bytes_expected=65536),
        implementation_contract=dict(
            shared_image=True, format='Rgba32Float', color_space='linear',
            mip_count=1, material_binding=122, filterable=False,
            sampler_required=False, sampling='four textureLoad reads with explicit bilinear clamp',
            texel_x='sqrt(clamp(NdotV, 0.0001, 1.0)) * 63.0',
            texel_y='perceptual_roughness * 63.0',
            diffuse_factor='sampled_blue + 0.5',
            specular_factor='(F90 - F0) * sampled_red + F0 * sampled_green',
            preserve_source_uniform_abi=True, default_binding_admitted=False,
            missing_required_image='explicit refusal; no default native BRDF',
            no_per_material_lut_copy=True),
        admission_refusals_expected=[
            'wrong content SHA', 'wrong dimensions', 'truncated or oversized buffer',
            'NaN or infinite channel', 'channel outside [0,1]',
            'nonlinear/sRGB image declaration', 'missing required image',
            'texture binding other than exact admitted scene declaration'],
        sampling_cases=[dict(case_id=f'brdf-{i:02d}', **row)
            for i, row in enumerate(proof['cpu_sampling_cases'])],
        gpu_acceptance=dict(expected_cases=35, minimum_pixels_per_case=100,
            readback='untonemapped RGBA16Float; explicitly marked witness pixels',
            relative_channel_error_limit=2**-10, absolute_channel_error_floor=0.0001,
            threshold_fixed_before_measurement=True, thresholds_may_not_be_fitted=True,
            missing_case_or_nonfinite_value='refused',
            compiler_and_ready_pipelines_required=True),
        gpu_cases_verified=0, full_import=False, render_fidelity_qualified=False,
        editor_visual_verified=False, input_pins=pins,
        next_action='Implement admitted shared image and binding; run native refusals, GPU 35-case witness and Web compile; then close this technical lot')
    if ROOT.exists():
        old = read(ROOT / 'CASE.v1.json')
        assert old['input_pins'] == pins, 'Existing protocol inputs changed; preserve its snapshot'
        print(json.dumps(dict(status='existing_protocol_preserved', path=str(ROOT / 'CASE.v1.json'))))
        return
    ROOT.mkdir()
    (ROOT / 'CASE.v1.json').write_text(json.dumps(case, indent=2), 'utf-8')
    print(json.dumps(dict(status=case['status'], path=str(ROOT / 'CASE.v1.json'),
        expected_gpu_cases=35, gpu_cases_verified=0, source_sha256=sha(raw_path))))


if __name__ == '__main__':
    main()
