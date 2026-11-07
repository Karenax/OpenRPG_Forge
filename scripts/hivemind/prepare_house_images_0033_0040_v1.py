"""Start the next eight missing House images, preserving existing deliveries."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
out=S/'queue_house_thumbnail_batch_0033_0040_v1.py'
assert not out.exists()
q=(S/'queue_house_thumbnail_batch_0025_0032_v1.py').read_text(encoding='utf-8')
q=q.replace('0025-0032','0033-0040').replace('L08.IMAGE.HOUSE.05','L08.IMAGE.HOUSE.06')
compile(q,str(out),'exec')
out.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(out)],check=True)
