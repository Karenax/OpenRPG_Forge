"""Full scene sampling control; preserve original thresholds and limitations."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_forge_shadow_pair_v1.py')
source=BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-scene-common-sampling-v1')
source=source.replace('firepit-forge-shadow-pair-comparison-v1','firepit-scene-common-sampling-comparison-v1')
source=source.replace('firepit-shadow-comparison-case-v1/CASE.v1.json','firepit-source-scene-instance-fix-case-v1/CASE.v1.json')
source=source.replace('source_shadow_settings_matched=False,full_shadow_fidelity_qualified=False',
    'source_shadow_settings_matched=False,source_ssao_algorithm_matched=False,explicit_common_scene_sampling=True,full_shadow_fidelity_qualified=False')
exec(compile(source,str(BASE),'exec'),globals())
