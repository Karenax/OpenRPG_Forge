// SPDX-License-Identifier: MIT
// Grid adaptation from Tidewater, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE; upstream 1438b1abfcaee3267092b75573014f4d9b4a983c.
//! Shared authored CDLOD geometry and source vertex deformation.
use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{ExtendedMaterial, MaterialExtension, MaterialExtensionKey, MaterialExtensionPipeline},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, ShaderType,
        TextureDimension, TextureFormat, VertexFormat,
    },
    shader::ShaderRef,
};
use forge_world::{
    cdlod::{CdlodConfig, CdlodNode, CdlodRoot, CdlodView, select_nodes},
    sampled_terrain::{RasterLodBounds, SampledTerrainPlan},
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
    let corrections = crate::tidewater_terrain_edits::Corrections::from_document(document)?;
    let source = corrections.shader(source, "#{MATERIAL_BIND_GROUP}", 102)?;
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Renderer shader assets unavailable")?
        .insert(
            EDITED_SHADER.id(),
            Shader::from_wgsl(source, "tidewater-edited-terrain.wgsl"),
        )?;
    Ok(())
}

const NODE: MeshVertexAttribute =
    MeshVertexAttribute::new("TerrainLodNode", 0x7457_0004, VertexFormat::Float32x4);
pub type LodMaterial = ExtendedMaterial<super::TerrainMaterial, LodExtension>;

#[derive(Clone, Debug, ShaderType)]
pub struct LodSettings {
    pub morph: [Vec4; 9],
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct LodExtension {
    #[texture(100, filterable = false)]
    pub height: Handle<Image>,
    #[uniform(101)]
    pub settings: LodSettings,
    #[texture(102, filterable = false)]
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
    pub document_hash: forge_core::ContentHash,
}
impl ViewGeometry {
    pub fn new(plan: &SampledTerrainPlan) -> Result<Self, String> {
        Ok(Self {
            bounds: RasterLodBounds::from_plan(plan).map_err(|e| e.to_string())?,
            document_hash: plan.document_hash(),
        })
    }

    pub fn mesh(&self, view: CdlodView) -> Result<Option<(Mesh, usize)>, String> {
        let nodes = select_nodes(
            source_lod_config(),
            CdlodRoot::Fixed {
                origin_xz: [-1024.0; 2],
                size_m: 2048.0,
            },
            view,
            |origin, size| self.bounds.bounds_for(origin, size),
        )
        .map_err(|e| e.to_string())?;
        Ok((!nodes.is_empty()).then(|| (expanded_mesh(&nodes), nodes.len())))
    }
}
impl PreparedLod {
    #[allow(clippy::cast_possible_truncation)] // Same bounded source morph rows as the acquired renderer.
    pub fn new(
        plan: &SampledTerrainPlan,
        bytes: &[u8],
        view: CdlodView,
    ) -> Result<Option<Self>, String> {
        let doc = plan.document();
        let source = doc
            .sampled_source
            .as_ref()
            .ok_or("Terrain raster missing")?;
        if source.width != 2048
            || source.height != 2048
            || source.outside_height_m.to_bits() != (-90.0_f32).to_bits()
            || bytes.len() != 2048 * 2048 * 4
            || forge_core::bytes_sha256(bytes) != source.samples_sha256
        {
            return Err("Terrain mesh requires its verified 2048 raster".into());
        }
        let edit = crate::tidewater_terrain_edits::Corrections::from_document(doc)?;
        let config = source_lod_config();
        let Some((mesh, node_count)) = ViewGeometry::new(plan)?.mesh(view)? else {
            return Ok(None);
        };
        let image = |side, bytes| {
            Image::new(
                Extent3d {
                    width: side,
                    height: side,
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
        Ok(Some(Self {
            mesh,
            height: image(2048, bytes.to_vec()),
            corrections: image(edit.side, edit.bytes),
            settings: LodSettings { morph },
            node_count,
        }))
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
