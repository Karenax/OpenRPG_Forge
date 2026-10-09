"""Keep original thresholds for point world-depth-offset causal control."""
from pathlib import Path
base=Path(__file__).with_name('compare_firepit_shadow_oriented_images_v1.py')
source=base.read_text('utf-8').replace('firepit-source-shadow-oriented-native-v1','firepit-point-world-bias-control-native-v1').replace('firepit-source-shadow-oriented-image-comparison-v1','firepit-point-world-bias-control-image-comparison-v1').replace('firepit-source-shadow-oriented-case-v1','firepit-point-world-bias-control-case-v1')
exec(compile(source,str(base),'exec'),globals())
