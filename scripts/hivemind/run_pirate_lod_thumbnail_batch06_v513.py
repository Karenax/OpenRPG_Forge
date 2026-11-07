"""Select the next missing source variants without repeating delivered lots."""
from pathlib import Path
recipe = Path(__file__).with_name('run_pirate_lod_thumbnail_batch05_v502.py')
code = recipe.read_text('utf-8').replace('pirate-lod-thumbnail-batch05-v502', 'pirate-lod-thumbnail-batch06-v513').replace('L08.IMAGE.PIRATE.LOD.05', 'L08.IMAGE.PIRATE.LOD.06')
code = code.replace('parent,audit_path,Path(__file__)', 'parent,recipe,audit_path,Path(__file__)')
exec(compile(code, str(recipe)+'#next-missing-source-images', 'exec'), globals())
