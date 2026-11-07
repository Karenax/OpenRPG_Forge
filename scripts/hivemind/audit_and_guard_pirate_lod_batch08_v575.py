"""Keep native source/material checks unchanged for the frontier-routed image lot."""
from pathlib import Path
parent=Path(__file__).with_name('audit_and_guard_pirate_lod_batch07_v566.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch07-v565','pirate-lod-thumbnail-batch08-v574').replace('pirate-lod-thumbnail-batch07-guard-v566','pirate-lod-thumbnail-batch08-guard-v575')
exec(compile(code,str(parent)+'#lot08-source-guard','exec'),globals())
