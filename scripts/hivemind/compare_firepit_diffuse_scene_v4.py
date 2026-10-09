"""Compare the original scene after measured diffuse, retaining old thresholds."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_forge_shadow_pair_v1.py')
source=BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-diffuse-scene-v4').replace('firepit-forge-shadow-pair-comparison-v1','firepit-diffuse-scene-comparison-v4')
source=source.replace("W/'firepit-shadow-comparison-case-v1/CASE.v1.json'","W/'firepit-diffuse-scene-case-v1/CASE.v1.json'")
source=source.replace('source_shadow_settings_matched=False,full_shadow_fidelity_qualified=False',
    'source_shadow_settings_matched=False,source_ssao_algorithm_matched=False,measured_diffuse_on_object_and_receiver=True,full_shadow_fidelity_qualified=False')
source=source.replace('Resolve shadow map/bias/filter and presentation/color discrepancies with source-native state evidence',
    'Resolve measured residuals: source versus native SSAO, HDRI specular, direct BRDF and shadow map/bias/filter; retain the original comparison thresholds')
exec(compile(source,str(BASE),'exec'),globals())
