"""Compose House surface equations, including the original POM depth terminal."""
from pathlib import Path

S = Path(__file__).resolve().parent
BASE = S / 'build_delta_native_surface_interfaces_v1.py'
code = BASE.read_text('utf-8-sig')
edits = {
    'delta-native-surface-interfaces-v1': 'house-native-surface-interfaces-v658',
    'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json': 'house-bevy-vertex-stages-v657/RESULT.v1.json',
    'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json': 'house-main-material-lot-v603/RESULT.v1.json',
    "ir_rows = {row['material_guid']: row for row in read(topology_path)['rows']}": "ir_rows = {row['material_guid']:dict(output=row['ir_path'],output_sha256=row['ir_sha256']) for row in read(topology_path)['rows']}",
    'assert len(surface_terminals) == 10': 'assert len(surface_terminals) == 9 and "SurfaceDescription.DepthOffset" in surface_terminals',
    'assert len(arguments) == len(fields) == 13': 'assert len(arguments) == len(fields) == 12',
    'hivemind.delta-native-surface-interfaces.v1': 'hivemind.house-native-surface-interfaces.v658',
    '    rows, failures = [], []': '    pins[str(BASE)] = sha(BASE)\n    pins[str(S / "build_house_bevy_vertex_stages_v656.py")] = sha(S / "build_house_bevy_vertex_stages_v656.py")\n    rows, failures = [], []',
}
for old, new in edits.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(BASE) + '#house-original-depth-surface', 'exec'), globals())
