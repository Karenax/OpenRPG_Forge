"""Seal reviewed batch05 images only after transactional and product-reader checks."""
from pathlib import Path
parent = Path(__file__).with_name('seal_pirate_lod_thumbnail_batch03_delivery_v374.py')
code = parent.read_text('utf-8')
for old, new in [
    ('pirate-lod-thumbnail-batch03-delivery-v374', 'pirate-lod-thumbnail-batch05-delivery-v511'),
    ('pirate-lod-thumbnail-batch03-index-v372', 'pirate-lod-thumbnail-batch05-index-v509'),
    ('pirate-lod-thumbnail-batch03-native-reader-v373', 'pirate-lod-thumbnail-batch05-native-reader-v510'),
    ('pirate-lod-batch03-combined-review-v371', 'pirate-lod-batch05-combined-review-v508'),
    ('after==680', 'after==648'),
    ('Lot images Pirate LOD 03 livre - v368 a v374', 'Lot images Pirate LOD 05 livre - v502 a v511'),
    ('696 -> 680', '664 -> 648'),
]:
    assert old in code, old
    code = code.replace(old, new)
code = code.replace('for p in [Path(__file__),ip,rp,vp,pointer', 'for p in [parent,Path(__file__),ip,rp,vp,pointer')
exec(compile(code, str(parent) + '#batch05-controlled-delivery', 'exec'), globals())
