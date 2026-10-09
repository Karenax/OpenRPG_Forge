"""Close only the predeclared model varying MAE checks, not renderer fidelity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
ROOT = W/'firepit-varying-transport-technical-v1'

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    path = W/'firepit-varying-native-reopen-v1/RESULT.v1.json'
    d = json.loads(path.read_text('utf-8-sig'))
    for p, expected in d['input_pins'].items():
        assert sha(Path(p)) == expected, p
    model = [r for view in d['rows'] for r in view['groups']['model']]
    receiver_uv = [r for view in d['rows'] for r in view['groups']['receiver'] if r['role']=='Texcoord0']
    assert len(model)==8 and all(r['within_predeclared_mae'] for r in model)
    assert len(receiver_uv)==2 and not any(r['within_predeclared_mae'] for r in receiver_uv)
    result = dict(status='model_interpolated_uv_and_tbn_predeclared_average_error_checks_verified',
        finished_utc=datetime.now(timezone.utc).isoformat(),views_verified=2,
        model_role_comparisons_verified=8,model_role_measurements=d['rows'][0]['groups']['model'],
        maximum_encoded_surface_mae=d['maximum_encoded_surface_mae'],
        maximum_local_channel_error=max(r['maximum_absolute_error'] for r in model),
        local_pixel_fidelity_qualified=False, receiver_uv_mismatch_preserved=True,
        receiver_uv_geometry_parity_expected=False, implicit_texture_gradient_parity_qualified=False,
        texture_sampling_fidelity_qualified=False, full_import=False, editor_visual_verified=False,
        vault_assets_updated=0,complete_assets_delivered_increment=0,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),path]},
        next_action='Capture original per-fragment UV derivatives and implicit mip selection; do not infer sampling parity from average encoded UV/TBN error.')
    ROOT.mkdir()
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','model_role_measurements']}))

if __name__=='__main__':
    main()
