"""Preserve referenced ShaderGraph render data and exact original material state."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import sys

sys.path.insert(0, 'F:/OpenRPG_AssetVault/cache/skill-dev-python')
import yaml

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-foliage-render-contracts-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def objects(path):
    text = Path(path).read_text('utf-8-sig')
    decoder = json.JSONDecoder()
    offset, rows = 0, []
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset == len(text):
            break
        row, offset = decoder.raw_decode(text, offset)
        rows.append(row)
    mapping = {row['m_ObjectId']: row for row in rows}
    assert len(mapping) == len(rows)
    return rows, mapping


def property_table(entries):
    table = {}
    for entry in entries:
        assert len(entry) == 1
        key, value = next(iter(entry.items()))
        assert key not in table
        table[key] = value
    return table


def main():
    assert not ROOT.exists()
    sources_path = W / 'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    bindings_path = W / 'nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json'
    surface_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'
    sources, bindings, surfaces = read(sources_path), read(bindings_path), read(surface_path)
    wanted = {row['material_guid'] for row in surfaces['rows']}
    assert len(wanted) == surfaces['native_surface_interfaces_reopened'] == 6
    materials = {row['material_identity']['guid']: row for row in sources['rows']}
    graphs = {row['material_identity']['guid']: row['graph'] for row in bindings['rows']}
    pins = {str(path): sha(path) for path in [sources_path, bindings_path, surface_path, Path(__file__).resolve()]}
    rows = []
    for guid in sorted(wanted):
        source, graph = materials[guid], graphs[guid]
        material_path, graph_path = Path(source['source_material']['path']), Path(graph['path'])
        assert sha(material_path) == source['source_material']['sha256']
        assert sha(graph_path) == graph['sha256']
        pins[str(material_path)], pins[str(graph_path)] = sha(material_path), sha(graph_path)
        text = re.sub(r'^%.*\n', '', material_path.read_text('utf-8-sig'), flags=re.M)
        text = re.sub(r'^--- !u!\d+ &-?\d+.*$', '---', text, flags=re.M)
        documents = [doc['Material'] for doc in yaml.safe_load_all(text) if isinstance(doc, dict) and 'Material' in doc]
        assert len(documents) == 1 and documents[0] == source['source_material_properties']
        material = documents[0]
        assert material['m_Shader']['guid'] == graph['source_shader_guid']
        all_objects, by_id = objects(graph_path)
        roots = [obj for obj in all_objects if obj['m_Type'].endswith('.GraphData')]
        assert len(roots) == 1 and len(roots[0]['m_ActiveTargets']) == 1
        target = by_id[roots[0]['m_ActiveTargets'][0]['m_Id']]
        subtarget = by_id[target['m_ActiveSubTarget']['m_Id']]
        active_data = [by_id[reference['m_Id']] for reference in target['m_Datas']]
        lit = [obj for obj in active_data if obj['m_Type'].endswith('.HDLitData')]
        lighting = [obj for obj in active_data if obj['m_Type'].endswith('.LightingData')]
        builtin = [obj for obj in active_data if obj['m_Type'].endswith('.BuiltinData')]
        floats = property_table(material['m_SavedProperties']['m_Floats'])
        colors = property_table(material['m_SavedProperties']['m_Colors'])
        keywords = set(material['m_ValidKeywords'])
        row = dict(material_guid=guid, original_material=str(material_path), original_material_sha256=sha(material_path),
            original_graph=str(graph_path), original_graph_sha256=sha(graph_path), shader_guid=graph['source_shader_guid'],
            active_target=target, active_subtarget=subtarget, original_active_data=active_data,
            source_floats=floats, source_colors=colors, source_valid_keywords=material['m_ValidKeywords'],
            source_invalid_keywords_preserved=material.get('m_InvalidKeywords', []),
            source_custom_render_queue=material['m_CustomRenderQueue'], source_disabled_shader_passes=material.get('disabledShaderPasses', []),
            source_saved_properties=material['m_SavedProperties'], source_keyword_alpha_test='_ALPHATEST_ON' in keywords,
            source_keyword_alpha_to_mask='_ALPHATOMASK_ON' in keywords, source_keyword_double_sided='_DOUBLESIDED_ON' in keywords,
            source_keyword_transparent='_SURFACE_TYPE_TRANSPARENT' in keywords,
            hdrp_standard_active_data=len(lit) == 1 and lit[0]['m_MaterialType'] == 0,
            hdrp_normal_tangent_space=len(lighting) == 1 and lighting[0]['m_NormalDropOffSpace'] == 0,
            hdrp_specular_occlusion_mode=lighting[0]['m_SpecularOcclusionMode'] if len(lighting) == 1 else None,
            source_depth_offset_enabled=len(builtin) == 1 and builtin[0]['m_DepthOffset'],
            original_double_sided_constants_xyz=[colors['_DoubleSidedConstants'][component] for component in ['r', 'g', 'b']]
                if '_DOUBLESIDED_ON' in keywords else None,
            native_material_instance_capture_qualified=False, original_package_version_proven=False,
            renderer_installed=False, gpu_rendered=False, full_import=False)
        assert subtarget['m_Type'].endswith('.HDLitSubTarget')
        assert len(lit) == len(lighting) == len(builtin) == 1
        rows.append(row)
    assert all(sha(path) == digest for path, digest in pins.items())
    ROOT.mkdir()
    result = dict(schema='hivemind.delta-foliage-render-contracts.v1', observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        contracts_reopened=len(rows), rows=rows, input_pins=pins, only_referenced_active_target_data_used=True,
        original_valid_and_invalid_keywords_distinguished=True, renderer_installed=False, gpu_rendered=False, full_import=False)
    with (ROOT / 'RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2)
    print(json.dumps(dict(contracts=len(rows), rows=[{key: row[key] for key in ['material_guid', 'source_keyword_alpha_test',
        'source_keyword_alpha_to_mask', 'source_keyword_double_sided', 'original_double_sided_constants_xyz',
        'source_keyword_transparent', 'hdrp_specular_occlusion_mode', 'source_depth_offset_enabled']} for row in rows])))


if __name__ == '__main__':
    main()
