//! Existing shared surface-shader assembly; caller supplies admitted source programs.
// SPDX-License-Identifier: MIT
// Source shader adaptations: Tidewater 1438b1ab, copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
use crate::{
    source_lighting::scene_buffer,
    source_surface::{neutral_surface_shader_source, stage_prepared_shader},
};
use bevy::prelude::*;
pub fn install_shader(app: &mut App, source: &str) -> Result<(), Box<dyn std::error::Error>> {
    stage_shader(app.world_mut(), source).map_err(Into::into)
}
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let source = scene_shader_source(&neutral_surface_shader_source(source)?)?;
    install_scene_receivers(world)?;
    stage_prepared_shader(world, &source)
}
/// Shared scene receivers for procedural materials with the qualified lighting seams.
pub fn scene_shader_source(source: &str) -> Result<String, String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid building shader size".into());
    }
    let source = crate::source_shadows::receiver::shader_source(source)?;
    let source = crate::source_surface::underwater::receivers::surface_shader_source(&source)?;
    let source = ground_bounce_shader_source(&source)?;
    let source = crate::source_lighting::local_lights::receiver::shader_source(&source)?;
    scene_buffer::receiver::shader_source(&source, "VillageSceneLighting")
}

pub fn install_scene_receivers(world: &mut World) -> Result<(), String> {
    scene_buffer::receiver::install(world)?;
    crate::source_shadows::receiver::install(world)?;
    crate::source_surface::underwater::install_neutral(world)?;
    crate::source_lighting::local_lights::gpu::install_neutral(world)?;
    Ok(())
}

fn ground_bounce_shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+sourceEnvironmentLight(s,N,V)*villageUnderwaterAmbient(P)+s.emissive;";
    if !source.contains(MARKER) || source.contains("fn sourceGroundBounce(") {
        return Err("Building shader lacks the expected indirect-light seam".into());
    }
    let source = source.replace(
        MARKER,
        "+(sourceEnvironmentLight(s,N,V)+sourceGroundBounce(s,P,N))*villageUnderwaterAmbient(P)+s.emissive;",
    );
    Ok(format!(
        "{source}\n{}",
        r"
@group(#{MATERIAL_BIND_GROUP}) @binding(50) var groundBounceMap: texture_2d<f32>;
// x = source bounce strength, y = sea level. The map's alpha is ground height.
@group(#{MATERIAL_BIND_GROUP}) @binding(51) var<uniform> bounceSettings: vec4f;

fn sourceGroundBounce(s: LightingSurface, P: vec3f, N: vec3f) -> vec3f {
    let view = clamp(0.5 - N.y * 0.5, 0.0, 1.0);
    if (bounceSettings.x <= 0.0 || view <= 0.03 || P.y <= bounceSettings.y - 0.3) {
        return vec3f(0.0);
    }
    let size = vec2f(textureDimensions(groundBounceMap));
    let st = clamp(((P.xz + 1024.0) / 2048.0) * size - 0.5,
                   vec2f(0.0), size - vec2f(1.001));
    let i = vec2i(floor(st));
    let t = fract(st);
    let bounced = mix(
        mix(textureLoad(groundBounceMap, i, 0),
            textureLoad(groundBounceMap, i + vec2i(1, 0), 0), t.x),
        mix(textureLoad(groundBounceMap, i + vec2i(0, 1), 0),
            textureLoad(groundBounceMap, i + vec2i(1, 1), 0), t.x), t.y);
    let above = P.y - bounced.w;
    let fade = smoothstep(-0.3, 0.3, above)
        * mix(0.4, 1.0, 1.0 - smoothstep(4.0, 30.0, above));
    let L = sceneLighting.sunDirection.xyz;
    let Lh = L.xz / max(length(L.xz), 0.001);
    let selfShade = 1.0 - clamp(-dot(N.xz, Lh), 0.0, 1.0)
        * clamp(1.0 - L.y, 0.0, 1.0) * 0.6;
    let irradiance = bounced.xyz * sceneLighting.sunColor.xyz * cloudsShadow(P.xz)
        * (view * fade * selfShade * bounceSettings.x * 0.6);
    return s.albedo * (1.0 - s.metalness) * s.ao * irradiance * INV_PI;
}
"
    ))
}
