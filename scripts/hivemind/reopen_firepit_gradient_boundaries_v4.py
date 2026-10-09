"""Use measured native expression rounding; retain exact one-ULP rejection.

This accepts a measurement capture, not a runtime gradient implementation.
"""
from pathlib import Path
import ast

HERE=Path(__file__).resolve().parent
BASE_SCRIPT=HERE/'reopen_firepit_gradient_boundaries_v3.py'
tree=ast.parse(BASE_SCRIPT.read_text('utf-8'))
namespace=dict(__file__=str(Path(__file__).resolve()))
exec(compile(ast.Module(body=[n for n in tree.body if not isinstance(n,ast.If)],type_ignores=[]),str(BASE_SCRIPT),'exec'),namespace)
namespace['ROOT']=namespace['W']/'firepit-gradient-boundaries-independent-reopen-v4'
node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='main')
code=ast.unparse(node)
old="expected = f32(1 - f32(f32(particle['remaining_lifetime']) / f32(particle['start_lifetime'])))"
assert code.count(old)==1
code=code.replace(old,"expected = f32(1 - f32(particle['remaining_lifetime']) / f32(particle['start_lifetime']))")
# The RHS string in the replacement map changes, while its old-assertion key
# still addresses the original verifier faithfully.
old="'f32(1 - f32(f32(particle[\'remaining_lifetime\']) / f32(particle[\'start_lifetime\'])))'"
# AST unparse chooses double quotes for strings containing single quotes.
old='"f32(1 - f32(f32(particle[\'remaining_lifetime\']) / f32(particle[\'start_lifetime\'])))"'
assert code.count(old)==1
code=code.replace(old,'"f32(1 - f32(particle[\'remaining_lifetime\']) / f32(particle[\'start_lifetime\']))"')
code=code.replace("CAPTURE / 'GRADIENT-BOUNDARIES.v1.json']", "CAPTURE / 'GRADIENT-BOUNDARIES.v1.json', BASE_SCRIPT, DIAGNOSIS, PRIOR]")
needle='result = module.verify(report, prepared)'
assert code.count(needle)==1
code=code.replace(needle,needle+"\n        manifest = read(CAPTURE / 'manifest.json')\n        for case in report['cases']:\n            for index, row in enumerate(case['rows']):\n                assert f32(row['requested_normalized_age']) == manifest['times'][index % len(manifest['times'])], 'Requested native age order/identity changed'")
needle='decimal_representation_differences=len(decimal_mismatches), float32_bit_mismatches=0,'
assert code.count(needle)==1
code=code.replace(needle,needle+"native_arithmetic_cause_qualified=False, native_expression_rounding_mapping_verified=True,")
namespace['BASE_SCRIPT']=BASE_SCRIPT
namespace['DIAGNOSIS']=namespace['W']/'firepit-native-age-arithmetic-diagnosis-v1/RESULT.v1.json'
namespace['PRIOR']=namespace['W']/'firepit-gradient-boundaries-independent-reopen-v3/RUN-RESULT.v1.json'
exec(compile(code,str(Path(__file__).resolve()),'exec'),namespace)
if __name__=='__main__':
    diagnosis=namespace['read'](namespace['DIAGNOSIS'])
    assert diagnosis['rows']==4096 and diagnosis['hypotheses']['division_extended']['exact']==4096
    assert namespace['read'](namespace['PRIOR'])['status']=='refused_or_unverified'
    namespace['main']()
