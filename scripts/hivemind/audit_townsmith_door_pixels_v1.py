from pathlib import Path
import hashlib, json
from datetime import datetime, timezone
import numpy as np
from PIL import Image

W = Path("F:/OpenRPG_ImportWork")
P = W / "townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json"
OUT = W / "townsmith-door-pixel-audit-v1"
assert not OUT.exists()
proof = json.loads(P.read_text(encoding="utf-8"))
pins = {str(P): hashlib.sha256(P.read_bytes()).hexdigest()}
rows = []
for row in proof["comparisons"]:
    if not row["id"].endswith("slot_repeat_last") or not row["id"].startswith(("case-00", "case-01", "case-02")):
        continue
    original = W / "townsmith-native-draw-slots-v3" / (row["id"][:7] + ".rgba16f")
    control = W / "townsmith-explicit-slot-controls-v1" / (row["id"] + ".rgba16f")
    for p in [original, control]:
        actual = hashlib.sha256(p.read_bytes()).hexdigest()
        assert proof["input_pins"][str(p)] == actual
        pins[str(p)] = actual
    a = np.fromfile(original, dtype="<f2").reshape(512,512,4).astype(np.float32)
    b = np.fromfile(control, dtype="<f2").reshape(512,512,4).astype(np.float32)
    points = np.argwhere(np.any(a[:,:,:3] != b[:,:,:3], axis=2))
    observations = [{"x":int(x),"y_raw":int(y),"source_rgb":a[y,x,:3].tolist(),"control_rgb":b[y,x,:3].tolist(),"max_channel_difference":float(np.max(np.abs(a[y,x,:3]-b[y,x,:3])))} for y,x in points]
    rows.append({"id":row["id"],"different_pixels":len(observations),"observations":observations})
OUT.mkdir()
result = {"status":"three_door_residual_gpu_pixel_sets_localized","finished_utc":datetime.now(timezone.utc).isoformat(),"rows":rows,"thresholds_changed":False,"fidelity_qualified":False,"complete_assets_delivered_increment":0,"next_action":"Render controls preserving original index buffer and submesh descriptor, to distinguish regrouping from source renderer semantics","input_pins":pins}
(OUT / "RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
print(json.dumps(result))
