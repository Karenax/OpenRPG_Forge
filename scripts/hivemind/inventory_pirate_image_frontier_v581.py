"""Refresh the uncaptured image frontier after the proven lot08 transaction."""
from pathlib import Path
parent=Path(__file__).with_name('inventory_pirate_image_frontier_v573.py')
code=parent.read_text('utf-8').replace('pirate-image-frontier-v573','pirate-image-frontier-v581')
exec(compile(code,str(parent)+'#post-lot08-frontier','exec'),globals())
