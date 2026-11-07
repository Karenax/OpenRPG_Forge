"""Install the ten visually reviewed source thumbnails through the skill transaction."""
from pathlib import Path
parent=Path(__file__).with_name('apply_pirate_lod_batch06_admitted_v520.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch06-admitted-index-v520','pirate-lod-thumbnail-batch07-admitted-index-v570').replace('pirate-lod-batch06-admitted-review-v519','pirate-lod-batch07-admitted-review-v569')
exec(compile(code,str(parent)+'#lot07-thumbnail-transaction','exec'),globals())
