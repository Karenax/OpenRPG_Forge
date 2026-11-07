"""Retain original authored HDRP state before assembling real lighting fragments."""
from pathlib import Path
from datetime import datetime, timezone
import json
import re
from read_delta_foliage_render_contracts_v1 import objects, property_table, sha, yaml

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-original-render-contract-v1'


def main():
    assert not ROOT.exists()
    evidence = W / 'townsmith-s-paint-source-program-v1/RESULT.v1.json'
    prior = json.loads(evidence.read_text(encoding='utf-8'))
    pins = {str(evidence): sha(evidence), str(Path(__file__)): sha(__file__),
            str(Path(__file__).with_name('read_delta_foliage_render_contracts_v1.py')): sha(Path(__file__).with_name('read_delta_foliage_render_contracts_v1.py'))}
    material = next(Path(p) for p in prior['input_pins'] if p.endswith('/M_Trim-Wood2.mat') or p.endswith('\\M_Trim-Wood2.mat'))
    graph = next(Path(p) for p in prior['input_pins'] if p.endswith('S_Paint.shadergraph'))
    meta = Path(str(material) + '.meta')
    graph_meta = Path(str(graph) + '.meta')
    for path in (material, graph, meta, graph_meta):
        assert sha(path) == prior['input_pins'][str(path)], path
        pins[str(path)] = sha(path)
    text = re.sub(r'^%.*\n', '', material.read_text(encoding='utf-8-sig'), flags=re.M)
    text = re.sub(r'^--- !u!\d+ &-?\d+.*$', '---', text, flags=re.M)
    documents = [d['Material'] for d in yaml.safe_load_all(text) if isinstance(d, dict) and 'Material' in d]
    assert len(documents) == 1
    authored = documents[0]
    shader_guid = re.findall(r'^guid: ([0-9a-f]{32})$', graph_meta.read_text(encoding='utf-8-sig'), re.M)
    material_guid = re.findall(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
    assert len(shader_guid) == len(material_guid) == 1
    assert authored['m_Shader']['guid'] == shader_guid[0]
    assert material_guid[0] == 'abc00000000009486088228754689894'
    all_objects, by_id = objects(graph)
    roots = [o for o in all_objects if o['m_Type'].endswith('.GraphData')]
    assert len(roots) == 1 and len(roots[0]['m_ActiveTargets']) == 1
    target = by_id[roots[0]['m_ActiveTargets'][0]['m_Id']]
    subtarget = by_id[target['m_ActiveSubTarget']['m_Id']]
    active = [by_id[r['m_Id']] for r in target['m_Datas']]
    lit = [o for o in active if o['m_Type'].endswith('.HDLitData')]
    lighting = [o for o in active if o['m_Type'].endswith('.LightingData')]
    builtin = [o for o in active if o['m_Type'].endswith('.BuiltinData')]
    assert len(lit) == len(lighting) == len(builtin) == 1
    assert subtarget['m_Type'].endswith('.HDLitSubTarget')
    floats = property_table(authored['m_SavedProperties']['m_Floats'])
    colors = property_table(authored['m_SavedProperties']['m_Colors'])
    keywords = authored.get('m_ValidKeywords', authored.get('m_ShaderKeywords', '').split())
    assert isinstance(keywords, list)
    row = {'material_guid': material_guid[0], 'original_material': str(material), 'original_material_sha256': sha(material),
           'original_graph': str(graph), 'original_graph_sha256': sha(graph), 'shader_guid': shader_guid[0],
           'active_target': target, 'active_subtarget': subtarget, 'original_active_data': active,
           'source_floats': floats, 'source_colors': colors, 'source_valid_keywords': keywords,
           'source_invalid_keywords_preserved': authored.get('m_InvalidKeywords', []),
           'source_custom_render_queue': authored['m_CustomRenderQueue'],
           'source_disabled_shader_passes': authored.get('disabledShaderPasses', []),
           'source_saved_properties': authored['m_SavedProperties'], 'original_material_properties': authored,
           'source_keyword_alpha_test': '_ALPHATEST_ON' in keywords, 'source_keyword_alpha_to_mask': '_ALPHATOMASK_ON' in keywords,
           'source_keyword_double_sided': '_DOUBLESIDED_ON' in keywords, 'source_keyword_transparent': '_SURFACE_TYPE_TRANSPARENT' in keywords,
           'hdrp_standard_active_data': lit[0]['m_MaterialType'] == 0,
           'hdrp_normal_tangent_space': lighting[0]['m_NormalDropOffSpace'] == 0,
           'hdrp_specular_occlusion_mode': lighting[0]['m_SpecularOcclusionMode'],
           'source_depth_offset_enabled': builtin[0]['m_DepthOffset'],
           'original_double_sided_constants_xyz': [colors['_DoubleSidedConstants'][k] for k in ('r', 'g', 'b')] if '_DOUBLESIDED_ON' in keywords else None,
           'native_material_instance_capture_qualified': False, 'renderer_installed': False, 'gpu_rendered': False, 'full_import': False}
    for path, expected in pins.items():
        assert sha(path) == expected, path
    ROOT.mkdir()
    result = {'status': 'original_townsmith_hdrp_render_contract_reopened', 'finished_utc': datetime.now(timezone.utc).isoformat(),
              'contracts_reopened': 1, 'rows': [row], 'input_pins': pins,
              'original_valid_and_invalid_keywords_distinguished': True, 'only_referenced_active_target_data_used': True,
              'original_sources_unchanged': True, 'renderer_installed': False, 'gpu_rendered': False,
              'full_import': False, 'complete_assets_delivered_increment': 0}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({'status': result['status'], 'material_guid': row['material_guid'], 'source_custom_render_queue': row['source_custom_render_queue'],
                      'source_valid_keywords': keywords, **{k: row[k] for k in ('hdrp_standard_active_data', 'hdrp_normal_tangent_space', 'hdrp_specular_occlusion_mode', 'source_keyword_alpha_test', 'source_keyword_double_sided', 'source_keyword_transparent', 'source_depth_offset_enabled')}}))


if __name__ == '__main__':
    main()
