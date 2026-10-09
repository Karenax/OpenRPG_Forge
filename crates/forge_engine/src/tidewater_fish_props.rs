//! Prepare verified fish instance templates and meshes without substituting a material.
#[path = "tidewater_fish_props/clock.rs"]
pub mod clock;
#[path = "tidewater_fish_props/lod.rs"]
pub mod lod;
#[path = "tidewater_fish_props/material.rs"]
pub mod material;
#[path = "tidewater_fish_props/pose.rs"]
pub mod pose;
#[path = "tidewater_fish_props/resident.rs"]
pub mod resident;
#[path = "tidewater_fish_props/shadow.rs"]
pub mod shadow;
#[path = "tidewater_fish_props/surface.rs"]
pub mod surface;

use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, MeshVertexAttribute},
    prelude::Mesh,
    render::render_resource::{PrimitiveTopology, VertexFormat},
};
use forge_assets::tidewater_fish_props::{self as fish_props, FishPropPackage};
use forge_core::{AssetId, Transform, UnitQuaternion, Vec3};
use serde::Deserialize;
use std::collections::BTreeMap;

pub const REST_POSITION: MeshVertexAttribute =
    MeshVertexAttribute::new("FishRestPosition", 0x7457_0010, VertexFormat::Float32x3);
pub const FISH_DATA: MeshVertexAttribute =
    MeshVertexAttribute::new("FishData", 0x7457_0011, VertexFormat::Float32x4);

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
    positions: Vec<[f32; 3]>,
    normals: Vec<[f32; 3]>,
    data: Vec<[f32; 4]>,
    indices: Vec<u16>,
}

/// Retained procedural material inputs; a dedicated material must consume these.
#[derive(Clone, Copy, Debug)]
pub struct SurfaceParameters {
    pub pattern_and_seed: f32,
    pub source_length_m: f32,
    /// Cloudy eye, wetness, dried skin and blood, in original source order.
    pub flags: [f32; 4],
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

/// Lightweight source identity for authoring; no mesh or GPU handle is persistent.
#[derive(Clone, Debug)]
pub struct FishPropsTemplate {
    pub source_id: String,
    pub asset_id: AssetId,
    pub transform: Transform,
}

/// Verified CPU meshes; no GPU handles, colliders or render entities are published here.
pub struct PreparedFishProps {
    pub templates: Vec<FishPropTemplate>,
}

impl PreparedFishProps {
    #[must_use]
    pub fn templates(&self) -> Vec<FishPropsTemplate> {
        self.templates
            .iter()
            .map(|template| FishPropsTemplate {
                source_id: template.source_id.clone(),
                asset_id: template.asset_id,
                transform: template.transform,
            })
            .collect()
    }

    /// Preserve the source inventory, rest-shading attributes and static poses.
    ///
    /// # Errors
    /// Rejects missing attributes, invalid indices, transforms or source identities.
    pub fn from_package(package: &FishPropPackage) -> Result<Self, String> {
        Self::from_source(
            package.description(),
            |name| package.file(name),
            Inventory::Village,
        )
    }

    /// Prepare the five displayed game fish, excluding the parked catch pool.
    /// Preserves all three source LOD, original poses and material inputs.
    ///
    /// # Errors
    /// Rejects invalid geometry, identities, skin tables or source transforms.
    pub fn from_game_package(
        package: &forge_assets::tidewater_game_fish::GameFishPackage,
    ) -> Result<Self, String> {
        Self::from_source(
            package.description(),
            |name| package.file(name),
            Inventory::Stand,
        )
    }

    fn from_source<'a>(
        description: &[u8],
        file: impl Fn(&str) -> Option<&'a [u8]>,
        inventory: Inventory,
    ) -> Result<Self, String> {
        let description: Description =
            serde_json::from_slice(description).map_err(|error| error.to_string())?;
        let (count, first, kind) = match inventory {
            Inventory::Village => (78, 0, "fish-prop"),
            Inventory::Stand => (23, 18, "game-fish"),
        };
        if description.prototypes.len() != 18 || description.instances.len() != count {
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
        let templates = description
            .instances
            .into_iter()
            .enumerate()
            .skip(first)
            .map(|(ordinal, instance)| {
                let asset_id = match inventory {
                    Inventory::Village => fish_props::asset_id(ordinal),
                    Inventory::Stand => forge_assets::tidewater_game_fish::asset_id(ordinal),
                }
                .ok_or("Fish ordinal out of range")?;
                let expected =
                    format!("tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:{kind}:{ordinal}");
                if instance.source_id != expected {
                    return Err("Fish source identity differs".into());
                }
                prepare_instance(asset_id, instance, &models, &skin)
            })
            .collect::<Result<Vec<_>, String>>()?;
        Ok(Self { templates })
    }
}

#[derive(Clone, Copy)]
enum Inventory {
    Village,
    Stand,
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
            || normals.len() != positions.len()
            || data.len() != positions.len()
            || indices.is_empty()
            || indices.len() != geometry.triangles * 3
            || indices
                .iter()
                .any(|&index| usize::from(index) >= positions.len())
        {
            return Err("Fish geometry shape or indices differ".into());
        }
        Ok(Self {
            positions,
            normals,
            data,
            indices,
        })
    }

    fn mesh(&self, bending: pose::Bend) -> Result<Mesh, String> {
        let posed = self
            .positions
            .iter()
            .zip(&self.normals)
            .zip(&self.data)
            .map(|((&position, &normal), &data)| pose::deform(position, normal, data, bending))
            .collect::<Result<Vec<_>, _>>()?;
        Ok(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_POSITION,
            posed
                .iter()
                .map(|vertex| vertex.position)
                .collect::<Vec<_>>(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_NORMAL,
            posed.iter().map(|vertex| vertex.normal).collect::<Vec<_>>(),
        )
        .with_inserted_attribute(REST_POSITION, self.positions.clone())
        .with_inserted_attribute(FISH_DATA, self.data.clone())
        .with_inserted_indices(Indices::U16(self.indices.clone())))
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
    use std::path::Path;

    #[test]
    #[ignore = "requires the captured GameFish source package"]
    fn game_stand_excludes_pool_and_preserves_three_source_lods() {
        let root = std::env::var("FORGE_TIDEWATER_GAME_FISH_PACKAGE")
            .expect("Real game fish package required");
        let root = Path::new(&root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = forge_assets::tidewater_game_fish::GameFishPackage::load(
            &manifest,
            forge_assets::tidewater_game_fish::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap();
        let prepared = PreparedFishProps::from_game_package(&package).unwrap();
        let description: Description = serde_json::from_slice(package.description()).unwrap();
        assert_eq!(prepared.templates.len(), 5);
        for (ordinal, template) in (18..23).zip(prepared.templates) {
            assert_eq!(
                Some(template.asset_id),
                forge_assets::tidewater_game_fish::asset_id(ordinal)
            );
            let instance = &description.instances[ordinal];
            assert_eq!(template.source_id, instance.source_id);
            assert_eq!(
                template.surface.pattern_and_seed.to_bits(),
                instance.record_f32[8].to_bits()
            );
            assert_eq!(
                template.surface.flags.map(f32::to_bits),
                instance.record_f32[12..16]
                    .iter()
                    .map(|v| v.to_bits())
                    .collect::<Vec<_>>()
                    .as_slice()
            );
            let prototype = description
                .prototypes
                .iter()
                .find(|p| p.key == instance.prototype)
                .unwrap();
            assert_eq!(template.lod_meshes.len(), 3);
            for (mesh, geometry) in template.lod_meshes.iter().zip(&prototype.lods) {
                assert_eq!(mesh.count_vertices(), geometry.vertices);
                assert_eq!(mesh.indices().unwrap().len(), geometry.triangles * 3);
                for (name, attribute) in [("position", REST_POSITION), ("aData", FISH_DATA)] {
                    let source = package
                        .file(&format!("geometry/{}", geometry.attributes[name].file))
                        .unwrap();
                    let actual = match mesh.attribute(attribute).unwrap() {
                        VertexAttributeValues::Float32x3(values) => values
                            .iter()
                            .flat_map(|v| v.iter().flat_map(|f| f.to_le_bytes()))
                            .collect::<Vec<_>>(),
                        VertexAttributeValues::Float32x4(values) => values
                            .iter()
                            .flat_map(|v| v.iter().flat_map(|f| f.to_le_bytes()))
                            .collect::<Vec<_>>(),
                        _ => panic!("Unexpected fish attribute format"),
                    };
                    assert_eq!(actual, source);
                }
            }
        }
    }

    #[test]
    fn all_source_templates_retain_lod_and_material_inputs() {
        let root =
            std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").expect("Real fish package required");
        let root = Path::new(&root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = FishPropPackage::load(&manifest, fish_props::package_hash(), |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let prepared = PreparedFishProps::from_package(&package).unwrap();
        assert_eq!(prepared.templates.len(), 78);
        let mut vertices = 0;
        for (ordinal, template) in prepared.templates.iter().enumerate() {
            assert_eq!(Some(template.asset_id), fish_props::asset_id(ordinal));
            assert_eq!(template.lod_meshes.len(), 3);
            for mesh in &template.lod_meshes {
                vertices += mesh.count_vertices();
                assert!(matches!(
                    mesh.attribute(REST_POSITION),
                    Some(VertexAttributeValues::Float32x3(_))
                ));
                assert!(matches!(
                    mesh.attribute(FISH_DATA),
                    Some(VertexAttributeValues::Float32x4(_))
                ));
            }
            assert_eq!(
                template.lod_meshes[1].indices(),
                template.lod_meshes[2].indices()
            );
            assert_eq!(
                template.lod_meshes[1].attribute(Mesh::ATTRIBUTE_POSITION),
                template.lod_meshes[2].attribute(Mesh::ATTRIBUTE_POSITION)
            );
        }
        assert_eq!(vertices, 169_709);
    }
}
