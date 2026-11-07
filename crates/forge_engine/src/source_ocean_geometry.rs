// SPDX-License-Identifier: MIT
// Adapted from Tidewater water surface and the pinned native Forge probe.
// Tidewater copyright 2026 DRG Software Solutions LLC; revision 1438b1ab.
//! Diagnostic source geometry for the resident ocean FFT. Optical shading is separate.
//! The white fragment is never enabled as the default Studio water presentation.
use crate::sampled_terrain_corrections::Corrections;
use crate::source_ocean;
use crate::source_terrain::resident_tiles::{PreparedTiles, ResidentTiles, TileAdmission};
use bevy::{
    asset::{RenderAssetUsages, uuid_handle},
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{Material, MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, TextureDimension,
        TextureFormat, VertexFormat,
    },
    shader::ShaderRef,
};
use forge_project::TerrainDocument;
use {
    forge_world::sampled_terrain::SampledTerrainPlan,
    forge_world::terrain_lod::{
        CdlodConfig, CdlodGrid, CdlodNode, CdlodRoot, CdlodView, select_nodes,
    },
};

const SHADER: Handle<Shader> = uuid_handle!("8ac781a3-cf53-4d12-8975-14c350f608ad");
const WATER_LEVEL_ASSIGNMENT: &str = "frame.seaLevel=0.0;";
/// Shader handle for the optional geometry/depth diagnostic material.
pub const DIAGNOSTIC_SHADER: Handle<Shader> = uuid_handle!("5b5af56a-2eeb-45fa-b7f2-26f3e85fcd21");
pub const NODE: MeshVertexAttribute =
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
    tiles: ResidentTiles,
    tiled: bool,
}

/// Result of selecting an ocean view through the shared resident geometry.
pub struct ViewUpdate {
    pub nodes: usize,
    pub changed: bool,
}

/// Adopt an existing optical ocean surface without replacing its mesh or material.
/// An already owned surface may be adopted again, but another owner is refused.
pub fn adopt_optical_surface(world: &mut World, entity: Entity) -> Result<(), String> {
    if world.contains_resource::<crate::source_water_host::Status>() {
        return Err("Ocean geometry belongs to a water host".into());
    }
    if world.get::<Mesh3d>(entity).is_none()
        || world
            .get::<MeshMaterial3d<crate::source_water_optics::OpticalMaterial>>(entity)
            .is_none()
    {
        return Err("Ocean surface requires an optical material and mesh".into());
    }
    if let Some(resident) = world.get_resource::<ResidentGeometry>() {
        return if resident.entity == entity {
            Ok(())
        } else {
            Err("Ocean geometry already has another owner".into())
        };
    }
    world.insert_resource(ResidentGeometry {
        entity,
        nodes: 0,
        selected: Vec::new(),
        tiles: ResidentTiles::default(),
        tiled: false,
    });
    Ok(())
}

/// Current resident tile entities, for diagnostics of the shared ocean host.
#[must_use]
pub fn tile_entities(world: &World) -> Vec<Entity> {
    world
        .get_resource::<ResidentGeometry>()
        .map_or_else(Vec::new, |geometry| geometry.tiles.entities())
}

/// Active and retained tile counts, for diagnostics of the shared ocean host.
#[must_use]
pub fn tile_counts(world: &World) -> [usize; 2] {
    world
        .get_resource::<ResidentGeometry>()
        .map_or([0, 0], |geometry| geometry.tiles.counts())
}

/// Source shader and canonical relief, verified before GPU resources are changed.
pub struct PreparedGeometry {
    shader: String,
    height: Vec<u8>,
    height_dimensions: [u32; 2],
    corrections: Corrections,
    surface: [Vec4; 2],
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct GeometryMaterial {
    #[uniform(0)]
    pub morph: [Vec4; 12],
    #[uniform(1)]
    pub(crate) sizes: [Vec4; 4],
    #[uniform(2)]
    pub surface: [Vec4; 2],
    #[texture(3, filterable = false)]
    pub height: Handle<Image>,
    #[texture(50, filterable = false)]
    pub corrections: Handle<Image>,
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

/// Optional source geometry diagnostic with scene-depth inspection.
/// The normal optical water material remains responsible for presentation.
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct DiagnosticMaterial {
    #[uniform(0)]
    pub morph: [Vec4; 12],
    #[uniform(1)]
    pub sizes: [Vec4; 4],
    #[uniform(2)]
    pub surface: [Vec4; 2],
    #[texture(3, filterable = false)]
    pub height: Handle<Image>,
    #[texture(50, filterable = false)]
    pub corrections: Handle<Image>,
    #[texture(4, dimension = "2d_array")]
    #[sampler(5)]
    pub displacement: Handle<Image>,
    #[texture(6, sample_type = "depth")]
    pub scene_depth: Handle<Image>,
    #[uniform(7)]
    pub diagnostic: UVec4,
}

impl Material for DiagnosticMaterial {
    fn reads_view_transmission_texture(&self) -> bool {
        true
    }
    fn vertex_shader() -> ShaderRef {
        DIAGNOSTIC_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        DIAGNOSTIC_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        pipeline: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        <GeometryMaterial as Material>::specialize(
            pipeline,
            descriptor,
            layout,
            MaterialPipelineKey {
                mesh_key: key.mesh_key,
                bind_group_data: (),
            },
        )
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
    /// Keep the original source profile for consumers which have not admitted
    /// the remaining water resources for another terrain.
    pub fn new(
        shader: &[u8],
        surface_parameters: &[u8],
        document: &TerrainDocument,
        height: &[u8],
    ) -> Result<Self, String> {
        let plan =
            SampledTerrainPlan::from_document(document, height).map_err(|e| e.to_string())?;
        validate_terrain_domain(plan.raster().source())?;
        Self::prepare(
            shader,
            surface_parameters,
            document,
            height,
            &plan,
            0.0,
            false,
        )
    }

    /// Prepare geometry against the zone's admitted raster and water level.
    /// Other water hosts must independently admit their own resources and domain.
    pub fn new_for_terrain(
        shader: &[u8],
        surface_parameters: &[u8],
        plan: &SampledTerrainPlan,
        height: &[u8],
        water_level_m: f64,
    ) -> Result<Self, String> {
        let raster = plan.raster().source();
        if height.len() != raster.byte_length().map_err(|error| error.to_string())?
            || forge_core::bytes_sha256(height) != raster.samples_sha256
        {
            return Err("Ocean height input differs from the admitted terrain".into());
        }
        Self::prepare(
            shader,
            surface_parameters,
            plan.document(),
            height,
            plan,
            water_level_m,
            true,
        )
    }

    fn prepare(
        shader: &[u8],
        surface_parameters: &[u8],
        document: &TerrainDocument,
        height: &[u8],
        plan: &SampledTerrainPlan,
        water_level_m: f64,
        require_water_level: bool,
    ) -> Result<Self, String> {
        #[allow(
            clippy::cast_possible_truncation,
            reason = "The renderer stores the validated water level as float32."
        )]
        let water_level = water_level_m as f32;
        if !water_level_m.is_finite()
            || !water_level.is_finite()
            || (water_level_m != 0.0 && water_level == 0.0)
        {
            return Err("Invalid ocean water level".into());
        }
        let corrections = Corrections::from_document(document)?;
        if shader.is_empty() || shader.len() > 64 * 1024 {
            return Err("Invalid ocean geometry shader size".into());
        }
        let source = std::str::from_utf8(shader).map_err(|e| e.to_string())?;
        if source.trim().is_empty() {
            return Err("Empty ocean geometry shader".into());
        }
        let source = if crate::source_terrain::supports_fixed_source_raster(plan.raster().source())
        {
            source.to_owned()
        } else {
            crate::source_terrain::sampling::prepare_shader(source, document)?
        };
        if (require_water_level || water_level != 0.0)
            && source.matches(WATER_LEVEL_ASSIGNMENT).count() != 1
        {
            return Err("Expected one source ocean water-level assignment".into());
        }
        let source = if water_level == 0.0 {
            source
        } else {
            source.replacen(
                WATER_LEVEL_ASSIGNMENT,
                &format!("frame.seaLevel={water_level:?};"),
                1,
            )
        };
        let shader = if document.height_deltas.is_empty() && corrections.elevation == 0.0 {
            source
        } else {
            corrections.shader(&source, "#{MATERIAL_BIND_GROUP}", 50)?
        };
        let bytes = surface_parameters;
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
            height_dimensions: [plan.raster().source().width, plan.raster().source().height],
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
            || !world.contains_resource::<source_ocean::Targets>()
        {
            return Err("Ocean geometry or FFT renderer not registered".into());
        }
        let nodes = view_nodes(view)?;
        let mesh = node_mesh(&nodes)?;
        world
            .resource_mut::<Assets<Shader>>()
            .insert(
                SHADER.id(),
                Shader::from_wgsl(self.shader, "source-ocean-geometry.wgsl"),
            )
            .map_err(|e| e.to_string())?;
        let height = world.resource_mut::<Assets<Image>>().add(Image::new(
            Extent3d {
                width: self.height_dimensions[0],
                height: self.height_dimensions[1],
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            self.height,
            TextureFormat::R32Float,
            RenderAssetUsages::default(),
        ));
        let corrections = world.resource_mut::<Assets<Image>>().add(Image::new(
            Extent3d {
                width: self.corrections.width,
                height: self.corrections.height,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            self.corrections.bytes,
            TextureFormat::R32Float,
            RenderAssetUsages::default(),
        ));
        let ocean = world.resource::<source_ocean::Targets>().clone();
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
            tiles: ResidentTiles::default(),
            tiled: false,
        };
        world.insert_resource(ResidentGeometry {
            entity,
            nodes: resident.nodes,
            selected: nodes,
            tiles: ResidentTiles::default(),
            tiled: false,
        });
        Ok(resident)
    }
}

/// Check the raster frame used by the source ocean and shore shaders.
pub fn validate_terrain_domain(
    terrain: &forge_project::sampled_terrain::SampledTerrainSource,
) -> Result<(), String> {
    if !crate::source_terrain::supports_fixed_source_raster(terrain) {
        return Err("Ocean geometry requires the source centred unit-grid terrain domain".into());
    }
    Ok(())
}

/// Select source nodes through the same bounded tile cache used by the reference
/// walk. Oversized selections retain the existing combined-mesh path because a
/// renderer inventory admits at most 512 meshes per material family.
pub fn update_view(world: &mut World, view: CdlodView) -> Result<usize, String> {
    Ok(update_view_observed(world, view)?.nodes)
}

/// Update the shared selection and report whether its geometry was changed.
pub fn update_view_observed(world: &mut World, view: CdlodView) -> Result<ViewUpdate, String> {
    let nodes = view_nodes(view)?;
    let resident = world
        .get_resource::<ResidentGeometry>()
        .ok_or("Ocean geometry not staged")?;
    if resident.tiled == (nodes.len() <= crate::render_inventory::MAX_SCOPED_MESHES)
        && forge_world::terrain_lod::same_node_geometry(&resident.selected, &nodes)
    {
        return Ok(ViewUpdate {
            nodes: nodes.len(),
            changed: false,
        });
    }
    let entity = resident.entity;
    let grid = config().grid().map_err(|error| error.to_string())?;
    let count = if world
        .get::<MeshMaterial3d<crate::source_water_optics::OpticalMaterial>>(entity)
        .is_some()
    {
        project_nodes::<crate::source_water_optics::OpticalMaterial>(world, &nodes, &grid)
    } else if world
        .get::<MeshMaterial3d<GeometryMaterial>>(entity)
        .is_some()
    {
        project_nodes::<GeometryMaterial>(world, &nodes, &grid)
    } else {
        Err("Ocean geometry has no owned surface material".into())
    }?;
    Ok(ViewUpdate {
        nodes: count,
        changed: true,
    })
}

/// Prepare a possible future source-ocean view without changing its current
/// entities. The existing typed material and bounded CDLOD cache are reused.
pub fn prepare_view<M: Material>(
    world: &mut World,
    view: CdlodView,
) -> Result<PreparedTiles<M>, String> {
    prepare_views(world, std::slice::from_ref(&view))
}

/// Prepare the union of bounded possible ocean views without publishing them.
pub fn prepare_views<M: Material>(
    world: &mut World,
    views: &[CdlodView],
) -> Result<PreparedTiles<M>, String> {
    prepare_views_inner::<M>(world, views, None)?.ok_or("Ocean selection was not prepared".into())
}

/// Prepare a bounded batch of missing tiles from possible future views.
/// Returns `None` when every tile is resident or the renderer budget is full.
pub fn prepare_missing_views<M: Material>(
    world: &mut World,
    views: &[CdlodView],
    maximum_new: usize,
) -> Result<Option<PreparedTiles<M>>, String> {
    prepare_views_inner::<M>(world, views, Some(maximum_new))
}

fn prepare_views_inner<M: Material>(
    world: &mut World,
    views: &[CdlodView],
    maximum_new: Option<usize>,
) -> Result<Option<PreparedTiles<M>>, String> {
    if views.is_empty() || views.len() > 32 {
        return Err("Ocean preparation requires one to 32 views".into());
    }
    require_tiled_owner(world)?;
    let selections = views
        .iter()
        .map(|view| view_nodes(*view))
        .collect::<Result<Vec<_>, _>>()?;
    let nodes = crate::source_terrain::resident_tiles::merge_selections(
        selections,
        crate::render_inventory::MAX_SCOPED_MESHES,
    )?;
    let grid = config().grid().map_err(|error| error.to_string())?;
    world.resource_scope(|world, resident: Mut<ResidentGeometry>| {
        let build = |node: &CdlodNode| tile_mesh(node, &grid);
        if let Some(maximum_new) = maximum_new {
            resident.tiles.prepare_missing::<M>(
                world,
                resident.entity,
                &nodes,
                crate::render_inventory::MAX_SCOPED_MESHES,
                maximum_new,
                build,
            )
        } else {
            resident
                .tiles
                .prepare::<M>(
                    world,
                    resident.entity,
                    &nodes,
                    crate::render_inventory::MAX_SCOPED_MESHES,
                    build,
                )
                .map(Some)
        }
    })
}

/// Retain a GPU-admitted future ocean selection while keeping the current view.
/// A later publication still requires its own renderer completion.
pub fn retain_prepared<M: Material>(
    world: &mut World,
    candidate: &PreparedTiles<M>,
    token: &crate::render_inventory::RenderInventoryToken,
    admission: &TileAdmission,
) -> Result<usize, String> {
    require_tiled_owner(world)?;
    world.resource_scope(|world, mut resident: Mut<ResidentGeometry>| {
        resident
            .tiles
            .retain_prepared(world, candidate, token, admission)
    })
}

fn require_tiled_owner(world: &World) -> Result<(), String> {
    let resident = world
        .get_resource::<ResidentGeometry>()
        .ok_or("Ocean geometry not staged")?;
    if !resident.tiled || !resident.tiles.owns(world, resident.entity) {
        return Err("Ocean tile cache is not current".into());
    }
    if world.contains_resource::<crate::source_water_host::Status>()
        && !crate::source_water_host::owns(world, resident.entity)
    {
        return Err("Ocean geometry does not belong to the current water host".into());
    }
    Ok(())
}

fn project_nodes<M: Material>(
    world: &mut World,
    nodes: &[CdlodNode],
    grid: &CdlodGrid,
) -> Result<usize, String> {
    world.resource_scope(|world, mut resident: Mut<ResidentGeometry>| {
        let use_tiles = nodes.len() <= crate::render_inventory::MAX_SCOPED_MESHES;
        if resident.tiled == use_tiles
            && forge_world::terrain_lod::same_node_geometry(&resident.selected, nodes)
        {
            return Ok(nodes.len());
        }
        let entity = resident.entity;
        if use_tiles {
            resident
                .tiles
                .select::<M>(world, entity, nodes, |node| tile_mesh(node, grid));
        } else {
            // Prepare the full fallback before removing any currently owned tiles.
            let mesh = node_mesh(nodes)?;
            resident.tiles.clear(world);
            let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
            world
                .entity_mut(entity)
                .insert((Mesh3d(mesh), Visibility::Inherited));
        }
        resident.nodes = nodes.len();
        resident.selected = nodes.to_vec();
        resident.tiled = use_tiles;
        Ok(nodes.len())
    })
}

/// An ocean child is owned only while its parent, mesh and tile cache agree.
#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<ResidentGeometry>()
        .is_some_and(|geometry| {
            geometry.entity == entity || (geometry.tiled && geometry.tiles.owns(world, entity))
        })
}

/// Cached ocean sources for admission by an owning water host.
#[must_use]
pub fn retained_meshes(world: &World) -> Option<Vec<Handle<Mesh>>> {
    let geometry = world.get_resource::<ResidentGeometry>()?;
    if !geometry.tiled || !crate::source_water_host::owns(world, geometry.entity) {
        return None;
    }
    geometry.tiles.retained_meshes(world)
}

/// Cached hidden child tiles are not active draw requirements.
#[must_use]
pub fn inactive_tile(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<ResidentGeometry>()
        .is_some_and(|geometry| {
            entity != geometry.entity
                && geometry.tiled
                && geometry.tiles.owns(world, entity)
                && world.get::<Visibility>(entity) == Some(&Visibility::Hidden)
        })
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Source grid node values are uploaded as shader float32 values"
)]
fn tile_mesh(node: &CdlodNode, grid: &CdlodGrid) -> Mesh {
    let data = vec![
        [
            node.origin_xz[0] as f32,
            node.origin_xz[1] as f32,
            node.size_m as f32,
            f32::from(node.lod)
        ];
        grid.positions.len()
    ];
    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, grid.positions.clone())
    .with_inserted_attribute(NODE, data)
    .with_inserted_indices(Indices::U32(grid.indices.clone()))
}

/// Common source ocean CDLOD settings used by every consumer.
#[must_use]
pub fn config() -> CdlodConfig {
    CdlodConfig {
        grid_size: 32,
        leaf_size_m: 8.0,
        levels: 12,
        range_factor: 2.5,
        morph_start_ratio: 0.66,
        max_nodes: 1500,
    }
}

/// Select source ocean nodes for the supplied camera view.
pub fn view_nodes(view: CdlodView) -> Result<Vec<CdlodNode>, String> {
    #[cfg(feature = "qualification")]
    let _profile = crate::cpu_timing::Span::new("source.water.view_selection");
    select_nodes(config(), CdlodRoot::CameraFollowing, view, |_, _| {
        Ok([-25.0, 25.0])
    })
    .map_err(|e| e.to_string())
}

#[allow(clippy::cast_possible_truncation)] // Source CDLOD nodes are bounded by the config.
/// Build the common source ocean mesh for an admitted selection.
pub fn node_mesh(nodes: &[CdlodNode]) -> Result<Mesh, String> {
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

#[cfg(test)]
mod tests {
    use super::{PreparedGeometry, validate_terrain_domain};
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument,
        TerrainExtent,
        authoring::new_terrain,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };

    const GEOMETRY_SHADER: &str = r"
@group(0) @binding(3) var terrainHeightTex:texture_2d<f32>;
struct Frame { seaLevel:f32, };
var<private> frame:Frame;
fn terrainHeightAt(xz:vec2f)->f32 { return xz.x; }
fn prepareFrame() { frame.seaLevel=0.0; }
";

    fn terrain(width: u16, height: u16, spacing: f32, outside: f32) -> (TerrainDocument, Vec<u8>) {
        let mut document = new_terrain("ocean", "authoring", "Ocean geometry").unwrap();
        document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        document.extent_m =
            TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
        document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        document.height_sampling.sample_spacing_m = spacing;
        document.height_sampling.base_elevation_m = 0.0;
        document.height_sampling.amplitude_m = 0.0;
        document.height_sampling.base_frequency_cycles_per_m = 1.0;
        document.height_sampling.octaves = 1;
        document.height_sampling.lacunarity = 2.0;
        document.height_sampling.persistence = 0.5;
        let bytes = vec![0_u8; usize::from(width) * usize::from(height) * 4];
        document.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: u32::from(width),
            height: u32::from(height),
            origin_xz_m: [
                -f64::from(document.extent_m.size_x_m) * 0.5,
                -f64::from(document.extent_m.size_z_m) * 0.5,
            ],
            sample_spacing_m: f64::from(spacing),
            outside_height_m: outside,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&bytes),
        });
        (document, bytes)
    }

    #[test]
    fn shoreline_domain_encloses_rectangular_terrain_without_coarsening() {
        let (document, _) = terrain(64, 32, 64.0, -12.0);
        let domain = crate::source_shore_field::Domain::for_terrain(&document, 3.5).unwrap();
        let expected = crate::source_shore_field::Domain::preserving_spacing(
            [-2048.0; 2],
            4096.0,
            3.5,
            4.0,
            1024 * 1024,
        )
        .unwrap();
        assert_eq!(domain, expected);
        let (too_large, _) = terrain(64, 32, 512.0, -12.0);
        assert!(crate::source_shore_field::Domain::for_terrain(&too_large, 0.0).is_err());
    }

    #[test]
    fn ocean_geometry_uses_the_shared_rectangular_height_and_water_domain() {
        let (mut document, bytes) = terrain(11, 19, 0.5, -12.0);
        document
            .height_deltas
            .push(forge_project::TerrainHeightDelta::new(2, 3, 1.0));
        let plan =
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(&document, &bytes)
                .unwrap();
        let prepared = PreparedGeometry::new_for_terrain(
            GEOMETRY_SHADER.as_bytes(),
            &[0; 32],
            &plan,
            &bytes,
            3.5,
        )
        .unwrap();
        assert_eq!(prepared.height_dimensions, [11, 19]);
        assert_eq!(prepared.height, bytes);
        assert!(prepared.shader.contains("vec2f(-2.75,-4.75))/0.5"));
        assert!(prepared.shader.contains("f>=vec2f(10.0,18.0)"));
        assert!(prepared.shader.contains("return -12.0;"));
        assert!(prepared.shader.contains("frame.seaLevel=3.5;"));
        assert!(prepared.shader.contains("fn forgeOriginalTerrainHeightAt("));
        let shader = prepared.shader.replace("#{MATERIAL_BIND_GROUP}", "0");
        let module = naga::front::wgsl::parse_str(&shader).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        assert!(
            PreparedGeometry::new(GEOMETRY_SHADER.as_bytes(), &[0; 32], &document, &bytes).is_err()
        );
    }

    #[test]
    fn ocean_geometry_preserves_the_reference_and_refuses_invalid_domains_or_levels() {
        let (reference, reference_bytes) = terrain(2048, 2048, 1.0, -90.0);
        let reference_plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
            &reference,
            &reference_bytes,
        )
        .unwrap();
        let prepared = PreparedGeometry::new_for_terrain(
            GEOMETRY_SHADER.as_bytes(),
            &[0; 32],
            &reference_plan,
            &reference_bytes,
            0.0,
        )
        .unwrap();
        assert_eq!(prepared.shader, GEOMETRY_SHADER);
        assert_eq!(prepared.height_dimensions, [2048, 2048]);
        let (document, bytes) = terrain(11, 19, 0.5, -12.0);
        let plan =
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(&document, &bytes)
                .unwrap();
        for level in [f64::NAN, f64::INFINITY, f64::MAX, f64::MIN_POSITIVE] {
            assert!(
                PreparedGeometry::new_for_terrain(
                    GEOMETRY_SHADER.as_bytes(),
                    &[0; 32],
                    &plan,
                    &bytes,
                    level,
                )
                .is_err()
            );
        }
        assert!(
            PreparedGeometry::new_for_terrain(
                GEOMETRY_SHADER
                    .replace("frame.seaLevel=0.0;", "")
                    .as_bytes(),
                &[0; 32],
                &plan,
                &bytes,
                1.0,
            )
            .is_err()
        );
        assert!(
            PreparedGeometry::new_for_terrain(
                GEOMETRY_SHADER
                    .replace("frame.seaLevel=0.0;", "")
                    .as_bytes(),
                &[0; 32],
                &plan,
                &bytes,
                0.0,
            )
            .is_err()
        );
        assert!(
            PreparedGeometry::new_for_terrain(
                GEOMETRY_SHADER.as_bytes(),
                &[0; 32],
                &plan,
                &bytes[..bytes.len() - 4],
                0.0,
            )
            .is_err()
        );
        let mut altered = bytes.clone();
        altered[0] ^= 1;
        assert!(
            PreparedGeometry::new_for_terrain(
                GEOMETRY_SHADER.as_bytes(),
                &[0; 32],
                &plan,
                &altered,
                0.0,
            )
            .is_err()
        );
    }

    #[test]
    fn adopted_optical_ocean_uses_shared_view_and_preserves_its_owner() {
        use super::*;
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(Mesh::from(Cuboid::default()));
        let invalid = world.spawn(Mesh3d(mesh.clone())).id();
        assert!(adopt_optical_surface(&mut world, invalid).is_err());
        assert!(!world.contains_resource::<ResidentGeometry>());
        let foreign = world
            .spawn((
                Mesh3d(mesh.clone()),
                MeshMaterial3d(Handle::<crate::source_water_optics::OpticalMaterial>::default()),
            ))
            .id();
        let root = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(Handle::<crate::source_water_optics::OpticalMaterial>::default()),
                Transform::default(),
                Visibility::Inherited,
            ))
            .id();
        adopt_optical_surface(&mut world, root).unwrap();
        adopt_optical_surface(&mut world, root).unwrap();
        assert!(adopt_optical_surface(&mut world, foreign).is_err());
        assert_eq!(world.resource::<ResidentGeometry>().entity, root);
        let view = crate::scene_camera::source_view(
            &Transform::from_xyz(0.0, 8.0, 0.0),
            &PerspectiveProjection::default(),
        )
        .unwrap();
        let expected = view_nodes(view).unwrap().len();
        let first = update_view_observed(&mut world, view).unwrap();
        assert_eq!(first.nodes, expected);
        assert!(first.changed);
        assert!(owns(&world, root));
        assert!(!tile_entities(&world).is_empty());
        let counts = tile_counts(&world);
        let second = update_view_observed(&mut world, view).unwrap();
        assert_eq!(second.nodes, expected);
        assert!(!second.changed);
        assert_eq!(tile_counts(&world), counts);
        assert!(
            tile_entities(&world)
                .iter()
                .all(|entity| owns(&world, *entity))
        );
        world
            .entity_mut(root)
            .remove::<MeshMaterial3d<crate::source_water_optics::OpticalMaterial>>();
        assert!(adopt_optical_surface(&mut world, root).is_err());
    }

    #[test]
    fn ocean_tiles_retain_overlapping_meshes_and_admit_only_owned_children() {
        use super::*;
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<StandardMaterial>>();
        let mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(Mesh::from(Cuboid::default()));
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        let root = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Transform::default(),
                Visibility::Inherited,
            ))
            .id();
        world.insert_resource(ResidentGeometry {
            entity: root,
            nodes: 0,
            selected: vec![],
            tiles: ResidentTiles::default(),
            tiled: false,
        });
        let grid = config().grid().unwrap();
        let node = |x| CdlodNode {
            origin_xz: [x, 0.0],
            size_m: 8.0,
            lod: 0,
        };
        let initial = [node(0.0), node(8.0)];
        project_nodes::<StandardMaterial>(&mut world, &initial, &grid).unwrap();
        let before: Vec<_> = world
            .resource::<ResidentGeometry>()
            .tiles
            .entities()
            .into_iter()
            .map(|e| (e, world.get::<Mesh3d>(e).unwrap().0.id()))
            .collect();
        assert_eq!(before.len(), 2);
        assert!(before.iter().all(|(e, _)| owns(&world, *e)));
        let view = crate::scene_camera::source_view(
            &Transform::from_xyz(0.0, 8.0, 0.0),
            &PerspectiveProjection::default(),
        )
        .unwrap();
        let candidate = prepare_view::<StandardMaterial>(&mut world, view).unwrap();
        assert!(!candidate.meshes().is_empty());
        assert_eq!(world.resource::<ResidentGeometry>().tiles.counts(), [2, 2]);
        assert!(before.iter().all(|(entity, mesh)| {
            world
                .get::<Mesh3d>(*entity)
                .is_some_and(|current| current.0.id() == *mesh)
        }));
        assert!(prepare_view::<GeometryMaterial>(&mut world, view).is_err());
        let foreign = world
            .spawn((Mesh3d(Handle::<Mesh>::default()), ChildOf(root)))
            .id();
        assert!(!owns(&world, foreign));
        project_nodes::<StandardMaterial>(&mut world, &[node(8.0), node(16.0)], &grid).unwrap();
        let after: Vec<_> = world
            .resource::<ResidentGeometry>()
            .tiles
            .entities()
            .into_iter()
            .map(|e| world.get::<Mesh3d>(e).unwrap().0.id())
            .collect();
        assert_eq!(after.len(), 2);
        assert!(
            before.iter().any(|(_, id)| after.contains(id)),
            "The overlapping source tile must keep its mesh"
        );
        assert_eq!(world.resource::<ResidentGeometry>().tiles.counts(), [2, 3]);
    }

    #[test]
    fn oversized_ocean_selection_preserves_the_combined_mesh_fallback() {
        use super::*;
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<StandardMaterial>>();
        let mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(Mesh::from(Cuboid::default()));
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        let root = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Transform::default(),
                Visibility::Inherited,
            ))
            .id();
        world.insert_resource(ResidentGeometry {
            entity: root,
            nodes: 0,
            selected: vec![],
            tiles: ResidentTiles::default(),
            tiled: false,
        });
        let grid = config().grid().unwrap();
        let node = |x: u16| CdlodNode {
            origin_xz: [f64::from(x) * 8.0, 0.0],
            size_m: 8.0,
            lod: 0,
        };
        project_nodes::<StandardMaterial>(&mut world, &[node(0), node(1)], &grid).unwrap();
        let oversized: Vec<_> = (0..513).map(node).collect();
        assert_eq!(
            project_nodes::<StandardMaterial>(&mut world, &oversized, &grid).unwrap(),
            513
        );
        assert!(!world.resource::<ResidentGeometry>().tiled);
        assert_eq!(world.resource::<ResidentGeometry>().tiles.counts(), [0, 0]);
        assert!(owns(&world, root));
        project_nodes::<StandardMaterial>(&mut world, &[node(0), node(1)], &grid).unwrap();
        assert!(world.resource::<ResidentGeometry>().tiled);
        assert_eq!(world.resource::<ResidentGeometry>().tiles.counts(), [2, 2]);
    }

    #[test]
    fn single_ocean_tile_has_the_exact_existing_node_mesh() {
        use super::*;
        let node = CdlodNode {
            origin_xz: [24.0, -8.0],
            size_m: 8.0,
            lod: 2,
        };
        let grid = config().grid().unwrap();
        let old = node_mesh(std::slice::from_ref(&node)).unwrap();
        let tiled = tile_mesh(&node, &grid);
        assert_eq!(
            old.attribute(Mesh::ATTRIBUTE_POSITION),
            tiled.attribute(Mesh::ATTRIBUTE_POSITION)
        );
        assert_eq!(old.attribute(NODE), tiled.attribute(NODE));
        assert_eq!(old.indices(), tiled.indices());
    }

    #[test]
    fn source_shader_domain_rejects_misaligned_terrain() {
        let source = SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 2048,
            height: 2048,
            origin_xz_m: [-1024.0, -1024.0],
            sample_spacing_m: 1.0,
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&[]),
        };
        assert!(validate_terrain_domain(&source).is_ok());
        let mut altered = source.clone();
        altered.width = 1024;
        assert!(validate_terrain_domain(&altered).is_err());
        altered = source.clone();
        altered.height = 1024;
        assert!(validate_terrain_domain(&altered).is_err());
        altered = source.clone();
        altered.origin_xz_m[1] = -1023.0;
        assert!(validate_terrain_domain(&altered).is_err());
        altered = source.clone();
        altered.sample_spacing_m = 2.0;
        assert!(validate_terrain_domain(&altered).is_err());
        altered = source;
        altered.outside_height_m = 0.0;
        assert!(validate_terrain_domain(&altered).is_err());
    }
}
