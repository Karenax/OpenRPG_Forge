"""Reopen exact LODs and PNGs before the lot07 agent visual review."""
from pathlib import Path
parent=Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch05_v507.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch05-v502','pirate-lod-thumbnail-batch07-v565').replace('pirate-lod-thumbnail-batch05-review-v507','pirate-lod-thumbnail-batch07-review-v567').replace('pirate-lod-thumbnail-batch05-guard-v506','pirate-lod-thumbnail-batch07-guard-v566')
exec(compile(code,str(parent)+'#lot07-exact-lod-reopen','exec'),globals())
