"""Measure paired diagnostic silhouettes; never promotes scene visual parity."""
import hashlib
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image

left, right, output = map(Path, sys.argv[1:])
if output.exists():
    raise SystemExit("Refusing to replace existing comparison")

def load(path):
    with Image.open(path) as image:
        data = np.asarray(image.convert("RGB"))
    # A binary coverage pass must be achromatic, with black outside the plant.
    assert np.max(data, axis=2).max() == 255
    assert np.array_equal(data[:, :, 0], data[:, :, 1])
    assert np.array_equal(data[:, :, 1], data[:, :, 2])
    return data[:, :, 0] > 127

def dilate(mask):
    height, width = mask.shape
    padded = np.pad(mask, 1)
    result = np.zeros_like(mask)
    for y in range(3):
        for x in range(3):
            result |= padded[y:y+height, x:x+width]
    return result

reference, candidate = load(left), load(right)
assert reference.shape == candidate.shape == (1080, 1920)
union = reference | candidate
intersection = reference & candidate
assert 100 < int(reference.sum()) < reference.size
assert 100 < int(candidate.sum()) < candidate.size
disagreement = reference ^ candidate
outside_edge = (reference & ~dilate(candidate)) | (candidate & ~dilate(reference))
result = {
    "schema": "forge.tidewater.coverage-comparison", "version": 1,
    "status": "measured_geometry_alpha_only_not_scene_parity",
    "reference": str(left.resolve()), "candidate": str(right.resolve()),
    "reference_sha256": hashlib.sha256(left.read_bytes()).hexdigest(),
    "candidate_sha256": hashlib.sha256(right.read_bytes()).hexdigest(),
    "width": 1920, "height": 1080, "threshold": 127,
    "reference_pixels": int(reference.sum()), "candidate_pixels": int(candidate.sum()),
    "intersection_pixels": int(intersection.sum()), "union_pixels": int(union.sum()),
    "disagreement_pixels": int(disagreement.sum()),
    "intersection_over_union": float(intersection.sum() / union.sum()),
    "disagreement_beyond_one_pixel": int(outside_edge.sum()),
    "limits": ["fixed_pose_and_time_only", "no_lighting_shadows_or_temporal_comparison",
               "one_pixel_metric_is_diagnostic_not_automatic_acceptance"],
}
output.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))
