"""Transactionally index only the ten actually reviewed, guard-admitted variants."""
from pathlib import Path
parent=Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-index-v372','pirate-lod-thumbnail-batch06-admitted-index-v520').replace('pirate-lod-batch03-combined-review-v371','pirate-lod-batch06-admitted-review-v519')
code=code.replace('[Path(__file__).resolve(),HELPER,REVIEW]','[parent,Path(__file__).resolve(),HELPER,REVIEW]')
exec(compile(code,str(parent)+'#ten-admitted-variants','exec'),globals())
