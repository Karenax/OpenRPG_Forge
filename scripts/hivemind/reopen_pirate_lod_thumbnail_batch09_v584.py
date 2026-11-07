"""Reopen requested native LOD identities and PNGs before visual review."""
from pathlib import Path
parent=Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch08_v576.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch08-v574','pirate-lod-thumbnail-batch09-v582').replace('pirate-lod-thumbnail-batch08-review-v576','pirate-lod-thumbnail-batch09-review-v584').replace('pirate-lod-thumbnail-batch08-guard-v575','pirate-lod-thumbnail-batch09-guard-v583')
exec(compile(code,str(parent)+'#lot09-exact-lod-reopen','exec'),globals())
