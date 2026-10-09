"""Pin original URP capture inputs; retain unresolved graph defaults explicitly."""
import datetime
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-urp-native-capture-inputs-v1'
BINDINGS = W / 'villa-urp-six-material-source-bindings-v1/RESULT.v1.json'
MANIFEST = W / 'hivemind-run/villa-forge/variants/51e32236d3832c1f/source.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def references(value):
    if isinstance(value, dict):
        if isinstance(value.get('guid'), str) and 'fileID' in value:
            yield dict(guid=value['guid'], file_id=str(value['fileID']))
        for child in value.values():
            yield from references(child)
    elif isinstance(value, list):
        for child in value:
            yield from references(child)
    elif isinstance(value, str) and value.lstrip().startswith(('{', '[')):
        try:
            child = json.loads(value)
        except json.JSONDecodeError:
            return
        yield from references(child)


def graph_documents(path):
    text = path.read_text('utf-8-sig')
    decoder, offset = json.JSONDecoder(), 0
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset == len(text):
            break
        document, offset = decoder.raw_decode(text, offset)
        yield document


def main():
    assert not ROOT.exists(), 'Existing preparation preserved'
    bindings, manifest = read(BINDINGS), read(MANIFEST)
    assert bindings['status'] == 'six_villa_urp_materials_and_two_active_graphs_bound'
    assert bindings['unresolved_authored_texture_bindings'] == 0
    pins = dict(bindings['input_pins'])
    pins.update({str(p): sha(p) for p in [BINDINGS, MANIFEST, Path(__file__).resolve()]})
    index = {(r['guid'], r['kind']): r for r in manifest['files']}
    assert len(index) == len(manifest['files'])
    files, graph_references = {}, []

    def include(guid):
        if guid in files:
            return Path(files[guid]['source'])
        row = index[(guid, 'asset')]
        path = MANIFEST.parent / row['stored_path']
        assert path.resolve().is_relative_to(MANIFEST.parent.resolve())
        assert path.suffix.lower() not in ['.cs', '.dll', '.exe']
        for kind in ['asset', 'asset.meta']:
            item = index[(guid, kind)]
            source = MANIFEST.parent / item['stored_path']
            assert source.resolve().is_relative_to(MANIFEST.parent.resolve())
            assert source.stat().st_size == item['bytes'] and sha(source) == item['sha256']
            pins[str(source)] = item['sha256']
        files[guid] = dict(guid=guid, source=str(path), sha256=row['sha256'],
                           meta_source=str(MANIFEST.parent / index[(guid, 'asset.meta')]['stored_path']))
        if path.suffix.lower() in ['.shadergraph', '.shadersubgraph']:
            refs = {(r['guid'], r['file_id']) for d in graph_documents(path) for r in references(d)
                    if len(r['guid']) == 32 and r['file_id'] != '0'}
            for child_guid, file_id in sorted(refs):
                resolved = (child_guid, 'asset') in index
                graph_references.append(dict(graph_guid=guid, guid=child_guid, file_id=file_id,
                                             source_resolved=resolved,
                                             missing_source_preserved=not resolved))
                if resolved:
                    include(child_guid)
        return path

    requests, defaults = [], []
    for row in bindings['rows']:
        material_path = include(row['material_guid'])
        include(row['shader_guid'])
        required = []
        for binding in row['texture_property_bindings']:
            if binding['authored_nonnull']:
                guid = binding['authored_texture']['m_Texture']['guid']
                include(guid)
                required.append(dict(property=binding['property'], guid=guid,
                                     file_id=binding['authored_file_id']))
            else:
                defaults.append(dict(material_guid=row['material_guid'], property=binding['property'],
                                     original_graph_property=binding['graph_property_object'],
                                     observation_required=True, replacement_invented=False))
        requests.append(dict(material_guid=row['material_guid'], material_file_id=row['material_file_id'],
                             asset_path=material_path.relative_to(MANIFEST.parent / 'source').as_posix(),
                             expected_shader_guid=row['shader_guid'], expected_shader_file_id=row['shader_file_id'],
                             required_textures=required))
    assert len(requests) == 6 and len(defaults) == 10
    assert all(sha(p) == h for p, h in pins.items()), 'Capture source changed'
    result = dict(status='six_original_urp_capture_requests_and_dependencies_pinned',
                  requests=requests, native_default_observations=defaults,
                  source_files=list(files.values()), graph_references=graph_references,
                  unresolved_graph_references=[r for r in graph_references if not r['source_resolved']],
                  input_pins=pins, native_pipeline_required='UniversalRenderPipeline',
                  next_action='Prepare isolated URP pipeline and compile capture helper; queue after existing Unity controllers',
                  source_scripts_executed=False, native_capture_executed=False,
                  assets_delivered=0, gpu_verified=False, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, allow_nan=False), 'utf-8')
    print(json.dumps(dict(status=result['status'], materials=len(requests),
                          source_files=len(files), native_defaults=len(defaults),
                          unresolved_graph_references=len(result['unresolved_graph_references']))))


if __name__ == '__main__':
    main()
