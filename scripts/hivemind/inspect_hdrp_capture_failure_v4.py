"""Preserve v4 refusals and distinguish original sources from Unity-upgraded copies."""
import datetime
import difflib
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'hdrp-v4-failure-and-material-copy-analysis-v1'
CAPTURE = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4'
OLD = W / 'unity-dungeon-original-shadergraph-compiler-v1'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Existing failure analysis preserved'
    paths = [CAPTURE / 'NATIVE-RESULT.v4.json', CAPTURE / 'RUN-RESULT.v4.json',
             CAPTURE / 'manifest.v4.json', OLD / 'PREPARED.v1.json', Path(__file__).resolve()]
    pins = {str(p): sha(p) for p in paths}
    native, manifest, prep = read(paths[0]), read(paths[2]), read(paths[3])
    assert native['requested'] == native['failed'] == len(native['cases']) == 42 and native['captured'] == 0
    assert read(paths[1])['status'] == 'refused'
    index = {Path(r['project_copy']).relative_to(Path(prep['project'])).as_posix(): r for r in prep['copies']}
    rows, diffs = [], {}
    for request, failure in zip(manifest['cases'], native['cases']):
        assert request['material_guid'] == failure['material_guid']
        copy = index[request['asset_path']]
        original, meta = Path(copy['original']), Path(copy['original_meta'])
        assert sha(original) == copy['original_sha256'] and sha(meta) == copy['original_meta_sha256']
        pins[str(original)] = copy['original_sha256']
        pins[str(meta)] = copy['original_meta_sha256']
        private = CAPTURE / 'project' / request['asset_path']
        private_sha = sha(private)
        pins[str(private)] = private_sha
        changed = private_sha != copy['original_sha256']
        if changed:
            diffs[request['material_guid']] = ''.join(difflib.unified_diff(
                original.read_text('utf-8-sig').splitlines(keepends=True),
                private.read_text('utf-8-sig').splitlines(keepends=True),
                fromfile=str(original), tofile=str(private)))
        rows.append(dict(material_guid=request['material_guid'], original=str(original),
                         original_sha256=copy['original_sha256'], private_copy=str(private),
                         private_sha256=private_sha, private_copy_changed=changed,
                         native_failure=failure['error'], active_render_pipeline_tag=failure['active_render_pipeline_tag']))
    assert all(sha(p) == h for p,h in pins.items()), 'Analysis input changed'
    ROOT.mkdir()
    result = dict(status='hdrp_v4_refusals_and_original_materials_reopened', materials=42,
                  original_materials_unchanged=42, private_material_copies_changed=len(diffs),
                  selected_pipeline_type=native['selected_pipeline_type'], graphics_api=native['graphics_api'],
                  captured=0, failed=42, rows=rows, input_pins=pins, native_materials_verified=0,
                  next_action='Observe actual pipeline instance, global shader pipeline tag, shader subshader tags and compiler messages before retrying',
                  gpu_verified=False, studio_verified=False, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'MATERIAL-COPY-DIFFS.v1.json').write_text(json.dumps(diffs, indent=2), 'utf-8')
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], original_materials_unchanged=42,
                         private_material_copies_changed=len(diffs), captured=0)))


if __name__ == '__main__':
    main()
