"""Measure white-albedo material-clone controls with unchanged source mesh IDs."""
from pathlib import Path
recipe=Path(__file__).with_name('measure_mountain_vertex_color_controls_v930.py')
code=recipe.read_text('utf-8').replace('mountain-vertex-color-measurements-v930','mountain-albedo-measurements-v936').replace('mountain-vertex-color-controls-run-v929','mountain-albedo-controls-run-v935').replace('mountain-vertex-color-controls-v928/PREPARED.v1.json','mountain-albedo-controls-v934/PREPARED.v1.json')
code=code.replace('native_diagnostic_vertex_color_controls_finished_no_image_admission','native_diagnostic_albedo_controls_finished_no_image_admission').replace('diagnostic_white_vertex_colors','diagnostic_white_main_texture')
anchor="assert reports[3]['selected_mesh_identities']==reports[4]['selected_mesh_identities']==['diagnostic-white-vertex-colors-clone-of:'+identity for identity in original_ids]"
assert code.count(anchor)==1
code=code.replace(anchor,"assert reports[3]['selected_mesh_identities']==reports[4]['selected_mesh_identities']==original_ids")
code=code.replace('DIAGNOSTIC WHITE COLOR CLONE','DIAGNOSTIC WHITE MAIN TEXTURE').replace('three_source_vs_diagnostic_white_color_controls_measured','three_source_vs_diagnostic_white_albedo_controls_measured')
code=code.replace(" and run['all_views_repeat_exact']",'')
first="assert source['raw_sha256']==reports[2]['images'][view]['raw_sha256']==reports[5]['images'][view]['raw_sha256']"
second="assert diagnostic['raw_sha256']==reports[4]['images'][view]['raw_sha256']"
assert code.count(first)==code.count(second)==1
code=code.replace(first,"source_repeat_exact=source['raw_sha256']==reports[2]['images'][view]['raw_sha256']==reports[5]['images'][view]['raw_sha256']")
code=code.replace(second,"diagnostic_repeat_exact=diagnostic['raw_sha256']==reports[4]['images'][view]['raw_sha256']")
anchor="            delta=np.abs(arrays[0][:,:,:3]-arrays[1][:,:,:3])"
assert code.count(anchor)==1
code=code.replace(anchor,anchor+"\n            source_repeat=np.fromfile(reports[5]['images'][view]['raw_path'],dtype='<f2').reshape(512,512,4).astype(np.float32)\n            diagnostic_repeat=np.fromfile(reports[4]['images'][view]['raw_path'],dtype='<f2').reshape(512,512,4).astype(np.float32)\n            assert np.isfinite(source_repeat).all() and np.isfinite(diagnostic_repeat).all()\n            source_delta=np.abs(arrays[0][:,:,:3]-source_repeat[:,:,:3]);diagnostic_delta=np.abs(arrays[1][:,:,:3]-diagnostic_repeat[:,:,:3])")
code=code.replace("rows.append(dict(id=cases[0]['source_card_id'],", "rows.append(dict(source_repeat_exact=source_repeat_exact,diagnostic_repeat_exact=diagnostic_repeat_exact,source_repeat_differing_rgb_pixels=int(np.count_nonzero(np.any(source_delta!=0,axis=2))),source_repeat_max_delta=float(source_delta.max()),diagnostic_repeat_differing_rgb_pixels=int(np.count_nonzero(np.any(diagnostic_delta!=0,axis=2))),diagnostic_repeat_max_delta=float(diagnostic_delta.max()),id=cases[0]['source_card_id'],")
code=code.replace('source_repeat_exact=True,diagnostic_repeat_exact=True','source_repeat_exact=all(r[\'source_repeat_exact\'] for r in rows),diagnostic_repeat_exact=all(r[\'diagnostic_repeat_exact\'] for r in rows)')
code=code.replace('three_source_vs_diagnostic_white_albedo_controls_measured','white_albedo_deltas_measured_repeats_unstable_no_causal_verdict')
code=code.replace("['id','view','differing_rgb_pixels','max_rgb_absolute_delta']", "['id','view','differing_rgb_pixels','max_rgb_absolute_delta','source_repeat_differing_rgb_pixels','source_repeat_max_delta','diagnostic_repeat_differing_rgb_pixels','diagnostic_repeat_max_delta']")
exec(compile(code,str(recipe)+'#unchanged-mesh-source-white-albedo','exec'),globals())
