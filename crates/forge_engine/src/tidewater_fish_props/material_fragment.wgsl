// SPDX-License-Identifier: MIT
// Clearcoat composition adapted from Tidewater lighting.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
fn fishSceneLight(s:FishSurface,V:vec3f,P:vec3f,pixel:vec2f,geomN:vec3f)->vec3f {
 let N=normalize(s.normal);
 let base=LightingSurface(s.albedo,N,s.roughness,s.metalness,
     s.specularIntensity,s.translucency,1.0,1.0,vec3f(0.0));
 let color=sourceSceneLight(base,V,P,pixel);
 let L=sceneLighting.sunDirection.xyz;
 let lightColor=sceneLighting.sunColor.xyz*terrainSunShadowAt(P)*cloudsShadow(P.xz)
     *sunShadow(underwaterShadowPosition(P,pixel),N,pixel)*villageUnderwaterDirect(P);
 let ccSpec=sat(dot(geomN,L))*lightColor
     *BRDF_GGX(L,V,geomN,vec3f(0.04),1.0,clamp(s.clearcoatRoughness,0.03,1.0));
 let Fcc=F_Schlick(vec3f(0.04),1.0,sat(dot(geomN,V)))*s.clearcoat;
 let ccRad=textureSampleLevel(envCube,envSampler,reflect(-V,geomN),
     clamp(s.clearcoatRoughness,0.03,1.0)*5.0).rgb*villageUnderwaterAmbient(P);
 return color*(1.0-Fcc)+ccSpec*s.clearcoat+ccRad*Fcc;
}
@fragment fn fragment(vs:Output)->@location(0) vec4f {
 frame.time=select(settings.info.w,bitcast<f32>(fishClock.x),fishClock.z!=0u);
 frame.view=fish_camera::view.view_from_world;
 frame.frameIndex=select(settings.frameIndex,fishClock.y,fishClock.z!=0u);
 var input:FishSurfaceInput;
 input.vs.vFishLocal=vs.rest;input.vs.vFishData=vs.data;
 input.vs.vFishInfo=vec4f(settings.info.xy,vs.length_m,0.0);
 input.vs.vFishFlags=settings.flags;
 input.P=vs.P;input.N=normalize(vs.normal);
 input.V=normalize(fish_camera::view.world_position-vs.P);input.pixel=vs.position.xy;
 let surface=fishSurface(input);
 if (!lodFadeVisible(input.pixel,settings.lod.x,settings.lod.y>0.5)) {discard;}
 return vec4f(fishSceneLight(surface,input.V,input.P,input.pixel,input.N),1.0);
}
