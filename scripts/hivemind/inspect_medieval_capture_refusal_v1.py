"""Preserve the actual three-material refusal and reopen purchased cloth sources."""
import datetime
import json
from pathlib import Path

import inspect_hdrp_material_migration_fields_v1 as fields

W = Path('F:/OpenRPG_ImportWork')
CAPTURE = W / 'unity-medieval-cloth-material-source-v1'
SOURCE = W / 'hivemind-run/medieval-kingdom/source'
ROOT = W / 'medieval-three-material-refusal-analysis-v1'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Prior refusal evidence preserved'
    paths = [CAPTURE / name for name in ('RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json',
                                        'PREPARED.v1.json', 'manifest.v1.json')]
    paths += [Path(__file__).resolve(), Path(fields.__file__).resolve()]
    pins = {str(path): fields.sha(path) for path in paths}
    run, native, prep, manifest = [read(path) for path in paths[:4]]
    assert run['status'] == 'refused' and run['actual_unity_exit_code'] == 1
    assert native['requested'] == native['failed'] == len(native['cases']) == 3
    assert native['captured'] == 0 and len(manifest['cases']) == 3
    source_pins = {path: expected for path, expected in prep['input_pins'].items()
                   if Path(path).resolve().is_relative_to(SOURCE.resolve())}
    assert source_pins and all(fields.sha(path) == expected for path, expected in source_pins.items())
    pins.update(source_pins)
    rows = []
    for request, refusal in zip(manifest['cases'], native['cases']):
        assert request['material_guid'] == refusal['material_guid']
        relative = Path(request['asset_path'])
        original, private = SOURCE / relative, Path(prep['project']) / relative
        meta = Path(str(original) + '.meta')
        assert original.resolve().is_relative_to(SOURCE.resolve())
        assert private.resolve().is_relative_to(Path(prep['project']).resolve())
        assert fields.sha(original) == source_pins[str(original)]
        assert fields.sha(meta) == source_pins[str(meta)]
        assert f"guid: {request['material_guid']}" in meta.read_text('utf-8-sig').splitlines()
        before, after = fields.documents(original), fields.documents(private)
        original_material = before['21:2100000']['Material']
        shader = original_material['m_Shader']
        assert shader['guid'] == request['expected_shader_guid']
        assert str(shader['fileID']) == str(request['expected_shader_file_id'])
        diff = fields.differences(before, after)
        changed_props = [d for d in diff if '/m_SavedProperties/' in d['path'] and d['kind'] != 'added']
        added_props = [d for d in diff if '/m_SavedProperties/' in d['path'] and d['kind'] == 'added']
        private_sha = fields.sha(private)
        pins[str(private)] = private_sha
        rows.append(dict(material_guid=request['material_guid'], original=str(original),
            original_sha256=source_pins[str(original)], private_copy=str(private),
            private_sha256=private_sha, private_copy_changed=private_sha != source_pins[str(original)],
            original_document_identities_retained=before.keys() == after.keys(),
            original_serialized_properties_retained=not changed_props,
            original_property_changes=changed_props, added_properties=added_props,
            all_field_differences=diff, native_failure=refusal['error'],
            active_render_pipeline_tag=refusal['active_render_pipeline_tag'],
            expected_shader_guid=request['expected_shader_guid'],
            shader_identity_and_passes_natively_verified=False, migration_accepted=False))
    assert all(fields.sha(path) == expected for path, expected in pins.items()), 'Analysis inputs changed'
    result = dict(status='three_medieval_original_materials_preserved_native_capture_refused',
        recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), materials=3,
        original_source_files_reopened=len(source_pins), original_materials_unchanged=3,
        private_material_copies_changed=sum(row['private_copy_changed'] for row in rows),
        original_serialized_properties_retained=sum(row['original_serialized_properties_retained'] for row in rows),
        added_property_occurrences=sum(len(row['added_properties']) for row in rows),
        actual_unity_exit_code=1, captured=0, failed=3, selected_pipeline_type=native['selected_pipeline_type'],
        graphics_api=native['graphics_api'], controller_failure=run['error'], rows=rows, input_pins=pins,
        input_pins_unchanged=True, sources_modified=False, native_materials_verified=0,
        migration_accepted=False, gpu_verified=False, studio_verified=False, full_import=False,
        next_action='Resolve the actual selected-subshader/pipeline context and observe private-copy migration additions before retry; retain all originals and keep current controllers unchanged')
    payload = json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False)
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(payload, 'utf-8')
    print(json.dumps({key: result[key] for key in ('status', 'original_source_files_reopened',
        'original_materials_unchanged', 'private_material_copies_changed',
        'original_serialized_properties_retained', 'added_property_occurrences', 'captured', 'failed')}))


if __name__ == '__main__':
    main()
