"""Compare decoded RGBA pixels of pinned historical preview pairs."""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image, ImageChops


def difference(first, second):
    a, b = first.convert('RGBA'), second.convert('RGBA')
    if a.size != b.size:
        return {'state': 'different_dimensions', 'retained_size': a.size, 'retired_size': b.size}
    delta = ImageChops.difference(a, b)
    # RGBA getbbox() can default to checking alpha alone; compare both explicitly.
    rgb_box = delta.convert('RGB').getbbox()
    alpha_box = delta.getchannel('A').getbbox()
    histogram = delta.histogram()
    return {'state': 'different_pixels' if rgb_box or alpha_box else 'equal_pixels',
            'size': a.size, 'rgb_bbox': rgb_box, 'alpha_bbox': alpha_box,
            'absolute_channel_difference': sum((i % 256) * n for i, n in enumerate(histogram)),
            'pixels': a.width * a.height}


def open_pinned(entry):
    raw = Path(entry['path']).read_bytes()
    if hashlib.sha256(raw).hexdigest() != entry['sha256']:
        raise ValueError('preview SHA changed')
    with Image.open(io.BytesIO(raw)) as image:
        image.load()
        return image.copy()


def compare(source):
    raw = source.read_bytes()
    results = []
    for entry in json.loads(raw)['results']:
        if entry['same_bytes']:
            continue
        a, b = open_pinned(entry['retained_preview']), open_pinned(entry['retired_preview'])
        result = difference(a, b)
        result.update(retained_id=entry['retained_id'], retired_id=entry['retired_id'])
        results.append(result)
    return {'source_sha256': hashlib.sha256(raw).hexdigest(),
            'limitations': 'Historical thumbnails only; not a current Studio render or placement test.',
            'comparisons': results}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = compare(args.source)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'compared': len(result['comparisons']),
                      'different_pixels': sum(x['state'] == 'different_pixels' for x in result['comparisons'])}))
