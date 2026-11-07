"""Reopen every captured image; keep per-card capture and quality refusals."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'pirate-source-thumbnail-batch-0001-0024-v2'
ROOT = W / 'pirate-source-thumbnail-batch-0001-0024-review-v2'


def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
run = read(CAP / 'RUN-RESULT.v1.json')
migration_path = W / 'pirate-private-material-migrations-reopen-v2/RESULT.v1.json'
migration = read(migration_path)
assert run['actual_exit_code'] == 0 and migration['source_inputs_unchanged']
assert migration['status'] == 'sdk_import_changes_classified_source_thumbnail_review_admitted'
assert migration['alpha_keyword_changes_not_used_by_selected_cases']
prep = read(CAP / 'PREPARED.v1.json')
batch = read(CAP / 'NATIVE-RESULT.v1.json')
assert len(prep['cases']) == 24 and prep['original_lod_level'] == 0
assert [c['id'] for c in batch['cases']] == [c['id'] for c in prep['cases']]
pins = dict(migration['input_pins'])
pins[str(migration_path)] = sha(migration_path)
for p in [Path(__file__), CAP / 'PREPARED.v1.json', CAP / 'RUN-RESULT.v1.json', CAP / 'NATIVE-RESULT.v1.json']:
    pins[str(p)] = sha(p)
rows, refusals = [], []
for case, request in zip(batch['cases'], prep['cases']):
    if not case['captured']:
        refusals.append(dict(id=case['id'], stage='native_capture', reason=case['error']))
        continue
    native = read(case['receipt'])
    assert native['id'] == case['id'] and native['prefab_guid'] == request['prefab_guid']
    assert native['source_object_unchanged'] and native['preview_scene_isolated'] and native['gpu'] != 'Null'
    assert native['presentation_fixed_ev'] == 8 and native['presentation_only']
    assert native['compiled_passes'] and all(p['compiled'] for p in native['compiled_passes'])
    assert len(native['images']) == 2
    pins[case['receipt']] = sha(case['receipt'])
    errors = []
    for image in native['images']:
        p = Path(image['path'])
        assert p.resolve().parent == (Path(case['receipt']).parent / 'images').resolve()
        assert sha(p) == image['sha256']
        pins[str(p)] = image['sha256']
        with Image.open(p) as png:
            png.load()
            assert png.format == 'PNG' and png.size == (512, 512)
            if png.convert('RGB').getcolors(1) is not None:
                errors.append('uniform image')
        if image['clipped_rgb_pixels'] != 0 or image['linear_max_channel'] > 1:
            errors.append('clipped channel or highlights')
    if errors:
        refusals.append(dict(id=case['id'], stage='image_reopen', reason=errors, native_receipt=case['receipt']))
    else:
        rows.append(dict(id=case['id'], prefab_path=request['prefab_path'], native_receipt=case['receipt'],
                         native_receipt_sha256=sha(case['receipt']), images=native['images']))
for p, h in pins.items():
    assert sha(p) == h, p
ROOT.mkdir()
sheets = []
for group, start in enumerate(range(0, len(rows), 4)):
    selected = rows[start:start + 4]
    sheet = Image.new('RGB', (1024, len(selected) * 540), (35, 35, 35))
    draw = ImageDraw.Draw(sheet)
    for index, row in enumerate(selected):
        draw.text((8, index * 540 + 6), Path(row['prefab_path']).stem, fill=(255, 255, 255))
        for im in row['images']:
            with Image.open(im['path']) as png:
                sheet.paste(png.convert('RGB'), (im['view'] * 512, index * 540 + 28))
    p = ROOT / f'contact-sheet-{group}.png'
    sheet.save(p)
    sheets.append(dict(path=str(p), sha256=sha(p), cards=[r['id'] for r in selected]))
(ROOT / 'CANDIDATES.v1.json').write_text(json.dumps(dict(
    status='reopened_images_pending_individual_visual_review', finished_utc=datetime.now(timezone.utc).isoformat(),
    lot='L08.IMAGE.PIRATE.01', rows=rows, refusals=refusals, contact_sheets=sheets,
    input_pins=pins, private_import_migration_receipt=str(migration_path), source_version_parity_qualified=False, conversion_qualified=False, agent_review=False, human_review=False, full_import=False), indent=2), encoding='utf-8')
print(json.dumps(dict(cards_ready_for_review=len(rows), cards_refused=len(refusals), contact_sheets=sheets)))
