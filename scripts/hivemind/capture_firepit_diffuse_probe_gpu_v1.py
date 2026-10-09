"""Exercise measured SH in binding 121 using the exact common GPU host."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_forge_shadow_pair_v4.py')
source=BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v4','firepit-diffuse-probe-gpu-v1')
old_fixture="fixture=Path((OLD/'fixture-path.txt').read_text().strip());assert fixture.is_file() and sha(fixture)==read(OLD/'PREPARED.v1.json')['input_pins'][str(fixture)]"
new_fixture="fixture=W/'firepit-diffuse-probe-gpu-case-v1/private-six-direction-witness.glb';assert fixture.is_file() and sha(fixture)==read(W/'firepit-diffuse-probe-gpu-case-v1/RESULT.v1.json')['private_fixture_sha256']"
bind=r'''
 let coefficients:[Vec4;7]=std::array::from_fn(|i|{let row=&case["diffuse_probe_coefficients"][i];Vec4::new(row["x"].as_f64().unwrap() as f32,row["y"].as_f64().unwrap() as f32,row["z"].as_f64().unwrap() as f32,row["w"].as_f64().unwrap() as f32)});
 let diffuse_probe=SourceGraphDiffuseProbeUniforms::from_preconvolved(coefficients,reflection)?;
 for h in materials.values(){app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().get_mut(h).unwrap().extension.diffuse_probe=diffuse_probe.clone();}
'''
commands="source=source.replace('firepit-shadow-comparison-case-v1','firepit-diffuse-probe-gpu-case-v1')\n"
commands+="source=source.replace('previous.patched_code()',"+repr('previous.patched_code().replace('+repr(old_fixture)+','+repr(new_fixture)+')')+")\n"
commands+="source=source.replace('host.install_shadow_routes(app.world_mut(),31)?;',"+repr(bind+'\n host.install_shadow_routes(app.world_mut(),31)?;')+")\n"
commands+="source=source.replace("+repr('"actual_editor_visual_verified":false,')+","+repr('"six_direction_diffuse_probe_witness":true,"diffuse_probe_bound_from_case":true,"actual_editor_visual_verified":false,')+")\n"
needle="needle='source=source.replace(needle,injection+needle)'"
assert source.count(needle)==1
source=source.replace(needle,'commands+='+repr(commands)+'\n'+needle)
exec(compile(source,str(BASE),'exec'),globals())
