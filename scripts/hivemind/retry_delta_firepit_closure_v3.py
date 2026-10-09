"""Retry FirePit alone with two byte-identical original subgraph dependencies."""
from pathlib import Path
import hashlib
import json

BASE = Path(__file__).with_name('queue_delta_remaining_native_materials_v1.py')
W = Path('F:/OpenRPG_ImportWork')
OLD = W / 'unity-delta-remaining-native-materials-v2'
SCRIPT = BASE.read_text('utf-8-sig')

def replace(old, new):
    global SCRIPT
    assert SCRIPT.count(old) == 1, old
    SCRIPT = SCRIPT.replace(old, new)

replace("ROOT = common.W / 'unity-delta-remaining-native-materials-v1'",
        "ROOT = common.W / 'unity-delta-firepit-closure-retry-v3'")
replace("WANTED = {'abc00000000009294800760372203426', 'abc00000000014678987794877247210'}",
        "WANTED = {'abc00000000009294800760372203426'}")
replace("    assert len(requests) == 2 and sum(len(r['required_textures']) for r in requests) == 10", """    assert len(requests) == 1 and sum(len(r['required_textures']) for r in requests) == 3
    original_manifest = SOURCE.parent / 'source.v1.json'
    inventory = common.read(original_manifest)
    pins[str(original_manifest)] = common.sha(original_manifest)
    entries = {str((SOURCE.parent / row['stored_path']).resolve()): row for row in inventory['files']}
    folder = SOURCE / 'Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Shaders'
    for name, guid in [('SubG_WorldAlignedTexture', 'c73979feb143b394ab7802f32a955d51'),
                       ('SubG_WorldAlignedTextureNormal', '467167fecd430ac4c982f83c94f86f75')]:
        graph = folder / (name + '.shadersubgraph')
        assert 'SubGraphNode' not in graph.read_text('utf-8-sig'), 'Resolve nested subgraphs first'
        assert re.search(r'^guid: ' + guid + r'\\s*$', Path(str(graph)+'.meta').read_text('utf-8-sig'), re.M)
        for path in [graph, Path(str(graph)+'.meta')]:
            row = entries[str(path.resolve())]
            assert common.sha(path) == row['sha256'] and path.stat().st_size == row['bytes']
            requested_files[str(path)] = row['sha256']
    previous = common.read(OLD / 'RUN-RESULT.v1.json')
    assert previous.get('finished_utc') and previous['actual_unity_exit_code'] == 1
    for path in [BASE, OLD / 'RUN-RESULT.v1.json', OLD / 'NATIVE-RESULT.v1.json',
                 W / 'delta-rockstemperate-native-independent-reopen-v1/RESULT.v1.json']:
        pins[str(path)] = common.sha(path)
    # Preserve successful Rocks capture; only FirePit is requested in the new manifest.
""")
replace("['.mat','.shadergraph','.png','.tga','.jpg','.jpeg','.dds','.meta']",
        "['.mat','.shadergraph','.shadersubgraph','.png','.tga','.jpg','.jpeg','.dds','.meta']")
replace('requested_materials=2, requested_texture_bindings=10', 'requested_materials=1, requested_texture_bindings=3')
replace('original_materials=2, active_bindings=10', 'original_materials=1, active_bindings=3')
replace("status='capturing_two_remaining_delta_native_materials'", "status='capturing_firepit_with_original_subgraph_closure'")
replace("len(independent_reconcile(manifest, captured)) == 2", "len(independent_reconcile(manifest, captured)) == 1")
replace("common.save('INDEPENDENT-REOPEN.v1.json', dict(native_materials_verified=2, active_texture_bindings_verified=10,",
        "common.save('INDEPENDENT-REOPEN.v1.json', dict(native_materials_verified=1, active_texture_bindings_verified=3,")
replace("status='two_remaining_delta_native_materials_and_ten_bindings_reopened'", "status='firepit_original_material_and_three_bindings_reopened'")
replace('native_materials_verified=2, active_texture_bindings_verified=10,', 'native_materials_verified=1, active_texture_bindings_verified=3,')
namespace = {'__name__': '__main__', '__file__': str(Path(__file__).resolve()), 'BASE': BASE, 'OLD': OLD, 'W': W}
exec(compile(SCRIPT, str(BASE), 'exec'), namespace)
