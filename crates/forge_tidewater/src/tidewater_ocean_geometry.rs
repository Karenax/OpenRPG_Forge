// SPDX-License-Identifier: MIT
// Adapted from Tidewater water surface and the pinned native Forge probe.
// Tidewater copyright 2026 DRG Software Solutions LLC; revision 1438b1ab.
//! Diagnostic source geometry for the resident ocean FFT. Optical shading is separate.
//! The white fragment is never enabled as the default Studio water presentation.
use crate::{tidewater_ocean, tidewater_terrain_edits::Corrections};
use bevy::{
    asset::{RenderAssetUsages, uuid_handle},
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
use forge_project::TerrainDocument;
use forge_tidewater_assets::tidewater_water_surface::WaterSurfacePackage;
use {
    forge_tidewater_assets::cdlod::{CdlodConfig, CdlodNode, CdlodRoot, CdlodView, select_nodes},
    forge_world::sampled_terrain::SampledTerrainPlan,
};

const SHADER: Handle<Shader> = uuid_handle!("8ac781a3-cf53-4d12-8975-14c350f608ad");
const NODE: MeshVertexAttribute =
    MeshVertexAttribute::new("OceanLodNode", 0x7457_0005, VertexFormat::Float32x4);

#[derive(Resource)]
struct Registered;

#[derive(Component)]
pub struct OceanGeometryDiagnostic;

#[derive(Resource)]
pub struct ResidentGeometry {
    pub entity: Entity,
    pub nodes: usize,
    selected: Vec<CdlodNode>,
}

/// Source shader and canonical relief, verified before GPU resources are changed.
pub struct PreparedGeometry {
    shader: String,
    height: Vec<u8>,
    corrections: Corrections,
    surface: [Vec4; 2],
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct GeometryMaterial {
    #[uniform(0)]
    pub(crate) morph: [Vec4; 12],
    #[uniform(1)]
    pub(crate) sizes: [Vec4; 4],
    #[uniform(2)]
    pub(crate) surface: [Vec4; 2],
    #[texture(3, filterable = false)]
    pub(crate) height: Handle<Image>,
    #[texture(50, filterable = false)]
    pub(crate) corrections: Handle<Image>,
    #[texture(4, dimension = "2d_array")]
    #[sampler(5)]
    pub(crate) displacement: Handle<Image>,
}

impl Material for GeometryMaterial {
    fn reads_view_transmission_texture(&self) -> bool {
        true
    }

    fn vertex_shader() -> ShaderRef {
        SHADER.into()
    }

    fn fragment_shader() -> ShaderRef {
        SHADER.into()
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

/// Register only for an explicitly requested geometry diagnostic.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>()
        || !app.world().contains_resource::<Assets<Shader>>()
    {
        return Err("Ocean geometry requires initialized Bevy assets".into());
    }
    app.add_plugins(MaterialPlugin::<GeometryMaterial>::default());
    app.insert_resource(Registered);
    Ok(())
}

impl PreparedGeometry {
    pub fn new(
        package: &WaterSurfacePackage,
        document: &TerrainDocument,
        height: &[u8],
    ) -> Result<Self, String> {
        SampledTerrainPlan::from_document(document, height).map_err(|e| e.to_string())?;
        if height.len() != 2048 * 2048 * 4 {
            return Err("Ocean geometry requires the source 2048-square height raster".into());
        }
        let corrections = Corrections::from_document(document)?;
        let source = std::str::from_utf8(
            package
                .file("ocean-geometry.wgsl")
                .ok_or("Pinned ocean geometry shader missing")?,
        )
        .map_err(|e| e.to_string())?;
        let shader = if document.height_deltas.is_empty() && corrections.elevation == 0.0 {
            source.to_owned()
        } else {
            corrections.shader(source, "#{MATERIAL_BIND_GROUP}", 50)?
        };
        let bytes = package
            .file("uniform-waterSurface.bin")
            .ok_or("Pinned water surface parameters missing")?;
        if bytes.len() != 32 {
            return Err("Invalid water surface parameter size".into());
        }
        let mut values = [0.0_f32; 8];
        for (value, chunk) in values.iter_mut().zip(bytes.as_chunks::<4>().0) {
            *value = f32::from_le_bytes(*chunk);
        }
        if !values.iter().all(|value| value.is_finite()) {
            return Err("Nonfinite water surface parameters".into());
        }
        Ok(Self {
            shader,
            height: height.to_vec(),
            corrections,
            surface: [
                Vec4::from_slice(&values[..4]),
                Vec4::from_slice(&values[4..]),
            ],
        })
    }

    /// Replace disposable geometry after all source inputs were validated.
    pub fn stage(self, world: &mut World, view: CdlodView) -> Result<ResidentGeometry, String> {
        if !world.contains_resource::<Registered>()
            || !world.contains_resource::<tidewater_ocean::Targets>()
        {
            return Err("Ocean geometry or FFT renderer not registered".into());
        }
        let nodes = view_nodes(view)?;
        let mesh = node_mesh(&nodes)?;
        world
            .resource_mut::<Assets<Shader>>()
            .insert(
                SHADER.id(),
                Shader::from_wgsl(self.shader, "tidewater-ocean-geometry.wgsl"),
            )
            .map_err(|e| e.to_string())?;
        let height = world.resource_mut::<Assets<Image>>().add(Image::new(
            Extent3d {
                width: 2048,
                height: 2048,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            self.height,
            TextureFormat::R32Float,
            RenderAssetUsages::default(),
        ));
        let corrections = world.resource_mut::<Assets<Image>>().add(Image::new(
            Extent3d {
                width: self.corrections.side,
                height: self.corrections.side,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            self.corrections.bytes,
            TextureFormat::R32Float,
            RenderAssetUsages::default(),
        ));
        let ocean = world.resource::<tidewater_ocean::Targets>().clone();
        #[allow(
            clippy::cast_possible_truncation,
            reason = "Validated source morph rows are uploaded as shader float32 values."
        )]
        let morph = config()
            .morph_rows()
            .map_err(|e| e.to_string())?
            .into_iter()
            .map(|row| Vec4::from_array(row.map(|value| value as f32)))
            .collect::<Vec<_>>()
            .try_into()
            .map_err(|_| "Invalid ocean morph row count")?;
        let material = world
            .resource_mut::<Assets<GeometryMaterial>>()
            .add(GeometryMaterial {
                morph,
                sizes: ocean.sizes,
                surface: self.surface,
                height,
                corrections,
                displacement: ocean.images[0].clone(),
            });
        let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
        let entity = world
            .spawn((
                OceanGeometryDiagnostic,
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Transform::default(),
                NoFrustumCulling,
                NotShadowCaster,
            ))
            .id();
        if let Some(previous) = world.remove_resource::<ResidentGeometry>() {
            world.despawn(previous.entity);
        }
        let resident = ResidentGeometry {
            entity,
            nodes: nodes.len(),
            selected: nodes.clone(),
        };
        world.insert_resource(ResidentGeometry {
            entity,
            nodes: resident.nodes,
            selected: nodes,
        });
        Ok(resident)
    }
}

pub fn update_view(world: &mut World, view: CdlodView) -> Result<usize, String> {
    let resident = world
        .get_resource::<ResidentGeometry>()
        .ok_or("Ocean geometry not staged")?;
    let entity = resident.entity;
    let nodes = view_nodes(view)?;
    if forge_tidewater_assets::cdlod::same_node_geometry(&resident.selected, &nodes) {
        return Ok(nodes.len());
    }
    if nodes.is_empty() {
        world.entity_mut(entity).insert(Visibility::Hidden);
    } else {
        let mesh = world.resource_mut::<Assets<Mesh>>().add(node_mesh(&nodes)?);
        world
            .entity_mut(entity)
            .insert((Mesh3d(mesh), Visibility::Inherited));
    }
    let count = nodes.len();
    let mut resident = world.resource_mut::<ResidentGeometry>();
    resident.nodes = count;
    resident.selected = nodes;
    Ok(count)
}

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

fn view_nodes(view: CdlodView) -> Result<Vec<CdlodNode>, String> {
    select_nodes(config(), CdlodRoot::CameraFollowing, view, |_, _| {
        Ok([-25.0, 25.0])
    })
    .map_err(|e| e.to_string())
}

#[allow(clippy::cast_possible_truncation)] // Source CDLOD nodes are bounded by the config.
fn node_mesh(nodes: &[CdlodNode]) -> Result<Mesh, String> {
    let grid = config().grid().map_err(|e| e.to_string())?;
    let mut positions = Vec::new();
    let mut data = Vec::new();
    let mut indices = Vec::new();
    for node in nodes {
        let offset = u32::try_from(positions.len()).map_err(|_| "Ocean mesh vertex limit")?;
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
        indices.extend(grid.indices.iter().map(|index| index + offset));
    }
    Ok(Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
    .with_inserted_attribute(NODE, data)
    .with_inserted_indices(Indices::U32(indices)))
}
