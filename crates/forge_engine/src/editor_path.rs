//! Presentation-only authoring-path overlay for the Studio viewport.
use crate::editor_viewport::{EditorViewport, EditorViewportStatus};
use bevy::prelude::*;
use forge_project::{
    MAX_PATH_CONTROL_POINTS, MAX_PATHS_PER_DOCUMENT, MAX_TOTAL_PATH_CONTROL_POINTS, PathDocument,
    TerrainDocument,
};
use forge_world::{
    GeneratedPaths, GeneratedTerrain, TERRAIN_CHUNK_COUNT, TERRAIN_HALF_EXTENT_M,
    validate_generated_path_centrelines,
};
use std::collections::BTreeSet;

// ceil(total 32,768 m / 2 m) + one rounding interval per control segment + one first sample/path.
const MAX_PRESENTED_PATH_SAMPLES: usize = 18_496;
const PATH_HEIGHT_LIMIT_M: f32 = 10_000.0;
const PATH_Y_OFFSET_M: f32 = 0.18;

/// Transient local stroke. It is never a canonical path and is discarded on projection changes.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct EditorPathDraft {
    pub generation: u64,
    pub points_m: Vec<[f32; 3]>,
}

#[derive(Default, Resource)]
pub(crate) struct EditorPathOverlay {
    pub(crate) canonical: Option<GeneratedPaths>,
}

pub(crate) fn validate_generated_paths(
    terrain: &GeneratedTerrain,
    generated: &GeneratedPaths,
    source_terrain: &TerrainDocument,
    source_paths: &PathDocument,
) -> Result<(), String> {
    validate_generated_path_centrelines(source_terrain, source_paths, generated).map_err(
        |error| format!("Chemins générés différents de leurs sources canoniques : {error}"),
    )?;
    if generated.terrain_document_hash != terrain.source_document_hash
        || generated.paths.len() > MAX_PATHS_PER_DOCUMENT
    {
        return Err("Sources ou nombre de chemins incohérents.".into());
    }

    let mut ids = BTreeSet::new();
    let mut previous_id = None;
    let mut total_control_points = 0_usize;
    let mut total_samples = 0_usize;
    for path in &generated.paths {
        if path.id.as_entity_id().as_uuid().is_nil()
            || !ids.insert(path.id)
            || previous_id.is_some_and(|previous| previous >= path.id)
            || path.name.trim().is_empty()
            || path.name.len() > 128
            || path.name.chars().any(char::is_control)
            || path.control_points.len() < if path.closed { 3 } else { 2 }
            || path.control_points.len() > MAX_PATH_CONTROL_POINTS
            || path.samples.len() < 2
        {
            return Err("Chemin généré invalide ou dupliqué.".into());
        }
        previous_id = Some(path.id);
        total_control_points = total_control_points
            .checked_add(path.control_points.len())
            .ok_or_else(|| "Trop de points de contrôle de chemins.".to_owned())?;
        total_samples = total_samples
            .checked_add(path.samples.len())
            .ok_or_else(|| "Trop d’échantillons de chemins.".to_owned())?;
        if total_control_points > MAX_TOTAL_PATH_CONTROL_POINTS
            || total_samples > MAX_PRESENTED_PATH_SAMPLES
            || path
                .control_points
                .iter()
                .any(|point| !valid_position(*point))
        {
            return Err("Projection de chemin hors limites.".into());
        }
        let segment_count = if path.closed {
            path.control_points.len()
        } else {
            path.control_points.len() - 1
        };
        if path.samples.iter().any(|sample| {
            !valid_position(sample.position_m)
                || usize::from(sample.source_segment_index) >= segment_count
                || !sample.segment_fraction.is_finite()
                || !(0.0..=1.0).contains(&sample.segment_fraction)
        }) {
            return Err("Échantillon de chemin invalide.".into());
        }
        validate_sample_sequence(path, segment_count)?;
    }
    Ok(())
}

fn validate_sample_sequence(
    path: &forge_world::GeneratedPath,
    segment_count: usize,
) -> Result<(), String> {
    let first = path.samples.first().expect("sample count checked");
    let last = path.samples.last().expect("sample count checked");
    let target = if path.closed {
        path.control_points[0]
    } else {
        *path.control_points.last().expect("control count checked")
    };
    if first.source_segment_index != 0
        || first.segment_fraction.to_bits() != 0.0_f32.to_bits()
        || first.position_m.x.to_bits() != path.control_points[0].x.to_bits()
        || first.position_m.z.to_bits() != path.control_points[0].z.to_bits()
        || usize::from(last.source_segment_index) != segment_count - 1
        || last.segment_fraction.to_bits() != 1.0_f32.to_bits()
        || last.position_m.x.to_bits() != target.x.to_bits()
        || last.position_m.z.to_bits() != target.z.to_bits()
    {
        return Err("Extrémités de chemin généré incohérentes.".into());
    }
    for pair in path.samples.windows(2) {
        let previous_segment = pair[0].source_segment_index;
        let current_segment = pair[1].source_segment_index;
        if current_segment < previous_segment
            || current_segment > previous_segment + 1
            || (current_segment == previous_segment
                && pair[1].segment_fraction < pair[0].segment_fraction)
        {
            return Err("Ordre des échantillons de chemin incohérent.".into());
        }
    }
    Ok(())
}

fn valid_position(position: forge_core::Vec3) -> bool {
    position.is_finite()
        && (-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position.x)
        && (-PATH_HEIGHT_LIMIT_M..=PATH_HEIGHT_LIMIT_M).contains(&position.y)
        && (-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position.z)
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters are injected by value"
)]
pub(crate) fn draw_editor_paths(
    viewport: Res<EditorViewport>,
    overlay: Res<EditorPathOverlay>,
    mut gizmos: Gizmos,
) {
    let Ok(bridge) = viewport.0.lock() else {
        return;
    };
    let status = bridge.status.clone();
    let desired_generation = bridge.desired_generation;
    let draft = bridge.path_draft.clone();
    drop(bridge);

    if status.chunks != TERRAIN_CHUNK_COUNT {
        return;
    }
    if let Some(generated) = &overlay.canonical {
        for path in &generated.paths {
            gizmos.linestrip(
                path.samples.iter().map(|sample| {
                    Vec3::new(
                        sample.position_m.x,
                        sample.position_m.y + PATH_Y_OFFSET_M,
                        sample.position_m.z,
                    )
                }),
                Color::srgb(0.98, 0.73, 0.20),
            );
        }
    }
    if draft_is_current(draft.as_ref(), &status, desired_generation) {
        let draft = draft.expect("current draft was checked above");
        gizmos.linestrip(
            draft
                .points_m
                .into_iter()
                .map(Vec3::from_array)
                .map(|point| point + Vec3::Y * (PATH_Y_OFFSET_M + 0.08)),
            Color::srgb(0.25, 0.90, 1.0),
        );
    }
}

fn draft_is_current(
    draft: Option<&EditorPathDraft>,
    status: &EditorViewportStatus,
    desired_generation: u64,
) -> bool {
    draft.is_some_and(|draft| {
        draft.generation == status.generation
            && status.generation == desired_generation
            && (2..=MAX_PATH_CONTROL_POINTS).contains(&draft.points_m.len())
            && draft.points_m.iter().all(|point| {
                point.iter().all(|value| value.is_finite())
                    && (-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&point[0])
                    && (-PATH_HEIGHT_LIMIT_M..=PATH_HEIGHT_LIMIT_M).contains(&point[1])
                    && (-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&point[2])
            })
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn draft_requires_current_generation_and_bounded_points() {
        let status = EditorViewportStatus {
            generation: 7,
            chunks: TERRAIN_CHUNK_COUNT,
            ..default()
        };
        let mut draft = EditorPathDraft {
            generation: 7,
            points_m: vec![[0.0, 1.0, 0.0], [8.0, 1.0, 8.0]],
        };
        assert!(draft_is_current(Some(&draft), &status, 7));
        assert!(!draft_is_current(Some(&draft), &status, 8));
        draft.generation = 6;
        assert!(!draft_is_current(Some(&draft), &status, 7));
        draft.generation = 7;
        draft.points_m[1][0] = TERRAIN_HALF_EXTENT_M;
        assert!(!draft_is_current(Some(&draft), &status, 7));
    }
}
