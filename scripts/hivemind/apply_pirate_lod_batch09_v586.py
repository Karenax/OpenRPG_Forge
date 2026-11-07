"""Index the sixteen reviewed source variants through the immutable skill transaction."""
from pathlib import Path
parent=Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-index-v372','pirate-lod-thumbnail-batch09-index-v586').replace('pirate-lod-batch03-combined-review-v371','pirate-lod-batch09-combined-review-v585')
code=code.replace('[Path(__file__).resolve(),HELPER,REVIEW]','[parent,Path(__file__).resolve(),HELPER,REVIEW]')
exec(compile(code,str(parent)+'#lot09-immutable-thumbnail-transaction','exec'),globals())
