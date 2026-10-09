"""Use active graph property names while preserving the refused v1 selection."""
from pathlib import Path
import hashlib
import json

BASE = Path(__file__).with_name('queue_delta_foliage_native_materials_v1.py')
EXPECTED = 'c97a5219b9d6b2212e2df209f3da279f36d49ce2ff45f2a858e248bf85356e27'
assert hashlib.sha256(BASE.read_bytes()).hexdigest() == EXPECTED
script = BASE.read_text('utf-8-sig')


def replace(before, after):
    global script
    assert script.count(before) == 1, 'Baseline configuration changed'
    script = script.replace(before, after, 1)


replace("ROOT = common.W / 'unity-delta-foliage-native-materials-v1'",
        "ROOT = common.W / 'unity-delta-foliage-native-materials-v2'")
replace("    contracts, sources, kernels = [common.read(path) for path in [contracts_path, sources_path, kernels_path]]",
        "    bindings_path = common.W / 'nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json'\n"
        "    graph_bindings = {row['material_identity']['guid']: row['bindings'] for row in common.read(bindings_path)['rows']}\n"
        "    contracts, sources, kernels = [common.read(path) for path in [contracts_path, sources_path, kernels_path]]")
replace("    for path, expected in contracts['input_pins'].items():",
        "    pins[str(bindings_path)] = common.sha(bindings_path)\n"
        "    baseline = Path(__file__).with_name('queue_delta_foliage_native_materials_v1.py')\n"
        "    pins[str(baseline)] = common.sha(baseline)\n"
        "    for path, expected in contracts['input_pins'].items():")
replace("""        for entry in material['m_SavedProperties']['m_TexEnvs']:
            for name, value in entry.items():
                reference = value['m_Texture']
                if reference.get('guid') in required_guids:
                    textures.append(dict(property=name, guid=reference['guid'], file_id=str(reference['fileID'])))""",
        """        environments = {}
        for entry in material['m_SavedProperties']['m_TexEnvs']:
            for name, value in entry.items():
                assert name not in environments
                environments[name] = value
        for binding in graph_bindings[guid]:
            if not binding['source_property_object']['m_Type'].endswith('.Texture2DShaderProperty'):
                continue
            name = binding['reference_name']
            assert name in environments, 'Active authored texture property absent'
            reference = environments[name]['m_Texture']
            assert reference.get('guid') in required_guids, 'Active texture outside converted source closure'
            textures.append(dict(property=name, guid=reference['guid'], file_id=str(reference['fileID'])))""")

# Record the actual earlier preparation failure without creating any fake
# native output or rewriting v1. Preparation had stopped before project copies.
refusal_root = Path('F:/OpenRPG_ImportWork/delta-foliage-native-preparation-refusal-v1')
if not refusal_root.exists():
    refusal_root.mkdir()
    with (refusal_root / BASE.name).open('xb') as stream:
        stream.write(BASE.read_bytes())
    with (refusal_root / 'RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(actual_preparation_exit_code=1, source=str(BASE), source_sha256=EXPECTED,
            error='Serialized aliases selected by matching texture GUID exceeded the three active bindings',
            failed_assertion='len(textures) == 3 and requested GUID set equals kernel GUID set',
            project_copy_stage_reached=False, native_capture_launched=False, full_import=False), stream, indent=2)
assert hashlib.sha256((refusal_root / BASE.name).read_bytes()).hexdigest() == EXPECTED
exec(compile(script, str(BASE), 'exec'))
