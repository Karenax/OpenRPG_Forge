"""Install only the 22 visually reviewed source cards via the skill transaction."""
from pathlib import Path

parent = Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code = parent.read_text('utf-8')
for before, after in {
    'pirate-lod-thumbnail-batch03-index-v372': 'pirate-upper-lod-images-index-v955',
    'pirate-lod-batch03-combined-review-v371': 'pirate-upper-lod-images-reviewed-v954',
    '0<count<=16': 'count==22',
    '[Path(__file__).resolve(),HELPER,REVIEW]': '[parent,Path(__file__).resolve(),HELPER,REVIEW]',
}.items():
    assert before in code
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#upper-source-lod-images', 'exec'), globals())
