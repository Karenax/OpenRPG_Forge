"""Compare water-pass scene inputs on an independently rendered water mask."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image,ImageFilter

reference,candidate,mask_file,output=map(Path,sys.argv[1:5])
assert len(sys.argv)==5 and not output.exists()
read=lambda p:np.array(Image.open(p).convert('RGB'))
a,b,mask_rgb=map(read,[reference,candidate,mask_file])
assert a.shape==b.shape==mask_rgb.shape==(1080,1920,3)
mask=np.all(mask_rgb==255,axis=2)
# Exclude silhouette discontinuities from the input-texture assessment; also
# report the complete image, so the edge exclusion cannot hide regressions.
interior=np.array(Image.fromarray(mask.astype('uint8')*255).filter(ImageFilter.MinFilter(3)))==255
assert int(interior.sum())>1000
d=np.abs(a.astype('int16')-b.astype('int16'))
def stats(x):
 return dict(pixels=int(x.size//3),mean_channel=float(x.mean()),p99_max_channel=float(np.quantile(x.max(axis=-1),.99)),max_channel=int(x.max()))
hash_file=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report=dict(reference=str(reference),candidate=str(candidate),mask=str(mask_file),
 sha256={p.name:hash_file(p) for p in [reference,candidate,mask_file]},whole_image=stats(d),water_interior=stats(d[interior]),
 limitations=['single_fixed_camera','8bit_srgb_capture_not_raw_depth_precision','white_color_mask_not_object_id','no_full_optical_parity_verdict'])
output.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
