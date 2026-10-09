"""Repair Rust numeric literals and use the SDK's global ambient resource."""
from pathlib import Path
BASE_GENERATOR=Path(__file__).with_name('capture_firepit_forge_shadow_pair_v1.py')
source=BASE_GENERATOR.read_text('utf-8').replace('firepit-forge-shadow-pair-v1','firepit-forge-shadow-pair-v2')
needle='def main():\n'
assert source.count(needle)==1
source=source.replace(needle,"def main():\n global PROBE\n import re\n PROBE=re.sub(r'(?<![\\w])\\.(?=\\d)','0.',PROBE).replace('insert_resource(AmbientLight{','insert_resource(bevy::light::GlobalAmbientLight{')\n")
source=source.replace('extra=[BASE,previous.BASE,','extra=[BASE_GENERATOR,BASE,previous.BASE,')
exec(compile(source,str(BASE_GENERATOR),'exec'),globals())
