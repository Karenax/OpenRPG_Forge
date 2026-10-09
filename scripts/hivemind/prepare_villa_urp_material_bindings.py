"""Preserve six URP materials and resolve active targets/blackboard bindings."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, 'F:/OpenRPG_AssetVault/cache/skill-dev-python')
import yaml
from prepare_nested_graph_property_bindings import graph_properties

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-urp-six-material-source-bindings-v1'
EQUIVALENCE = W / 'villa-urp-no-png-source-equivalence-v1/RESULT.v1.json'
MANIFEST = W / 'hivemind-run/villa-forge/variants/51e32236d3832c1f/source.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def objects(text):
    decoder, offset, documents = json.JSONDecoder(), 0, []
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset == len(text):
            break
        document, offset = decoder.raw_decode(text, offset)
        assert isinstance(document, dict)
        documents.append(document)
    index = {d['m_ObjectId']: d for d in documents}
    assert len(index) == len(documents), 'Ambiguous graph object identity'
    roots = [d for d in documents if d.get('m_Type') == 'UnityEditor.ShaderGraph.GraphData']
    assert len(roots) == 1
    return roots[0], index


def main():
    assert not ROOT.exists(), 'Previous material preparation retained'
    equivalent, manifest = read(EQUIVALENCE), read(MANIFEST)
    pins = dict(equivalent['input_pins'])
    for path in [EQUIVALENCE, MANIFEST, Path(__file__).resolve(),
                 Path(__file__).with_name('prepare_nested_graph_property_bindings.py'),
                 *[p for p in Path(yaml.__file__).parent.rglob('*.py') if p.is_file()]]:
        pins[str(path)] = sha(path)
    index = {(r['guid'], r['kind']): r for r in manifest['files']}
    assert len(index) == len(manifest['files'])

    def source(guid, kind='asset'):
        row = index[guid, kind]
        path = MANIFEST.parent / row['stored_path']
        assert path.resolve().is_relative_to(MANIFEST.parent.resolve())
        assert sha(path) == row['sha256'] and path.stat().st_size == row['bytes']
        pins[str(path)] = row['sha256']
        return path

    rows, graphs = [], {}
    for comparison in equivalent['material_comparisons']:
        guid = comparison['guid']
        path = source(guid)
        source(guid, 'asset.meta')
        original = path.read_text('utf-8-sig')
        assert re.search(r'^--- !u!21 &2100000\s*$', original, re.M)
        plain = re.sub(r'^%.*\n', '', original, flags=re.M)
        plain = re.sub(r'^--- !u!\d+ &-?\d+.*$', '---', plain, flags=re.M)
        materials = [d['Material'] for d in yaml.safe_load_all(plain) if isinstance(d, dict) and 'Material' in d]
        assert len(materials) == 1
        material = materials[0]
        shader_guid = material['m_Shader']['guid']
        shader_id = str(material['m_Shader']['fileID'])
        graph_path = source(shader_guid)
        source(shader_guid, 'asset.meta')
        assert graph_path.suffix.lower() == '.shadergraph'
        text = graph_path.read_text('utf-8-sig')
        root, graph_index = objects(text)
        active = []
        for ref in root['m_ActiveTargets']:
            target = graph_index[ref['m_Id']]
            assert target['m_Type'] == 'UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget'
            subtarget = graph_index[target['m_ActiveSubTarget']['m_Id']]
            assert subtarget['m_Type'] == 'UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget'
            active.append(dict(target=target, subtarget=subtarget))
        assert len(active) == 1
        properties = graph_properties(text)
        graphs[shader_guid] = dict(path=str(graph_path), sha256=pins[str(graph_path)],
                                   active_targets=active, properties=properties)
        saved = material['m_SavedProperties']
        textures = {}
        for entry in saved.get('m_TexEnvs', []):
            for name, value in entry.items():
                assert name not in textures
                textures[name] = value
        bindings = []
        for prop in properties:
            if not prop['source_property_object']['m_Type'].endswith('Texture2DShaderProperty'):
                continue
            name = prop['reference_name']
            authored = textures.get(name)
            reference = authored.get('m_Texture', {}) if authored else {}
            nonnull = reference.get('fileID', 0) != 0 and bool(reference.get('guid'))
            asset_guid = reference.get('guid') if nonnull else None
            resolved = source(asset_guid) if asset_guid and (asset_guid, 'asset') in index else None
            if resolved:
                source(asset_guid, 'asset.meta')
            bindings.append(dict(property=name, graph_property_object=prop['source_property_object'],
                                 authored_texture=authored,
                                 authored_file_id=str(reference.get('fileID', 0)),
                                 authored_nonnull=nonnull, source_asset=str(resolved) if resolved else None,
                                 source_resolution=('authored_source_reopened' if resolved else
                                                    'authored_reference_missing' if nonnull else 'native_graph_default_observation_required')))
        rows.append(dict(material_guid=guid, material_file_id='2100000', source=str(path),
                         source_sha256=pins[str(path)], shader_guid=shader_guid, shader_file_id=shader_id,
                         source_material_properties=material, texture_property_bindings=bindings,
                         serialized_texture_names_not_in_graph=sorted(set(textures) - {p['reference_name'] for p in properties}),
                         native_pipeline_required='UniversalRenderPipeline', native_capture_executed=False,
                         converted=False, gpu_verified=False))
    assert len(rows) == 6 and len(graphs) == 2
    assert all(sha(p) == h for p, h in pins.items()), 'Source changed during material preparation'
    ROOT.mkdir()
    result = dict(status='six_villa_urp_materials_and_two_active_graphs_bound', materials=6, graphs=graphs,
                  rows=rows, input_pins=pins, native_capture_executed=False, shaders_generated=False,
                  unresolved_authored_texture_bindings=sum(b['source_resolution'] == 'authored_reference_missing'
                                                          for r in rows for b in r['texture_property_bindings']),
                  native_default_observations_required=sum(b['source_resolution'] == 'native_graph_default_observation_required'
                                                          for r in rows for b in r['texture_property_bindings']),
                  source_scripts_executed=False, assets_delivered=0, studio_verified=False, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, allow_nan=False), 'utf-8')
    print(json.dumps({k: result[k] for k in ['status', 'materials', 'unresolved_authored_texture_bindings',
                                          'native_default_observations_required', 'assets_delivered']}))


if __name__ == '__main__':
    main()
