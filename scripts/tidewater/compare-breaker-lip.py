"""Compare the visible lip using paired source/native captures with and without it."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image,ImageFilter
assert len(sys.argv)==6
before,reference,native_before,candidate,output=map(Path,sys.argv[1:])
assert not output.exists()
read=lambda p:np.array(Image.open(p).convert('RGB')).astype('int16')
a,b,c,d=map(read,[before,reference,native_before,candidate]);assert a.shape==b.shape==c.shape==d.shape==(1080,1920,3)
source_mask=np.any(a!=b,axis=-1);native_mask=np.any(c!=d,axis=-1)
assert source_mask.any() and native_mask.any()
region=np.array(Image.fromarray(source_mask.astype('uint8')*255).filter(ImageFilter.MaxFilter(3)))>0
error=np.abs(b-d);effect=error[region]
report={'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [before,reference,native_before,candidate]},'source_changed_pixels':int(source_mask.sum()),'native_changed_pixels':int(native_mask.sum()),'intersection_pixels':int((source_mask&native_mask).sum()),'union_pixels':int((source_mask|native_mask).sum()),'effect_region_pixels':int(region.sum()),'effect_region':{'mean_channel':float(effect.mean()),'p99_max_channel':float(np.quantile(effect.max(axis=1),.99)),'max_channel':int(effect.max())},'whole_image_mean_channel':float(error.mean()),'limits':['fixed seaward camera at 2 seconds','nonzero source effect mask dilated one pixel','colour errors include baseline differences','not temporal or full-scene parity']}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
