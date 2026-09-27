"""Measure sprites plus their shore feedback without asserting visual parity."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image,ImageFilter
assert len(sys.argv)==6
before,reference,native_before,candidate,output=map(Path,sys.argv[1:])
assert not output.exists()
meta=json.loads(Path(str(reference)+'.json').read_text())
probe=json.loads(candidate.with_suffix('.spray').joinpath('probe.json').read_text())
assert meta['spray']['steps']==120 and meta['spray']['global_wind']=={'speed':6,'direction':[1,0]}
assert probe['shore_coupled'] is True and probe['source_particle_results_uploaded'] is False
assert probe['source_fft_textures_uploaded'] is False and probe['source_shore_state_uploaded'] is False
read=lambda p:np.asarray(Image.open(p).convert('RGB')).astype('int16')
a,b,c,d=map(read,[before,reference,native_before,candidate]);assert a.shape==b.shape==c.shape==d.shape==(1080,1920,3)
source_mask=np.max(np.abs(a-b),axis=2)>3;native_mask=np.max(np.abs(c-d),axis=2)>3
assert source_mask.any() and native_mask.any()
region=np.asarray(Image.fromarray(source_mask.astype('uint8')*255).filter(ImageFilter.MaxFilter(3)))>0
error=np.abs(b-d);effect=error[region]
effect_error=np.abs((b-a).astype('int32')-(d-c).astype('int32'))
report={'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [before,reference,native_before,candidate]},'source_effect_pixels_over_3':int(source_mask.sum()),'native_effect_pixels_over_3':int(native_mask.sum()),'intersection_pixels':int((source_mask&native_mask).sum()),'union_pixels':int((source_mask|native_mask).sum()),'effect_region_pixels':int(region.sum()),'effect_region':{'mean_channel':float(effect.mean()),'p99_max_channel':float(np.quantile(effect.max(axis=1),.99)),'max_channel':int(effect.max()),'effect_delta_mean_channel':float(effect_error[region].mean())},'whole_image_mean_channel':float(error.mean()),'limits':['fixed seaward camera at 2 seconds','source effect above 3 levels dilated one pixel','sprites and coupled foam measured together','colour errors include baseline differences','source emission budget replayed','not temporal or full-scene parity']}
if probe.get('native_budget_feedback',False):report['limits'][4]='native adaptive budget; controlled one-step GPU readback latency'
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
