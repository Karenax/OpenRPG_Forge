"""Use exactly the previously fixed scene thresholds and Unity image references."""
from pathlib import Path
BASE_SCENE=Path(__file__).with_name('compare_firepit_forge_shadow_pair_v1.py')
source=BASE_SCENE.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-specular-scene-v1').replace('firepit-forge-shadow-pair-comparison-v1','firepit-specular-scene-comparison-v1').replace('firepit-shadow-comparison-case-v1/CASE.v1.json','firepit-specular-scene-case-v1/CASE.v1.json')
source=source.replace('source_shadow_settings_matched=False,full_shadow_fidelity_qualified=False',
    'source_shadow_settings_matched=False,source_ssao_algorithm_matched=False,measured_diffuse_on_object_and_receiver=True,measured_specular_on_object_and_receiver=True,full_shadow_fidelity_qualified=False')
exec(compile(source,str(BASE_SCENE),'exec'),globals())
