// SPDX-License-Identifier: MIT
// Adapted from dgreenheck/tidewater, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Prepare a verified source-fish profile for any module.
//!
//! The caller owns package admission, provenance, inventory and asset identities.
//! This module decodes geometry and poses but does not choose a storage path or
//! grant permission to distribute the payload.
use super::{material::SurfaceParameters, pose};
use bevy::prelude::Mesh;
use forge_core::{AssetId, Transform, UnitQuaternion, Vec3};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Deserialize)]
struct Description {
    prototypes: Vec<Prototype>,
    instances: Vec<Instance>,
}

#[derive(Deserialize)]
struct Prototype {
    key: String,
    lods: Vec<Geometry>,
}

#[derive(Deserialize)]
struct Geometry {
    lod: usize,
    vertices: usize,
    triangles: usize,
    attributes: BTreeMap<String, Attribute>,
    indices: Attribute,
}

#[derive(Deserialize)]
struct Attribute {
    file: String,
    component_type: String,
}

#[derive(Deserialize)]
struct Instance {
    source_id: String,
    prototype: String,
    record_f32: [f32; 16],
}

struct Model {
    geometry: super::geometry::Geometry,
}

pub struct FishPropTemplate {
    pub source_id: String,
    pub prototype: String,
    pub asset_id: AssetId,
    pub transform: Transform,
    pub surface: SurfaceParameters,
    /// High, low and shadow-only source models, all locally posed.
    pub lod_meshes: Vec<Mesh>,
}

/// Identity and authored pose without mesh or renderer state.
#[derive(Clone, Debug)]
pub struct FishPropsTemplate {
    pub source_id: String,
    pub asset_id: AssetId,
    pub transform: Transform,
}

#[must_use]
pub fn summaries(templates: &[FishPropTemplate]) -> Vec<FishPropsTemplate> {
    templates
        .iter()
        .map(|template| FishPropsTemplate {
            source_id: template.source_id.clone(),
            asset_id: template.asset_id,
            transform: template.transform,
        })
        .collect()
}

/// Prepare the caller-admitted profile using its declared inventory and identities.
///
/// # Errors
/// Refuses the same invalid inventory, identities, geometry and poses as `prepare`.
pub fn profile(
    profile: &(impl forge_assets::source_fish_payload::SourceFishProfile + ?Sized),
) -> Result<Vec<FishPropTemplate>, String> {
    let (prototypes, instances) = profile.inventory();
    prepare(
        profile.description(),
        |name| profile.file(name),
        Inventory {
            prototypes,
            instances,
        },
        |ordinal, source_id| profile.identify(ordinal, source_id),
    )
}

/// Transfer prepared meshes and the verified package pin to the common resident host.
#[cfg(feature = "physics-rapier")]
#[must_use]
pub fn resident_templates(
    templates: Vec<FishPropTemplate>,
    asset_hash: forge_core::ContentHash,
) -> Vec<super::resident::Template> {
    templates
        .into_iter()
        .map(|template| super::resident::Template {
            source_id: template.source_id,
            asset_id: template.asset_id,
            asset_hash,
            lod_meshes: template.lod_meshes,
        })
        .collect()
}

/// Inventory expected by the caller after it verifies the source package.
#[derive(Clone, Copy, Debug)]
pub struct Inventory {
    pub prototypes: usize,
    pub instances: usize,
}

/// Decode a verified source profile into CPU meshes without publishing GPU state.
///
/// The identity callback chooses which rows belong to this module, and must
/// explicitly supply the asset ID for each selected row.
///
/// # Errors
/// Rejects incorrect inventory, duplicate identities, invalid geometry or poses,
/// missing payloads and any identity rejected by the caller.
pub fn prepare<'a>(
    description: &[u8],
    file: impl Fn(&str) -> Option<&'a [u8]>,
    inventory: Inventory,
    mut identify: impl FnMut(usize, &str) -> Result<Option<AssetId>, String>,
) -> Result<Vec<FishPropTemplate>, String> {
    let description: Description =
        serde_json::from_slice(description).map_err(|error| error.to_string())?;
    if inventory.prototypes == 0
        || description.prototypes.len() != inventory.prototypes
        || description.instances.len() != inventory.instances
    {
        return Err("Fish source inventory differs".into());
    }
    let mut models = BTreeMap::new();
    for prototype in description.prototypes {
        if prototype.lods.len() != 3 {
            return Err("Three source fish LOD required".into());
        }
        let lods = prototype
            .lods
            .iter()
            .enumerate()
            .map(|(lod, geometry)| {
                if geometry.lod != lod {
                    return Err("Fish LOD order differs".into());
                }
                Model::read(&file, geometry)
            })
            .collect::<Result<Vec<_>, String>>()?;
        if models.insert(prototype.key, lods).is_some() {
            return Err("Repeated source fish prototype".into());
        }
    }
    let skin = file("material/skin-table-f32.bin").ok_or("Fish skin table absent")?;
    let skin = vectors::<4>(skin)?;
    let mut ids = BTreeSet::new();
    let mut sources = BTreeSet::new();
    let mut prepared = Vec::new();
    for (ordinal, instance) in description.instances.into_iter().enumerate() {
        let Some(asset_id) = identify(ordinal, &instance.source_id)? else {
            continue;
        };
        if !ids.insert(asset_id) || !sources.insert(instance.source_id.clone()) {
            return Err("Repeated source fish identity".into());
        }
        prepared.push(prepare_instance(asset_id, instance, &models, &skin)?);
    }
    Ok(prepared)
}

fn prepare_instance(
    asset_id: AssetId,
    instance: Instance,
    models: &BTreeMap<String, Vec<Model>>,
    skin: &[[f32; 4]],
) -> Result<FishPropTemplate, String> {
    let record = instance.record_f32;
    if record.iter().any(|value| !value.is_finite()) || record[3] <= 0.0 {
        return Err("Invalid fish instance record".into());
    }
    let pattern = (0_u8..21)
        .find(|&value| f32::from(value).to_bits() == record[8].floor().to_bits())
        .ok_or("Fish skin pattern absent")?;
    let hinge = skin
        .get(usize::from(pattern) * 8 + 7)
        .ok_or("Fish skin hinge absent")?;
    let bending = pose::Bend {
        curl: record[9],
        sag: record[10],
        jaw: record[11],
        hinge: [hinge[2], hinge[3]],
    };
    let transform = Transform {
        translation_m: Vec3::new(record[0], record[1], record[2]),
        rotation: UnitQuaternion::new(record[4], record[5], record[6], record[7]),
        scale: Vec3::new(record[3], record[3], record[3]),
    };
    if (record[4..8].iter().map(|value| value * value).sum::<f32>() - 1.0).abs() > 0.001 {
        return Err("Fish source rotation is not unit length".into());
    }
    let source = models.get(&instance.prototype).ok_or("Fish model absent")?;
    let lod_meshes = source
        .iter()
        .map(|model| model.mesh(bending))
        .collect::<Result<Vec<_>, _>>()?;
    Ok(FishPropTemplate {
        source_id: instance.source_id,
        prototype: instance.prototype,
        asset_id,
        transform,
        surface: SurfaceParameters {
            pattern_and_seed: record[8],
            source_length_m: record[3],
            flags: record[12..16].try_into().map_err(|_| "Fish flags absent")?,
        },
        lod_meshes,
    })
}

impl Model {
    fn read<'a>(
        file: &impl Fn(&str) -> Option<&'a [u8]>,
        geometry: &Geometry,
    ) -> Result<Self, String> {
        let attribute = |name: &str| -> Result<&[u8], String> {
            let reference = geometry
                .attributes
                .get(name)
                .ok_or("Fish attribute absent")?;
            if reference.component_type != "Float32Array" {
                return Err("Fish attribute type differs".into());
            }
            file(&format!("geometry/{}", reference.file))
                .ok_or_else(|| "Fish attribute payload absent".into())
        };
        let positions = vectors::<3>(attribute("position")?)?;
        let normals = vectors::<3>(attribute("normal")?)?;
        let data = vectors::<4>(attribute("aData")?)?;
        let bytes =
            file(&format!("geometry/{}", geometry.indices.file)).ok_or("Fish indices absent")?;
        if geometry.indices.component_type != "Uint16Array" || !bytes.len().is_multiple_of(2) {
            return Err("Fish index component differs".into());
        }
        let indices: Vec<_> = bytes
            .as_chunks::<2>()
            .0
            .iter()
            .map(|value| u16::from_le_bytes(*value))
            .collect();
        if positions.len() != geometry.vertices
            || Some(indices.len()) != geometry.triangles.checked_mul(3)
        {
            return Err("Fish geometry shape or indices differ".into());
        }
        Ok(Self {
            geometry: super::geometry::Geometry::new(positions, normals, data, indices)?,
        })
    }

    fn mesh(&self, bending: pose::Bend) -> Result<Mesh, String> {
        self.geometry.mesh(bending)
    }
}

fn vectors<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, String> {
    if bytes.is_empty() || !bytes.len().is_multiple_of(N * 4) {
        return Err("Fish attribute width differs".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|words| {
            let values: [f32; N] = std::array::from_fn(|i| f32::from_le_bytes(words[i]));
            if values.iter().any(|value| !value.is_finite()) {
                return Err("Non-finite fish attribute".into());
            }
            Ok(values)
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;

    struct IndependentProfile {
        description: Vec<u8>,
        files: BTreeMap<String, Vec<u8>>,
    }

    impl forge_assets::source_fish_payload::SourceFishPayload for IndependentProfile {
        fn file(&self, name: &str) -> Option<&[u8]> {
            self.files.get(name).map(Vec::as_slice)
        }
    }

    impl forge_assets::source_fish_payload::SourceFishProfile for IndependentProfile {
        fn description(&self) -> &[u8] {
            &self.description
        }
        fn inventory(&self) -> (usize, usize) {
            (1, 2)
        }
        fn identify(&self, ordinal: usize, source_id: &str) -> Result<Option<AssetId>, String> {
            if source_id != format!("other-module/fish/{ordinal}") {
                return Err("unexpected independent identity".into());
            }
            Ok((ordinal == 1).then(|| AssetId::deterministic(source_id)))
        }
    }

    fn floats(values: &[f32]) -> Vec<u8> {
        values
            .iter()
            .flat_map(|value| value.to_le_bytes())
            .collect()
    }

    fn fixture() -> (Vec<u8>, BTreeMap<String, Vec<u8>>) {
        let attribute = |name| serde_json::json!({"file": name, "component_type": "Float32Array"});
        let lods: Vec<_> = (0..3)
            .map(|lod| serde_json::json!({
                "lod": lod, "vertices": 3, "triangles": 1,
                "attributes": {"position":attribute("position.bin"), "normal":attribute("normal.bin"), "aData":attribute("data.bin")},
                "indices": {"file":"indices.bin", "component_type":"Uint16Array"}
            }))
            .collect();
        let record = [
            1.0_f32, 2.0, 3.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        ];
        let description = serde_json::to_vec(&serde_json::json!({
            "prototypes": [{"key":"custom-fish", "lods":lods}],
            "instances": [
                {"source_id":"other-module/fish/0", "prototype":"custom-fish", "record_f32":record},
                {"source_id":"other-module/fish/1", "prototype":"custom-fish", "record_f32":record}
            ]
        }))
        .unwrap();
        let files = BTreeMap::from([
            (
                "geometry/position.bin".into(),
                floats(&[0.0, 0.0, 0.0, 0.2, 0.0, 0.5, 0.0, 0.2, 1.0]),
            ),
            (
                "geometry/normal.bin".into(),
                floats(&[0.0, 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 1.0, 0.0]),
            ),
            (
                "geometry/data.bin".into(),
                floats(&[0.0, 0.4, 0.0, 0.0, 0.0, 0.4, 0.0, 0.0, 0.0, 0.4, 0.0, 0.0]),
            ),
            (
                "geometry/indices.bin".into(),
                [0_u16, 1, 2]
                    .into_iter()
                    .flat_map(u16::to_le_bytes)
                    .collect(),
            ),
            ("material/skin-table-f32.bin".into(), floats(&[0.0; 32])),
        ]);
        (description, files)
    }

    #[test]
    fn another_module_can_prepare_selected_fish_with_its_own_identity() {
        let (description, files) = fixture();
        let asset = AssetId::deterministic("other-module/fish/1");
        let prepared = profile(&IndependentProfile { description, files }).unwrap();
        assert_eq!(prepared.len(), 1);
        assert_eq!(prepared[0].asset_id, asset);
        assert_eq!(prepared[0].source_id, "other-module/fish/1");
        assert_eq!(
            prepared[0].transform.translation_m,
            Vec3::new(1.0, 2.0, 3.0)
        );
        assert_eq!(prepared[0].lod_meshes.len(), 3);
        for mesh in &prepared[0].lod_meshes {
            assert_eq!(mesh.count_vertices(), 3);
            assert_eq!(mesh.indices().unwrap().len(), 3);
            let Some(VertexAttributeValues::Float32x3(values)) =
                mesh.attribute(super::super::geometry::REST_POSITION)
            else {
                panic!("Missing rest positions");
            };
            for (&actual, expected) in values[2].iter().zip([0.0_f32, 0.2, 1.0]) {
                assert_eq!(
                    actual.partial_cmp(&expected),
                    Some(std::cmp::Ordering::Equal)
                );
            }
        }
    }

    #[test]
    fn preparation_refuses_duplicate_ids_bad_inventory_and_invalid_geometry() {
        let (description, mut files) = fixture();
        let asset = AssetId::deterministic("other-module/fish");
        let read = |name: &str| files.get(name).map(Vec::as_slice);
        assert!(
            prepare(
                &description,
                read,
                Inventory {
                    prototypes: 1,
                    instances: 2
                },
                |_, _| Ok(Some(asset))
            )
            .is_err()
        );
        assert!(
            prepare(
                &description,
                read,
                Inventory {
                    prototypes: 1,
                    instances: 3
                },
                |_, _| Ok(Some(asset))
            )
            .is_err()
        );
        assert!(
            prepare(
                &description,
                read,
                Inventory {
                    prototypes: 1,
                    instances: 2
                },
                |_, _| Err("unverified identity".into())
            )
            .is_err()
        );
        files.insert(
            "geometry/indices.bin".into(),
            [0_u16, 1, 3]
                .into_iter()
                .flat_map(u16::to_le_bytes)
                .collect(),
        );
        assert!(
            prepare(
                &description,
                |name| files.get(name).map(Vec::as_slice),
                Inventory {
                    prototypes: 1,
                    instances: 2
                },
                |ordinal, _| Ok(Some(AssetId::deterministic(format!(
                    "other-module/{ordinal}"
                ))))
            )
            .is_err()
        );
    }
}
