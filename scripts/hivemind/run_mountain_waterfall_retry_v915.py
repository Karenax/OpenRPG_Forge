"""Retry only the waterfall after the terminal missing-directory refusal."""
from pathlib import Path
recipe=Path(__file__).with_name('run_mountain_waterfall_typed_controls_v913.py')
code=recipe.read_text('utf-8').replace('mountain-waterfall-typed-controls-run-v913','mountain-waterfall-typed-controls-run-v915').replace('mountain-waterfall-typed-controls-v912/PREPARED.v1.json','mountain-waterfall-typed-controls-v914/PREPARED.v1.json')
exec(compile(code,str(recipe)+'#preserved-terminal-IO-refusal-retry','exec'),globals())
