"""Reopen full FirePit particle inheritance and search the missing Rocks GUID in all 48 manifests."""
from pathlib import Path
import collections
import datetime
import hashlib
import importlib.util
import json

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-complete-prefab-source-reopen-v1'
TARGET_GUID = 'abc00000000001326820419954783426'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def write(path, value):
    with Path(path).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False, allow_nan=False)


def main():
    assert not ROOT.exists(), 'Prior evidence preserved'
    coverage_path = W / 'review/source-format-coverage.v1.json'
    coverage = read(coverage_path)
    assert len(coverage['manifests']) == 48
    parser_path = W / 'hivemind-effect-source-ir-v1/capture_source_ir.py'
    assert sha(parser_path) == read(parser_path.parent / 'RESULT.v1.json')['source_pins'][str(parser_path)]
    spec = importlib.util.spec_from_file_location('retained_effect_parser', parser_path)
    parser = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(parser)
    controls = parser.controls()
    assert controls['passed'] == 10
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(), coverage_path, parser_path]}
    manifests = []
    hits = []
    for row in coverage['manifests']:
        path = Path(row['manifest'])
        assert sha(path) == row['manifest_sha256']
        pins[str(path)] = row['manifest_sha256']
        source = read(path)
        assert source['private_only'] and source['license']
        matching = [f for f in source['files'] if f['guid'] == TARGET_GUID]
        indexed = source['guid_paths'].get(TARGET_GUID)
        if matching or indexed:
            hits.append(dict(manifest=str(path), archive_sha256=source['archive_sha256'],
                indexed_path=indexed, entries=matching, automatically_bound=False))
        manifests.append(dict(manifest=str(path), manifest_sha256=pins[str(path)],
            indexed_path=indexed, matching_entries=len(matching)))
    original_manifest = W / 'hivemind-run/native-american-village/source.v1.json'
    source = read(original_manifest)
    assert original_manifest.as_posix().replace('/', '\\') in pins or str(original_manifest) in pins
    source_root = Path(source['source_root'])
    index = {(f['source_path'], f['kind']): f for f in source['files']}

    def asset(guid):
        relative = source['guid_paths'][guid]
        path = source_root / relative
        assert path.resolve().is_relative_to(source_root.resolve())
        entry = index[(relative, 'asset')]
        meta = index[(relative, 'asset.meta')]
        assert entry['guid'] == meta['guid'] == guid
        assert sha(path) == entry['sha256'] and sha(str(path) + '.meta') == meta['sha256']
        pins[str(path)] = entry['sha256']
        pins[str(path) + '.meta'] = meta['sha256']
        return path

    parent_guid = 'abc00000000014341674918090329798'
    parent_path = asset(parent_guid)
    parent_documents = parser.parse(parent_path.read_bytes())
    instances = [d for d in parent_documents if d['type'] == 'PrefabInstance']
    assert len(instances) == 2
    plan_path = W / 'nested-prefab-scene-bindings-v2/RESULT.v1.json'
    plan = read(plan_path)
    assert not plan['errors'] and plan['source_and_derivative_pins_unchanged']
    pins[str(plan_path)] = sha(plan_path)
    firepit = next(r for r in plan['rows'] if r['original_root']['guid'] == parent_guid)
    assert len(firepit['nodes']) == 9 and len(firepit['static_renderer_bindings']) == 4
    assert len(firepit['deferred_renderers']) == 2
    rows = []
    for instance in instances:
        inherited_guid = instance['data']['m_SourcePrefab']['guid']
        path = asset(inherited_guid)
        documents = parser.parse(path.read_bytes())
        types = collections.Counter(d['type'] for d in documents)
        assert types['ParticleSystem'] == types['ParticleSystemRenderer'] == 1
        system = next(d for d in documents if d['type'] == 'ParticleSystem')
        renderer = next(d for d in documents if d['type'] == 'ParticleSystemRenderer')
        materials = renderer['data']['m_Materials']
        assert len(materials) == 1 and materials[0]['fileID'] == '2100000'
        material_guid = materials[0]['guid']
        native = next(r for r in firepit['deferred_renderers']
            if r['renderer']['original_material_slots'][0]['guid'] == material_guid)
        material_path = asset(material_guid)
        material_documents = parser.parse(material_path.read_bytes())
        selected_materials = [d for d in material_documents if d['type'] == 'Material' and d['file_id'] == '2100000']
        assert len(selected_materials) == 1
        # HDRP serialized helper documents remain intact; bought scripts never execute.
        material = selected_materials[0]['data']
        refs = list(parser.refs(material))
        dependencies = []
        for ref in refs:
            value = ref['reference']
            guid = value.get('guid')
            if guid in source['guid_paths'] and value['fileID'] != '0':
                dependency_path = asset(guid)
                dependencies.append(dict(field_path=ref['field_path'], source_reference=value,
                    source=str(dependency_path), source_sha256=pins[str(dependency_path)]))
        overrides = instance['data']['m_Modification']
        rows.append(dict(id='L06.NATIVE_AMERICAN.FIREPIT.' + path.stem,
            inherited_prefab_guid=inherited_guid, source=str(path), source_sha256=pins[str(path)],
            original_complete_documents=documents, particle_system_file_id=system['file_id'],
            particle_renderer_file_id=renderer['file_id'], parent_prefab_instance=instance,
            complete_parent_modifications=overrides, native_node_path=native['node_path'],
            native_hierarchy_renderer=native['renderer'], material_guid=material_guid,
            material_source=str(material_path), original_material_documents=material_documents,
            material_dependencies=dependencies, native_effective_overrides_captured=False,
            temporal_behavior_verified=False, runtime_converted=False, full_import=False))
    assert {r['native_node_path'] for r in rows} == {'root/0004', 'root/0005'}
    ROOT.mkdir()
    write(ROOT / 'MISSING-ROCKS-TEXTURE.v1.json', dict(checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        target_guid=TARGET_GUID, manifests_checked=48, rows=manifests, hits=hits,
        scope='Indexed retained source manifests only; no fresh archive payload qualification',
        automatically_substituted=False, native_default_pixels_verified=False, full_import=False))
    for ordinal, row in enumerate(rows):
        write(ROOT / f'{ordinal:02}-SOURCE-IR.v1.json', row)
    for p,d in pins.items():
        assert sha(p) == d
    # Independent file reopening checks retained IR against a fresh strict parse.
    for ordinal, row in enumerate(rows):
        saved = read(ROOT / f'{ordinal:02}-SOURCE-IR.v1.json')
        assert saved == row
        assert saved['original_complete_documents'] == parser.parse(Path(saved['source']).read_bytes())
        assert saved['original_material_documents'] == parser.parse(Path(saved['material_source']).read_bytes())
        assert saved['parent_prefab_instance'] in parser.parse(parent_path.read_bytes())
    output_pins = {str(p): sha(p) for p in ROOT.iterdir() if p.is_file()}
    result = dict(status='two_firepit_inherited_effect_sources_and_all_48_missing_guid_searches_reopened',
        checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), source_manifest=str(original_manifest),
        input_pins=pins, output_pins=output_pins, effect_sources_reopened=2,
        complete_parent_overrides_retained=True, manifests_searched=48,
        missing_rocks_texture_guid=TARGET_GUID, matching_manifests=len(hits),
        parser_controls=controls, sources_unchanged=True, vault_modified=False,
        temporal_behavior_verified=False, native_effective_overrides_captured=False,
        gpu_rendered=False, studio_verified=False, full_import=False)
    write(ROOT / 'RESULT.v1.json', result)
    print(json.dumps({k:v for k,v in result.items() if k not in {'input_pins','output_pins','parser_controls'}}))


if __name__ == '__main__':
    main()
