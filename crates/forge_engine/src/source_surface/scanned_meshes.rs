//! Shared acquired scanned prototypes; placement transforms and variation stay separate.
use super::mesh::{DATA, TINT};
pub use super::scanned_scene::Prototype;
use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::Mesh,
    render::render_resource::PrimitiveTopology,
};
use forge_assets::source_scanned_package::{ScannedPackage, geometry};

pub struct PreparedMeshes {
    pub prototypes: Vec<Prototype>,
    pub instances: Vec<geometry::Instance>,
}

/// Construct twelve prototype meshes before inserting any renderer asset.
///
/// # Errors
/// Propagates acquired geometry validation errors. Callers must install the
/// instance-buffer shader and publish bounded mesh-tag records before drawing.
pub fn prepare(package: &ScannedPackage) -> Result<PreparedMeshes, String> {
    Ok(from_geometry(geometry::prepare(package)?))
}

/// Build the renderer prototypes directly from an already decoded scan package.
#[must_use]
pub fn from_geometry(geometry: geometry::Geometry) -> PreparedMeshes {
    let prototypes = geometry
        .prototypes
        .into_iter()
        .map(|prototype| {
            let vertices = prototype.positions.len();
            // The unchanged source vertex reads aAsset from tint.x. Instance data is
            // supplied by the resident storage buffer, indexed by the Bevy mesh tag.
            let tint = vec![[f32::from(prototype.asset), 0.0, 0.0]; vertices];
            let unused_data = vec![[0.0_f32; 4]; vertices];
            let mesh = Mesh::new(
                PrimitiveTopology::TriangleList,
                RenderAssetUsages::default(),
            )
            .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, prototype.positions)
            .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, prototype.normals)
            .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, prototype.atlas_uv)
            .with_inserted_attribute(TINT, tint)
            .with_inserted_attribute(DATA, unused_data)
            .with_inserted_indices(Indices::U32(prototype.indices));
            Prototype {
                asset: prototype.asset,
                lod: prototype.lod,
                mesh,
            }
        })
        .collect();
    PreparedMeshes {
        prototypes,
        instances: geometry.instances,
    }
}
