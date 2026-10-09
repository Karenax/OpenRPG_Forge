"""Measure scene progress without turning a regional improvement into qualification."""
from pathlib import Path
BASE_SPECULAR_RESIDUAL=Path(__file__).with_name('record_firepit_diffuse_scene_residual_v1.py')
source=BASE_SPECULAR_RESIDUAL.read_text('utf-8').replace('firepit-diffuse-scene-residual-v1','firepit-specular-scene-residual-v1').replace('firepit-diffuse-scene-comparison-v5','firepit-specular-scene-comparison-v2').replace('firepit-diffuse-scene-v5','firepit-specular-scene-v2')
source=source.replace('compare_firepit_diffuse_scene_v5.py','compare_firepit_specular_scene_v2.py')
source=source.replace('actual_scene_diffuse_progress_measured_full_render_still_refused','actual_scene_diffuse_and_specular_progress_measured_full_render_still_refused')
source=source.replace('Receiver is closer; object remains visibly dark and source shadows are broader/softer. This is not editor or human visual acceptance.','Image review pending; numeric comparison alone is not editor or human visual acceptance.')
source=source.replace('Capture source prefiltered specular HDRI and effective shadow filtering; qualify common scene dependencies and target SSAO before claiming render parity.','Resolve remaining source SSAO, direct lighting and shadow-filter discrepancies; preserve measured source specular and original thresholds.')
exec(compile(source,str(BASE_SPECULAR_RESIDUAL),'exec'),globals())
