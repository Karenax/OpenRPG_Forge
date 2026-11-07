//! CPU projection of acquired Debris into Bevy meshes, before material staging.
//! This adapter does not spawn an incomplete render or replace source materials with PBR.
use bevy::prelude::Mesh;
use forge_core::AssetId;
use forge_engine::source_surface::mesh::SourceMeshBytes;
use forge_tidewater_assets::tidewater_debris as source_assets;
use forge_tidewater_assets::tidewater_debris::DebrisPackage;
use serde::Deserialize;
use std::collections::BTreeMap;

pub mod collisions;
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
    #[allow(
        clippy::float_cmp,
        reason = "This adapter admits only exact identity source rotations and scales; approximate frames need a separate adapter."
    )]
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
                let prepared = SourceMeshBytes {
                    positions: attribute("pos")?,
                    normals: attribute("nrm")?,
                    uv: attribute("uv")?,
                    tint: attribute("tint")?,
                    data: attribute("data")?,
                    indices,
                }
                .into_mesh_with_source_shadow(bytes(package, &part.reference_position)?, shadow)
                .map_err(str::to_owned)?;
                triangles += indices.len() / 12;
                shadow_triangles += shadow.len() / 12;
                parts.push(PreparedDebrisPart {
                    surface: part.material,
                    mesh: prepared.mesh,
                    shadow_mesh: prepared.shadow_mesh,
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
    use bevy::mesh::{Indices, MeshVertexAttribute, VertexAttributeValues};
    use forge_engine::source_surface::mesh::{DATA, SOURCE_REST_POSITION, TINT};

    fn assert_floats<const N: usize>(values: &[[f32; N]], bytes: &[u8]) {
        assert_eq!(values.len() * N * 4, bytes.len());
        for (value, original) in values.iter().flatten().zip(bytes.as_chunks::<4>().0.iter()) {
            assert_eq!(value.to_bits(), u32::from_le_bytes(*original));
        }
    }

    fn assert_attribute(mesh: &Mesh, attribute: MeshVertexAttribute, bytes: &[u8]) {
        match mesh
            .attribute(attribute)
            .expect("Original source attribute missing")
        {
            VertexAttributeValues::Float32x2(values) => assert_floats(values, bytes),
            VertexAttributeValues::Float32x3(values) => assert_floats(values, bytes),
            VertexAttributeValues::Float32x4(values) => assert_floats(values, bytes),
            _ => panic!("Original source attribute type differs"),
        }
    }

    fn assert_indices(mesh: &Mesh, bytes: &[u8]) {
        let Some(Indices::U32(indices)) = mesh.indices() else {
            panic!("Original source index type differs");
        };
        assert_eq!(indices.len() * 4, bytes.len());
        for (index, original) in indices.iter().zip(bytes.as_chunks::<4>().0.iter()) {
            assert_eq!(*index, u32::from_le_bytes(*original));
        }
    }

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
        let description: Description = serde_json::from_value(package.frames().clone()).unwrap();
        assert_eq!(description.props.len(), prepared.objects.len());
        for (object, original) in prepared.objects.iter().zip(&description.props) {
            assert_eq!(object.asset_id, AssetId::deterministic(&object.source_id));
            assert_eq!(object.source_id, original.source_id);
            assert_eq!(object.parts.len(), original.parts.len());
            for (part, source) in object.parts.iter().zip(&original.parts) {
                parts += 1;
                assert_eq!(part.surface, source.material);
                for (attribute, name) in [
                    (Mesh::ATTRIBUTE_POSITION, "pos"),
                    (Mesh::ATTRIBUTE_NORMAL, "nrm"),
                    (Mesh::ATTRIBUTE_UV_0, "uv"),
                    (TINT, "tint"),
                    (DATA, "data"),
                ] {
                    let reference = source.attributes.get(name).unwrap();
                    assert_attribute(&part.mesh, attribute, bytes(&package, reference).unwrap());
                }
                assert_attribute(
                    &part.mesh,
                    SOURCE_REST_POSITION,
                    bytes(&package, &source.reference_position).unwrap(),
                );
                assert_indices(&part.mesh, bytes(&package, &source.indices).unwrap());
                main += part.mesh.indices().unwrap().len() / 3;
                let shadow_bytes = bytes(&package, &source.shadow_indices).unwrap();
                assert_eq!(part.shadow_mesh.is_none(), shadow_bytes.is_empty());
                if part.surface == DebrisSurface::Fabric {
                    assert!(part.shadow_mesh.is_none());
                }
                if let Some(mesh) = &part.shadow_mesh {
                    assert_indices(mesh, shadow_bytes);
                    for attribute in [
                        Mesh::ATTRIBUTE_POSITION,
                        Mesh::ATTRIBUTE_NORMAL,
                        Mesh::ATTRIBUTE_UV_0,
                        TINT,
                        DATA,
                        SOURCE_REST_POSITION,
                    ] {
                        assert_eq!(mesh.attribute(attribute), part.mesh.attribute(attribute));
                    }
                    shadow += mesh.indices().unwrap().len() / 3;
                }
            }
        }
        assert_eq!(parts, 865);
        assert_eq!(main, prepared.triangles);
        assert_eq!(shadow, prepared.shadow_triangles);
    }
}
