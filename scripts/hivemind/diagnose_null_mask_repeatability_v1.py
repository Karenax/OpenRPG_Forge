"""Quantify refused native repeats; never promote them into render parity."""
from pathlib import Path
import hashlib
import json
import math
import struct

ROOT = Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v2')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    target = ROOT/'REPEATABILITY-DIAGNOSIS.v1.json'
    assert not target.exists()
    result = json.loads((ROOT/'RUN-RESULT.v1.json').read_text('utf-8-sig'))
    assert result['unity_exit_code'] == 0 and not result['native_default_render_behavior_verified']
    report_path = ROOT/'NATIVE.v1.json'
    report = json.loads(report_path.read_text('utf-8-sig'))
    assert len(report['rows']) == 12
    pins = {str(report_path):sha(report_path), str(__file__):sha(Path(__file__)),
            str(ROOT/'RUN-RESULT.v1.json'):sha(ROOT/'RUN-RESULT.v1.json')}
    groups = {}
    for row in report['rows']:
        path = Path(row['path'])
        assert path.resolve().is_relative_to((ROOT/'native-pixels').resolve())
        raw = path.read_bytes()
        assert len(raw) == 8192 and sha(path) == row['sha256']
        pixels = list(struct.iter_unpack('<4e', raw))
        assert all(math.isfinite(channel) for pixel in pixels for channel in pixel)
        pins[str(path)] = sha(path)
        groups.setdefault(row['camera'], {})[row['variant']] = (pixels, row)
    comparisons = []
    for camera, group in groups.items():
        base, row = group['original_null']
        for variant in ('original_null_repeat','explicit_white_control','explicit_black_control'):
            other, other_row = group[variant]
            differences = [[abs(a-b) for a,b in zip(p,q)] for p,q in zip(base,other)]
            comparisons.append(dict(camera=camera, variant=variant,
                changed_pixels=sum(any(delta>0 for delta in pixel) for pixel in differences),
                maximum_channel_difference=max(max(pixel) for pixel in differences),
                source_time_before_equal=row['source_time_before']==other_row['source_time_before'],
                source_time_after_equal=row['source_time_after']==other_row['source_time_after']))
    data = dict(input_pins=pins, native_images_reopened=12, native_pixels_reopened=12288,
                comparisons=comparisons, cause_qualified=False, default_pixels_verified=False,
                native_refusal_preserved=True, full_import=False)
    for path,digest in pins.items(): assert sha(Path(path))==digest
    target.write_text(json.dumps(data,indent=2),'utf-8')
    print(json.dumps(dict(receipt=str(target),comparisons=comparisons)))


if __name__=='__main__': main()
