//! Original terrain-aware FFT displacement on the native ocean CDLOD grid.
//! White output measures geometry; this is not the optical water material.
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
#[derive(Component)]
pub(super) struct OceanSurface;
pub(super) const OPTICS_SHADER: Handle<Shader> = forge_engine::source_water_optics::SHADER;
pub(super) const UNDERWATER_SURFACE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("4d4b4a40-9aac-45d4-b880-9f49238efcef");

pub fn install_shaders(
    app: &mut App,
    files: &std::collections::BTreeMap<String, Vec<u8>>,
    document: &forge_project::TerrainDocument,
) -> Result<(), Box<dyn std::error::Error>> {
    let corrections = crate::terrain_edits::Corrections::from_document(document)?;
    if !corrections.supports_fixed_source_profile() {
        return Err("Ocean source shaders require their admitted fixed raster profile".into());
    }
    for (name, handle) in [
        (
            "ocean-geometry.wgsl",
            forge_engine::source_ocean_geometry::DIAGNOSTIC_SHADER,
        ),
        ("water-optics.wgsl", OPTICS_SHADER),
    ] {
        if let Some(source) = files.get(name) {
            let original = std::str::from_utf8(source)?;
            // Preserve the original shader arithmetic after Undo, including signed
            // zero and compiler optimizations around shoreline intersections.
            let adapted = if document.height_deltas.is_empty() && corrections.elevation == 0.0 {
                original.to_owned()
            } else {
                corrections.shader(original, "#{MATERIAL_BIND_GROUP}", 50)?
            };
            let adapted = if name == "water-optics.wgsl" {
                forge_engine::source_water_optics::prepare_optical_shader(&adapted)?
            } else {
                forge_engine::source_shadows::receiver::shader_source(&adapted)?
            };
            forge_engine::source_shadows::receiver::install(app.world_mut())?;
            if name == "water-optics.wgsl" {
                forge_engine::source_underwater_lighting::install_shader(
                    app.world_mut(),
                    &adapted,
                )?;
                let source =
                    forge_engine::source_underwater::surface_query_shader_source(&adapted)?;
                app.world_mut().resource_mut::<Assets<Shader>>().insert(
                    UNDERWATER_SURFACE_SHADER.id(),
                    Shader::from_wgsl(source, "water-underwater-query.wgsl"),
                )?;
            }
            app.world_mut()
                .resource_mut::<Assets<Shader>>()
                .insert(handle.id(), Shader::from_wgsl(adapted, name))?;
        }
    }
    Ok(())
}

#[derive(Resource)]
pub struct Enabled(pub u32);
#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines, // Bounded mesh and texture setup on Bevy's device.
    clippy::cast_possible_truncation
)]
pub fn setup(
    mut commands: Commands,
    enabled: Option<Res<Enabled>>,
    probe: Res<super::Probe>,
    ocean: Option<Res<super::ocean::Targets>>,
    scene_depth: Option<Res<super::water_scene::SceneDepth>>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<forge_engine::source_ocean_geometry::DiagnosticMaterial>>,
) {
    let Some(enabled) = enabled else {
        return;
    };
    let ocean = ocean.expect("ocean graph installed");
    let config = forge_engine::source_ocean_geometry::config();
    let base = Vec3::new(probe.instance.x, probe.instance.y, probe.instance.z);
    let transform = super::camera_transform(&probe, base);
    let Projection::Perspective(projection) = super::camera_projection(&probe) else {
        unreachable!()
    };
    let nodes = forge_engine::source_ocean_geometry::view_nodes(super::terrain_lod::source_view(
        &transform,
        &projection,
    ))
    .expect("bounded ocean selection");
    println!("native ocean nodes: {}", nodes.len());
    println!(
        "native ocean selection {}",
        serde_json::json!(
            nodes
                .iter()
                .map(|n| [n.origin_xz[0], n.origin_xz[1], n.size_m, f64::from(n.lod)])
                .collect::<Vec<_>>()
        )
    );
    if nodes.is_empty() {
        return;
    }
    let mesh = forge_engine::source_ocean_geometry::node_mesh(&nodes).expect("bounded ocean mesh");
    let height = images.add(Image::new(
        Extent3d {
            width: 2048,
            height: 2048,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        probe.lod_heights.as_ref().expect("terrain heights").clone(),
        TextureFormat::R32Float,
        RenderAssetUsages::default(),
    ));
    let morph: Vec<_> = config
        .morph_rows()
        .expect("morph rows")
        .iter()
        .map(|r| Vec4::from_array(r.map(|v| v as f32)))
        .collect();
    let edit = crate::terrain_edits::Corrections::from_document(
        probe
            .terrain
            .as_ref()
            .expect("validated ocean terrain")
            .document(),
    )
    .expect("validated ocean terrain document");
    let corrections = images.add(Image::new(
        Extent3d {
            width: edit.width,
            height: edit.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        edit.bytes,
        TextureFormat::R32Float,
        RenderAssetUsages::default(),
    ));
    let material = materials.add(forge_engine::source_ocean_geometry::DiagnosticMaterial {
        morph: morph.try_into().expect("12 levels"),
        sizes: ocean.sizes,
        surface: [
            Vec4::new(1.0, 1.0, 1.0, 2.2),
            Vec4::new(0.09, 0.0, 0.0, 0.0),
        ],
        height,
        corrections,
        displacement: ocean.images[0].clone(),
        scene_depth: scene_depth.expect("water depth copy installed").0.clone(),
        diagnostic: UVec4::new(enabled.0, 0, 0, 0),
    });
    commands.spawn((
        OceanSurface,
        Mesh3d(meshes.add(mesh)),
        MeshMaterial3d(material),
        Transform::default(),
        NoFrustumCulling,
        NotShadowCaster,
    ));
}
