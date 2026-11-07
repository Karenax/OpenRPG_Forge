"""Guard the third lot including its thirteen source LOD0 controls."""
from pathlib import Path

recipe_path = Path(__file__).with_name('audit_pirate_upper_lod_images_v952.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v983',
    'pirate-upper-lod-frontier-v950': 'pirate-upper-lod-frontier-v982',
    'pirate-upper-lod-images-guard-v952': 'pirate-upper-lod-images-guard-v985',
    "==len(prep['cases'])==39": "==len(prep['cases'])==37",
    '[parent,frontier_path,ownership_path]': '[parent,recipe_path,frontier_path,ownership_path]',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#third-source-lod-guard', 'exec'), globals())
