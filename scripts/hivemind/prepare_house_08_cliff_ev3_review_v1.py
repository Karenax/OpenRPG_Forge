"""Decode every new PNG and create labelled contact sheets for agent inspection."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'house-source-thumbnail-batch-0001-0003-exposure-v5'
ROOT = W / 'house-cliffs-ev3-review-v1'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
run = read(CAP / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['source_inputs_unchanged'] and run['cards_captured'] == 3 and run['cards_refused'] == 0
prep = read(CAP / 'PREPARED.v1.json')
batch = read(CAP / 'NATIVE-RESULT.v1.json')
assert [r['id'] for r in batch['cases']] == [r['id'] for r in prep['cases']]
pins = dict(prep['input_pins'])
pins.update({str(p): sha(p) for p in [Path(__file__), CAP / 'PREPARED.v1.json', CAP / 'RUN-RESULT.v1.json', CAP / 'NATIVE-RESULT.v1.json']})
ROOT.mkdir()
rows, sheets = [], []
for group in range(1):
    sheet = Image.new('RGB', (1024, 3 * 540), (35, 35, 35))
    draw = ImageDraw.Draw(sheet)
    for index in range(3):
        case = batch['cases'][index]
        assert case['captured']
        native = read(Path(case['receipt']))
        assert native['id'] == case['id'] and native['source_object_unchanged'] and native['preview_scene_isolated']
        assert native['gpu'] != 'Null' and native['presentation_only'] and native['presentation_fixed_ev'] == 3
        assert native['compiled_passes'] and all(p['compiled'] for p in native['compiled_passes'])
        assert len(native['images']) == 2
        pins[case['receipt']] = sha(case['receipt'])
        image_rows = []
        y = (index % 4) * 540
        draw.text((8, y + 6), Path(prep['cases'][index]['prefab_path']).stem, fill=(255, 255, 255))
        for image in native['images']:
            path = Path(image['path'])
            assert path.resolve().parent == (Path(case['receipt']).parent / 'images').resolve()
            assert sha(path) == image['sha256']
            assert image['clipped_rgb_pixels'] == 0 and image['linear_max_channel'] <= 1
            with Image.open(path) as png:
                png.load()
                assert png.format == 'PNG' and png.size == (512, 512)
                rgb = png.convert('RGB')
                assert rgb.getcolors(1) is None, 'Uniform blank thumbnail'
                sheet.paste(rgb, (512 * image['view'], y + 28))
            pins[str(path)] = image['sha256']
            image_rows.append(image)
        rows.append({'id': case['id'], 'native_receipt': case['receipt'], 'native_receipt_sha256': sha(case['receipt']), 'images': image_rows})
    path = ROOT / f'contact-sheet-{group}.png'
    sheet.save(path)
    sheets.append({'path': str(path), 'sha256': sha(path), 'cards': [r['id'] for r in rows[:3]]})
for p, h in pins.items():
    assert sha(p) == h, p
(ROOT / 'CANDIDATES.v1.json').write_text(json.dumps({'status': 'six_pngs_decoded_agent_visual_review_pending', 'finished_utc': datetime.now(timezone.utc).isoformat(),
           'rows': rows, 'contact_sheets': sheets, 'input_pins': pins, 'agent_review': False, 'human_review': False, 'full_import': False}, indent=2), encoding='utf-8')
print(json.dumps({'cards': 3, 'images': 6, 'sheets': sheets}))
