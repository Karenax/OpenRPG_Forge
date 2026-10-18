//! Prepare the complete acquired camera-following layer before renderer publication.
use super::{pebble_cells::CellMap, pebble_patch::Patch};
use forge_tidewater_assets::tidewater_debris::DebrisPackage;

pub struct Prepared {
    pub patch: Patch,
    pub cells: CellMap,
}

/// Resolve original patch, density mask and cell bounds from one verified package.
/// The consuming terrain adapter supplies the acquired terrain origin.
///
/// # Errors
/// Refuses changed source parameters, absent buffers and invalid geometry or grids.
pub fn prepare(package: &DebrisPackage, origin: f64) -> Result<Prepared, String> {
    let description = package.pebble_field();
    for (key, expected) in [
        ("PCELL", 4),
        ("R_FAR", 25),
        ("cells_per_side", 512),
        ("patch_triangles", 4704),
        ("occupied_cells", 10_415),
    ] {
        if description
            .get(key)
            .is_none_or(|v| v.as_u64() != Some(expected))
        {
            return Err(format!("Acquired pebble parameter differs: {key}"));
        }
    }
    let patch = Patch::decode(
        payload(package, &["geometry", "attributes", "position"])?,
        payload(package, &["geometry", "attributes", "normal"])?,
        payload(package, &["geometry", "attributes", "aSlot"])?,
        payload(package, &["geometry", "indices"])?,
        payload(package, &["mask"])?,
    )?;
    let flags = payload(package, &["cell_flags"])?;
    if flags.iter().map(|flag| usize::from(*flag)).sum::<usize>() != 10_415 {
        return Err("Acquired occupied pebble population differs".into());
    }
    let cells = CellMap::new(
        512,
        origin,
        flags.to_vec(),
        heights(payload(package, &["cell_min_y"])?)?,
        heights(payload(package, &["cell_max_y"])?)?,
    )?;
    Ok(Prepared { patch, cells })
}

fn payload<'a>(package: &'a DebrisPackage, path: &[&str]) -> Result<&'a [u8], String> {
    let mut reference = package.pebble_field();
    for part in path {
        reference = reference
            .get(*part)
            .ok_or("Acquired pebble reference absent")?;
    }
    let file = reference
        .get("file")
        .and_then(|v| v.as_str())
        .ok_or("Acquired pebble payload path absent")?;
    package
        .file(file)
        .ok_or_else(|| "Acquired pebble payload absent".into())
}

fn heights(raw: &[u8]) -> Result<Vec<f32>, String> {
    if raw.len() != 512 * 512 * 4 {
        return Err("Acquired pebble height dimensions differ".into());
    }
    Ok(raw
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| f32::from_le_bytes(*v))
        .collect())
}
