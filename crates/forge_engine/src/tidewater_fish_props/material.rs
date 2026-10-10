//! Dedicated fish material consuming the common scene's lighting resources.
use super::{FISH_DATA, REST_POSITION, SurfaceParameters, surface};
use crate::tidewater_assets::materials::LightingBindings;
use crate::tidewater_material::{ObjectShadowSettings, SceneLighting};
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, Face, RenderPipelineDescriptor, ShaderType},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};
use forge_assets::tidewater_fish_props::FishPropPackage;

pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("7120d94c-faa1-42dc-8154-eb2b2be1e360");

#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct Settings {
    /// Pattern, fractional seed, source length and surface time.
    pub info: Vec4,
    /// Cloudy eye, wetness, dried skin and blood.
    pub flags: Vec4,
    /// Fade, outgoing marker and reserved components.
    pub lod: Vec4,
    pub frame_index: u32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct Key {
    pub shared_scene_lighting: bool,
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, PartialEq)]
#[bind_group_data(Key)]
pub struct FishMaterial {
    /// Neutral keeps per-material time; a caller may opt into its shared live clock.
    #[storage(56, read_only)]
    pub clock: Handle<ShaderBuffer>,
    #[uniform(0)]
    pub settings: Settings,
    #[storage(19, read_only)]
    pub skin: Handle<ShaderBuffer>,
    #[storage(55, read_only)]
    pub scene_buffer: Handle<ShaderBuffer>,
    pub shared_scene_lighting: bool,
    #[storage(54, read_only)]
    pub local_lights: Handle<ShaderBuffer>,
    #[uniform(18)]
    pub scene_lighting: SceneLighting,
    #[texture(10, dimension = "cube")]
    #[sampler(11)]
    pub environment: Handle<Image>,
    #[texture(12)]
    pub hill_shadow: Handle<Image>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    pub object_shadow: Handle<Image>,
    #[uniform(17)]
    pub object_settings: ObjectShadowSettings,
    #[storage(44, read_only)]
    pub live_shadows: Handle<ShaderBuffer>,
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    pub cloud_shadow: Option<Handle<Image>>,
    #[uniform(48)]
    pub underwater_params: crate::tidewater_caustics::CausticSampleParams,
    #[texture(49, dimension = "2d_array")]
    #[sampler(47)]
    pub underwater_level: Handle<Image>,
    #[storage(53, read_only)]
    pub underwater_map_frame: Handle<ShaderBuffer>,
    #[texture(50, filterable = false)]
    pub ground_bounce: Option<Handle<Image>>,
    #[uniform(51)]
    pub bounce_settings: Vec4,
}

impl From<&FishMaterial> for Key {
    fn from(material: &FishMaterial) -> Self {
        Self {
            shared_scene_lighting: material.shared_scene_lighting,
        }
    }
}

impl FishMaterial {
    /// Follow the scene's resident receivers without replacing fish skin or LOD.
    ///
    /// # Errors
    /// Rejects invalid scene lighting before changing any receiver binding.
    pub fn update_scene_receivers(
        &mut self,
        lighting: &crate::tidewater_material::VillageMaterial,
    ) -> Result<(), String> {
        SceneLighting::new(
            lighting.scene_lighting.sun_direction.truncate().to_array(),
            lighting.scene_lighting.sun_color.truncate().to_array(),
            lighting
                .scene_lighting
                .environment_sh
                .map(|value| value.truncate().to_array()),
        )?;
        self.scene_lighting = lighting.scene_lighting.clone();
        self.scene_buffer = lighting.scene_buffer.clone();
        self.shared_scene_lighting = lighting.shared_scene_lighting;
        self.local_lights = lighting.local_lights.clone();
        self.environment = lighting.environment.clone();
        self.hill_shadow = lighting.hill_shadow.clone();
        self.object_shadow = lighting.object_shadow.clone();
        self.object_settings = lighting.object_settings.clone();
        self.live_shadows = lighting.live_shadows.clone();
        self.clouds = lighting.clouds;
        self.cloud_shadow.clone_from(&lighting.cloud_shadow);
        self.underwater_params = lighting.underwater_params.clone();
        self.underwater_level = lighting.underwater_level.clone();
        self.underwater_map_frame = lighting.underwater_map_frame.clone();
        self.ground_bounce.clone_from(&lighting.ground_bounce);
        self.bounce_settings = lighting.bounce_settings;
        Ok(())
    }
}

impl Material for FishMaterial {
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
        // Tidewater's depth graph must consume the dedicated masked proxy.
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            Mesh::ATTRIBUTE_NORMAL.at_shader_location(1),
            REST_POSITION.at_shader_location(2),
            FISH_DATA.at_shader_location(3),
        ])?];
        if key.bind_group_data.shared_scene_lighting {
            let define = crate::tidewater_material::scene_buffer::receiver::DEFINE;
            descriptor.vertex.shader_defs.push(define.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(define.into());
            }
        }
        descriptor.primitive.cull_mode = Some(Face::Back);
        Ok(())
    }
}

/// Prepare the original instance parameters and use the scene's actual resources.
///
/// # Errors
/// Refuses invalid source parameters, flags, time or length before publication.
pub fn prepare(
    parameters: SurfaceParameters,
    skin: Handle<ShaderBuffer>,
    lighting: &LightingBindings,
    time: f32,
) -> Result<FishMaterial, String> {
    let settings = source_settings(parameters, time)?;
    Ok(FishMaterial {
        clock: super::clock::NEUTRAL.clone(),
        settings,
        skin,
        scene_buffer: crate::tidewater_material::scene_buffer::HANDLE.clone(),
        shared_scene_lighting: false,
        local_lights: crate::tidewater_material::local_lights::gpu::BUFFER.clone(),
        scene_lighting: lighting.scene_lighting.clone(),
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        live_shadows: crate::tidewater_object_shadow::receiver::DEFAULT_BUFFER.clone(),
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        underwater_params: lighting.caustics.params.clone(),
        underwater_level: lighting.caustics.light_maps.level.clone(),
        underwater_map_frame: lighting.caustics.light_maps.frame.clone(),
        ground_bounce: lighting.ground_bounce.clone(),
        bounce_settings: lighting.bounce_settings,
    })
}

/// Prepare directly from the real scene material, including dynamic receivers.
///
/// # Errors
/// Rejects invalid source surface parameters or scene lighting before publication.
pub fn prepare_scene(
    parameters: SurfaceParameters,
    skin: Handle<ShaderBuffer>,
    lighting: &crate::tidewater_material::VillageMaterial,
    time: f32,
) -> Result<FishMaterial, String> {
    let settings = source_settings(parameters, time)?;
    SceneLighting::new(
        lighting.scene_lighting.sun_direction.truncate().to_array(),
        lighting.scene_lighting.sun_color.truncate().to_array(),
        lighting
            .scene_lighting
            .environment_sh
            .map(|value| value.truncate().to_array()),
    )?;
    Ok(FishMaterial {
        clock: super::clock::NEUTRAL.clone(),
        settings,
        skin,
        scene_buffer: lighting.scene_buffer.clone(),
        shared_scene_lighting: lighting.shared_scene_lighting,
        local_lights: lighting.local_lights.clone(),
        scene_lighting: lighting.scene_lighting.clone(),
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        live_shadows: lighting.live_shadows.clone(),
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        underwater_params: lighting.underwater_params.clone(),
        underwater_level: lighting.underwater_level.clone(),
        underwater_map_frame: lighting.underwater_map_frame.clone(),
        ground_bounce: lighting.ground_bounce.clone(),
        bounce_settings: lighting.bounce_settings,
    })
}

fn source_settings(parameters: SurfaceParameters, time: f32) -> Result<Settings, String> {
    let pattern = parameters.pattern_and_seed.floor();
    if !parameters.pattern_and_seed.is_finite()
        || !(0.0..21.0).contains(&pattern)
        || !parameters.source_length_m.is_finite()
        || parameters.source_length_m <= 0.0
        || !time.is_finite()
        || parameters.flags.iter().any(|value| !value.is_finite())
    {
        return Err("Invalid fish material parameters".into());
    }
    Ok(Settings {
        info: Vec4::new(
            pattern,
            parameters.pattern_and_seed.fract(),
            parameters.source_length_m,
            time,
        ),
        flags: Vec4::from_array(parameters.flags),
        lod: Vec4::X,
        frame_index: 0,
    })
}

/// Build the source skin table as the exact 168 aligned vec4 storage rows.
///
/// # Errors
/// Refuses missing, malformed or non-finite package payloads.
pub fn skin_buffer(package: &FishPropPackage) -> Result<ShaderBuffer, String> {
    let bytes = package
        .file("material/skin-table-f32.bin")
        .ok_or("Fish skin table missing")?;
    if bytes.len() != 168 * 16 {
        return Err("Fish skin table width differs".into());
    }
    let values = bytes
        .as_chunks::<16>()
        .0
        .iter()
        .map(|row| {
            let mut values = [0.0; 4];
            for (value, bytes) in values.iter_mut().zip(row.as_chunks::<4>().0) {
                *value = f32::from_le_bytes(*bytes);
            }
            Vec4::from_array(values)
        })
        .collect::<Vec<_>>();
    if values.iter().any(|row| !row.is_finite()) {
        return Err("Non-finite fish skin table".into());
    }
    Ok(ShaderBuffer::from(values))
}

/// Stage a verified surface with the scene's common lighting shader closure.
/// The lighting source must come from the caller's verified scene package.
///
/// # Errors
/// Refuses shader seam drift or absent GPU asset resources.
pub fn stage_shader(
    world: &mut World,
    package: &FishPropPackage,
    lighting_source: &str,
    mode: surface::SpecularMode,
) -> Result<(), String> {
    let source = shader_source(package, lighting_source, mode)?;
    crate::tidewater_material::install_scene_receivers(world)?;
    super::clock::install_neutral(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Fish shader assets unavailable")?
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "tidewater-fish.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

/// Assemble the dedicated vertex and fragment adapter without publishing resources.
///
/// # Errors
/// Refuses missing source lighting seams and duplicate shader helpers.
pub fn shader_source(
    package: &FishPropPackage,
    lighting_source: &str,
    mode: surface::SpecularMode,
) -> Result<String, String> {
    if lighting_source.len() > 1_000_000
        || lighting_source.matches("struct LightingSurface {").count() != 1
    {
        return Err("Fish material requires verified scene lighting closure".into());
    }
    let start = lighting_source
        .find("struct LightingSurface {")
        .ok_or("Scene lighting absent")?;
    let tail = &lighting_source[start..];
    let noise = surface::extract_function(tail, "interleavedGradientNoise")?;
    let tail = tail.replacen(noise, "", 1);
    let tail = crate::tidewater_material::scene_shader_source(&tail)?;
    let surface = surface::shader_library(package, mode)?;
    let fade = std::str::from_utf8(
        package
            .file("material/module-1-lodFade.wgsl")
            .ok_or("Fish LOD shader missing")?,
    )
    .map_err(|error| error.to_string())?;
    Ok(format!(
        "{}\n{surface}\n{fade}\n{tail}\n{}",
        include_str!("material_vertex.wgsl"),
        include_str!("material_fragment.wgsl")
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    fn package() -> FishPropPackage {
        let root =
            std::path::PathBuf::from(std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").unwrap());
        FishPropPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            forge_assets::tidewater_fish_props::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap()
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn skin_storage_matches_all_acquired_rows_exactly() {
        let package = package();
        let buffer = skin_buffer(&package).unwrap();
        assert_eq!(
            buffer.data.as_deref(),
            package.file("material/skin-table-f32.bin")
        );
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn every_source_instance_keeps_surface_parameters_and_scene_bindings() {
        let package = package();
        let prepared = super::super::PreparedFishProps::from_package(&package).unwrap();
        let mut images = Assets::<Image>::default();
        let lighting = LightingBindings {
            scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [3.0; 3], [[0.4; 3]; 9]).unwrap(),
            environment: Handle::default(),
            hill_shadow: Handle::default(),
            object_shadow: Handle::default(),
            object_settings: ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            wind: Vec4::ZERO,
            flags: Vec4::ZERO,
            caustics: crate::tidewater_caustics::CausticMaterialInputs::disabled(&mut images),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        };
        for template in prepared.templates {
            let mut material =
                prepare(template.surface, Handle::default(), &lighting, 0.625).unwrap();
            assert_eq!(
                material.settings.flags.to_array().map(f32::to_bits),
                template.surface.flags.map(f32::to_bits)
            );
            assert_eq!(
                material.settings.info.to_array().map(f32::to_bits),
                [
                    template.surface.pattern_and_seed.floor(),
                    template.surface.pattern_and_seed.fract(),
                    template.surface.source_length_m,
                    0.625,
                ]
                .map(f32::to_bits)
            );
            assert_eq!(
                material.underwater_level,
                lighting.caustics.light_maps.level
            );
            assert_eq!(
                material.underwater_map_frame,
                lighting.caustics.light_maps.frame
            );
            assert_eq!(material.environment, lighting.environment);
            let mut scene = crate::tidewater_assets::materials::prepare(
                &crate::tidewater_assets::MaterialParameters {
                    kind: 0,
                    textures: vec!["fixture".into()],
                    color: [1.0; 4],
                    emissive: [0.0; 4],
                    surface: [0.0; 4],
                },
                &std::collections::BTreeMap::from([("fixture".into(), Handle::default())]),
                &lighting,
            )
            .unwrap();
            scene.shared_scene_lighting = true;
            scene.scene_lighting =
                SceneLighting::new([1.0, 0.0, 0.0], [0.125; 3], [[0.05; 3]; 9]).unwrap();
            scene.environment = images.add(Image::default());
            scene.object_settings.frame_index = 91;
            material.settings.lod = Vec4::new(0.35, 1.0, 0.0, 0.0);
            let info = material.settings.info;
            let flags = material.settings.flags;
            let lod = material.settings.lod;
            let skin = material.skin.clone();
            material.update_scene_receivers(&scene).unwrap();
            let direct = prepare_scene(template.surface, skin.clone(), &scene, 0.625).unwrap();
            assert!(direct.shared_scene_lighting);
            assert_eq!(direct.environment, scene.environment);
            assert_eq!(direct.settings.info, info);
            assert_eq!(direct.settings.flags, flags);
            assert!(material.shared_scene_lighting);
            assert_eq!(material.environment, scene.environment);
            assert_eq!(
                material.scene_lighting.sun_color,
                scene.scene_lighting.sun_color
            );
            assert_eq!(material.object_settings.frame_index, 91);
            assert_eq!(material.settings.info, info);
            assert_eq!(material.settings.flags, flags);
            assert_eq!(material.settings.lod, lod);
            assert_eq!(material.skin, skin);
            scene.environment = images.add(Image::default());
            scene.scene_lighting.sun_color.x = f32::NAN;
            let previous_environment = material.environment.clone();
            let previous_light = material.scene_lighting.sun_color;
            assert!(material.update_scene_receivers(&scene).is_err());
            assert!(prepare_scene(template.surface, skin.clone(), &scene, 0.625).is_err());
            assert_eq!(material.environment, previous_environment);
            assert_eq!(material.scene_lighting.sun_color, previous_light);
            assert!(prepare(template.surface, Handle::default(), &lighting, f32::NAN).is_err());
        }
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn full_material_assembly_retains_surface_and_common_receivers() {
        let package = package();
        let lighting = std::fs::read_to_string(
            std::env::var("FORGE_TIDEWATER_SCENE_LIGHTING_SOURCE").unwrap(),
        )
        .unwrap();
        let shader =
            shader_source(&package, &lighting, surface::SpecularMode::ThreeReference).unwrap();
        let body =
            std::str::from_utf8(package.file("material/main-surface.wgsl").unwrap()).unwrap();
        assert_eq!(shader.matches(body).count(), 1);
        for marker in [
            "fn fishSurface(",
            "fn fishSceneLight(",
            "fn sourceGroundBounce(",
            "fn villageLocalLights(",
            "fn currentShadowParams(",
            "FORGE_SHARED_SCENE_LIGHTING",
            "@binding(19)",
            "s.specularIntensity = 0.0;",
        ] {
            assert!(shader.contains(marker), "Missing receiver: {marker}");
        }
        assert!(shader_source(&package, "", surface::SpecularMode::Intended).is_err());
        if let Ok(output) = std::env::var("FORGE_FISH_MATERIAL_SHADER_OUTPUT") {
            let mut file = std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(output)
                .unwrap();
            file.write_all(shader.as_bytes()).unwrap();
        }
    }
}
