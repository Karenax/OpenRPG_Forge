"""Reuse the proven data-only capture recipe for the next eight missing cards."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
source = S / 'queue_house_thumbnail_batch_0009_0016_v1.py'
target = S / 'queue_house_thumbnail_batch_0017_0024_v1.py'
assert not target.exists()
text = source.read_text(encoding='utf-8')
assert text.count('house-source-thumbnail-batch-0009-0016-v1') == 1
text = text.replace('house-source-thumbnail-batch-0009-0016-v1', 'house-source-thumbnail-batch-0017-0024-v1')
text = text.replace('L08.IMAGE.HOUSE.03', 'L08.IMAGE.HOUSE.04')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
