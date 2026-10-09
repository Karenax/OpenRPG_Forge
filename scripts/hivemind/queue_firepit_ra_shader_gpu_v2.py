"""Preserve v1 refusal; fix Rust header placement only, same GPU comparison."""
from pathlib import Path
import ast

HERE = Path(__file__).resolve().parent
BASE_SCRIPT = HERE / 'queue_firepit_ra_shader_gpu_v1.py'
tree = ast.parse(BASE_SCRIPT.read_text('utf-8'))
nodes = [node for node in tree.body if not isinstance(node, ast.If)]
namespace = dict(__file__=str(Path(__file__).resolve()))
exec(compile(ast.Module(body=nodes, type_ignores=[]), str(BASE_SCRIPT), 'exec'), namespace)
namespace['ROOT'] = namespace['W'] / 'delta-firepit-ra-shader-gpu-v2'
namespace['NAME'] = 'openrpg-firepit-ra-shader-gpu-v2'
original_source = namespace['gpu_source']

def gpu_source():
    text = original_source()
    # Prefix adds imports before the original two inner module doc comments.
    # Comments only: no shader, sampling, fixtures or tolerance change.
    lines = text.splitlines(keepends=True)
    headers = [index for index, line in enumerate(lines) if line.startswith('//!')]
    assert len(headers) == 2, 'Expected original two GPU helper header comments'
    for index in headers:
        lines[index] = '//' + lines[index][3:]
    return ''.join(lines)

namespace['gpu_source'] = gpu_source
if __name__ == '__main__':
    refusal = namespace['W'] / 'delta-firepit-ra-shader-gpu-v1/RUN-RESULT.v1.json'
    prior = namespace['read'](refusal)
    assert prior['build_exit_code'] == 101 and prior.get('finished_utc')
    namespace['run' if len(namespace['sys'].argv) > 1 and namespace['sys'].argv[1] == 'run' else 'prepare']()
