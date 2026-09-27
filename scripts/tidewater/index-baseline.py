"""Index inspected reference captures without asserting temporal determinism."""
import hashlib
import json
import struct
import sys
from pathlib import Path
import numpy as np
from PIL import Image

root = Path(sys.argv[1]).resolve(strict=True)
overlay = Path(sys.argv[2]).resolve(strict=True)
captures = []
for name in ("coast", "village", "island", "reef", "canopy", "coast-night"):
    png = (root / f"{name}.png").read_bytes()
    metadata_bytes = (root / f"{name}.json").read_bytes()
    metadata = json.loads(metadata_bytes)
    assert png[:8] == b"\x89PNG\r\n\x1a\n"
    dimensions = list(struct.unpack(">II", png[16:24]))
    assert dimensions == [1920, 1080] == metadata["output"]
    with Image.open(root / f"{name}.png") as image:
        pixels = np.asarray(image.convert('RGB'))
    # Reject the repeated 1280x720 presentation found in the first acquisition.
    assert not np.array_equal(pixels[:,1280:],pixels[:,:640]), 'tiled reference presentation'
    assert not np.array_equal(pixels[720:],pixels[:360]), 'tiled reference presentation'
    assert metadata["status"] == "ready_reference_only"
    assert metadata["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"
    assert metadata["view"] == ("coast" if name == "coast-night" else name)
    assert metadata["render_scale"] == 1
    assert metadata["warmup_frames"] == 120 and metadata["step_s"] == 1 / 60
    assert abs(metadata["time_s"] - 2) < 1e-12
    assert metadata["settings"]["timeSpeed"] == 0
    assert metadata["settings"]["timeOfDay"] == (22 if name == "coast-night" else 16.2)
    captures.append({"name": name, "image": f"{name}.png", "metadata": f"{name}.json",
                     "image_sha256": hashlib.sha256(png).hexdigest(),
                     "metadata_sha256": hashlib.sha256(metadata_bytes).hexdigest(),
                     "dimensions": dimensions})
result = {"schema": "forge.tidewater.reference-captures", "version": 1,
          "renderer": "upstream_webgpu", "overlay_sha256": hashlib.sha256(overlay.read_bytes()).hexdigest(),
          "captures": captures, "forge_parity": False,
          "limits": ["fixed_views_only_not_paths", "temporal_replay_not_qualified",
                     "island_oblique_view_not_full_cartographic_coverage", "adapter_metadata_not_captured"]}
acquisition_path = root / "acquisition.json"
if acquisition_path.exists():
    acquisition_bytes = acquisition_path.read_bytes()
    acquisition = json.loads(acquisition_bytes)
    assert acquisition["schema"] == "forge.tidewater.capture-session"
    assert acquisition["version"] == 1
    assert acquisition["viewport_set_before_load"] is True
    assert acquisition["environment"]["viewport"] == [1920, 1080]
    assert acquisition["environment"]["canvas"] == [1920, 1080]
    result["acquisition"] = {"path": acquisition_path.name,
                             "sha256": hashlib.sha256(acquisition_bytes).hexdigest()}
    result["limits"].remove("adapter_metadata_not_captured")
    result["limits"].append("session_adapter_metadata_only_not_per_view")
with (root / "index.json").open("x", encoding="utf-8") as output:
    json.dump(result, output, indent=2)
print(json.dumps({"captures": len(captures), "status": "indexed_reference_only"}))
