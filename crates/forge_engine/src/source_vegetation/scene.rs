//! Caller-decoded source geometry, one selection and entity/buffer lifecycle for reference and module hosts.
//! These are derived adapter inputs, not a second canonical document format.
use super::{LOBE, MAT, Record, VEG, world::SourceMaterial};
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    mesh::{Indices, MeshTag},
    prelude::*,
    render::{render_resource::PrimitiveTopology, storage::ShaderBuffer},
};
use serde::Deserialize;
use std::collections::BTreeMap;
pub mod resident;
pub mod shadow;
#[cfg(feature = "physics-rapier")]
pub mod streamed;

/// Prepare the same authored source projection for editor and game hosts,
/// without imposing the acquisition's reference population on the module.
#[cfg(feature = "physics-rapier")]
pub fn from_population(
    package: &forge_assets::source_vegetation_package::PlantPackage,
    population: &forge_world::imported_vegetation::ImportedPopulation,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    heights: &[u8],
) -> Result<Vegetation, String> {
    let terrain = head
        .effective()?
        .0
        .content_hash()
        .map_err(|error| error.to_string())?;
    streamed::Prepared::new(
        package,
        package.decode_geometry()?,
        population,
        head,
        heights,
        terrain,
    )
    .map(streamed::Prepared::into_scene)
}
#[cfg(test)]
mod tests;
mod validation;
#[derive(Deserialize)]
pub struct LevelDescription {
    pub near: bool,
    pub capacity: usize,
    pub range: [f32; 3],
    pub kind: u8,
    pub attributes: BTreeMap<String, String>,
    pub indices: String,
}
pub struct Level {
    pub description: LevelDescription,
    pub mesh: Mesh,
}
pub struct Group {
    pub identities: Option<Vec<forge_core::EntityId>>,
    pub name: String,
    pub query_radius: f64,
    pub cell_size: f64,
    pub far_exclude_near: bool,
    pub near_range: f64,
    pub refresh_distance: f64,
    pub sort_far: bool,
    pub records: Vec<Record>,
    pub models: Vec<Mat4>,
    pub query_radii: Vec<f32>,
    pub levels: Vec<Level>,
}
pub struct Vegetation {
    pub head_hash: Option<forge_core::ContentHash>,
    pub groups: Vec<Group>,
}

impl Vegetation {
    /// One source selection for reference scenes and installed modules alike.
    /// Levels retain their original ranges, capacities and overlapping fades.
    pub fn selection_indices(&self, camera: Vec3) -> Result<Vec<Vec<Vec<usize>>>, String> {
        if !camera.is_finite()
            || self
                .groups
                .iter()
                .any(|group| group.records.len() != group.query_radii.len())
        {
            return Err("Invalid plant view".into());
        }
        Ok(self
            .groups
            .iter()
            .map(|group| {
                if group.records.is_empty() {
                    return Vec::new();
                }
                let near = near_ids(group, camera);
                let mut far = None;
                group
                    .levels
                    .iter()
                    .map(|level| {
                        let ids = if level.description.near {
                            &near
                        } else {
                            far.get_or_insert_with(|| far_ids(group, camera))
                        };
                        ids[..ids.len().min(level.description.capacity)].to_vec()
                    })
                    .collect()
            })
            .collect())
    }
}

#[must_use]
pub fn mesh_from_geometry(
    geometry: forge_assets::source_vegetation_geometry::SourceVegetationGeometry,
) -> Mesh {
    let count = geometry.positions.len();
    let mut result = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    result.insert_attribute(Mesh::ATTRIBUTE_POSITION, geometry.positions);
    result.insert_attribute(Mesh::ATTRIBUTE_NORMAL, geometry.normals);
    result.insert_attribute(
        Mesh::ATTRIBUTE_UV_0,
        geometry.uv.unwrap_or_else(|| vec![[0.0; 2]; count]),
    );
    for (values, attribute) in [
        (geometry.vegetation, VEG),
        (geometry.material, MAT),
        (geometry.lobe, LOBE),
    ] {
        result.insert_attribute(attribute, values.unwrap_or_else(|| vec![[0.0; 4]; count]));
    }
    result.insert_indices(Indices::U32(geometry.indices));
    result
}

/// Canonical identity survives camera/LOD changes; this is not yet a picking backend.
#[derive(Component, Clone, Copy, Debug)]
pub struct ImportedPlant {
    pub id: forge_core::EntityId,
    pub head_hash: Option<forge_core::ContentHash>,
}

fn near_ids(group: &Group, camera: Vec3) -> Vec<usize> {
    crate::source_vegetation_selection::near_indices(
        group.records.len(),
        |i| group.records[i].i_pos.truncate().to_array(),
        |i| group.query_radii[i],
        camera.to_array(),
        crate::source_vegetation_selection::NearPolicy {
            query_radius_m: group.query_radius,
            cell_size_m: group.cell_size,
        },
    )
}

fn far_ids(group: &Group, camera: Vec3) -> Vec<usize> {
    crate::source_vegetation_selection::far_indices(
        group.records.len(),
        |i| group.records[i].i_pos.truncate().to_array(),
        camera.to_array(),
        crate::source_vegetation_selection::FarPolicy {
            exclude_near: group.far_exclude_near,
            near_range_m: group.near_range,
            refresh_distance_m: group.refresh_distance,
            sort_far: group.sort_far,
        },
    )
}

#[derive(Clone, Copy)]
pub struct View {
    pub camera: Vec3,
    pub visibility: Visibility,
}

/// Caller owns returned derived entities and can retire them on revision/view change.
pub fn spawn<M: SourceMaterial>(
    commands: &mut Commands,
    scene: &Vegetation,
    base: &M,
    view: View,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<M>,
    buffers: &mut Assets<ShaderBuffer>,
) -> Result<Vec<Entity>, String> {
    scene.validate_material::<M>()?;
    if !view.camera.is_finite() {
        return Err("Invalid plant view".into());
    }
    let selected = scene.selection_indices(view.camera)?;
    let visibility = view.visibility;
    let mut entities = Vec::new();
    for (group, selection) in scene.groups.iter().zip(&selected) {
        if group.records.is_empty() {
            continue;
        }
        let records = buffers.add(ShaderBuffer::from(group.records.clone()));
        let models = buffers.add(ShaderBuffer::from(group.models.clone()));
        for (level, ids) in group.levels.iter().zip(selection) {
            let data = &level.description;
            let count = ids.len();
            let mut material = base.clone();
            material.set_source(data.kind, records.clone(), models.clone());
            material.params_mut().gust_lod.z = data.range[0];
            material.params_mut().gust_lod.w = data.range[1];
            material.params_mut().lod_end_frame.x = data.range[2];
            let material = materials.add(material);
            let mesh = meshes.add(level.mesh.clone());
            for &index in ids.iter().take(count) {
                let entity = commands
                    .spawn((
                        Mesh3d(mesh.clone()),
                        MeshMaterial3d(material.clone()),
                        MeshTag(u32::try_from(index).expect("bounded source count")),
                        Transform::IDENTITY,
                        NoFrustumCulling,
                        visibility,
                    ))
                    .id();
                if !M::enable_shadows() {
                    commands.entity(entity).insert(NotShadowCaster);
                }
                if let Some(ids) = &group.identities {
                    commands.entity(entity).insert(ImportedPlant {
                        id: ids[index],
                        head_hash: scene.head_hash,
                    });
                }
                entities.push(entity);
            }
            info!(
                group = group.name,
                near = data.near,
                instances = count,
                "Source vegetation level submitted"
            );
        }
    }
    Ok(entities)
}

impl From<forge_assets::source_vegetation_package::scene::Level> for LevelDescription {
    fn from(d: forge_assets::source_vegetation_package::scene::Level) -> Self {
        Self {
            near: d.near,
            capacity: d.capacity,
            range: d.range,
            kind: d.kind,
            attributes: d.attributes,
            indices: d.indices,
        }
    }
}

#[cfg(feature = "physics-rapier")]
pub(super) fn read_vectors<const N: usize>(
    bytes: &[u8],
) -> Result<Vec<[f32; N]>, Box<dyn std::error::Error>> {
    if N == 0 || !bytes.len().is_multiple_of(N * 4) {
        return Err("invalid attribute size".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|chunk| {
            let mut result = [0.0; N];
            for (index, value) in chunk.iter().enumerate() {
                result[index] = f32::from_le_bytes(*value);
                if !result[index].is_finite() {
                    return Err("nonfinite attribute".into());
                }
            }
            Ok(result)
        })
        .collect()
}

#[cfg(test)]
mod selection_tests {
    use super::*;

    fn group() -> Group {
        Group {
            identities: None,
            name: "selection-boundaries".into(),
            query_radius: 512.0,
            cell_size: 64.0,
            far_exclude_near: false,
            near_range: 20.0,
            refresh_distance: 2.0,
            sort_far: true,
            records: [12.0, 6.0, 11.999, 0.0, -6.0, 6144.0, 1.0e30, 6140.0]
                .map(|x| Record {
                    i_pos: Vec4::new(x, 0.0, 0.0, 1.0),
                    i_dat: Vec4::ZERO,
                })
                .into(),
            models: Vec::new(),
            query_radii: Vec::new(),
            levels: Vec::new(),
        }
    }

    #[test]
    fn far_selection_retains_source_ties_bucket_boundaries_and_capacity_prefixes() {
        let group = group();
        let ids = far_ids(&group, Vec3::ZERO);
        // Exact 6 m/12 m boundaries, tied near buckets, and the 1023 clamp.
        assert_eq!(ids, [3, 1, 2, 4, 0, 5, 6, 7]);
        for (capacity, expected) in [
            (0, &[][..]),
            (2, &[3, 1][..]),
            (4, &[3, 1, 2, 4][..]),
            (8, &[3, 1, 2, 4, 0, 5, 6, 7][..]),
        ] {
            assert_eq!(&ids[..ids.len().min(capacity)], expected);
        }
        // A moved camera must recompute the keys and preserve newly tied order.
        assert_eq!(far_ids(&group, Vec3::X), [1, 3, 0, 2, 4, 5, 6, 7]);
    }

    #[test]
    fn far_near_exclusion_uses_vertical_distance_and_keeps_unsorted_source_order() {
        let mut group = group();
        group.far_exclude_near = true;
        group.records[1].i_pos.y = 30.0;
        assert_eq!(far_ids(&group, Vec3::ZERO), [1, 5, 6, 7]);
        group.sort_far = false;
        group.far_exclude_near = false;
        assert_eq!(far_ids(&group, Vec3::ZERO), (0..8).collect::<Vec<_>>());
    }
}
