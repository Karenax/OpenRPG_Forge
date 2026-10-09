"""Use a fresh protocol on the corrected host; retain all previous captures."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_forge_shadow_pair_v1.py')
source=BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-forge-shadow-pair-v4').replace('firepit-forge-shadow-pair-comparison-v1','firepit-forge-shadow-pair-comparison-v2')
exec(compile(source,str(BASE),'exec'),globals())
