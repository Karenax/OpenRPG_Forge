// SPDX-License-Identifier: MIT
// Grid adaptation from Tidewater, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE; upstream 1438b1abfcaee3267092b75573014f4d9b4a983c.
//! Shared authored CDLOD geometry and source vertex deformation.
use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialExtension, MaterialExtensionKey, MaterialExtensionPipeline},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, ShaderType,
        TextureDimension, TextureFormat, VertexFormat,
    },
    shader::ShaderRef,
};
use {
    forge_world::sampled_terrain::SampledTerrainPlan,
    forge_world::{
        terrain_lod::{CdlodConfig, CdlodNode, CdlodRoot, CdlodView, select_nodes},
        terrain_lod_bounds::RasterLodBounds,
    },
};

pub(super) const EDITED_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("32200bfc-a385-413b-83c0-20dcd59abb5c");

pub fn install_shader(
    app: &mut App,
    source: &str,
    document: &forge_project::TerrainDocument,
) -> Result<(), Box<dyn std::error::Error>> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid terrain LOD shader".into());
    }
    let corrections = crate::sampled_terrain_corrections::Corrections::from_document(document)?;
    if !corrections.supports_fixed_source_profile() {
        return Err("Source terrain LOD shader requires its admitted fixed raster profile".into());
    }
    let source = super::sampling::prepare_shader(source, document)?;
    let source = super::sampling::clip_vertex_shader(&source, document)?;
    let source = corrections.shader(&source, "#{MATERIAL_BIND_GROUP}", 102)?;
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Renderer shader assets unavailable")?
        .insert(
            EDITED_SHADER.id(),
            Shader::from_wgsl(source, "source-edited-terrain.wgsl"),
        )?;
    Ok(())
}

const NODE: MeshVertexAttribute =
    MeshVertexAttribute::new("TerrainLodNode", 0x7457_0004, VertexFormat::Float32x4);
pub type LodMaterial =
    crate::material_composition::ComposedMaterial<super::TerrainMaterial, LodExtension>;

#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct LodSettings {
    pub morph: [Vec4; 9],
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, PartialEq)]
pub struct LodExtension {
    #[texture(100, filterable = false)]
    #[dependency]
    pub height: Handle<Image>,
    #[uniform(101)]
    pub settings: LodSettings,
    #[texture(102, filterable = false)]
    #[dependency]
    pub corrections: Handle<Image>,
}
impl MaterialExtension for LodExtension {
    fn vertex_shader() -> ShaderRef {
        EDITED_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialExtensionPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        _: MaterialExtensionKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            Mesh::ATTRIBUTE_NORMAL.at_shader_location(1),
            NODE.at_shader_location(2),
        ])?];
        Ok(())
    }
}

/// CPU preparation before touching the current scene. The expanded mesh preserves
/// the reference geometry; instancing and live-camera budgets remain to qualify.
pub struct PreparedLod {
    pub mesh: Mesh,
    pub height: Image,
    pub corrections: Image,
    pub settings: LodSettings,
    pub node_count: usize,
}

/// Camera-dependent mesh selection with immutable bounds cached per document.
/// Moving the editor camera must not rescan the raster or allocate height textures.
pub struct ViewGeometry {
    bounds: RasterLodBounds,
    root: CdlodRoot,
    pub document_hash: forge_core::ContentHash,
}
impl ViewGeometry {
    /// Prepare selection for the sampled domain. A renderer displaying a
    /// non-reference domain must also install its matching clipped shader and
    /// bind the verified resources for the same domain.
    pub fn new(plan: &SampledTerrainPlan) -> Result<Self, String> {
        let source = plan.raster().source();
        let root = if super::supports_fixed_source_raster(source) {
            CdlodRoot::Fixed {
                origin_xz: [-1024.0; 2],
                size_m: 2048.0,
            }
        } else {
            let document = plan.document();
            let extent = [document.extent_m.size_x_m, document.extent_m.size_z_m];
            let config = source_lod_config();
            let maximum = config.leaf_size_m * 2_f64.powi(i32::from(config.levels - 1)) * 16.0;
            if !document.height_sampling.sample_spacing_m.is_normal()
                || extent
                    .iter()
                    .any(|size| !(*size * 0.5).is_normal() || f64::from(*size) > maximum)
            {
                return Err("Sampled terrain domain exceeds LOD shader or root limits".into());
            }
            CdlodRoot::FixedRectangle {
                origin_xz: source.origin_xz_m,
                size_xz: extent.map(f64::from),
            }
        };
        Ok(Self {
            bounds: RasterLodBounds::from_plan(plan).map_err(|e| e.to_string())?,
            root,
            document_hash: plan.document_hash(),
        })
    }

    pub fn mesh(&self, view: CdlodView) -> Result<Option<(Mesh, usize)>, String> {
        let nodes = self.nodes(view)?;
        Ok(Self::mesh_from_nodes(&nodes))
    }

    /// Lattice and morph settings used by this shared terrain selector.
    #[must_use]
    pub fn config() -> CdlodConfig {
        source_lod_config()
    }

    /// Ordered source tiles; camera motion alone does not change their vertex data.
    pub fn nodes(&self, view: CdlodView) -> Result<Vec<CdlodNode>, String> {
        #[cfg(feature = "qualification")]
        let _profile = crate::cpu_timing::Span::new("source.terrain.view_selection");
        select_nodes(Self::config(), self.root, view, |origin, size| {
            self.bounds.bounds_for(origin, size)
        })
        .map_err(|e| e.to_string())
    }

    /// Initial cache anchor, on the same lattice as the first selector root.
    /// It is replaced by the current view selection before publishing tiles.
    #[must_use]
    pub(super) fn bootstrap_node(&self) -> CdlodNode {
        let origin_xz = match self.root {
            CdlodRoot::Fixed { origin_xz, .. } | CdlodRoot::FixedRectangle { origin_xz, .. } => {
                origin_xz
            }
            CdlodRoot::CameraFollowing => unreachable!("sampled terrain has a fixed domain"),
        };
        let config = Self::config();
        let lod = config.levels - 1;
        CdlodNode {
            origin_xz,
            size_m: config.leaf_size_m * 2_f64.powi(i32::from(lod)),
            lod,
        }
    }

    #[must_use]
    pub fn mesh_from_nodes(nodes: &[CdlodNode]) -> Option<(Mesh, usize)> {
        (!nodes.is_empty()).then(|| (expanded_mesh(nodes), nodes.len()))
    }
}
impl PreparedLod {
    /// Prepare geometry and height textures for an admitted sampled domain.
    /// Non-reference renderers must install the matching clipped vertex shader.
    pub fn new(
        plan: &SampledTerrainPlan,
        bytes: &[u8],
        view: CdlodView,
    ) -> Result<Option<Self>, String> {
        validate_source(plan, bytes)?;
        let Some((mesh, node_count)) = ViewGeometry::new(plan)?.mesh(view)? else {
            return Ok(None);
        };
        Self::with_mesh(plan, bytes, mesh, node_count).map(Some)
    }

    /// The host selects visible tiles separately. Its initial root stays valid
    /// even when the first camera view contains no terrain.
    pub(super) fn resident_root(
        plan: &SampledTerrainPlan,
        bytes: &[u8],
        geometry: &ViewGeometry,
    ) -> Result<Self, String> {
        validate_source(plan, bytes)?;
        if geometry.document_hash != plan.document_hash() {
            return Err("Resident terrain root geometry belongs to another document".into());
        }
        Self::with_mesh(plan, bytes, expanded_mesh(&[geometry.bootstrap_node()]), 1)
    }

    #[allow(clippy::cast_possible_truncation)] // Same bounded source morph rows as the acquired renderer.
    fn with_mesh(
        plan: &SampledTerrainPlan,
        bytes: &[u8],
        mesh: Mesh,
        node_count: usize,
    ) -> Result<Self, String> {
        let doc = plan.document();
        let source = plan.raster().source();
        let edit = crate::sampled_terrain_corrections::Corrections::from_document(doc)?;
        let config = source_lod_config();
        let image = |width, height, bytes| {
            Image::new(
                Extent3d {
                    width,
                    height,
                    depth_or_array_layers: 1,
                },
                TextureDimension::D2,
                bytes,
                TextureFormat::R32Float,
                RenderAssetUsages::default(),
            )
        };
        let morph = config
            .morph_rows()
            .map_err(|e| e.to_string())?
            .into_iter()
            .map(|row| Vec4::from_array(row.map(|v| v as f32)))
            .collect::<Vec<_>>()
            .try_into()
            .map_err(|_| "Nine terrain morph rows required")?;
        Ok(Self {
            mesh,
            height: image(source.width, source.height, bytes.to_vec()),
            corrections: image(edit.width, edit.height, edit.bytes),
            settings: LodSettings { morph },
            node_count,
        })
    }
    pub fn stage(
        self,
        meshes: &mut Assets<Mesh>,
        images: &mut Assets<Image>,
    ) -> (Handle<Mesh>, LodExtension) {
        (
            meshes.add(self.mesh),
            LodExtension {
                height: images.add(self.height),
                corrections: images.add(self.corrections),
                settings: self.settings,
            },
        )
    }
}

fn validate_source(plan: &SampledTerrainPlan, bytes: &[u8]) -> Result<(), String> {
    let source = plan
        .document()
        .sampled_source
        .as_ref()
        .ok_or("Terrain raster missing")?;
    if bytes.len() != source.byte_length().map_err(|e| e.to_string())?
        || forge_core::bytes_sha256(bytes) != source.samples_sha256
    {
        return Err("Terrain mesh requires its verified sampled raster".into());
    }
    Ok(())
}

#[allow(clippy::cast_possible_truncation)]
fn expanded_mesh(nodes: &[CdlodNode]) -> Mesh {
    let mut positions = Vec::new();
    let mut data = Vec::new();
    let mut indices = Vec::new();
    for node in nodes {
        let offset = u32::try_from(positions.len()).expect("bounded vertices");
        for z in 0..=40_u16 {
            for x in 0..=40_u16 {
                positions.push([f32::from(x) / 40.0, 0.0, f32::from(z) / 40.0]);
                data.push([
                    node.origin_xz[0] as f32,
                    node.origin_xz[1] as f32,
                    node.size_m as f32,
                    f32::from(node.lod),
                ]);
            }
        }
        for z in 0..40_u32 {
            for x in 0..40_u32 {
                let a = offset + z * 41 + x;
                let b = a + 1;
                let c = a + 41;
                let d = c + 1;
                indices.extend(if (x + z).is_multiple_of(2) {
                    [a, c, b, b, c, d]
                } else {
                    [a, c, d, a, d, b]
                });
            }
        }
    }
    let normals = vec![[0.0, 1.0, 0.0]; positions.len()];
    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, normals)
    .with_inserted_attribute(NODE, data)
    .with_inserted_indices(Indices::U32(indices))
}

fn source_lod_config() -> CdlodConfig {
    CdlodConfig {
        grid_size: 40,
        leaf_size_m: 8.0,
        levels: 9,
        range_factor: 2.0,
        morph_start_ratio: 0.66,
        max_nodes: 1500,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        TerrainHeightDelta,
        authoring::new_terrain,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };

    fn plan(width: u16, height: u16) -> SampledTerrainPlan {
        scaled_plan(width, height, 1.0)
    }

    fn scaled_plan(width: u16, height: u16, spacing: f32) -> SampledTerrainPlan {
        let mut document = new_terrain("sampled", "authoring", "Raster").unwrap();
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
        let bytes = vec![0; usize::from(width) * usize::from(height) * 4];
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
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&bytes),
        });
        SampledTerrainPlan::from_document(&document, &bytes).unwrap()
    }

    #[test]
    fn rectangle_selection_does_not_bypass_renderer_admission() {
        let rectangle = plan(11, 19);
        assert!(RasterLodBounds::from_plan(&rectangle).is_ok());
        let geometry = ViewGeometry::new(&rectangle).unwrap();
        let view = CdlodView {
            position: [0.0; 3],
            planes: [[0.0; 4]; 6],
        };
        let nodes = geometry.nodes(view).unwrap();
        assert!(!nodes.is_empty());
        assert!(
            nodes
                .iter()
                .all(|node| node.origin_xz[0] < 5.5 && node.origin_xz[1] < 9.5)
        );
        for z in 0..19 {
            for x in 0..11 {
                let point = [-5.5 + f64::from(x) + 0.5, -9.5 + f64::from(z) + 0.5];
                assert_eq!(
                    nodes
                        .iter()
                        .filter(|node| (0..2).all(|axis| {
                            point[axis] >= node.origin_xz[axis]
                                && point[axis] < node.origin_xz[axis] + node.size_m
                        }))
                        .count(),
                    1
                );
            }
        }
        assert_eq!(
            install_shader(&mut App::new(), "fn terrainHeightAt(", rectangle.document())
                .unwrap_err()
                .to_string(),
            "Source terrain LOD shader requires its admitted fixed raster profile"
        );
        assert!(ViewGeometry::new(&plan(8, 8)).is_ok());
        let reference = ViewGeometry::new(&plan(2048, 2048)).unwrap();
        assert_eq!(
            reference.bootstrap_node(),
            CdlodNode {
                origin_xz: [-1024.0; 2],
                size_m: 2048.0,
                lod: 8,
            }
        );
        let historical = select_nodes(
            source_lod_config(),
            CdlodRoot::Fixed {
                origin_xz: [-1024.0; 2],
                size_m: 2048.0,
            },
            view,
            |origin, size| reference.bounds.bounds_for(origin, size),
        )
        .unwrap();
        assert_eq!(reference.nodes(view).unwrap(), historical);
        assert!(ViewGeometry::new(&scaled_plan(11, 19, 4096.0)).is_err());
        assert!(ViewGeometry::new(&scaled_plan(11, 19, f32::MIN_POSITIVE * 0.5)).is_err());
    }

    #[test]
    fn rectangular_geometry_prepares_verified_height_and_edit_textures() {
        let source = plan(11, 19);
        let bytes = vec![0; 11 * 19 * 4];
        let mut document = source.document().clone();
        document.height_deltas = vec![TerrainHeightDelta::new(2, 3, 4.0)];
        let edited = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
        let view = CdlodView {
            position: [0.0; 3],
            planes: [[0.0; 4]; 6],
        };
        let lod = PreparedLod::new(&edited, &bytes, view).unwrap().unwrap();
        assert_eq!([lod.height.width(), lod.height.height()], [11, 19]);
        assert_eq!(
            [lod.corrections.width(), lod.corrections.height()],
            [12, 20]
        );
        assert_eq!(lod.height.data.as_deref(), Some(bytes.as_slice()));
        let values = lod.corrections.data.as_deref().unwrap();
        let index = (3 * 12 + 2) * 4;
        assert_eq!(&values[index..index + 4], &4.0_f32.to_le_bytes());
        assert!(lod.node_count > 0);
        assert!(super::super::scene::require_fixed_profile(&edited).is_err());
        let geometry = ViewGeometry::new(&edited).unwrap();
        let root = PreparedLod::resident_root(&edited, &bytes, &geometry).unwrap();
        assert_eq!(root.node_count, 1);
        assert_eq!([root.height.width(), root.height.height()], [11, 19]);
        assert!(
            PreparedLod::resident_root(&edited, &bytes, &ViewGeometry::new(&source).unwrap())
                .is_err()
        );
        assert!(PreparedLod::new(&edited, &bytes[..bytes.len() - 1], view).is_err());
        let mut corrupted = bytes;
        corrupted[0] = 1;
        assert!(PreparedLod::new(&edited, &corrupted, view).is_err());
    }

    #[test]
    fn bootstrap_mesh_and_cache_key_follow_the_rectangular_selector_lattice() {
        use bevy::mesh::VertexAttributeValues;
        use std::cell::Cell;

        // More than one top-level root is needed on both axes.
        let plan = scaled_plan(11, 19, 256.0);
        let bytes = vec![0; 11 * 19 * 4];
        let geometry = ViewGeometry::new(&plan).unwrap();
        let node = geometry.bootstrap_node();
        assert_eq!(
            node.origin_xz.map(f64::to_bits),
            [-1408.0_f64, -2432.0].map(f64::to_bits)
        );
        assert_eq!(node.size_m.to_bits(), 2048.0_f64.to_bits());
        assert_eq!(node.lod, 8);
        let root = PreparedLod::resident_root(&plan, &bytes, &geometry).unwrap();
        let Some(VertexAttributeValues::Float32x4(vertices)) = root.mesh.attribute(NODE) else {
            panic!("bootstrap mesh must retain its actual terrain node");
        };
        let expected = [-1408.0_f32, -2432.0, 2048.0, 8.0].map(f32::to_bits);
        assert_eq!(vertices.len(), 41 * 41);
        assert!(
            vertices
                .iter()
                .all(|value| value.map(f32::to_bits) == expected)
        );

        // The actual resident cache must adopt that mesh under the same key,
        // then retain it when another root enters and leaves the view.
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<StandardMaterial>>();
        let mesh = world.resource_mut::<Assets<Mesh>>().add(root.mesh);
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        let entity = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Visibility::Inherited,
            ))
            .id();
        let mut cache = super::super::resident_tiles::ResidentTiles::default();
        cache.adopt_root(&mut world, entity, &node).unwrap();
        let next = CdlodNode {
            origin_xz: [node.origin_xz[0] + node.size_m, node.origin_xz[1]],
            ..node
        };
        let builds = Cell::new(0);
        for selection in [&[node, next][..], &[next][..], &[node, next][..]] {
            cache.select::<StandardMaterial>(&mut world, entity, selection, |candidate| {
                builds.set(builds.get() + 1);
                ViewGeometry::mesh_from_nodes(std::slice::from_ref(candidate))
                    .unwrap()
                    .0
            });
            assert!(cache.covers_exact::<StandardMaterial>(&world, selection));
        }
        assert_eq!(builds.get(), 1);
    }
}
