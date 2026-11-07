"""Decode exact Mountain views and preserve full SDK material diffs; no admission."""
from pathlib import Path
from datetime import datetime, timezone
import difflib
import hashlib
import json
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
O = W / 'mountain-exact-capture-review-v897'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    run_path = W / 'mountain-exact-native-capture-v875/RUN-RESULT.v1.json'
    run = read(run_path)
    assert run['status'] == 'native_capture_finished_requires_migration_and_visual_review'
    assert run['unity_exit_code'] == 0 and run['all_views_repeat_exact'] and not run['refused_cases']
    for path, digest in run['output_pins'].items():
        assert sha(path) == digest, path
    prepared_path = W / 'mountain-exact-source-capture-v873/PREPARED.v1.json'
    prepared = read(prepared_path)
    stage = read(W / 'mountain-hdrp-capture-stage-v869/RESULT.v1.json')
    copies = {row['target']: row for row in stage['copies']}
    O.mkdir(exist_ok=False)
    (O / 'material-diffs').mkdir()
    changes = []
    for index, change in enumerate(run['input_changes']):
        copy = copies[change['path']]
        assert copy['kind'] == 'purchased-render-data' and Path(copy['target']).suffix == '.mat'
        assert sha(copy['source']) == copy['sha256'] == change['before']
        assert sha(copy['target']) == change['after']
        before = Path(copy['source']).read_text('utf-8-sig')
        after = Path(copy['target']).read_text('utf-8-sig')
        diff = ''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True),
                                         fromfile=copy['source'], tofile=copy['target']))
        target = O / 'material-diffs' / f'{index:03d}-{Path(copy["target"]).stem}.diff'
        target.write_text(diff, encoding='utf-8')
        changes.append(dict(source=copy['source'], target=copy['target'],
                            original_sha256=change['before'], migrated_sha256=change['after'],
                            complete_diff=str(target), diff_sha256=sha(target),
                            migration_semantics_qualified=False))
    rows, flags = [], []
    for index in range(47):
        request = prepared['cases'][index*3+1]
        receipt = Path(request['output'])
        native = read(receipt)
        assert native['id'] == request['id'] and native['prefab_guid'] == request['prefab_guid']
        assert native['requested_lod'] == request['requested_lod']
        assert native['source_object_unchanged'] and native['requested_lod_selection_verified']
        assert native['preview_scene_isolated'] and native['presentation_only']
        assert native['gpu'] == 'Direct3D11' and len(native['images']) == 2
        failed_passes = [p for p in native['compiled_passes'] if not p['compiled']]
        if failed_passes:
            flags.append(dict(id=request['source_card_id'], reason='uncompiled_source_passes', passes=failed_passes))
        for image in native['images']:
            assert sha(image['path']) == image['sha256']
            assert sha(image['raw_path']) == image['raw_sha256']
            with Image.open(image['path']) as png:
                png.load()
                assert png.format == 'PNG' and png.size == (512, 512)
                if png.convert('RGB').getcolors(1) is not None:
                    flags.append(dict(id=request['source_card_id'], view=image['view'], reason='uniform_image'))
            if image['clipped_rgb_pixels']:
                flags.append(dict(id=request['source_card_id'], view=image['view'], reason='clipped_pixels', count=image['clipped_rgb_pixels']))
        rows.append(dict(id=request['source_card_id'], prefab_path=request['prefab_path'],
                         requested_lod=native['requested_lod'], native_receipt=str(receipt),
                         native_receipt_sha256=sha(receipt), images=native['images']))
    sheets = []
    for page, start in enumerate(range(0, len(rows), 6)):
        selected = rows[start:start+6]
        sheet = Image.new('RGB', (512, 280*len(selected)), (35,35,35))
        draw = ImageDraw.Draw(sheet)
        for offset, row in enumerate(selected):
            draw.text((5, 280*offset+3), Path(row['prefab_path']).stem+' LOD '+str(row['requested_lod']), fill='white')
            for image in row['images']:
                with Image.open(image['path']) as png:
                    thumb = png.convert('RGB').resize((256,256))
                    sheet.paste(thumb, (256*image['view'], 280*offset+24))
        path = O / f'contact-sheet-{page:02d}.png'
        sheet.save(path)
        sheets.append(dict(path=str(path), sha256=sha(path)))
    result = dict(status='mountain_images_decoded_full_material_diffs_preserved_review_pending',
                  utc=datetime.now(timezone.utc).isoformat(), cards=47, views=94,
                  source_material_changes=changes, review_flags=flags, rows=rows,
                  contact_sheets=sheets, all_views_repeat_exact=True,
                  input_pins={str(run_path): sha(run_path), str(prepared_path): sha(prepared_path),
                              str(Path(__file__)): sha(Path(__file__))},
                  agent_visual_review=False, sdk_migration_semantics_qualified=False,
                  images_admitted=0, full_import_increment=0, actual_studio_gui_verified=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(status=result['status'], cards=47, views=94,
                         material_changes=len(changes), review_flags=len(flags), sheets=sheets)))

if __name__ == '__main__':
    main()
