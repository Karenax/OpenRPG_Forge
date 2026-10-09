#import bevy_pbr::shadow_sampling as source_shadow_sampling
// Private directional source-filter candidate. Native bias/cascade selection retained explicitly.
fn source_tent7_directional_cascade(light_id:u32,cascade_index:u32,frag_position:vec4<f32>,normal:vec3<f32>) -> f32 {
 let light=view_bindings::lights.directional_lights[light_id];let cascade=light.cascades[cascade_index];
 let position=vec4<f32>(frag_position.xyz+light.shadow_normal_bias*cascade.texel_size*normal+light.shadow_depth_bias*light.direction_to_light,frag_position.w);
 let local=shadows::world_to_directional_light_local(light_id,cascade_index,position);
 if(local.w==0.0){return 1.0;}
 let dimensions=vec2<f32>(textureDimensions(view_bindings::directional_shadow_textures));
 let samples=source_tent7_samples(vec4<f32>(1.0/dimensions,dimensions),local.xy);
 var value=0.0;
 for(var tap=0u;tap<16u;tap+=1u){
  value+=samples.weights[tap]*source_shadow_sampling::sample_shadow_map_hardware(samples.uv[tap],local.z,i32(light.depth_texture_base_index+cascade_index));
 }
 return value;
}
fn source_tent7_fetch_directional_shadow(light_id:u32,position:vec4<f32>,normal:vec3<f32>,view_z:f32,frag_coord:vec2<f32>) -> f32 {
 let light=view_bindings::lights.directional_lights[light_id];let index=shadows::get_cascade_index(light_id,view_z);
 if(index>=light.num_cascades){return 1.0;}
 var value=source_tent7_directional_cascade(light_id,index,position,normal);
 if(index+1u<light.num_cascades){
  let far=light.cascades[index].far_bound;let near=(1.0-light.cascades_overlap_proportion)*far;
  if(-view_z>=near){let next=source_tent7_directional_cascade(light_id,index+1u,position,normal);value=mix(value,next,(-view_z-near)/(far-near));}
 }
 return value;
}
