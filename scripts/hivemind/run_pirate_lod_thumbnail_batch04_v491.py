"""Capture the next eight missing LOD thumbnail groups without repeating indexed cards."""
from pathlib import Path
parent = Path(__file__).with_name('run_pirate_lod_thumbnail_batch03_v368.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-v368', 'pirate-lod-thumbnail-batch04-v491').replace('L08.IMAGE.PIRATE.LOD.03', 'L08.IMAGE.PIRATE.LOD.04')
code = code.replace("for p in [Path(__file__),base", "for p in [parent,Path(__file__),base")
exec(compile(code, str(parent) + '#next-missing-lod-images', 'exec'), globals())
