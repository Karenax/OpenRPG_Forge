//! Pebble surface on the shared Village lighting receiver and existing Bevy device.
//! Shader bodies originate from Tidewater 1438b1ab; scene maps remain runtime-owned.
use crate::tidewater_material::VillageMaterial;
use bevy::{
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::{render_resource::AsBindGroup, storage::ShaderBuffer},
    shader::ShaderRef,
};

pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("e5ff54d0-0b27-4cee-9ce6-391dfef8ce70");

/// Additional source resources; the base already binds terrain sun shadow and scene light.
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct PebbleExtension {
    #[texture(100, filterable = false)]
    pub heights: Handle<Image>,
    /// Use the acquired RGBA8Unorm mip chain and repeat/linear/aniso-4 image sampler.
    #[texture(101)]
    #[sampler(102)]
    pub detail: Handle<Image>,
    /// origin, extent, height resolution, shore resolution (the shore map is unused here).
    #[uniform(103)]
    pub terrain_domain: Vec4,
    /// Baked sun direction xyz and whether the scene's heightfield shadow is baked.
    #[uniform(103)]
    pub terrain_sun: Vec4,
    /// Acquired linear RGBA8 density map with clamp/linear sampling and no mips.
    #[texture(104)]
    #[sampler(105)]
    pub density: Handle<Image>,
    /// Up to 240 source cell origins; mesh tags index these records.
    #[storage(106, read_only)]
    pub cells: Handle<ShaderBuffer>,
}

impl MaterialExtension for PebbleExtension {
    fn vertex_shader() -> ShaderRef {
        SHADER.clone().into()
    }

    fn fragment_shader() -> ShaderRef {
        SHADER.clone().into()
    }

    fn enable_prepass() -> bool {
        false
    }

    fn enable_shadows() -> bool {
        false
    }
}

pub type PebbleMaterial = ExtendedMaterial<VillageMaterial, PebbleExtension>;

/// Retain scene lighting resources while applying the acquired Pebble surface defaults.
///
/// # Errors
/// Rejects invalid terrain domains or unbounded bake flags. Callers supply actual
/// source maps and sampler; no placeholder terrain resources are allocated here.
/// The scene adapter must validate texture layouts, storage capacity, mesh tags
/// and identity model transforms before publishing any field instances.
pub fn prepare_material(
    mut base: VillageMaterial,
    extension: PebbleExtension,
) -> Result<PebbleMaterial, String> {
    let domain = extension.terrain_domain;
    let sun = extension.terrain_sun;
    if !domain.is_finite()
        || domain.y <= 0.0
        || domain.z < 2.0
        || domain.z.fract() != 0.0
        || domain.w < 1.0
        || !sun.is_finite()
        || (sun.truncate().length_squared() - 1.0).abs() > 0.001
        || !(0.0..=1.0).contains(&sun.w)
    {
        return Err("Invalid Pebble terrain binding parameters".into());
    }
    // Pebble is opaque/front-sided, matching the base's kind-0 pipeline state.
    // Its extension shader supplies the actual Pebble vertex/surface programs.
    base.kind = 0;
    base.settings.color = Vec4::ONE;
    base.settings.emissive = Vec4::new(0.0, 0.0, 0.0, 1.0);
    base.settings.surface = Vec4::new(0.8, 0.0, 0.0, 1.0);
    Ok(PebbleMaterial { base, extension })
}

/// Add the shared live shadow, caustics, bounce, local-light and scene-buffer receivers.
///
/// # Errors
/// Rejects an absent or drifted Pebble interface and any already adapted source.
pub fn shader_source(source: &str) -> Result<String, String> {
    for marker in [
        "@binding(100) var terrainHeightTex:texture_2d<f32>;",
        "@binding(101) var terrainDetailTex:texture_2d<f32>;",
        "@binding(102) var smpAniso4Repeat:sampler;",
        "@binding(103) var<uniform> terrainParams:TerrainParams;",
        "@binding(104) var debrisPebbleMask:texture_2d<f32>;",
        "@binding(105) var smpLinearClamp:sampler;",
        "@binding(106) var<storage,read> pebbleCells:PebbleCells;",
        "v.aSlot=input.vdata; v.iCell=pebbleCells.cells[mesh_functions::get_tag(input.instance)];",
        "fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){",
        "fn sourceSurface(in:FragInput,s:ptr<function,Surface>){",
        "fn debrisStoneSurface(",
        "fn terrainHeightAt(",
        "frame.cameraPos=view.world_position;",
        "frame.frameIndex=shadowParams.frameIndex;",
    ] {
        if source.matches(marker).count() != 1 {
            return Err("Pebble shader interface differs".into());
        }
    }
    crate::tidewater_material::scene_shader_source(source)
}

/// Install only a verified caller-supplied program in an existing renderer.
///
/// # Errors
/// Returns interface/resource errors without an AssetServer fixture fallback.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let source = shader_source(source)?;
    crate::tidewater_material::install_scene_receivers(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Pebble shader assets unavailable")?
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "tidewater-package-pebble.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the acquired Pebble shader and a fresh local evidence directory"]
    fn acquired_program_accepts_shared_receivers_without_duplicate_sun_shadow() {
        let input = std::env::var("FORGE_DEBRIS_PEBBLE_PROGRAM").unwrap();
        let output = std::env::var("FORGE_DEBRIS_PEBBLE_PREPARED").unwrap();
        let source =
            std::fs::read_to_string(std::path::Path::new(&input).join("pebbles.wgsl")).unwrap();
        let prepared = shader_source(&source).unwrap();
        assert_eq!(prepared.matches("fn terrainSunShadowAt(").count(), 1);
        assert!(prepared.contains("frame.frameIndex=currentShadowParams().frameIndex;"));
        assert!(shader_source(&prepared).is_err());
        assert!(shader_source(&source.replace("@binding(100)", "@binding(101)")).is_err());
        std::fs::create_dir(&output).unwrap();
        std::fs::write(std::path::Path::new(&output).join("pebbles.wgsl"), prepared).unwrap();
    }
}
