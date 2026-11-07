"""Seal only reviewed, transactionally indexed and product-reader-verified images."""
from pathlib import Path
parent = Path(__file__).with_name('seal_pirate_lod_thumbnail_batch03_delivery_v374.py')
code = parent.read_text('utf-8')
for old, new in [
    ('pirate-lod-thumbnail-batch03-delivery-v374', 'pirate-lod-thumbnail-batch04-delivery-v501'),
    ('pirate-lod-thumbnail-batch03-index-v372', 'pirate-lod-thumbnail-batch04-index-v498'),
    ('pirate-lod-thumbnail-batch03-native-reader-v373', 'pirate-lod-thumbnail-batch04-native-reader-v499'),
    ('pirate-lod-batch03-combined-review-v371', 'pirate-lod-batch04-combined-review-v497'),
    ('after==680', 'after==664'),
    ('Lot images Pirate LOD 03 livre - v368 a v374', 'Lot images Pirate LOD 04 livre - v491 a v501'),
    ('696 -> 680', '680 -> 664'),
]:
    assert old in code, old
    code = code.replace(old, new)
code = code.replace('for p in [Path(__file__),ip,rp,vp,pointer', 'for p in [parent,Path(__file__),ip,rp,vp,pointer')
exec(compile(code, str(parent) + '#sixteen-new-lod-images-delivery', 'exec'), globals())
