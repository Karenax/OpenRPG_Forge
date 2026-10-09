"""Reopen current Game-camera reference and its receiver-shadow control."""
from pathlib import Path
BASE=Path(__file__).with_name('measure_firepit_shadow_reference_v1.py')
source=BASE.read_text('utf-8').replace('firepit-shadow-source-reference-v1','firepit-shadow-source-reference-v5').replace('firepit-shadow-source-measurement-v1','firepit-shadow-source-measurement-v5')
source=source.replace("n['preview_scene_isolated']","n['private_unsaved_scene_isolated'] and n['camera_kind']=='Game' and n['native_light_unit']=='Lux'")
exec(compile(source,str(BASE),'exec'),globals())
