"""Observe punctual atlas depth and six actual live HDRP raster requests."""
from pathlib import Path
base=Path(__file__).with_name('queue_firepit_source_shadow_raster_v6.py')
source=base.read_text('utf-8').replace('SourceShadowRasterV6','SourcePointShadowRasterV1').replace('firepit-source-shadow-raster-v6','firepit-source-point-shadow-raster-v1')
source=source.replace('"m_CascadeAtlas"','"m_Atlas"').replace('"_CascadeShadowAtlas"','"_ShadowmapAtlas"')
source=source.replace('atlas_width=1024,atlas_height=1024','atlas_width=4096,atlas_height=4096')
source=source.replace('source_cascade_atlas_depth_gpu_pixels_and_live_raster_requests_captured','source_punctual_atlas_depth_gpu_pixels_and_live_raster_requests_captured').replace('source_cascade_raster_capture_refused','source_punctual_raster_capture_refused')
exec(compile(source,str(base),'exec'),globals())
