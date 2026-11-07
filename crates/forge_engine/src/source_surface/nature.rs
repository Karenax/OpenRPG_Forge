//! Nature surface on the shared source lighting receiver and existing Bevy device.
//! Shader bodies and scene maps remain caller-owned.
// Source shader adaptations: Tidewater 1438b1ab, copyright 2026 DRG Software Solutions LLC;
// see third_party/tidewater/LICENSE.
use super::SourceSurfaceMaterial;
use bevy::{
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::render_resource::AsBindGroup,
    shader::ShaderRef,
};

pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("0c590b97-e0d6-4c26-8a32-d449a09f2754");
// One stable fallback import identity for this shared shader handle.
const SOURCE_PATH: &str = "forge-source-nature.wgsl";

/// Additional source resources; the base already binds terrain sun shadow and scene light.
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct NatureExtension {
    #[texture(100, filterable = false)]
    #[dependency]
    pub heights: Handle<Image>,
    /// Use the acquired `RGBA8Unorm` mip chain and repeat/linear/aniso-4 image sampler.
    #[texture(101)]
    #[sampler(102)]
    #[dependency]
    pub detail: Handle<Image>,
    /// origin, extent, height resolution, shore resolution (the shore map is unused here).
    #[uniform(103)]
    pub terrain_domain: Vec4,
    /// Baked sun direction xyz and whether the scene's heightfield shadow is baked.
    #[uniform(103)]
    pub terrain_sun: Vec4,
}

impl MaterialExtension for NatureExtension {
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

pub type NatureMaterial = ExtendedMaterial<SourceSurfaceMaterial, NatureExtension>;

/// Retain scene lighting resources while applying the acquired Nature surface defaults.
///
/// # Errors
/// Rejects invalid terrain domains or unbounded bake flags. Callers supply actual
/// source maps and sampler; no placeholder terrain resources are allocated here.
pub fn prepare_material(
    mut base: SourceSurfaceMaterial,
    extension: NatureExtension,
) -> Result<NatureMaterial, String> {
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
        return Err("Invalid Nature terrain binding parameters".into());
    }
    // Nature is opaque/front-sided, matching the base's kind-0 pipeline state.
    // Its extension shader supplies the actual Nature vertex/surface programs.
    base.kind = 0;
    base.settings.color = Vec4::ONE;
    base.settings.emissive = Vec4::new(0.0, 0.0, 0.0, 1.0);
    base.settings.surface = Vec4::new(0.8, 0.0, 0.0, 1.0);
    Ok(NatureMaterial { base, extension })
}

/// Add the shared live shadow, caustics, bounce, local-light and scene-buffer receivers.
///
/// # Errors
/// Rejects an absent or drifted Nature interface and any already adapted source.
pub fn shader_source(source: &str) -> Result<String, String> {
    for marker in [
        "@binding(100) var terrainHeightTex:texture_2d<f32>;",
        "@binding(101) var terrainDetailTex:texture_2d<f32>;",
        "@binding(102) var smpAniso4Repeat:sampler;",
        "@binding(103) var<uniform> terrainParams:TerrainParams;",
        "fn debrisStoneSurface(",
        "fn terrainHeightAt(",
        "frame.cameraPos=view.world_position;",
        "frame.frameIndex=shadowParams.frameIndex;",
    ] {
        if source.matches(marker).count() != 1 {
            return Err("Nature shader interface differs".into());
        }
    }
    super::receivers::scene_shader_source(source)
}

/// Install only a verified caller-supplied program in an existing renderer.
/// WGSL import declarations retain their usual Bevy precedence over the fallback path.
///
/// # Errors
/// Returns interface/resource errors without an `AssetServer` fixture fallback.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let source = shader_source(source)?;
    super::receivers::install_scene_receivers(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Nature shader assets unavailable")?
        .insert(SHADER.id(), Shader::from_wgsl(source, SOURCE_PATH))
        .map_err(|error| error.to_string())?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::reflect::TypePath;

    fn extension() -> NatureExtension {
        NatureExtension {
            heights: Handle::default(),
            detail: Handle::default(),
            terrain_domain: Vec4::new(-1024.0, 2048.0, 2048.0, 1.0),
            terrain_sun: Vec4::new(0.0, 1.0, 0.0, 0.0),
        }
    }

    // Marker comments exercise CPU-side adaptation only; they are not a GPU program.
    fn synthetic_source() -> String {
        let mut source = crate::source_surface::PROFILE_PROGRAM.to_owned();
        for marker in [
            "@binding(100) var terrainHeightTex:texture_2d<f32>;",
            "@binding(101) var terrainDetailTex:texture_2d<f32>;",
            "@binding(102) var smpAniso4Repeat:sampler;",
            "@binding(103) var<uniform> terrainParams:TerrainParams;",
            "fn debrisStoneSurface(",
            "fn terrainHeightAt(",
            "frame.cameraPos=view.world_position;",
            "frame.frameIndex=shadowParams.frameIndex;",
        ] {
            source.push_str("\n// ");
            source.push_str(marker);
        }
        source
    }

    #[test]
    fn transferred_nature_keeps_reflection_and_shader_contract() {
        assert_eq!(
            NatureExtension::type_path(),
            "forge_engine::source_surface::nature::NatureExtension"
        );
        for shader in [
            NatureExtension::vertex_shader(),
            NatureExtension::fragment_shader(),
        ] {
            assert!(matches!(shader, ShaderRef::Handle(handle) if handle.id() == SHADER.id()));
        }
        assert!(!NatureExtension::enable_prepass());
        assert!(!NatureExtension::enable_shadows());
        assert!(shader_source("").is_err());
    }

    #[test]
    fn transferred_staging_preserves_sequential_program_replacement() {
        let first_source = synthetic_source();
        let second_source = format!("{first_source}\n// next source program");
        let first = shader_source(&first_source).unwrap();
        let second = shader_source(&second_source).unwrap();
        assert_ne!(first, second);

        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<bevy::render::storage::ShaderBuffer>>();
        stage_shader(&mut world, &first_source).unwrap();
        let shader = world.resource::<Assets<Shader>>().get(&SHADER).unwrap();
        assert_eq!(shader.source.as_str(), first);
        assert_eq!(SOURCE_PATH, "forge-source-nature.wgsl");
        assert_eq!(shader.path, SOURCE_PATH);
        assert!(matches!(
            &shader.import_path,
            bevy::shader::ShaderImport::AssetPath(path) if path == SOURCE_PATH
        ));
        stage_shader(&mut world, &second_source).unwrap();
        assert_eq!(
            world
                .resource::<Assets<Shader>>()
                .get(&SHADER)
                .unwrap()
                .source
                .as_str(),
            second
        );
        assert!(stage_shader(&mut world, "").is_err());
        assert_eq!(
            world
                .resource::<Assets<Shader>>()
                .get(&SHADER)
                .unwrap()
                .source
                .as_str(),
            second
        );
    }

    #[test]
    fn explicit_shader_import_identity_remains_authoritative() {
        let source = synthetic_source();
        let source = format!("#define_import_path fixture_module::nature\n{source}");
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<bevy::render::storage::ShaderBuffer>>();
        stage_shader(&mut world, &source).unwrap();
        let shader = world.resource::<Assets<Shader>>().get(&SHADER).unwrap();
        assert_eq!(shader.path, SOURCE_PATH);
        assert!(matches!(
            &shader.import_path,
            bevy::shader::ShaderImport::Custom(name) if name == "fixture_module::nature"
        ));
    }

    #[test]
    fn transferred_nature_preserves_base_bindings_and_refuses_invalid_inputs() {
        let mut before = crate::source_surface::tests::lighting();
        before.kind = 5;
        before.settings.color = Vec4::splat(0.25);
        before.settings.emissive = Vec4::splat(0.5);
        before.settings.surface = Vec4::splat(0.75);
        let ext = extension();
        let result = prepare_material(before.clone(), ext.clone()).unwrap();
        assert_eq!(result.base.kind, 0);
        assert_eq!(result.base.settings.color, Vec4::ONE);
        assert_eq!(result.base.settings.emissive, Vec4::new(0.0, 0.0, 0.0, 1.0));
        assert_eq!(result.base.settings.surface, Vec4::new(0.8, 0.0, 0.0, 1.0));
        let mut restored = result.base;
        restored.kind = before.kind;
        restored.settings.color = before.settings.color;
        restored.settings.emissive = before.settings.emissive;
        restored.settings.surface = before.settings.surface;
        assert_eq!(restored, before);
        assert_eq!(result.extension.heights.id(), ext.heights.id());
        assert_eq!(result.extension.detail.id(), ext.detail.id());
        assert_eq!(result.extension.terrain_domain, ext.terrain_domain);
        assert_eq!(result.extension.terrain_sun, ext.terrain_sun);

        for (domain, sun) in [
            (Vec4::new(f32::NAN, 2048.0, 2048.0, 1.0), Vec4::Y),
            (Vec4::new(0.0, 0.0, 2048.0, 1.0), Vec4::Y),
            (Vec4::new(0.0, 1.0, 1.0, 1.0), Vec4::Y),
            (Vec4::new(0.0, 1.0, 2.5, 1.0), Vec4::Y),
            (Vec4::new(0.0, 1.0, 2.0, 0.5), Vec4::Y),
            (Vec4::new(0.0, 1.0, 2.0, 1.0), Vec4::ZERO),
            (Vec4::new(0.0, 1.0, 2.0, 1.0), Vec4::new(0.0, 1.0, 0.0, 1.1)),
        ] {
            let mut invalid = extension();
            invalid.terrain_domain = domain;
            invalid.terrain_sun = sun;
            assert!(prepare_material(before.clone(), invalid).is_err());
        }
    }

    #[test]
    #[ignore = "Requires the acquired Nature shader and a fresh local evidence directory"]
    fn acquired_program_accepts_shared_receivers_without_duplicate_sun_shadow() {
        let input = std::env::var("FORGE_DEBRIS_NATURE_PROGRAM").unwrap();
        let output = std::env::var("FORGE_DEBRIS_NATURE_PREPARED").unwrap();
        let source =
            std::fs::read_to_string(std::path::Path::new(&input).join("nature.wgsl")).unwrap();
        let prepared = shader_source(&source).unwrap();
        assert_eq!(prepared.matches("fn terrainSunShadowAt(").count(), 1);
        assert!(prepared.contains("frame.frameIndex=currentShadowParams().frameIndex;"));
        assert!(shader_source(&prepared).is_err());
        assert!(shader_source(&source.replace("@binding(100)", "@binding(101)")).is_err());
        std::fs::create_dir(&output).unwrap();
        std::fs::write(std::path::Path::new(&output).join("nature.wgsl"), prepared).unwrap();
    }
}
