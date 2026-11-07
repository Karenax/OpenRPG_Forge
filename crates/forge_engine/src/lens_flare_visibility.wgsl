// SPDX-License-Identifier: MIT
// Tidewater LensFlare.js depth-disc visibility reduction, upstream 1438b1ab.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
struct FlareSettings { sun: vec4<f32>, optics: vec4<f32>, light: vec4<f32> }
@group(0) @binding(0) var scene_depth: texture_depth_2d;
@group(0) @binding(1) var<uniform> flare: FlareSettings;
@fragment fn visibility(input: FullscreenVertexOutput)->@location(0) vec4<f32>{
 let size=vec2<i32>(textureDimensions(scene_depth));let center=flare.sun.xy*vec2<f32>(size);
 if(flare.light.w>2.5){let px=clamp(vec2<i32>(center),vec2<i32>(0),size-1);return vec4(textureLoad(scene_depth,px,0),0.,0.,1.);}
 if(flare.sun.w<=0.0||flare.sun.z<=0.0||flare.optics.z<=0.0){return vec4(0.0);}
 let taps=array<vec2<f32>,24>(vec2(.102,0),vec2(-.130,.119),vec2(.020,-.228),vec2(.165,.216),vec2(-.306,-.054),vec2(.294,-.187),vec2(-.099,.368),vec2(-.194,-.374),vec2(.418,.153),vec2(-.434,.179),vec2(.209,-.447),vec2(.154,.493),vec2(-.486,-.282),vec2(.535,-.118),vec2(-.302,.481),vec2(-.080,-.570),vec2(.444,.392),vec2(-.592,.025),vec2(.425,-.442),vec2(-.014,.623),vec2(-.459,-.448),vec2(.656,.081),vec2(-.505,.439),vec2(.067,-.684));
 var visible=0.;for(var i=0;i<24;i+=1){let px=clamp(vec2<i32>(center+taps[i]*flare.optics.y),vec2<i32>(0),size-1);visible+=select(0.,1.,textureLoad(scene_depth,px,0)<1e-7);}
 return vec4(vec3(visible/24.*flare.sun.z*flare.optics.z),1.);
}
