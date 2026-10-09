"""Reopen exact nine-output source closures and all eight actual surface compositions."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_delta_native_surface_interfaces_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')
SCRIPT=SCRIPT.replace("ROOT = W / 'delta-native-surface-interfaces-v1'", "ROOT = W / 'delta-rocks-firepit-native-surface-v1'")
SCRIPT=SCRIPT.replace("W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json'", "W / 'delta-rocks-firepit-bevy-vertex-v1/RESULT.v1.json'")
SCRIPT=SCRIPT.replace('== 6','== 2').replace('== 24','== 8').replace('for channel in (0, 1):','for channel in (0,):')
start=SCRIPT.index("        assert '@location(9) source_uv1")
end=SCRIPT.index('        # Recompute closure independently',start)
SCRIPT=SCRIPT[:start]+"        assert 'source_uv1' not in text and 'output.uv1' not in text\n"+SCRIPT[end:]
SCRIPT=SCRIPT.replace('assert len(roots) == 10','assert len(roots) == 9')
SCRIPT=SCRIPT.replace('original_uv0_uv1_stage_transport_reopened=True','original_uv0_stage_transport_reopened=True, unused_uv1_not_required=True')
SCRIPT=SCRIPT.replace('native_surface_interfaces_reopened=6','native_surface_interfaces_reopened=2')
SCRIPT=SCRIPT.replace('actual_positive_compositions_reopened=24','actual_positive_compositions_reopened=8')
SCRIPT=SCRIPT.replace('reopened=6, compositions=24','reopened=2, compositions=8')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
