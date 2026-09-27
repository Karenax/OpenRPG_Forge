"""Measure a white WaterSurface geometry pass, without a water optics verdict."""
from pathlib import Path
import json,hashlib,sys
import numpy as np
from PIL import Image
source,native,log,output=map(Path,sys.argv[1:5])
assert len(sys.argv)==5 and not output.exists()
a=np.asarray(Image.open(source).convert('RGB')).astype(np.int16)
b=np.asarray(Image.open(native).convert('RGB')).astype(np.int16)
assert a.shape==b.shape==(1080,1920,3)
meta=json.loads(Path(str(source)+'.json').read_text())
assert meta['water_geometry']['output']=='white_geometry_no_water_optics'
lines=[line.split('native ocean selection ',1)[1] for line in log.read_text().splitlines() if 'native ocean selection ' in line]
assert len(lines)==1 and json.loads(lines[0])==meta['water_geometry']['nodes'], 'ordered ocean nodes differ'
am=(a==255).all(axis=2);bm=(b==255).all(axis=2)
union=am|bm;intersection=am&bm
assert union.sum()>1000
d=np.abs(a-b)
result=dict(status='measured_white_surface_not_optical_water_parity',
    reference_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),native_sha256=hashlib.sha256(native.read_bytes()).hexdigest(),
    ordered_nodes_exact=len(meta['water_geometry']['nodes']),source_white_pixels=int(am.sum()),native_white_pixels=int(bm.sum()),
    white_mask_iou=float(intersection.sum()/union.sum()),white_mask_disagreement=int((am^bm).sum()),
    mean_rgb_error_8bit=float(d.mean()),p99_max_channel_error=float(np.percentile(d.max(axis=2),99)),max_channel_error=int(d.max()),
    limitations=['white_pixel_classification_not_object_id','static_camera_and_time','no_shore_wake_or_optical_water_material'])
output.write_text(json.dumps(result,indent=2))
print(json.dumps(result))
