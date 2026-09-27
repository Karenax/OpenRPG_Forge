"""Measure the native shore effect where the independent source changes visibly."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image,ImageFilter
before,reference,candidate,output=map(Path,sys.argv[1:5])
assert len(sys.argv)==5 and not output.exists()
read=lambda p:np.array(Image.open(p).convert('RGB')).astype('int16')
a,b,c=map(read,[before,reference,candidate]);assert a.shape==b.shape==c.shape==(1080,1920,3)
changed=np.max(np.abs(a-b),axis=2)>3
mask=np.array(Image.fromarray(changed.astype('uint8')*255).filter(ImageFilter.MaxFilter(3)))>0
assert changed.sum()>1000
d=np.abs(b-c);effect=d[mask]
report=dict(before=str(before),reference=str(reference),candidate=str(candidate),
 sha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [before,reference,candidate]},
 visibly_changed_source_pixels=int(changed.sum()),effect_region_pixels=int(mask.sum()),
 effect_region=dict(mean_channel=float(effect.mean()),p99_max_channel=float(np.quantile(effect.max(axis=1),.99)),max_channel=int(effect.max())),
 whole_image_mean_channel=float(d.mean()),
 limitations=['single_time_two_seconds','source_change_mask_threshold_3_dilated_one_pixel','8bit_srgb','not_temporal_or_full_scene_parity'])
output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
