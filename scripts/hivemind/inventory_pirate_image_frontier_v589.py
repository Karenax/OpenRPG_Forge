"""Refresh the remaining LOD material work after verified lot09 delivery."""
from pathlib import Path
parent=Path(__file__).with_name('inventory_pirate_image_frontier_v573.py')
code=parent.read_text('utf-8').replace('pirate-image-frontier-v573','pirate-image-frontier-v589')
exec(compile(code,str(parent)+'#post-lot09-frontier','exec'),globals())
