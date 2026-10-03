// SPDX-License-Identifier: MIT
// Source-derived optics from Tidewater src/post/LensFlare.js at 1438b1ab.
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput
struct FlareSettings { sun: vec4<f32>, optics: vec4<f32>, light: vec4<f32> }
@group(0) @binding(0) var scene_color: texture_2d<f32>;
@group(0) @binding(1) var flare_visibility: texture_2d<f32>;
@group(0) @binding(2) var<uniform> flare: FlareSettings;
const SEG: f32 = 0.8975979;
fn poly_dist(d: vec2<f32>) -> f32 { let a = atan2(d.y,d.x)+0.3; return length(d)*cos(floor(a/SEG+0.5)*SEG-a); }
fn ghost(p: vec2<f32>,s: vec2<f32>,a: f32,r: f32,tint: vec3<f32>,k: f32)->vec3<f32>{let d=poly_dist(p-s*a);let b=vec3(smoothstep(r*.975,r*.8775,d),smoothstep(r,r*.9,d),smoothstep(r*1.025,r*.9225,d));return b*(smoothstep(r*.55,r,d)*.7+.3)*tint*(k*.0028/(r*r));}
@fragment fn fragment(input: FullscreenVertexOutput)->@location(0) vec4<f32>{
 let pixel=vec2<i32>(input.position.xy);let color=textureLoad(scene_color,pixel,0);let visible=textureLoad(flare_visibility,vec2<i32>(0),0).x;if(flare.light.w>1.5){return vec4(vec3(visible),color.a);}if(visible<=0.0||flare.sun.w<=0.0){return color;}let asp=vec2(flare.optics.x,1.0);let p=(input.uv-.5)*asp*vec2(1.0,-1.0);let s=(flare.sun.xy-.5)*asp*vec2(1.0,-1.0);var gs=vec3(0.0);
 gs+=ghost(p,s,.72,.018,vec3(1,.85,.6),.55);gs+=ghost(p,s,.44,.045,vec3(.55,.9,1),.35);gs+=ghost(p,s,.16,.022,vec3(.8,1,.7),.45);gs+=ghost(p,s,-.18,.07,vec3(.6,.75,1),.22);gs+=ghost(p,s,-.42,.03,vec3(1,.7,.9),.4);gs+=ghost(p,s,-.75,.11,vec3(.7,1,.85),.14);gs+=ghost(p,s,-1.15,.05,vec3(1,.8,.55),.28);
 let off=smoothstep(.02,.2,length(s));let r=length(p);let halo=vec3(smoothstep(.03,0.,abs(r-.43)),smoothstep(.03,0.,abs(r-.445)),smoothstep(.03,0.,abs(r-.46)))*smoothstep(.25,.8,length(s))*.12;let q=p-s;let rq=max(length(q),1e-4);let a=atan2(q.y,q.x)+.3;let burst=pow(abs(cos(a*7.)),600.)*exp(-rq*9.)*1.2+pow(abs(cos(a*53.+cos(a*11.)*2.)),80.)*exp(-rq*22.)*.35+exp(-rq*5.)*.05+exp(-rq*40.)*.4;let light=flare.light.rgb*visible*flare.sun.w*.02;return vec4(color.rgb+light*(gs*off+halo+vec3(burst)),color.a);
}
