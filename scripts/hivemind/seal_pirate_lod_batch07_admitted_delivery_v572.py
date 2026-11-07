"""Deliver ten lot07 thumbnails with current-reader proof and six explicit material refusals."""
from pathlib import Path
parent=Path(__file__).with_name('seal_pirate_lod_batch06_admitted_delivery_v522.py')
code=parent.read_text('utf-8')
for old,new in [
 ('pirate-lod-thumbnail-batch06-admitted-delivery-v522','pirate-lod-thumbnail-batch07-admitted-delivery-v572'),
 ('pirate-lod-thumbnail-batch06-admitted-index-v520','pirate-lod-thumbnail-batch07-admitted-index-v570'),
 ('pirate-lod-thumbnail-batch06-admitted-reader-v521','pirate-lod-thumbnail-batch07-admitted-reader-v571'),
 ('pirate-lod-batch06-admitted-review-v519','pirate-lod-batch07-admitted-review-v569'),
 ('before==648 and before-after==10 and after==638','before==638 and before-after==10 and after==628'),
 ('648 -> 638','638 -> 628'),
 ('Lot images Pirate LOD 06 partiellement livre - v513 a v522','Lot images Pirate LOD 07 partiellement livre - v565 a v572'),
 ('Six variantes de trois palmiers','Six variantes RockStoneFoliage et ShackFront'),
]:
    assert old in code,old;code=code.replace(old,new)
exec(compile(code,str(parent)+'#lot07-final-current-reader-delivery','exec'),globals())
