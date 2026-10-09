"""Private hardware output-format control; unchanged surface shader and geometry."""
from pathlib import Path
import json
from datetime import datetime, timezone

W = Path('F:/OpenRPG_ImportWork')
BASE = Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
ROOT = W / 'firepit-surface-packed-hdr-control-v1'
PLAN = W / 'firepit-surface-packed-hdr-control-plan-v1'

source = BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1', ROOT.name)
source = source.replace("CASE=W/'firepit-surface-native-case-v1'", "CASE=W/'firepit-surface-instance-fix-case-v1'")
source = source.replace("    rust=rust.replace(marker,observe+marker)", """    rust=rust.replace(marker,observe+marker)
    old_format='TextureFormat::Rgba16Float,None)'
    assert rust.count(old_format)==1
    rust=rust.replace(old_format,'TextureFormat::Rg11b10Ufloat,None)')
    rust=rust.replace('assert_eq!(meta["format"],"Rgba16Float");assert_eq!(meta["bytes"],512*512*8);','assert_eq!(meta["format"],"Rg11b10Ufloat");assert_eq!(meta["bytes"],512*512*4);')
""")
if __name__ == '__main__':
    import sys
    if sys.argv[-1] == 'prepare':
        assert not ROOT.exists() and not PLAN.exists()
        PLAN.mkdir()
        protocol = dict(created_utc=datetime.now(timezone.utc).isoformat(),
            diagnostic='single final hardware Rg11b10Ufloat output conversion; internal lighting buffer parity not inferred',
            actual_source_context=str(W/'firepit-source-sampling-context-reopen-v1/RESULT.v1.json'),
            unchanged_case=str(W/'firepit-surface-instance-fix-case-v1/CASE.v1.json'),
            original_alpha_mask=str(W/'firepit-surface-instance-fix-v1'),
            packed_word_layout='R low 11 bits, G next 11 bits, B high 10 bits; unsigned 5-bit exponent, 6/6/5 mantissa',
            maximum_encoded_surface_mae=2/255, geometry_and_shader_changes=False,
            alpha_not_in_packed_format=True, alpha_mask_from_unchanged_previous_capture=True,
            thresholds_not_fitted_after_pixels=True, gpu_pixels_absent_at_definition=True,
            full_import=False, vault_assets_updated=0)
        (PLAN/'PROTOCOL.v1.json').write_text(json.dumps(protocol,indent=2),'utf-8')
    exec(compile(source, str(BASE), 'exec'), globals())
