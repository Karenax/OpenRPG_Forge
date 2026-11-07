"""Keep repetition metrics for GeneralORM white BaseMap diagnostics."""
from pathlib import Path
recipe=Path(__file__).with_name('measure_mountain_albedo_controls_v936.py')
code=recipe.read_text('utf-8').replace('mountain-albedo-measurements-v936','mountain-generalorm-measurements-v940').replace('mountain-albedo-controls-run-v935','mountain-generalorm-controls-run-v939').replace('mountain-albedo-controls-v934/PREPARED.v1.json','mountain-generalorm-controls-v938/PREPARED.v1.json')
code=code.replace('native_diagnostic_albedo_controls_finished_no_image_admission','native_diagnostic_generalorm_controls_finished_no_image_admission').replace('diagnostic_white_main_texture','diagnostic_white_base_map')
code=code.replace('DIAGNOSTIC WHITE MAIN TEXTURE','DIAGNOSTIC WHITE BASEMAP')
code=code.replace('white_albedo_deltas_measured_repeats_unstable_no_causal_verdict','generalorm_white_basemap_deltas_and_repeat_stability_measured')
exec(compile(code,str(recipe)+'#generalorm-property-and-repeat-metrics','exec'),globals())
