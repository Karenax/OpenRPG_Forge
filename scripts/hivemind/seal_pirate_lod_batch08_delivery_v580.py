"""Seal the complete image lot only after transaction and current native-reader checks."""
from pathlib import Path
parent=Path(__file__).with_name('seal_pirate_lod_thumbnail_batch03_delivery_v374.py')
code=parent.read_text('utf-8')
for old,new in [
 ('pirate-lod-thumbnail-batch03-delivery-v374','pirate-lod-thumbnail-batch08-delivery-v580'),
 ('pirate-lod-thumbnail-batch03-index-v372','pirate-lod-thumbnail-batch08-index-v578'),
 ('pirate-lod-thumbnail-batch03-native-reader-v373','pirate-lod-thumbnail-batch08-native-reader-v579'),
 ('pirate-lod-batch03-combined-review-v371','pirate-lod-batch08-combined-review-v577'),
 ('before-after==16 and after==680','before==628 and before-after==16 and after==612'),
 ('Lot images Pirate LOD 03 livre - v368 a v374','Lot images Pirate LOD 08 livre - v573 a v580'),
 ('696 -> 680','628 -> 612'),
]:
    assert old in code,old;code=code.replace(old,new)
code=code.replace('for p in [Path(__file__),ip,rp,vp,pointer','for p in [parent,Path(__file__),ip,rp,vp,pointer')
exec(compile(code,str(parent)+'#lot08-controlled-delivery','exec'),globals())
