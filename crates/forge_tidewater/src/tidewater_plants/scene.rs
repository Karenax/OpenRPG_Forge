//! Validated packed vegetation and source-order LOD selection for shared scenes.
use super::canonical;
use bevy::prelude::*;
use forge_engine::source_vegetation::Record;
use forge_engine::source_vegetation::scene::{
    Group, Level, LevelDescription, Vegetation, mesh_from_geometry,
};
use std::{collections::BTreeMap, error::Error};
#[cfg(test)]
mod geometry_tests;

use forge_assets::source_vegetation_package::scene::Description;
pub fn mesh(
    data: &LevelDescription,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<Mesh, Box<dyn Error>> {
    let geometry = forge_tidewater_assets::tidewater_plants::SourceVegetationGeometry::decode(
        &data.attributes,
        &data.indices,
        |name| {
            files
                .get(name)
                .map(Vec::as_slice)
                .ok_or_else(|| format!("Missing vegetation payload: {name}"))
        },
    )?;
    Ok(mesh_from_geometry(geometry))
}

pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Vegetation>, Box<dyn Error>> {
    let canonical = canonical::from_files(files)?;
    load_prepared(files, canonical.as_ref())
}

#[expect(
    clippy::too_many_lines,
    reason = "bounded validation of legacy and canonical scene inputs"
)]
fn load_prepared(
    files: &BTreeMap<String, Vec<u8>>,
    canonical: Option<&forge_engine::source_vegetation::canonical::PreparedPopulation>,
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
            let level: LevelDescription = level.into();
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
