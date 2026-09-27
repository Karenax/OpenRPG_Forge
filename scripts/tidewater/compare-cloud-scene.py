"""Measure the visible cloud contribution separately from existing scene error."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image,ImageFilter
assert len(sys.argv)==8
source_clear,source,native_clear,native,source_repeat,native_repeat,output=map(Path,sys.argv[1:])
assert not output.exists()
meta=json.loads(Path(str(source)+'.json').read_text())
with_water=bool(meta.get('water_geometry'))
with_shadows=bool(meta.get('cloud_shadow_receivers'))
assert meta['cloud_sky']['mode']=='original_renderer_acquired_cloud_history'
read=lambda path:np.asarray(Image.open(path).convert('RGB')).astype('int16')
a,b,c,d,e,f=map(read,[source_clear,source,native_clear,native,source_repeat,native_repeat])
assert a.shape==b.shape==c.shape==d.shape==e.shape==f.shape==(1080,1920,3)
source_repeat_error=np.abs(b-e)
source_effect=np.max(np.abs(a-b),axis=2)>3
native_effect=np.max(np.abs(c-d),axis=2)>3
assert source_effect.any() and native_effect.any()
region=np.asarray(Image.fromarray(source_effect.astype('uint8')*255).filter(ImageFilter.MaxFilter(3)))>0
error=np.abs(b-d);repeat_error=np.abs(d-f)
report={'source':str(source),'native':str(native),'source_repeat_exact':bool(np.array_equal(b,e)),
    'source_repeat_different_pixels':int(np.any(source_repeat_error>0,axis=2).sum()),
    'source_repeat_max_channel':int(source_repeat_error.max()),
    'source_repeat_effect_region_max_channel':int(source_repeat_error[region].max()),
    'native_repeat_different_pixels':int(np.any(repeat_error>0,axis=2).sum()),
    'native_repeat_max_channel':int(repeat_error.max()),
    'source_effect_pixels_over_3':int(source_effect.sum()),'native_effect_pixels_over_3':int(native_effect.sum()),
    'effect_region_pixels':int(region.sum()),'effect_region_mean_channel':float(error[region].mean()),
    'effect_region_p99_max_channel':float(np.quantile(error[region].max(axis=1),.99)),
    'effect_region_max_channel':int(error[region].max()),'whole_scene_mean_channel':float(error.mean()),
    'sha256':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [source,native]},
    'with_water':with_water,'cloud_shadow_receivers':with_shadows,
    'limits':['fixed camera pose at 2 seconds','sky and water reflection' if with_water else 'sky composition only',
              'existing scene errors included; native atmosphere/cloud graph with acquired frame parameters',
              'fixed cloud-shadow receivers; no final scene parity' if with_shadows else 'no cloud-shadow receivers or final scene parity']}
if with_water:
    # Diagnostic screen band, not a segmentation mask: it includes piers/rocks.
    band=error[360:620]
    report['water_screen_band']={'bounds':[0,360,1920,620],'mean_channel':float(band.mean()),
        'p99_max_channel':float(np.quantile(band.max(axis=2),.99)),'max_channel':int(band.max())}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
