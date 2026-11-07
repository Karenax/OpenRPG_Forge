"""Test whether the source F90 fade rule can affect this measured surface."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
D = W / 'townsmith-door-source-deferred-inputs-v49'
ROOT = W / 'townsmith-fresnel90-source-audit-v1'
SDK = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
rule = SDK / 'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material/PreIntegratedFGD/PreIntegratedFGD.hlsl'
assert not ROOT.exists()
source = rule.read_text(encoding='utf-8')
assert 'saturate(50.0 * dot(F0, 0.333))' in source
assert '(_SpecularFade == 1)' in source


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


prior = W / 'townsmith-source-deferred-inputs-reopen-v49/RESULT.v1.json'
proof = json.loads(prior.read_text(encoding='utf-8'))
assert proof['status'] == 'actual_inpass_deferred_inputs_reopened'
pins = {str(rule): sha(rule), str(Path(__file__)): sha(Path(__file__)), str(prior): sha(prior)}
rows = []
for case in ['door-neutral', 'door-nopost-linear', 'door-noaniso-nopost-linear']:
    a, b, depth = [D / (case + suffix) for suffix in [
        '.actual-gbuffer2-2.rgba32f', '.actual-gbuffer2-3.rgba32f', '.depth-3.r32f']]
    for p in [a, b, depth]:
        h = sha(p)
        assert proof['input_pins'][str(p)] == h
        pins[str(p)] = h
    assert a.read_bytes() == b.read_bytes()
    mask = np.fromfile(depth, dtype='<f4').reshape(512, 512) > 0
    assert int(mask.sum()) == 34445
    rgb = np.fromfile(b, dtype='<f4').reshape(512, 512, 4)[mask, :3]
    assert np.isfinite(rgb).all()
    f0 = rgb * (rgb * (rgb * .305306011 + .682171111) + .012522878)
    f90 = np.clip(50.0 * np.sum(f0 * .333, axis=1), 0, 1)
    rows.append(dict(case=case, covered_pixels=34445, potential_faded_pixels=int((f90 < 1).sum()),
                     minimum_potential_f90=float(f90.min()), maximum_potential_f90=float(f90.max()),
                     minimum_fresnel0_rgb=f0.min(axis=0).tolist(), maximum_fresnel0_rgb=f0.max(axis=0).tolist()))
ROOT.mkdir()
result = dict(status='source_f90_applicability_audited', finished_utc=datetime.now(timezone.utc).isoformat(),
    rows=rows, input_pins=pins, actual_gpu_specular_fade_flag_captured=False,
    diagnostic_only=True, source_pixels_applied_to_native=False, full_material_fidelity_qualified=False,
    full_import=False, complete_assets_delivered_increment=0)
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))
