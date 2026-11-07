"""Capture the next sixteen variants selected by the refreshed material frontier."""
from pathlib import Path
parent=Path(__file__).with_name('run_pirate_lod_thumbnail_batch08_v574.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch08-v574','pirate-lod-thumbnail-batch09-v582').replace('L08.IMAGE.PIRATE.LOD.08','L08.IMAGE.PIRATE.LOD.09').replace('pirate-image-frontier-v573','pirate-image-frontier-v581')
exec(compile(code,str(parent)+'#lot09-refreshed-frontier','exec'),globals())
