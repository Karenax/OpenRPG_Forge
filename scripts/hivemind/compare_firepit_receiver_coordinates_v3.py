"""Original predeclared tolerances for source coordinate control."""
from pathlib import Path
base=Path(__file__).with_name('compare_firepit_shadow_oriented_images_v1.py')
source=base.read_text('utf-8').replace('firepit-source-shadow-oriented-native-v1','firepit-source-receiver-coordinates-native-v3').replace('firepit-source-shadow-oriented-image-comparison-v1','firepit-source-receiver-coordinates-image-comparison-v4').replace('firepit-source-shadow-oriented-case-v1','firepit-source-receiver-coordinates-case-v3')
exec(compile(source,str(base),'exec'),globals())
