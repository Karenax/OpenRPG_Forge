"""Reopen native LOD identities and PNGs before individual visual review."""
from pathlib import Path
parent=Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch03_v370.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-v368','pirate-lod-thumbnail-batch-next-v791').replace('pirate-lod-thumbnail-batch03-guard-v369','pirate-lod-thumbnail-next-guard-v793').replace('pirate-lod-thumbnail-batch03-review-v370','pirate-lod-thumbnail-next-review-v794')
exec(compile(code,str(parent)+'#next-lod-native-image-reopen','exec'),globals())
