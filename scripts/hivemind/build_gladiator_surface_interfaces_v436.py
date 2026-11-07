"""Compose the nine actual surface terminals without inventing a depth-offset terminal."""
from pathlib import Path
S=Path(__file__).resolve().parent;BASE=S/'build_delta_native_surface_interfaces_v1.py'
code=BASE.read_text('utf-8')
edits={
 'delta-native-surface-interfaces-v1':'gladiator-native-surface-interfaces-v436',
 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json':'gladiator-bevy-vertex-stages-v435/RESULT.v1.json',
 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json':'gladiator-active-material-ir-v386/RESULT.v1.json',
 "ir_rows = {row['material_guid']: row for row in read(topology_path)['rows']}":"ir_rows = {row['guid']:dict(output=row['path'],output_sha256=row['sha256']) for row in read(topology_path)['rows']}",
 'assert len(surface_terminals) == 10':'assert len(surface_terminals) == 9 and "SurfaceDescription.DepthOffset" not in surface_terminals',
 'assert len(arguments) == len(fields) == 13':'assert len(arguments) == len(fields) == 12'
}
for a,b in edits.items():assert a in code,a;code=code.replace(a,b)
anchor="    pins = {str(path): sha(path) for path in [parent_path, OLD / 'RESULT.v1.json',";assert code.count(anchor)==1
# The wrapper contains fixed data types for both stages; original surface equations are unchanged.
code=code.replace("    rows, failures = [], []","    pins[str(BASE)]=sha(BASE)\n    rows, failures = [], []")
exec(compile(code,str(BASE)+'#gladiator-nine-surface-terminals','exec'),globals())
