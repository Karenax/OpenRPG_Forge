"""Bind the measured shared table and render the 35-case private GPU witness."""
from pathlib import Path
BASE = Path(__file__).with_name('capture_firepit_forge_shadow_pair_v4.py')
source = BASE.read_text('utf-8').replace('firepit-forge-shadow-pair-v4', 'firepit-brdf-lookup-gpu-v1')
old_fixture = "fixture=Path((OLD/'fixture-path.txt').read_text().strip());assert fixture.is_file() and sha(fixture)==read(OLD/'PREPARED.v1.json')['input_pins'][str(fixture)]"
new_fixture = "fixture=W/'firepit-brdf-lookup-gpu-case-v1/private-brdf-lookup-witness.glb';assert fixture.is_file() and sha(fixture)==read(W/'firepit-brdf-lookup-gpu-case-v1/RESULT.v1.json')['private_fixture_sha256']"
bind = r'''
 let brdf_path=case["brdf_lookup_protocol"]["inputs"]["rgba32f"].as_str().unwrap();
 let brdf_sha=forge_core::ContentHash::parse(case["brdf_lookup_protocol"]["inputs"]["sha256"].as_str().unwrap()).unwrap();
 let table=SourceGraphPreintegratedBrdf::from_rgba32f_le(&std::fs::read(brdf_path)?,brdf_sha,64,64)?;
 let binding=table.install_unqualified(&mut app.world_mut().resource_mut::<Assets<Image>>());
 let mut bound_count=0;
 for h in materials.values(){
  let mut assets=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>();
  let material=assets.get_mut(h).unwrap();
  assert!(material.extension.program_key.requires_preintegrated_brdf);
  assert!(!SourceGraphKey::from(&material.extension).preintegrated_brdf_ready);
  material.extension.set_preintegrated_brdf(&binding);
  assert!(SourceGraphKey::from(&material.extension).preintegrated_brdf_ready);
  bound_count+=1;
 }
 assert_eq!(bound_count,4);
'''
negative = r'''
 let required_handle=materials.values().next().unwrap().clone();
 app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().get_mut(&required_handle).unwrap().extension.clear_preintegrated_brdf();
 let missing_brdf_frame=policy.frame(source_graph_runtime::SourceClock{current_seconds:1.,previous_seconds:0.})?;
 assert!(host.update_runtime_frame(app.world_mut(),&missing_brdf_frame).is_err());
 for h in materials.values(){assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source.source_time_parameters.x,0.);}
 app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().get_mut(&required_handle).unwrap().extension.set_preintegrated_brdf(&binding);
'''
commands = "source=source.replace('firepit-shadow-comparison-case-v1','firepit-brdf-lookup-gpu-case-v1')\n"
commands += "source=source.replace('previous.patched_code()'," + repr('previous.patched_code().replace(' + repr(old_fixture) + ',' + repr(new_fixture) + ')') + ")\n"
commands += "source=source.replace('host.install_shadow_routes(app.world_mut(),31)?;'," + repr(bind + '\n host.install_shadow_routes(app.world_mut(),31)?;') + ")\n"
commands += "source=source.replace('let receipt=serde_json::json!('," + repr(negative + '\n let receipt=serde_json::json!(') + ")\n"
commands += "source=source.replace(" + repr('"actual_editor_visual_verified":false,') + ',' + repr('"brdf_lookup_cases_expected":35,"shared_brdf_bound_materials":4,"missing_brdf_frame_refused_atomically":true,"actual_editor_visual_verified":false,') + ")\n"
needle = "needle='source=source.replace(needle,injection+needle)'"
assert source.count(needle) == 1
source = source.replace(needle, 'commands+=' + repr(commands) + '\n' + needle)
exec(compile(source, str(BASE), 'exec'), globals())
