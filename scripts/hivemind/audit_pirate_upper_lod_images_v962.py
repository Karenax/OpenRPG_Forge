"""Keep the same guarded source admission for the second upper-LOD lot."""
from pathlib import Path

recipe_path = Path(__file__).with_name('audit_pirate_upper_lod_images_v952.py')
code = recipe_path.read_text('utf-8')
for before, after in {
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v961',
    'pirate-upper-lod-frontier-v950': 'pirate-upper-lod-frontier-v960',
    'pirate-upper-lod-images-guard-v952': 'pirate-upper-lod-images-guard-v962',
    '[parent,frontier_path,ownership_path]': '[parent,recipe_path,frontier_path,ownership_path]',
}.items():
    assert before in code
    code = code.replace(before, after)
exec(compile(code, str(recipe_path)+'#second-upper-source-lod-guard', 'exec'), globals())
