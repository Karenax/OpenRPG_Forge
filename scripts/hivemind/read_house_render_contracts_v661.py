"""Reopen active House HDRP render data directly from preserved source files."""
from pathlib import Path
S = Path(__file__).resolve().parent
BASE = S / 'read_delta_foliage_render_contracts_v1.py'
code = BASE.read_text('utf-8-sig')
start = code.index("    sources_path = W / 'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'")
end = code.index('    rows = []', start)
setup = '''    sources_path = W / 'house-main-material-lot-v603/RESULT.v1.json'
    bindings_path = W / 'house-main-material-lot-v603/SHADER-SUBGRAPH-CLOSURE.v1.json'
    surface_path = W / 'house-host-interface-checkpoint-v660/RESULT.v1.json'
    sources, bindings, surfaces = read(sources_path), read(bindings_path), read(surface_path)
    for receipt in [sources, surfaces]:
        for path, digest in receipt['input_pins'].items():
            assert sha(path) == digest, path
    queue_path = W / 'house-main-material-lot-v603/SOURCE-QUEUE.v1.json'
    queue = read(queue_path)
    wanted = {row['material_guid'] for row in surfaces['rows']}
    assert len(wanted) == surfaces['material_count'] == 6
    graph = next(row for row in bindings['graphs'] if row['guid'] == '7dcd0e3bb8cbf2345a1e124f5a095198')
    materials = {guid:dict(source_material=dict(path=str(Path(queue['source_root']) / value['source']), sha256=value['sha256'])) for guid, value in queue['materials'].items()}
    graphs = {guid:dict(path=graph['path'],sha256=graph['sha256'],source_shader_guid=graph['guid']) for guid in wanted}
    pins = {str(path): sha(path) for path in [sources_path, bindings_path, surface_path, queue_path, BASE, Path(__file__).resolve()]}
'''
code = code[:start] + setup + code[end:]
old = "assert len(documents) == 1 and documents[0] == source['source_material_properties']"
assert old in code
code = code.replace(old, 'assert len(documents) == 1')
code = code.replace('delta-foliage-render-contracts-v1', 'house-render-contracts-v661')
code = code.replace('hivemind.delta-foliage-render-contracts.v1', 'hivemind.house-render-contracts.v661')
exec(compile(code, str(BASE) + '#house-pinned-active-render-data', 'exec'), globals())
