"""Render the 182-case source cube witness with the common scene binding."""
from pathlib import Path
BASE_SKY=Path(__file__).with_name('capture_firepit_brdf_lookup_gpu_v1.py')
source=BASE_SKY.read_text('utf-8')
source=source.replace('firepit-brdf-lookup-gpu-v1','firepit-sky-cube-gpu-v1')
source=source.replace('firepit-brdf-lookup-gpu-case-v1','firepit-sky-cube-gpu-case-v1')
source=source.replace('private-brdf-lookup-witness.glb','private-sky-cube-witness.glb')

bind=r'''
 let packet=&case["sky_cube_packet"];
 let sky_sha=packet["sha256"].as_str().unwrap().parse().unwrap();
 let cube=SourceGraphSkyCube::from_mip_major_rgba16f_le(
  &std::fs::read(packet["path"].as_str().unwrap())?,sky_sha,
  packet["width"].as_u64().unwrap() as u32,packet["cube_count"].as_u64().unwrap() as u32,
  packet["mip_count"].as_u64().unwrap() as u32)?;
 let binding=cube.install_unqualified(&mut app.world_mut().resource_mut::<Assets<Image>>());
 binding.validate_residency(app.world().resource::<Assets<Image>>())?;
 let mut bound_count=0;
 for h in materials.values(){
  let mut assets=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>();
  let mut material=assets.get_mut(h).unwrap();
  assert!(material.extension.program_key.requires_sky_cube);
  assert!(!SourceGraphKey::from(&material.extension).sky_cube_ready);
  material.extension.set_sky_cube(&binding);
  assert!(SourceGraphKey::from(&material.extension).sky_cube_ready);
  bound_count+=1;
 }
 assert_eq!(bound_count,4);
'''
start=source.index("bind = r'''");end=source.index("negative = r'''",start)
source=source[:start]+'bind='+repr(bind)+'\n'+source[end:]
source=source.replace('clear_preintegrated_brdf()','clear_sky_cube()')
source=source.replace('set_preintegrated_brdf(&binding)','set_sky_cube(&binding)')
source=source.replace('missing_brdf_frame','missing_sky_frame')
source=source.replace('brdf_lookup_cases_expected":35','sky_cube_cases_expected":182')
source=source.replace('shared_brdf_bound_materials','shared_sky_cube_bound_materials')
source=source.replace('missing_brdf_frame_refused_atomically','missing_sky_frame_refused_atomically')
exec(compile(source,str(BASE_SKY),'exec'),globals())
