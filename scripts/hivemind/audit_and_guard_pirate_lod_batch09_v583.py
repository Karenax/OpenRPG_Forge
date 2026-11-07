"""Retain all source/material checks for the lot09 native captures."""
from pathlib import Path
parent=Path(__file__).with_name('audit_and_guard_pirate_lod_batch08_v575.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch08-v574','pirate-lod-thumbnail-batch09-v582').replace('pirate-lod-thumbnail-batch08-guard-v575','pirate-lod-thumbnail-batch09-guard-v583')
exec(compile(code,str(parent)+'#lot09-source-material-checks','exec'),globals())
