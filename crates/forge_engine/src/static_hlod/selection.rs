use super::{CachedMesh, Group, Source};
use crate::cell_streaming::SampledRenderSelection;
use bevy::{asset::AssetId, prelude::*};
use forge_world::cell_streaming::{CellDetail, CellKey};
use forge_world::terrain_hlod::TerrainHlodKey;
use std::collections::BTreeMap;

/// Inclusive bounds are conservative at cell boundaries: touching a detailed
/// cell keeps the entire object detailed, independent of its origin/anchor.
pub(super) fn node(
    selection: &SampledRenderSelection<'_>,
    minimum: Vec3,
    maximum: Vec3,
) -> Option<TerrainHlodKey> {
    let policy = selection.policy;
    policy
        .cell_at([f64::from(minimum.x), f64::from(minimum.z)])
        .ok()?;
    policy
        .cell_at([f64::from(maximum.x), f64::from(maximum.z)])
        .ok()?;
    if selection.cells.iter().any(|(cell, loaded)| {
        let x = policy.origin_xz_m[0] + f64::from(cell.x) * policy.cell_size_m;
        let z = policy.origin_xz_m[1] + f64::from(cell.z) * policy.cell_size_m;
        loaded.detail == CellDetail::Full
            && f64::from(maximum.x) >= x
            && f64::from(minimum.x) <= x + policy.cell_size_m
            && f64::from(maximum.z) >= z
            && f64::from(minimum.z) <= z + policy.cell_size_m
    }) {
        return None;
    }
    let centre = minimum.midpoint(maximum);
    let anchor = policy
        .cell_at([f64::from(centre.x), f64::from(centre.z)])
        .ok()?;
    if selection.cells.contains_key(&anchor) {
        return Some(TerrainHlodKey {
            x: anchor.x,
            z: anchor.z,
            level: 0,
        });
    }
    selection
        .distant?
        .tiles()
        .keys()
        .copied()
        .find(|key| contains(*key, anchor))
}

fn contains(node: TerrainHlodKey, cell: CellKey) -> bool {
    cell.x >= node.x
        && cell.z >= node.z
        && cell.x < node.x + node.side_cells()
        && cell.z < node.z + node.side_cells()
}

pub(super) fn groups(
    selection: &SampledRenderSelection<'_>,
    sources: &[Source],
    cache: &BTreeMap<AssetId<Mesh>, CachedMesh>,
) -> Vec<Group> {
    let mut groups: Vec<Group> = Vec::new();
    for (index, source) in sources.iter().enumerate() {
        let Some(node) = source.node else { continue };
        let layout = &cache[&source.mesh].layout;
        if let Some(group) = groups.iter_mut().find(|group| {
            group.node == node
                && group.material == source.material.id()
                && group.layout == *layout
                && group.layers == source.layers
                && group.flags == source.flags
                && group.custom == source.custom
                && group.route == source.route
        }) {
            group.members.push(index);
        } else {
            // Policy coordinates are validated to <= ~2 Mm and remain f32 finite.
            #[allow(clippy::cast_possible_truncation)]
            let origin = Vec3::new(
                (selection.policy.origin_xz_m[0] + f64::from(node.x) * selection.policy.cell_size_m)
                    as f32,
                0.0,
                (selection.policy.origin_xz_m[1] + f64::from(node.z) * selection.policy.cell_size_m)
                    as f32,
            );
            groups.push(Group {
                node,
                origin,
                material: source.material.id(),
                layout: layout.clone(),
                layers: source.layers.clone(),
                flags: source.flags,
                custom: source.custom,
                route: source.route,
                members: vec![index],
            });
        }
    }
    groups
}
