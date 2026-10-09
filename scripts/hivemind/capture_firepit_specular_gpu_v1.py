"""Bind both proven shared resources and render the complete specular witness."""
from pathlib import Path
BASE_SPECULAR=Path(__file__).with_name('capture_firepit_sky_cube_gpu_v1.py')
source=BASE_SPECULAR.read_text('utf-8').replace('firepit-sky-cube-gpu-v1','firepit-specular-gpu-v1').replace('firepit-sky-cube-gpu-case-v1','firepit-specular-gpu-case-v1').replace('private-sky-cube-witness.glb','private-specular-witness.glb')
brdf=r'''
 let fgd_packet=&case["brdf_lookup_protocol"]["inputs"];
 let fgd_sha=fgd_packet["sha256"].as_str().unwrap().parse().unwrap();
 let fgd=SourceGraphPreintegratedBrdf::from_rgba32f_le(&std::fs::read(fgd_packet["rgba32f"].as_str().unwrap())?,fgd_sha,64,64)?;
 let fgd_binding=fgd.install_unqualified(&mut app.world_mut().resource_mut::<Assets<Image>>());
 for handle in materials.values(){
  let mut assets=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>();let mut material=assets.get_mut(handle).unwrap();
  assert!(material.extension.program_key.requires_preintegrated_brdf);material.extension.set_preintegrated_brdf(&fgd_binding);
 }
'''
needle=' assert_eq!(bound_count,4);';assert source.count(needle)==1;source=source.replace(needle,needle+'\n'+brdf)
source=source.replace('sky_cube_cases_expected\":182','source_specular_cases_expected\":756')
exec(compile(source,str(BASE_SPECULAR),'exec'),globals())
