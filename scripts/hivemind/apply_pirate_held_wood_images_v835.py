"""Index the four reviewed images with unchanged sources, licences and readiness."""
from pathlib import Path
parent = Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-index-v372', 'pirate-held-wood-images-index-v835').replace('pirate-lod-batch03-combined-review-v371', 'pirate-held-wood-images-seal-v834').replace('0<count<=16', '0<count<=4')
code = code.replace('[Path(__file__).resolve(),HELPER,REVIEW]', '[parent,Path(__file__).resolve(),HELPER,REVIEW]')
exec(compile(code, str(parent)+'#four-scoped-wood-source-thumbnails', 'exec'), globals())
