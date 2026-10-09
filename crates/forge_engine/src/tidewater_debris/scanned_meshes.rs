//! Shared acquired scanned prototypes; placement transforms and variation stay separate.
use crate::tidewater_mesh::{DATA, TINT};
use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::Mesh,
    render::render_resource::PrimitiveTopology,
};
use forge_assets::tidewater_debris::scanned::{ScannedPackage, geometry};

pub struct Prototype {
    pub asset: u8,
    pub lod: u8,
    pub mesh: Mesh,
}

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
    let geometry = geometry::prepare(package)?;
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
    Ok(PreparedMeshes {
        prototypes,
        instances: geometry.instances,
    })
}
