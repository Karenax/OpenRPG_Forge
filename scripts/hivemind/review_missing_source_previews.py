"""Reopen original preview candidates; identify observed generic/empty images."""
import collections
import datetime
import hashlib
import io
import json
from pathlib import Path
from PIL import Image

WORK = Path('F:/OpenRPG_ImportWork')
MISSING = WORK / 'library-source-preview-coverage-v1/MISSING_PREVIEWS.v1.json'
INVENTORY = WORK / 'remaining-source-preview-inventory-v1/RECORDS.v1.json'
ROOT = WORK / 'library-missing-preview-source-review-v1'
# Exact image inspected in this session: a generic cyan cube, not an asset render.
CUBE = 'e7606a6d82350b2850eb8fc9da7e1906c1e60a45a8c5b27959cae0f6a7e714b0'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    assert not ROOT.exists(), 'Preserve prior review'
    pins = {str(p): sha(p.read_bytes()) for p in (MISSING, INVENTORY, Path(__file__))}
    missing = json.loads(MISSING.read_bytes())['rows']
    old = json.loads(INVENTORY.read_bytes())['rows']
    candidates = {r['asset_id']: r for r in old}
    assert len(candidates) == len(old)
    assert len(missing) == 948 and all(r['id'] in candidates for r in missing)
    decoded = {}
    rows = []
    for source in missing:
        candidate = candidates[source['id']]
        assert candidate['pack_id'] == source['pack_id']
        if not candidate['preview_found']:
            rows.append({'id': source['id'], 'pack_id': source['pack_id'],
                         'classification': 'no_original_png_in_prior_inventory'})
            continue
        path = Path(candidate['preview_file'])
        raw = path.read_bytes()
        digest = sha(raw)
        assert digest == candidate['preview_sha256']
        if digest not in decoded:
            with Image.open(io.BytesIO(raw)) as image:
                image.load()
                assert image.format == 'PNG'
                assert image.size == (candidate['width'], candidate['height'])
                colors = image.convert('RGBA').getcolors(2)
                if digest == CUBE:
                    classification = 'visually_inspected_generic_cube_icon'
                else:
                    assert colors == [(image.width * image.height, (82, 82, 82, 255))]
                    classification = 'uniform_opaque_grey_image_without_asset_content'
                decoded[digest] = {'representative_file': str(path),
                                   'width': image.width, 'height': image.height,
                                   'classification': classification}
        rows.append({'id': source['id'], 'pack_id': source['pack_id'],
                     'original_png': str(path), 'sha256': digest,
                     'classification': decoded[digest]['classification']})
    for path, digest in pins.items():
        assert sha(Path(path).read_bytes()) == digest
    counts = dict(collections.Counter(r['classification'] for r in rows))
    assert counts == {'visually_inspected_generic_cube_icon': 738,
                      'uniform_opaque_grey_image_without_asset_content': 139,
                      'no_original_png_in_prior_inventory': 71}
    report = {'schema': 'openrpg.missing-source-preview-review.v1',
              'status': 'all_missing_source_preview_candidates_reviewed',
              'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'input_pins': pins, 'source_cards_reviewed': len(rows),
              'original_png_files_rehashed': 877, 'distinct_png_contents_decoded': len(decoded),
              'classification_counts': counts, 'image_contents': decoded, 'rows': rows,
              'faithful_asset_preview_candidates_found': 0,
              'generic_cube_agent_visual_inspection': True, 'human_visual_approval': False,
              'vault_modified': False, 'images_generated': False, 'full_import': False}
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('rows', 'input_pins', 'image_contents')}))


if __name__ == '__main__':
    main()
