"""Compose six full native material candidates with explicit RG-or-AG support."""
from pathlib import Path
from delta_foliage_rgorag import candidate

BASE = Path(__file__).with_name('build_delta_native_lit_fragments_v1.py')
HELPER = Path(__file__).with_name('delta_foliage_rgorag.py')
script = BASE.read_text('utf-8-sig')
script = script.replace("ROOT = W / 'delta-native-lit-fragments-v1'", "ROOT = W / 'delta-native-lit-fragments-rgorag-v2'")
needle = "native = path.read_text('utf-8-sig')"
assert script.count(needle) == 1
script = script.replace(needle, "native = candidate(path.read_text('utf-8-sig'))")
needle = "    result = dict(schema='hivemind.delta-native-lit-fragments.v1'"
assert script.count(needle) == 1
script = script.replace(needle, "    pins[str(BASE)], pins[str(HELPER)] = sha(BASE), sha(HELPER)\n    result = dict(schema='hivemind.delta-native-lit-fragments.v1'")
needle = 'pipeline_state_admission_pending=True))'
assert script.count(needle) == 1
script = script.replace(needle, 'pipeline_state_admission_pending=True, rgorag_mode2_requires_measured_gpu_and_host_admission=True))')
exec(compile(script, str(BASE), 'exec'))
