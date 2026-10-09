"""Keep all existing Lit-stage checks and verify the explicit decoder change."""
from pathlib import Path
from delta_foliage_rgorag import candidate, CANDIDATE

BASE = Path(__file__).with_name('reopen_delta_native_lit_fragments_v1.py')
HELPER = Path(__file__).with_name('delta_foliage_rgorag.py')
script = BASE.read_text('utf-8-sig')
script = script.replace("ROOT = W / 'delta-native-lit-fragments-v1'", "ROOT = W / 'delta-native-lit-fragments-rgorag-v2'")
needle = "assert text.startswith(Path(interfaces[guid]['native']).read_text('utf-8-sig'))"
assert script.count(needle) == 1
script = script.replace(needle, "assert text.startswith(candidate(Path(interfaces[guid]['native']).read_text('utf-8-sig')))\n            assert text.count(CANDIDATE) == 1\n            assert 'i.normal_mode_1' in text and 'source_graph.normal_decode_modes[0].y' in text")
needle = "    evidence = dict(schema='hivemind.delta-native-lit-fragments-independent-reopen.v1'"
assert script.count(needle) == 1
script = script.replace(needle, "    pins[str(BASE)], pins[str(HELPER)] = sha(BASE), sha(HELPER)\n    evidence = dict(schema='hivemind.delta-native-lit-fragments-independent-reopen.v1'")
exec(compile(script, str(BASE), 'exec'))
