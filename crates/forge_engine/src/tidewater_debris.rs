//! CPU projection of acquired Debris into Bevy meshes, before material staging.
//! This adapter does not spawn an incomplete render or replace source materials with PBR.
use crate::tidewater_mesh::VillageMeshBytes;
use bevy::{
    mesh::{Indices, MeshVertexAttribute},
    prelude::Mesh,
    render::render_resource::VertexFormat,
};
use forge_assets::tidewater_debris::{self as source_assets, DebrisPackage};
use forge_core::AssetId;
use serde::Deserialize;
use std::collections::BTreeMap;

pub mod collisions;
pub mod nature;
pub mod pebble_cells;
pub mod pebble_material;
pub mod pebble_mesh;
pub mod pebble_patch;
pub mod pebble_scene;
pub mod pebbles;
pub mod scanned_draws;
pub mod scanned_images;
pub mod scanned_lod;
pub mod scanned_material;
pub mod scanned_meshes;
pub mod scanned_placements;
pub mod scanned_scene;
pub mod scanned_shadow;
pub mod scanned_shadow_runtime;
pub mod scene;

/// Original world rest coordinates, retained for the source fabric wind phase.
pub const SOURCE_REST_POSITION: MeshVertexAttribute = MeshVertexAttribute::new(
    "DebrisSourceRestPosition",
    0x7457_0020,
    VertexFormat::Float32x3,
);

#[derive(Clone, Copy, Debug, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum DebrisSurface {
    Wood,
    Hard,
    Fabric,
    Nature,
}

#[derive(Deserialize)]
struct Description {
    props: Vec<SourceObject>,
}

#[derive(Deserialize)]
struct SourceObject {
    source_id: String,
    kind: String,
    source_frame: SourceFrame,
    parts: Vec<SourcePart>,
}

#[derive(Deserialize)]
struct SourceFrame {
    translation_m: [f64; 3],
    rotation_xyzw: [f64; 4],
    scale: [f64; 3],
}

#[derive(Deserialize)]
struct Reference {
    file: String,
}

#[derive(Deserialize)]
struct SourcePart {
    material: DebrisSurface,
    attributes: BTreeMap<String, Reference>,
    indices: Reference,
    shadow_indices: Reference,
    reference_position: Reference,
}

/// Exact source material role and independent main/shadow triangle populations.
pub struct PreparedDebrisPart {
    pub surface: DebrisSurface,
    pub mesh: Mesh,
    pub shadow_mesh: Option<Mesh>,
}

/// Immutable prototype frame; mutable placements belong to the common authoring document.
pub struct PreparedDebrisObject {
    pub source_id: String,
    pub kind: String,
    pub asset_id: AssetId,
    pub source_translation_m: [f64; 3],
    pub parts: Vec<PreparedDebrisPart>,
}

/// Prepared static procedural layer; scans and camera-following pebbles are separate.
pub struct PreparedDebris {
    pub objects: Vec<PreparedDebrisObject>,
    pub triangles: usize,
    pub shadow_triangles: usize,
}

/// Immutable source template; insertion identities and edits belong to the zone document.
pub struct DebrisTemplate {
    pub source_id: String,
    pub kind: String,
    pub asset_id: AssetId,
    pub transform: forge_core::Transform,
}

impl PreparedDebris {
    #[must_use]
    #[allow(
        clippy::cast_possible_truncation,
        reason = "The verified source frame is bounded; the common domain uses f32 metres."
    )]
    pub fn templates(&self) -> Vec<DebrisTemplate> {
        self.objects
            .iter()
            .map(|object| {
                let [x, y, z] = object.source_translation_m.map(|value| value as f32);
                DebrisTemplate {
                    source_id: object.source_id.clone(),
                    kind: object.kind.clone(),
                    asset_id: object.asset_id,
                    transform: forge_core::Transform::from_translation(forge_core::Vec3::new(
                        x, y, z,
                    )),
                }
            })
            .collect()
    }

    /// Decode verified bytes into CPU meshes without allocating materials/entities.
    ///
    /// # Errors
    /// Rejects incomplete geometry, altered frames, unknown roles or missing source data.
    pub fn from_package(package: &DebrisPackage) -> Result<Self, String> {
        let description: Description =
            serde_json::from_value(package.frames().clone()).map_err(|error| error.to_string())?;
        let mut objects = Vec::with_capacity(description.props.len());
        let mut triangles = 0;
        let mut shadow_triangles = 0;
        for (ordinal, source) in description.props.into_iter().enumerate() {
            if source.source_frame.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
                || source.source_frame.scale != [1.0, 1.0, 1.0]
                || source
                    .source_frame
                    .translation_m
                    .iter()
                    .any(|v| !v.is_finite())
            {
                return Err("Debris source frame differs".into());
            }
            let mut parts = Vec::with_capacity(source.parts.len());
            for part in source.parts {
                let attribute = |name: &str| {
                    let reference = part.attributes.get(name).ok_or("Debris attribute absent")?;
                    bytes(package, reference)
                };
                let indices = bytes(package, &part.indices)?;
                let shadow = bytes(package, &part.shadow_indices)?;
                let mut mesh = VillageMeshBytes {
                    positions: attribute("pos")?,
                    normals: attribute("nrm")?,
                    uv: attribute("uv")?,
                    tint: attribute("tint")?,
                    data: attribute("data")?,
                    indices,
                }
                .into_mesh()
                .map_err(str::to_owned)?;
                let rest = bytes(package, &part.reference_position)?;
                if rest.len() != attribute("pos")?.len() {
                    return Err("Debris rest coordinates differ".into());
                }
                let positions: Vec<[f32; 3]> = rest
                    .as_chunks::<12>()
                    .0
                    .iter()
                    .map(|chunk| {
                        std::array::from_fn(|axis| {
                            let offset = axis * 4;
                            f32::from_le_bytes([
                                chunk[offset],
                                chunk[offset + 1],
                                chunk[offset + 2],
                                chunk[offset + 3],
                            ])
                        })
                    })
                    .collect();
                if positions.iter().flatten().any(|v| !v.is_finite()) {
                    return Err("Non-finite Debris source coordinates".into());
                }
                mesh.insert_attribute(SOURCE_REST_POSITION, positions);
                let shadow_mesh = if shadow.is_empty() {
                    None
                } else {
                    let shadow_indices = shadow
                        .as_chunks::<4>()
                        .0
                        .iter()
                        .map(|b| u32::from_le_bytes(*b))
                        .collect();
                    Some(
                        mesh.clone()
                            .with_inserted_indices(Indices::U32(shadow_indices)),
                    )
                };
                triangles += indices.len() / 12;
                shadow_triangles += shadow.len() / 12;
                parts.push(PreparedDebrisPart {
                    surface: part.material,
                    mesh,
                    shadow_mesh,
                });
            }
            objects.push(PreparedDebrisObject {
                source_id: source.source_id,
                kind: source.kind,
                asset_id: source_assets::asset_id(ordinal).ok_or("Debris asset identity absent")?,
                source_translation_m: source.source_frame.translation_m,
                parts,
            });
        }
        if objects.len() != 805 || triangles != 112_669 || shadow_triangles != 50_522 {
            return Err("Debris prepared inventory differs".into());
        }
        Ok(Self {
            objects,
            triangles,
            shadow_triangles,
        })
    }
}

fn bytes<'a>(package: &'a DebrisPackage, reference: &Reference) -> Result<&'a [u8], String> {
    package
        .file(&reference.file)
        .ok_or_else(|| "Debris payload absent".into())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the real hash-pinned Debris frame acquisition"]
    fn source_meshes_preserve_roles_rest_coordinates_and_shadow_population() {
        let root = std::path::PathBuf::from(
            std::env::var_os("FORGE_DEBRIS_PACKAGE_FIXTURE")
                .expect("Explicit real Debris fixture required"),
        );
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let expected = forge_core::bytes_sha256(&manifest);
        let package = DebrisPackage::load(&manifest, expected, |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let prepared = PreparedDebris::from_package(&package).unwrap();
        assert_eq!(prepared.objects.len(), 805);
        assert_eq!(prepared.triangles, 112_669);
        assert_eq!(prepared.shadow_triangles, 50_522);
        let mut main = 0;
        let mut shadow = 0;
        let mut parts = 0;
        for object in &prepared.objects {
            assert_eq!(object.asset_id, AssetId::deterministic(&object.source_id));
            for part in &object.parts {
                parts += 1;
                main += part.mesh.indices().unwrap().len() / 3;
                assert!(part.mesh.attribute(SOURCE_REST_POSITION).is_some());
                assert!(part.mesh.attribute(crate::tidewater_mesh::DATA).is_some());
                if part.surface == DebrisSurface::Fabric {
                    assert!(part.shadow_mesh.is_none());
                }
                if let Some(mesh) = &part.shadow_mesh {
                    shadow += mesh.indices().unwrap().len() / 3;
                    assert!(mesh.attribute(SOURCE_REST_POSITION).is_some());
                }
            }
        }
        assert_eq!(parts, 865);
        assert_eq!(main, prepared.triangles);
        assert_eq!(shadow, prepared.shadow_triangles);
    }
}
