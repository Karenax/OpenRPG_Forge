"""Preserve and repair the exact cp1252 dashboard written by the v26 comparator."""
from pathlib import Path
import json
p=Path('F:/OpenRPG_ImportWork/SUIVI_LOTS_HIVEMIND/SUIVI_SIMPLE.v1.json')
raw=p.read_bytes()
backup=p.with_name('SUIVI_SIMPLE.before-utf8-repair-v1.json.saved')
assert not backup.exists()
text=raw.decode('cp1252')
assert text.encode('cp1252')==raw
data=json.loads(text)
assert data['last_technical_batch']=='L03.TOWNSMITH.MATERIAUX.UV.COMPARAISON'
backup.write_bytes(raw)
tmp=p.with_suffix('.encoding.tmp')
tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
tmp.replace(p)
print('Dashboard repaired to UTF-8; exact prior bytes preserved.')
