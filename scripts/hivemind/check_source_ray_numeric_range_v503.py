"""Compare native boxes and all finite Float32 ray magnitude extremes."""
from pathlib import Path
parent = Path(__file__).with_name('run_gladiator_rapier_boxes_v475.py')
code = parent.read_text('utf-8').replace('gladiator-rapier-box-query-v475', 'source-box-ray-numeric-range-v503')
write = "(O/'src/main.rs').write_text(source,'utf-8')"
extra = r'''
for sample in reference["rays"].as_array().unwrap(){
 let origin=basis.transform_point3(vec3(&sample["origin"]));
 let direction=basis.transform_vector3(vec3(&sample["direction"]));
 for magnitude in [f32::MAX,f32::MIN_POSITIVE,f32::from_bits(1)]{
  let hit=b.raycast(origin,direction*magnitude,sample["max_distance"].as_f64().unwrap() as f32).unwrap().unwrap();
  assert!(near(hit.time_of_impact,sample["distance"].as_f64().unwrap() as f32));
  assert!(vector_near(hit.point,basis.transform_point3(vec3(&sample["point"]))));
  assert!(vector_near(hit.normal,basis.transform_vector3(vec3(&sample["normal"]))));
 }
}
assert!(b.raycast(Vec3::ZERO,Vec3::splat(f32::INFINITY),1.).is_err());
assert!(b.raycast(Vec3::ZERO,Vec3::X,-1.).is_err());
assert!(b.raycast(Vec3::ZERO,Vec3::X,f32::INFINITY).is_err());
'''
injection = "source=source.replace('*.5','*0.5')\n"
injection += "source=source.replace('assert!(b.closest_point(Vec3::splat(f32::NAN)).is_err());'," + repr(extra) + "+'assert!(b.closest_point(Vec3::splat(f32::NAN)).is_err());')\n"
injection += "source=source.replace('\"product_rapier_patch_used\":true,','\"product_rapier_patch_used\":true,\"finite_ray_magnitude_extremes_checked\":boxes.len()*18,')\n" + write
assert code.count(write) == 1
code = code.replace(write, injection)
code = code.replace('pins={str(p):sha(p) for p in files}', 'files.append(parent)\npins={str(p):sha(p) for p in files}')
code = code.replace('three_common_rapier_boxes_match_63_unity_points_and_18_rays', 'three_source_boxes_native_queries_and_54_extreme_ray_magnitudes_verified')
exec(compile(code, str(parent) + '#finite-ray-range', 'exec'), globals())
