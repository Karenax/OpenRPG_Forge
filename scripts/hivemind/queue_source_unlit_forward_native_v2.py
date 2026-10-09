"""Queue unchanged four material tests after v4 actual renderer validation.

Retain all seventeen real Studio tests and the identical v3 renderer source.
"""
from pathlib import Path
import ast

HERE = Path(__file__).resolve().parent
BASE_SCRIPT = HERE / 'queue_source_unlit_forward_native_v1.py'
tree = ast.parse(BASE_SCRIPT.read_text('utf-8'))
namespace = dict(__file__=str(Path(__file__).resolve()))
exec(compile(ast.Module(body=[n for n in tree.body if not isinstance(n, ast.If)], type_ignores=[]), str(BASE_SCRIPT), 'exec'), namespace)
namespace['ROOT'] = namespace['W'] / 'source-unlit-forward-native-check-v2'
namespace['PROOF'] = namespace['W'] / 'source-graph-production-color-mask-check-v4'
node = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'run')
code = ast.unparse(node)
code = code.replace("result_path = BASE / 'RUN-RESULT.v1.json'", "result_path = PROOF / 'RUN-RESULT.v1.json'")
needle = "pins = dict(queued['input_pins'])"
assert code.count(needle) == 1
code = code.replace(needle, "verified = read(PROOF / 'PREPARED.v1.json')\n        check(verified['input_pins']); check(verified['frozen_pins'])\n        " + needle)
needle = 'pins.update(parent[\'frozen_pins\'])'
assert code.count(needle) == 1
code = code.replace(needle, needle + "\n        pins.update(verified['input_pins']); pins.update(verified['frozen_pins'])\n        pins[str(PROOF / 'PREPARED.v1.json')] = sha(PROOF / 'PREPARED.v1.json')\n        pins[str(BASE_SCRIPT)] = sha(BASE_SCRIPT)")
namespace['BASE_SCRIPT'] = BASE_SCRIPT
exec(compile(code, str(Path(__file__).resolve()), 'exec'), namespace)
if __name__ == '__main__':
    prior = namespace['read'](namespace['W'] / 'source-unlit-forward-native-check-v1/RUN-RESULT.v1.json')
    assert prior.get('finished_utc') and prior['actual_material_tests_passed'] == 0
    namespace['run' if len(namespace['sys'].argv) > 1 and namespace['sys'].argv[1] == 'run' else 'prepare']()
