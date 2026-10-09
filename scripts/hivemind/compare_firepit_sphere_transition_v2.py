"""Use original tolerances for spherical selector/border formula control."""
from pathlib import Path
base=Path(__file__).with_name('compare_firepit_shadow_oriented_images_v1.py')
source=base.read_text('utf-8').replace('firepit-source-shadow-oriented-native-v1','firepit-source-sphere-transition-native-v2').replace('firepit-source-shadow-oriented-image-comparison-v1','firepit-source-sphere-transition-image-comparison-v2').replace('firepit-source-shadow-oriented-case-v1','firepit-source-sphere-transition-case-v2')
exec(compile(source,str(base),'exec'),globals())
