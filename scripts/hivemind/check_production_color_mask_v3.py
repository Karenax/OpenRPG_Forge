"""Keep v2 evidence; complete the actual Studio frame fixture's legacy mask.

New immutable stage and sibling snapshot. No host assertion is removed.
"""
from pathlib import Path
import ast

HERE = Path(__file__).resolve().parent
BASE = HERE / 'check_production_color_mask_v1.py'
tree = ast.parse(BASE.read_text('utf-8'))
selected = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom, ast.FunctionDef))]
W = Path('F:/OpenRPG_ImportWork')
namespace = dict(__file__=str(Path(__file__).resolve()), W=W,
    ROOT=W / 'source-graph-production-color-mask-check-v3',
    STAGED=W / 'source-graph-production-color-mask-v2',
    HOST=W / 'delta-rgb8-frozen-canonical-loader-v7',
    OLD_SNAPSHOT=W / 'delta-rgb8-frozen-canonical-loader-v6/snapshot',
    SNAPSHOT=W / 'source-graph-production-color-mask-snapshot-v3',
    BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin'),
    TARGET=Path('F:/OpenRPG_Forge/relocated-work/Rust-target'),
    WAIT=W / 'delta-firepit-alpha-port-gpu-half-v2/RUN-RESULT.v1.json')
exec(compile(ast.Module(body=selected, type_ignores=[]), str(BASE), 'exec'), namespace)

def stage():
    read, sha, check = (namespace[name] for name in ('read', 'sha', 'check'))
    old = W / 'source-graph-production-color-mask-v1'
    prior = W / 'source-graph-production-color-mask-check-v2/RUN-RESULT.v1.json'
    result = read(prior)
    assert result['checking_seventeen_actual_studio_host_tests_exit_code'] == 101
    assert result['host_tests_passed'] == 0
    prepared = read(old / 'PREPARED.v1.json')
    pins = dict(prepared['input_pins']); pins.update(prepared['output_pins'])
    for source in (old / 'PREPARED.v1.json', prior, BASE, Path(__file__).resolve()):
        pins[str(source)] = sha(source)
    relative = 'apps/forge_studio/src/authoring_assets/source_graph_frame.rs'
    source = namespace['OLD_SNAPSHOT'] / relative
    pins[str(source)] = sha(source)
    content = source.read_text('utf-8')
    needle = '        let key = SourceGraphKey {\n'
    assert content.count(needle) == 1
    content = content.replace(needle, needle + '            color_write_mask: 15, // Existing legacy ALL-writes fixture.\n')
    check(pins)
    root = namespace['STAGED']; assert not root.exists()
    root.mkdir()
    outputs = {}
    paths = list(prepared['changed_paths']) + [relative]
    for path in paths:
        target = root / 'candidate' / path; target.parent.mkdir(parents=True, exist_ok=True)
        if path == relative:
            before = root / 'before' / path; before.parent.mkdir(parents=True, exist_ok=True)
            namespace['shutil'].copyfile(source, before)
            target.write_text(content, 'utf-8')
        else:
            namespace['shutil'].copyfile(old / 'candidate' / path, target)
        outputs[str(target)] = sha(target)
    check(pins); check(outputs)
    prepared.update(input_pins=pins, output_pins=outputs, changed_paths=paths,
        actual_engine_compiled=False, installed=False,
        prior_refusal_preserved=True, host_assertions_preserved=True)
    (root / 'PREPARED.v1.json').write_text(namespace['json'].dumps(prepared, indent=2), 'utf-8')

if __name__ == '__main__':
    if len(namespace['sys'].argv) > 1 and namespace['sys'].argv[1] == 'run':
        namespace['run']()
    else:
        assert not namespace['ROOT'].exists() and not namespace['SNAPSHOT'].exists()
        stage()
        namespace['prepare']()
