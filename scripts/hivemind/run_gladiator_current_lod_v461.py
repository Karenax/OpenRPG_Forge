"""Resume a terminal interrupted build, preserving its original evidence directory."""
from pathlib import Path
parent=Path(__file__).with_name('run_gladiator_current_lod_v460.py')
code=parent.read_text('utf-8').replace('gladiator-current-lod-v460','gladiator-current-lod-v461')
exec(compile(code,str(parent)+'#resume-v461','exec'),globals())
