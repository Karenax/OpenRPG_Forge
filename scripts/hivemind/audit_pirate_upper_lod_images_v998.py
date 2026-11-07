"""Guard all 19 source candidates and their ten LOD0 controls."""
from pathlib import Path

recipe_path = Path(__file__).with_name('audit_pirate_upper_lod_images_v952.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v996',
    'pirate-upper-lod-frontier-v950': 'pirate-upper-lod-frontier-v995',
    'pirate-upper-lod-images-guard-v952': 'pirate-upper-lod-images-guard-v998',
    "assert len(prep['candidate_ids']) == 24": "assert len(prep['candidate_ids']) == 19",
    "==len(prep['cases'])==39": "==len(prep['cases'])==29",
    '[parent,frontier_path,ownership_path]': '[parent,recipe_path,frontier_path,ownership_path]',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#fourth-lot-source-guard', 'exec'), globals())
