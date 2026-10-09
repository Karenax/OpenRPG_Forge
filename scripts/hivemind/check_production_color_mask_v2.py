"""Retain v1 refusal; put the frozen workspace beside the external harness.

The v1 nested standalone harness shadowed workspace.package inheritance.
No source renderer algorithm, fixture, assertion or locked dependency changes.
"""
from pathlib import Path
import ast

HERE = Path(__file__).resolve().parent
BASE = HERE / 'check_production_color_mask_v1.py'
tree = ast.parse(BASE.read_text('utf-8'))
selected = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom, ast.FunctionDef))]
W = Path('F:/OpenRPG_ImportWork')
namespace = dict(__file__=str(Path(__file__).resolve()), W=W,
    ROOT=W / 'source-graph-production-color-mask-check-v2',
    STAGED=W / 'source-graph-production-color-mask-v1',
    HOST=W / 'delta-rgb8-frozen-canonical-loader-v7',
    OLD_SNAPSHOT=W / 'delta-rgb8-frozen-canonical-loader-v6/snapshot',
    SNAPSHOT=W / 'source-graph-production-color-mask-snapshot-v2',
    BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin'),
    TARGET=Path('F:/OpenRPG_Forge/relocated-work/Rust-target'),
    WAIT=W / 'delta-firepit-alpha-port-gpu-v1/RUN-RESULT.v1.json')
exec(compile(ast.Module(body=selected, type_ignores=[]), str(BASE), 'exec'), namespace)

if __name__ == '__main__':
    if len(namespace['sys'].argv) > 1 and namespace['sys'].argv[1] == 'run':
        namespace['run']()
    else:
        previous=W / 'source-graph-production-color-mask-check-v1/RUN-RESULT.v1.json'
        receipt=namespace['read'](previous)
        assert receipt['status']=='refused_or_unverified' and receipt['checking_seventeen_actual_studio_host_tests_exit_code']==101
        assert not namespace['SNAPSHOT'].exists(), 'Preserve earlier snapshot'
        namespace['prepare']()
        prepared=namespace['read'](namespace['ROOT']/'PREPARED.v1.json')
        for p in [BASE,previous]: prepared['input_pins'][str(p)]=namespace['sha'](p)
        namespace['check'](prepared['input_pins'])
        namespace['save']('PREPARED.v1.json',prepared)
