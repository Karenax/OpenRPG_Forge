"""Retain the positive EV8 shadow control; capture unclipped presentation at EV10."""
from pathlib import Path
BASE_V7=Path(__file__).with_name('queue_firepit_shadow_reference_v7.py')
source=BASE_V7.read_text('utf-8').replace('firepit-shadow-source-reference-v7','firepit-shadow-source-reference-v8').replace('SourceShadowReferenceV7','SourceShadowReferenceV8')
needle="exec(compile(source,str(BASE_V6),'exec'),globals())"
assert source.count(needle)==1
# Inject into the v6 generator's explicit helper changes, before its final scene cleanup change.
source=source.replace(needle,"source=source.replace(\" text=replace(text,'directions[view]*radius*4','directions[view]*radius*6')\",\" text=replace(text,'directions[view]*radius*4','directions[view]*radius*6')\\n text=replace(text,'presentation_fixed_ev=8','presentation_fixed_ev=10')\\n text=replace(text,'exposure.fixedExposure.Override(8)','exposure.fixedExposure.Override(10)')\")\n"+needle)
exec(compile(source,str(BASE_V7),'exec'),globals())
