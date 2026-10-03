//! Validated packed vegetation and source-order LOD selection for shared scenes.
use super::canonical;
use super::{LOBE, MAT, PlantMaterial, Record, VEG};
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    mesh::{Indices, MeshTag},
    prelude::*,
    render::{render_resource::PrimitiveTopology, storage::ShaderBuffer},
};
use serde::Deserialize;
use std::{collections::BTreeMap, error::Error};
pub mod resident;
pub mod shadow;

#[derive(Deserialize)]
struct Description {
    schema: String,
    version: u32,
    groups: Vec<GroupDescription>,
}
#[derive(Deserialize)]
struct GroupDescription {
    name: String,
    count: usize,
    query_radius: f64,
    cell_size: f64,
    far_exclude_near: bool,
    near_range: f64,
    refresh_distance: f64,
    sort_far: bool,
    streams: BTreeMap<String, String>,
    levels: Vec<LevelDescription>,
}
#[derive(Deserialize)]
pub struct LevelDescription {
    near: bool,
    capacity: usize,
    range: [f32; 3],
    kind: u8,
    attributes: BTreeMap<String, String>,
    indices: String,
}
struct Level {
    description: LevelDescription,
    mesh: Mesh,
}
struct Group {
    identities: Option<Vec<forge_core::EntityId>>,
    name: String,
    query_radius: f64,
    cell_size: f64,
    far_exclude_near: bool,
    near_range: f64,
    refresh_distance: f64,
    sort_far: bool,
    records: Vec<Record>,
    models: Vec<Mat4>,
    query_radii: Vec<f32>,
    levels: Vec<Level>,
}
pub struct Vegetation {
    head_hash: Option<forge_core::ContentHash>,
    groups: Vec<Group>,
}

pub fn mesh(
    data: &LevelDescription,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<Mesh, Box<dyn Error>> {
    let get = |name: &str| files.get(name).ok_or("missing vegetation payload");
    let attribute = |name: &str| {
        data.attributes
            .get(name)
            .ok_or("missing vegetation attribute")
    };
    let positions = read_vectors::<3>(get(attribute("position")?)?)?;
    let count = positions.len();
    if count == 0 || count > 100_000 {
        return Err("vegetation vertex bound".into());
    }
    let normals = read_vectors::<3>(get(attribute("normal")?)?)?;
    let uv = if let Some(path) = data.attributes.get("uv") {
        read_vectors::<2>(get(path)?)?
    } else {
        vec![[0.0; 2]; count]
    };
    let mut result = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    if normals.len() != count || uv.len() != count {
        return Err("vegetation attribute count".into());
    }
    result.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    result.insert_attribute(Mesh::ATTRIBUTE_NORMAL, normals);
    result.insert_attribute(Mesh::ATTRIBUTE_UV_0, uv);
    for (name, attribute) in [("aVeg", VEG), ("aMat", MAT), ("aLobe", LOBE)] {
        let values = if let Some(path) = data.attributes.get(name) {
            read_vectors::<4>(get(path)?)?
        } else {
            vec![[0.0; 4]; count]
        };
        if values.len() != count {
            return Err("vegetation attribute count".into());
        }
        result.insert_attribute(attribute, values);
    }
    let raw = get(&data.indices)?;
    if raw.is_empty() || !raw.len().is_multiple_of(12) {
        return Err("vegetation triangle count".into());
    }
    let indices: Vec<_> = raw
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| u32::from_le_bytes(*v))
        .collect();
    if indices.iter().any(|&v| v as usize >= count) {
        return Err("vegetation index bound".into());
    }
    result.insert_indices(Indices::U32(indices));
    Ok(result)
}

/// Canonical identity survives camera/LOD changes; this is not yet a picking backend.
#[derive(Component, Clone, Copy, Debug)]
pub struct ImportedPlant {
    pub id: forge_core::EntityId,
    pub head_hash: Option<forge_core::ContentHash>,
}

pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Vegetation>, Box<dyn Error>> {
    let canonical = canonical::from_files(files)?;
    load_prepared(files, canonical.as_ref())
}

pub fn from_population(
    package: &forge_assets::tidewater_plants::PlantPackage,
    population: &forge_world::imported_vegetation::ImportedPopulation,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    heights: &[u8],
) -> Result<Vegetation, Box<dyn Error>> {
    let prepared = canonical::prepare(population, Some((head, heights)))?;
    load_prepared(package.files(), Some(&prepared))?
        .ok_or_else(|| "Missing plant description".into())
}

#[expect(
    clippy::too_many_lines,
    reason = "bounded validation of legacy and canonical scene inputs"
)]
fn load_prepared(
    files: &BTreeMap<String, Vec<u8>>,
    canonical: Option<&canonical::PreparedPopulation>,
) -> Result<Option<Vegetation>, Box<dyn Error>> {
    let Some(raw) = files.get("vegetation-scene.json") else {
        return Ok(None);
    };
    let description: Description = serde_json::from_slice(raw)?;
    if description.schema != "forge.tidewater.vegetation-scene-probe"
        || description.version != 1
        || description.groups.len() != 6
    {
        return Err("invalid vegetation scene".into());
    }
    let mut groups = Vec::new();
    let mut total = 0_usize;
    for group in description.groups {
        total = total
            .checked_add(group.count)
            .ok_or("vegetation count overflow")?;
        if group.count == 0
            || total > 65_383
            || group.levels.is_empty()
            || group.levels.len() > 2
            || !group.query_radius.is_finite()
            || !(0.0..=500.0).contains(&group.query_radius)
            || group.cell_size.to_bits() != 32.0_f64.to_bits()
            || !group.near_range.is_finite()
            || !(0.0..=500.0).contains(&group.near_range)
            || !group.refresh_distance.is_finite()
            || !(0.0..=50.0).contains(&group.refresh_distance)
        {
            return Err("vegetation group bounds".into());
        }
        let get = |name: &str| {
            if let Some(canonical) = &canonical {
                return canonical
                    .streams
                    .get(&group.name)
                    .and_then(|s| s.get(name))
                    .ok_or("missing canonical vegetation stream");
            }
            group
                .streams
                .get(name)
                .and_then(|p| files.get(p))
                .ok_or("missing vegetation stream")
        };
        let positions = read_vectors::<4>(get("iPos")?)?;
        let data = read_vectors::<4>(get("iDat")?)?;
        let matrices = read_vectors::<16>(get("matrices")?)?;
        let radii = read_vectors::<1>(get("qr2")?)?;
        if [positions.len(), data.len(), matrices.len(), radii.len()]
            .into_iter()
            .any(|n| n != positions.len())
            || (canonical.is_none() && positions.len() != group.count)
            || positions.len()
                > group.count
                    + if canonical.is_some() {
                        forge_project::zone_authoring::imported_plants::MAX_IMPORTED_PLANT_PLACEMENTS
                    } else {
                        0
                    }
            || positions.iter().any(|p| p[3] <= 0.0 || p[3] > 100.0)
            || radii.iter().any(|r| r[0] < 0.0)
        {
            return Err("invalid vegetation streams".into());
        }
        let models: Vec<_> = matrices.iter().map(Mat4::from_cols_array).collect();
        if models.iter().any(|m| m.determinant().abs() < 1e-12) {
            return Err("singular vegetation matrix".into());
        }
        let records = positions
            .into_iter()
            .zip(data)
            .map(|(p, d)| Record {
                i_pos: Vec4::from_array(p),
                i_dat: Vec4::from_array(d),
            })
            .collect();
        let mut levels = Vec::new();
        for level in group.levels {
            if level.kind > 2
                || level.capacity == 0
                || level.capacity > 65_383
                || level.range.iter().any(|v| !v.is_finite())
                || level.range[0] < 0.0
                || level.range[0] >= level.range[1]
                || level.range[1] >= level.range[2]
            {
                return Err("invalid vegetation LOD".into());
            }
            levels.push(Level {
                mesh: mesh(&level, files)?,
                description: level,
            });
        }
        let identities = canonical
            .map(|p| {
                p.identities
                    .get(&group.name)
                    .cloned()
                    .ok_or("Missing plant identities")
            })
            .transpose()?;
        if identities
            .as_ref()
            .is_some_and(|ids| ids.len() != models.len())
        {
            return Err("Plant identity/stream length mismatch".into());
        }
        groups.push(Group {
            identities,
            name: group.name,
            query_radius: group.query_radius,
            cell_size: group.cell_size,
            far_exclude_near: group.far_exclude_near,
            near_range: group.near_range,
            refresh_distance: group.refresh_distance,
            sort_far: group.sort_far,
            records,
            models,
            query_radii: radii.into_iter().map(|v| v[0]).collect(),
            levels,
        });
    }
    if total != 65_383 {
        return Err("incomplete source vegetation".into());
    }
    Ok(Some(Vegetation {
        groups,
        head_hash: canonical.and_then(|p| p.head_hash),
    }))
}

#[expect(
    clippy::cast_possible_truncation,
    reason = "match source Float32Array distance keys"
)]
fn near_ids(group: &Group, camera: Vec3) -> Vec<usize> {
    let x = f64::from(camera.x);
    let z = f64::from(camera.z);
    let r = group.query_radius;
    let cs = group.cell_size;
    let min_x = ((x - r) / cs).floor();
    let max_x = ((x + r) / cs).floor();
    let min_z = ((z - r) / cs).floor();
    let max_z = ((z + r) / cs).floor();
    let mut ids: Vec<_> = (0..group.records.len())
        .filter(|&i| {
            let p = group.records[i].i_pos;
            let px = f64::from(p.x);
            let pz = f64::from(p.z);
            let cell_x = (px / cs).floor();
            let cell_z = (pz / cs).floor();
            let radius = if group.query_radii[i] > 0.0 {
                f64::from(group.query_radii[i])
            } else {
                r * r
            };
            cell_x >= min_x
                && cell_x <= max_x
                && cell_z >= min_z
                && cell_z <= max_z
                && (px - x).powi(2) + (pz - z).powi(2) < radius
        })
        .collect();
    // Source grid traversal, then stable f32 distance sort; preserve ties.
    ids.sort_by(|&a, &b| {
        let pa = group.records[a].i_pos;
        let pb = group.records[b].i_pos;
        (f64::from(pa.z) / cs)
            .floor()
            .total_cmp(&(f64::from(pb.z) / cs).floor())
            .then_with(|| {
                (f64::from(pa.x) / cs)
                    .floor()
                    .total_cmp(&(f64::from(pb.x) / cs).floor())
            })
    });
    let distance = |i: usize| {
        let p = group.records[i].i_pos;
        ((f64::from(p.x) - x).powi(2) + (f64::from(p.z) - z).powi(2)) as f32
    };
    ids.sort_by(|&a, &b| distance(a).total_cmp(&distance(b)));
    ids
}

fn far_ids(group: &Group, camera: Vec3) -> Vec<usize> {
    let distance = |i: usize| {
        let p = group.records[i].i_pos;
        let dx = f64::from(p.x) - f64::from(camera.x);
        let dy = f64::from(p.y) - f64::from(camera.y);
        let dz = f64::from(p.z) - f64::from(camera.z);
        (dx * dx + dz * dz, dy * dy)
    };
    let radius = (group.near_range - group.refresh_distance - 2.0).max(0.0);
    let mut ids: Vec<_> = (0..group.records.len())
        .filter(|&i| {
            let (horizontal, vertical) = distance(i);
            !group.far_exclude_near || horizontal + vertical >= radius * radius
        })
        .collect();
    if group.sort_far && !group.far_exclude_near {
        let bucket = |i: usize| (distance(i).0.sqrt() / 6.0).floor().min(1023.0);
        // Nonnegative finite buckets have the same numeric and bit order. Cache
        // one key per instance instead of evaluating sqrt in every comparison;
        // cached-key sorting retains source order for equal buckets.
        ids.sort_by_cached_key(|&i| bucket(i).to_bits());
    }
    ids
}

#[derive(Clone, Copy)]
pub struct View {
    pub camera: Vec3,
    pub visibility: Visibility,
}

/// Caller owns returned derived entities and can retire them on revision/view change.
pub fn spawn(
    commands: &mut Commands,
    scene: &Vegetation,
    base: &PlantMaterial,
    view: View,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<PlantMaterial>,
    buffers: &mut Assets<ShaderBuffer>,
) -> Result<Vec<Entity>, String> {
    if !view.camera.is_finite() {
        return Err("Invalid plant view".into());
    }
    let camera = view.camera;
    let visibility = view.visibility;
    let mut entities = Vec::new();
    for group in &scene.groups {
        if group.records.is_empty() {
            continue;
        }
        let records = buffers.add(ShaderBuffer::from(group.records.clone()));
        let models = buffers.add(ShaderBuffer::from(group.models.clone()));
        let near = near_ids(group, camera);
        let mut far = None;
        for level in &group.levels {
            let data = &level.description;
            let ids = if data.near {
                &near
            } else {
                far.get_or_insert_with(|| far_ids(group, camera))
            };
            let count = ids.len().min(data.capacity);
            let mut material = base.clone();
            material.kind = data.kind;
            material.instances = records.clone();
            material.models = models.clone();
            material.params.gust_lod.z = data.range[0];
            material.params.gust_lod.w = data.range[1];
            material.params.lod_end_frame.x = data.range[2];
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
                        NotShadowCaster,
                        visibility,
                    ))
                    .id();
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

fn read_vectors<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, Box<dyn Error>> {
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
