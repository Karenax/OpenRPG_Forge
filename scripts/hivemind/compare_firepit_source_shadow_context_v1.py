"""Compare explicit source bounds/resolution replay using original thresholds."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_forge_shadow_pair_v1.py')
source=BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-source-shadow-context-native-v1')
source=source.replace('firepit-forge-shadow-pair-comparison-v1','firepit-source-shadow-context-comparison-v1')
source=source.replace('firepit-shadow-comparison-case-v1/CASE.v1.json','firepit-source-shadow-context-case-v1/CASE.v1.json')
source=source.replace('source_shadow_settings_matched=False,full_shadow_fidelity_qualified=False',
    'source_shadow_settings_matched=False,source_shadow_bounds_and_resolution_replayed=True,source_shadow_filter_matched=False,source_cascade_borders_matched=False,source_ssao_algorithm_matched=False,full_shadow_fidelity_qualified=False')
exec(compile(source,str(BASE),'exec'),globals())
