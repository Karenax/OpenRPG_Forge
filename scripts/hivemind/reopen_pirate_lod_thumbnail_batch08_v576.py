"""Reopen exact native LOD meshes and source images before visual admission."""
from pathlib import Path
parent=Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch07_v567.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch07-v565','pirate-lod-thumbnail-batch08-v574').replace('pirate-lod-thumbnail-batch07-review-v567','pirate-lod-thumbnail-batch08-review-v576').replace('pirate-lod-thumbnail-batch07-guard-v566','pirate-lod-thumbnail-batch08-guard-v575')
exec(compile(code,str(parent)+'#lot08-exact-lod-reopen','exec'),globals())
