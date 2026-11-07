"""Apply the independent pixel checks to the successful declared-contract GPU run."""
from pathlib import Path
import json
import reopen_firepit_lod_gpu_v221 as review

review.I=review.W/'firepit-contract-lod-gpu-v224'
review.O=review.W/'firepit-contract-lod-gpu-reopen-v226'
review.main()
p=review.O/'RESULT.v1.json'
receipt=review.read(p)
receipt['input_pins'][str(Path(__file__))]=review.sha(Path(__file__))
p.write_text(json.dumps(receipt,indent=2),'utf-8')
