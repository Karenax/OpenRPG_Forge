"""Render the predeclared combined diffuse witness on the common native host."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_brdf_lookup_gpu_v1.py')
source=BASE.read_text('utf-8').replace('firepit-brdf-lookup-gpu','firepit-combined-diffuse-gpu').replace('private-brdf-lookup-witness.glb','private-combined-diffuse-witness.glb')
source=source.replace('forge_core::ContentHash::parse(case["brdf_lookup_protocol"]["inputs"]["sha256"].as_str().unwrap())','case["brdf_lookup_protocol"]["inputs"]["sha256"].as_str().unwrap().parse()')
source=source.replace('let material=assets.get_mut(h).unwrap();','let mut material=assets.get_mut(h).unwrap();')
before=' let mut bound_count=0;'
assert source.count(before)==1
source=source.replace(before,''' let coefficients:[Vec4;7]=std::array::from_fn(|i|{let row=&case["diffuse_probe_coefficients"][i];Vec4::new(row["x"].as_f64().unwrap() as f32,row["y"].as_f64().unwrap() as f32,row["z"].as_f64().unwrap() as f32,row["w"].as_f64().unwrap() as f32)});
 let diffuse_probe=SourceGraphDiffuseProbeUniforms::from_preconvolved(coefficients,reflection)?;
'''+before)
source=source.replace('  material.extension.set_preintegrated_brdf(&binding);','  material.extension.set_preintegrated_brdf(&binding);\n  material.extension.diffuse_probe=diffuse_probe.clone();')
source=source.replace('"brdf_lookup_cases_expected":35','"brdf_lookup_cases_expected":48')
exec(compile(source,str(BASE),'exec'),globals())
