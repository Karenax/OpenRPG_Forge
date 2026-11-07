"""Refresh the source image frontier without rendering historically refused materials."""
from pathlib import Path
import hashlib
import json

parent = Path(__file__).with_name('inventory_pirate_image_frontier_v573.py')
latest_guard = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-next-guard-v793/RESULT.v1.json')
captured = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch-next-v791/PREPARED.v1.json')
guard = json.loads(latest_guard.read_text('utf-8-sig'))
assert guard['admitted_ids'] == [], 'Reassess frontier if material admission changed'
code = parent.read_text('utf-8').replace('pirate-image-frontier-v573', 'pirate-image-frontier-v815')
code = code.replace("pins={str(p):sha(p) for p in [Path(__file__),pointer,catalogpath,queuepath,guardpath]}",
                    "pins={str(p):sha(p) for p in [Path(__file__),parent,latest_guard,captured,pointer,catalogpath,queuepath,guardpath]}")
code = code.replace("'pirate-lod-thumbnail-batch07-v565']:",
                    "'pirate-lod-thumbnail-batch07-v565','pirate-lod-thumbnail-batch-next-v791']:")
# Keep all retained refusal GUIDs, not just the four encountered by the last lot.
exec(compile(code, str(parent)+'#current-source-frontier', 'exec'), globals())
assert all(sha(Path(p)) == digest for p, digest in pins.items()), 'Frontier inputs changed'
