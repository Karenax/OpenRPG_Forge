//! Source near plant casters, selected in Rust and masked on Bevy's device.
use super::canonical;
use super::{checked_file, plant_shader};
use bevy::prelude::Vec4;
pub(super) use forge_tidewater::tidewater_object_shadow::plants::Input as Plants;
use forge_tidewater::tidewater_object_shadow::plants::{Batch, Stream};
use forge_tidewater::tidewater_plants::scene::shadow::SelectionObservation;
use serde_json::Value;
use std::{error::Error, path::Path};

fn float_bytes(
    root: &Path,
    fixture: &Value,
    name: &str,
    width: usize,
) -> Result<Vec<u8>, Box<dyn Error>> {
    let bytes = checked_file(root, fixture, name)?;
    if bytes.is_empty()
        || !bytes.len().is_multiple_of(width * 4)
        || bytes
            .as_chunks::<4>()
            .0
            .iter()
            .any(|v| !f32::from_le_bytes(*v).is_finite())
    {
        return Err("invalid plant float stream".into());
    }
    Ok(bytes)
}
pub(super) struct Source {
    groups: Vec<Group>,
}
struct Group {
    name: String,
    pos: Vec<u8>,
    dat: Vec<u8>,
    models: Vec<u8>,
    radii: Vec<u8>,
    radius: f64,
    capacity: usize,
    range: [f32; 3],
}
impl Source {
    pub(super) fn select(&self, camera: Vec4) -> Result<Vec<Stream>, Box<dyn Error>> {
        self.select_observed(camera).map(|(streams, _)| streams)
    }

    pub(super) fn select_observed(
        &self,
        camera: Vec4,
    ) -> Result<(Vec<Stream>, Vec<SelectionObservation>), Box<dyn Error>> {
        if !camera.is_finite() {
            return Err("invalid plant selection camera".into());
        }
        let mut observations = Vec::with_capacity(self.groups.len());
        let streams = self
            .groups
            .iter()
            .map(|group| {
                let mut ids = select(&group.pos, &group.radii, camera, group.radius);
                let candidates = ids.len();
                ids.truncate(group.capacity);
                let distances = ids.iter().map(|&i| {
                    bevy::prelude::Vec3::new(
                        value(&group.pos, i * 4),
                        value(&group.pos, i * 4 + 1),
                        value(&group.pos, i * 4 + 2),
                    )
                    .distance(camera.truncate())
                });
                observations.push(SelectionObservation {
                    group: group.name.clone(),
                    candidates,
                    submitted: ids.len(),
                    capacity_dropped: candidates - ids.len(),
                    lod_range: group.range,
                    max_base_distance: distances.clone().reduce(f32::max),
                    submitted_beyond_lod_end: distances
                        .filter(|distance| *distance >= group.range[2])
                        .count(),
                });
                Stream {
                    records: ids
                        .iter()
                        .flat_map(|&i| {
                            group.pos[i * 16..i * 16 + 16]
                                .iter()
                                .chain(&group.dat[i * 16..i * 16 + 16])
                        })
                        .copied()
                        .collect(),
                    models: ids
                        .iter()
                        .flat_map(|&i| group.models[i * 64..i * 64 + 64].iter())
                        .copied()
                        .collect(),
                    count: u32::try_from(ids.len()).expect("plant capacity"),
                }
            })
            .collect();
        Ok((streams, observations))
    }
}

fn value(v: &[u8], index: usize) -> f32 {
    f32::from_le_bytes(
        v[index * 4..index * 4 + 4]
            .try_into()
            .expect("validated stream"),
    )
}

#[allow(clippy::cast_possible_truncation)] // Source Float32Array sorting keys.
fn select(pos: &[u8], radii: &[u8], camera: Vec4, radius: f64) -> Vec<usize> {
    let cx = f64::from(camera.x);
    let cz = f64::from(camera.z);
    let cell = |i: usize| {
        (
            (f64::from(value(pos, i * 4)) / 32.0).floor(),
            (f64::from(value(pos, i * 4 + 2)) / 32.0).floor(),
        )
    };
    let distance = |i: usize| {
        let dx = f64::from(value(pos, i * 4)) - cx;
        let dz = f64::from(value(pos, i * 4 + 2)) - cz;
        dx * dx + dz * dz
    };
    let mut ids: Vec<_> = (0..pos.len() / 16)
        .filter(|&i| {
            let (x, z) = cell(i);
            let r = value(radii, i);
            x >= ((cx - radius) / 32.0).floor()
                && x <= ((cx + radius) / 32.0).floor()
                && z >= ((cz - radius) / 32.0).floor()
                && z <= ((cz + radius) / 32.0).floor()
                && distance(i)
                    < if r > 0.0 {
                        f64::from(r)
                    } else {
                        radius * radius
                    }
        })
        .collect();
    ids.sort_by(|&a, &b| {
        let (x, z) = cell(a);
        let (xx, zz) = cell(b);
        z.total_cmp(&zz).then_with(|| x.total_cmp(&xx))
    });
    ids.sort_by(|&a, &b| (distance(a) as f32).total_cmp(&(distance(b) as f32)));
    ids
}

#[allow(clippy::too_many_lines)] // Development fixture validation and packing before GPU upload.
pub(super) fn load(
    root: &Path,
    fixture: &Value,
    camera: Vec4,
    scene_files: Option<&std::collections::BTreeMap<String, Vec<u8>>>,
) -> Result<Plants, Box<dyn Error>> {
    Ok(build(root, fixture, camera, scene_files, false)?.0)
}
pub(super) fn load_live(
    root: &Path,
    fixture: &Value,
    camera: Vec4,
    scene_files: Option<&std::collections::BTreeMap<String, Vec<u8>>>,
) -> Result<(Plants, Source), Box<dyn Error>> {
    build(root, fixture, camera, scene_files, true)
}

#[allow(clippy::too_many_lines)]
fn build(
    root: &Path,
    fixture: &Value,
    camera: Vec4,
    scene_files: Option<&std::collections::BTreeMap<String, Vec<u8>>>,
    live: bool,
) -> Result<(Plants, Source), Box<dyn Error>> {
    let scene: Value =
        serde_json::from_slice(&checked_file(root, fixture, "vegetation-scene.json")?)?;
    if scene["schema"] != "forge.tidewater.vegetation-scene-probe" || scene["version"] != 1 {
        return Err("unsupported vegetation".into());
    }
    let mut batches = Vec::new();
    let mut source_groups = Vec::new();
    let mut canonical_files = std::collections::BTreeMap::new();
    if scene_files.is_none() && fixture["files"].get("canonical-vegetation.json").is_some() {
        for name in fixture["files"]
            .as_object()
            .ok_or("missing fixture files")?
            .keys()
            .filter(|n| n.starts_with("canonical-"))
        {
            canonical_files.insert(name.clone(), checked_file(root, fixture, name)?);
        }
    }
    if scene_files.is_none() && canonical_files.contains_key("canonical-zone.json") {
        canonical_files.insert(
            "heights.bin".into(),
            checked_file(root, fixture, "heights.bin")?,
        );
    }
    let canonical = canonical::streams(scene_files.unwrap_or(&canonical_files))?;
    let groups = scene["groups"].as_array().ok_or("missing groups")?;
    let names = groups
        .iter()
        .filter_map(|g| g["name"].as_str())
        .collect::<std::collections::BTreeSet<_>>();
    if groups.len() != 6
        || names
            != std::collections::BTreeSet::from([
                "palms",
                "bananas",
                "understory",
                "monsteras",
                "broadleaf",
                "canopy",
            ])
    {
        return Err("incomplete or duplicate plant groups".into());
    }
    for group in groups {
        let name = group["name"].as_str().ok_or("missing group name")?;
        if name == "understory" {
            continue;
        }
        if !matches!(
            name,
            "palms" | "bananas" | "monsteras" | "broadleaf" | "canopy"
        ) {
            return Err("unknown shadow plant group".into());
        }
        let stream = |key: &str, width| {
            if let Some(canonical) = &canonical {
                return canonical
                    .get(name)
                    .and_then(|g| g.get(key))
                    .cloned()
                    .ok_or_else(|| "missing canonical shadow plant stream".into());
            }
            float_bytes(
                root,
                fixture,
                group["streams"][key]
                    .as_str()
                    .ok_or("missing plant stream")?,
                width,
            )
        };
        let pos = stream("iPos", 4)?;
        let dat = stream("iDat", 4)?;
        let models = stream("matrices", 16)?;
        let radii = stream("qr2", 1)?;
        if pos.len() != dat.len()
            || pos.len() * 4 != models.len()
            || pos.len() != radii.len() * 4
            || pos.len() / 16 > 65536
            || (canonical.is_none()
                && group["count"].as_u64() != Some(u64::try_from(pos.len() / 16)?))
            || group["count"]
                .as_u64()
                .is_none_or(|n| u64::try_from(pos.len() / 16).map_or(true, |actual| actual > n))
        {
            return Err("plant stream count mismatch".into());
        }
        let radius = group["query_radius"]
            .as_f64()
            .ok_or("missing query radius")?;
        if !radius.is_finite() || !(0.0..=500.0).contains(&radius) || group["cell_size"] != 32.0 {
            return Err("invalid query grid".into());
        }
        let level = group["levels"]
            .as_array()
            .ok_or("missing levels")?
            .iter()
            .find(|v| v["near"] == true)
            .ok_or("missing near level")?;
        let kind = u8::try_from(level["kind"].as_u64().ok_or("missing plant kind")?)?;
        if kind > 1 {
            return Err("invalid near material".into());
        }
        let capacity = usize::try_from(level["capacity"].as_u64().ok_or("missing capacity")?)?;
        if !(1..=65536).contains(&capacity) {
            return Err("invalid plant capacity".into());
        }
        source_groups.push(Group {
            name: name.to_owned(),
            pos: pos.clone(),
            dat: dat.clone(),
            models: models.clone(),
            radii: radii.clone(),
            radius,
            capacity,
            range: serde_json::from_value::<[f32; 3]>(level["range"].clone())?,
        });
        let ids = if live {
            Vec::new()
        } else {
            select(&pos, &radii, camera, radius)
                .into_iter()
                .take(capacity)
                .collect::<Vec<_>>()
        };
        println!(
            "native shadow plants {name}: {} instances; ids={ids:?}",
            ids.len()
        );
        let records = ids
            .iter()
            .flat_map(|&i| {
                pos[i * 16..i * 16 + 16]
                    .iter()
                    .chain(&dat[i * 16..i * 16 + 16])
                    .copied()
            })
            .collect();
        let models = ids
            .iter()
            .flat_map(|&i| models[i * 64..i * 64 + 64].iter().copied())
            .collect();
        let mut attributes = Vec::new();
        let mut count = 0;
        for (key, width) in [
            ("position", 3),
            ("normal", 3),
            ("uv", 2),
            ("aVeg", 4),
            ("aMat", 4),
            ("aLobe", 4),
        ] {
            let data = if let Some(path) = level["attributes"][key].as_str() {
                float_bytes(root, fixture, path, width)?
            } else if count > 0 {
                vec![0; count * width * 4]
            } else {
                return Err("missing positions".into());
            };
            if count == 0 {
                count = data.len() / (width * 4);
            } else if count != data.len() / (width * 4) {
                return Err("plant attributes differ".into());
            }
            attributes.push((data, width * 4));
        }
        let mut vertices = Vec::with_capacity(count * 80);
        for i in 0..count {
            for (a, stride) in &attributes {
                vertices.extend_from_slice(&a[i * stride..(i + 1) * stride]);
            }
        }
        let indices = checked_file(
            root,
            fixture,
            level["indices"].as_str().ok_or("missing plant indices")?,
        )?;
        if count > 100_000
            || indices.is_empty()
            || !indices.len().is_multiple_of(12)
            || indices
                .as_chunks::<4>()
                .0
                .iter()
                .any(|v| u32::from_le_bytes(*v) as usize >= count)
        {
            return Err("invalid plant indices".into());
        }
        let range = serde_json::from_value::<[f32; 3]>(level["range"].clone())?;
        if range.iter().any(|v| !v.is_finite()) {
            return Err("nonfinite plant range".into());
        }
        batches.push(Batch {
            kind,
            vertices,
            indices,
            records,
            models,
            count: u32::try_from(ids.len())?,
            range: Vec4::new(0.0, range[0], range[1], range[2]),
        });
    }
    let raw = String::from_utf8(checked_file(root, fixture, "plant.wgsl")?)?;
    let leaf = checked_file(root, fixture, "leaf.bin")?;
    let detail = checked_file(root, fixture, "detail.bin")?;
    if detail.len() != 512 * 512 * 4 {
        return Err("invalid plant detail texture".into());
    }
    let size = u32::try_from(
        fixture["leaf_texture"]["width"]
            .as_u64()
            .ok_or("missing leaf size")?,
    )?;
    let mips = u32::try_from(
        fixture["leaf_texture"]["mips"]
            .as_u64()
            .ok_or("missing leaf mips")?,
    )?;
    if size != 1024
        || fixture["leaf_texture"]["height"] != 1024
        || mips != 11
        || leaf.len()
            != (0..mips)
                .map(|i| ((size >> i) * (size >> i) * 4) as usize)
                .sum::<usize>()
    {
        return Err("unsupported leaf atlas".into());
    }
    Ok((
        Plants {
            batches,
            shaders: [plant_shader::make(&raw, 0)?, plant_shader::make(&raw, 1)?],
            leaf,
            detail,
            size,
            mips,
            camera,
            initial_motion: None,
        },
        Source {
            groups: source_groups,
        },
    ))
}
