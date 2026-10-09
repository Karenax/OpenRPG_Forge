"""Original full scene thresholds and references, now with its prefab light."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_forge_shadow_pair_v1.py')
source=BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-punctual-scene-v2')
source=source.replace('firepit-forge-shadow-pair-comparison-v1','firepit-punctual-scene-comparison-v2')
source=source.replace('firepit-shadow-comparison-case-v1/CASE.v1.json','firepit-punctual-scene-case-v1/CASE.v1.json')
source=source.replace('source_shadow_settings_matched=False,full_shadow_fidelity_qualified=False',
    'source_shadow_settings_matched=False,source_ssao_algorithm_matched=False,explicit_common_scene_sampling=True,source_prefab_point_light_transport=True,full_shadow_fidelity_qualified=False')
exec(compile(source,str(BASE),'exec'),globals())
