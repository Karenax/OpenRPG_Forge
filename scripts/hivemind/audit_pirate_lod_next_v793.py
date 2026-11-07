"""Apply the existing source/material fidelity guards to the next capture lot."""
from pathlib import Path
parent=Path(__file__).with_name('audit_and_guard_pirate_lod_batch04_v493.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch04-v491','pirate-lod-thumbnail-batch-next-v791').replace('pirate-lod-thumbnail-batch04-guard-v493','pirate-lod-thumbnail-next-guard-v793')
exec(compile(code,str(parent)+'#next-lod-source-guard','exec'),globals())
