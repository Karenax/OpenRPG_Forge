"""Compare selector-only replay against original predeclared tolerances."""
from pathlib import Path
base=Path(__file__).with_name('compare_firepit_shadow_oriented_images_v1.py')
source=base.read_text('utf-8').replace('firepit-source-shadow-oriented-native-v1','firepit-source-sphere-selection-native-v1').replace('firepit-source-shadow-oriented-image-comparison-v1','firepit-source-sphere-selection-image-comparison-v1').replace('firepit-source-shadow-oriented-case-v1','firepit-source-sphere-selection-case-v1')
exec(compile(source,str(base),'exec'),globals())
