"""Keep the same comparison thresholds for the source-attribute receiver."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_diffuse_scene_v4.py')
source=BASE.read_text('utf-8').replace('firepit-diffuse-scene-v4','firepit-diffuse-scene-v5').replace('firepit-diffuse-scene-comparison-v4','firepit-diffuse-scene-comparison-v5')
exec(compile(source,str(BASE),'exec'),globals())
