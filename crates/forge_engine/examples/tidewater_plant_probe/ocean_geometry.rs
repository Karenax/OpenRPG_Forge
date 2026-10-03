//! Original terrain-aware FFT displacement on the native ocean CDLOD grid.
//! White output measures geometry; this is not the optical water material.
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, TextureDimension,
        TextureFormat, VertexFormat,
    },
    shader::ShaderRef,
};
use forge_world::cdlod::{CdlodConfig, CdlodNode, CdlodRoot, select_nodes};
#[derive(Component)]
pub(super) struct OceanSurface;

fn config() -> CdlodConfig {
    CdlodConfig {
        grid_size: 32,
        leaf_size_m: 8.0,
        levels: 12,
        range_factor: 2.5,
        morph_start_ratio: 0.66,
        max_nodes: 1500,
    }
}

pub(super) fn view_nodes(
    transform: &Transform,
    projection: &PerspectiveProjection,
) -> Result<Vec<CdlodNode>, String> {
    select_nodes(
        config(),
        CdlodRoot::CameraFollowing,
        super::terrain_lod::source_view(transform, projection),
        |_, _| Ok([-25.0, 25.0]),
    )
    .map_err(|e| e.to_string())
}

#[allow(clippy::cast_possible_truncation)] // Bounded CDLOD source coordinates.
pub(super) fn node_mesh(nodes: &[CdlodNode]) -> Mesh {
    let grid = config().grid().expect("validated grid");
    let mut positions = vec![];
    let mut data = vec![];
    let mut indices = vec![];
    for node in nodes {
        let offset = u32::try_from(positions.len()).expect("bounded ocean vertices");
        positions.extend_from_slice(&grid.positions);
        data.extend(std::iter::repeat_n(
            [
                node.origin_xz[0] as f32,
                node.origin_xz[1] as f32,
                node.size_m as f32,
                f32::from(node.lod),
            ],
            grid.positions.len(),
        ));
        indices.extend(grid.indices.iter().map(|i| i + offset));
    }
    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
    .with_inserted_attribute(NODE, data)
    .with_inserted_indices(Indices::U32(indices))
}
const GEOMETRY_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("5b5af56a-2eeb-45fa-b7f2-26f3e85fcd21");
pub(super) const OPTICS_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("6f2a6373-301f-4918-862a-d234f3240578");
pub(super) const UNDERWATER_SURFACE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("4d4b4a40-9aac-45d4-b880-9f49238efcef");

pub fn install_shaders(
    app: &mut App,
    files: &std::collections::BTreeMap<String, Vec<u8>>,
    document: &forge_project::TerrainDocument,
) -> Result<(), Box<dyn std::error::Error>> {
    let corrections = crate::terrain_edits::Corrections::from_document(document)?;
    for (name, handle) in [
        ("ocean-geometry.wgsl", GEOMETRY_SHADER),
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
            let adapted = forge_engine::tidewater_object_shadow::receiver::shader_source(&adapted)?;
            let adapted = if name == "water-optics.wgsl" {
                forge_engine::tidewater_refraction::water_shader_source(
                    &encode_visible_water_mask(&adapted)?,
                )?
            } else {
                adapted
            };
            forge_engine::tidewater_object_shadow::receiver::install(app.world_mut())?;
            if name == "water-optics.wgsl" {
                forge_engine::tidewater_underwater_lighting::install_shader(
                    app.world_mut(),
                    &adapted,
                )?;
                let source =
                    forge_engine::tidewater_underwater::surface_query_shader_source(&adapted)?;
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

// Bevy's main HDR target has one color attachment. Preserve Tidewater's
// visible-surface mask in its otherwise unused alpha channel until the
// underwater pass consumes it immediately after the main pass. The water
// material already computes the source RG mask from the actual displaced
// mesh, including folded faces and shore waves.
fn encode_visible_water_mask(source: &str) -> Result<String, String> {
    forge_engine::tidewater_underwater::visible_water_mask_shader_source(source)
}
const NODE: MeshVertexAttribute =
    MeshVertexAttribute::new("OceanLodNode", 0x7457_0005, VertexFormat::Float32x4);
#[derive(Resource)]
pub struct Enabled(pub u32);
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct OceanGeometryMaterial {
    #[uniform(0)]
    pub(super) morph: [Vec4; 12],
    #[uniform(1)]
    sizes: [Vec4; 4],
    #[uniform(2)]
    pub(super) surface: [Vec4; 2],
    #[texture(3, filterable = false)]
    pub(super) height: Handle<Image>,
    #[texture(50, filterable = false)]
    pub(super) corrections: Handle<Image>,
    #[texture(4, dimension = "2d_array")]
    #[sampler(5)]
    displacement: Handle<Image>,
    #[texture(6, sample_type = "depth")]
    scene_depth: Handle<Image>,
    #[uniform(7)]
    diagnostic: UVec4,
}
impl Material for OceanGeometryMaterial {
    fn reads_view_transmission_texture(&self) -> bool {
        true
    }
    fn vertex_shader() -> ShaderRef {
        GEOMETRY_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        GEOMETRY_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        _: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            NODE.at_shader_location(1),
        ])?];
        descriptor.primitive.cull_mode = None;
        Ok(())
    }
}
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
    mut materials: ResMut<Assets<OceanGeometryMaterial>>,
) {
    let Some(enabled) = enabled else {
        return;
    };
    let ocean = ocean.expect("ocean graph installed");
    let config = config();
    let base = Vec3::new(probe.instance.x, probe.instance.y, probe.instance.z);
    let transform = super::camera_transform(&probe, base);
    let Projection::Perspective(projection) = super::camera_projection(&probe) else {
        unreachable!()
    };
    let nodes = select_nodes(
        config,
        CdlodRoot::CameraFollowing,
        super::terrain_lod::source_view(&transform, &projection),
        |_, _| Ok([-25.0, 25.0]),
    )
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
    let mesh = node_mesh(&nodes);
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
            width: edit.side,
            height: edit.side,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        edit.bytes,
        TextureFormat::R32Float,
        RenderAssetUsages::default(),
    ));
    let material = materials.add(OceanGeometryMaterial {
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
