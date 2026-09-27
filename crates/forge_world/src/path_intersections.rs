//! Explicit intersection anchors, not a mesh union or an implicit road network.
//! Existing controls remain unchanged; one reviewed command inserts all anchors.
use forge_core::CommandId;
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    paths::{self, AuthoredPath, PathEdit, PathPoint},
    session::{ZoneAction, ZoneRequest},
};

mod geometry;

const MAX_PAIRS: usize = 1_000_000;

/// Insert shared X/Z controls at transverse crossings and T junctions. Does not
/// snap nearby paths, merge IDs, modify locked paths, or bake derived terrain.
pub fn prepare(head: &ZoneAuthoringHead, command_id: CommandId) -> Result<ZoneRequest, String> {
    let expected_hash = head.content_hash()?;
    let paths = head.corrections.paths.as_deref().unwrap_or_default();
    // Do not turn a malformed source profile into a partial edit. `split` relies
    // on every original control having an elevation when it inserts new ones.
    paths::validate(paths, head.base.terrain.extent_m)?;
    let additions = split(paths)?;
    if additions.iter().all(Vec::is_empty) {
        return Err("Aucun nouveau point de croisement à insérer.".into());
    }
    let revised = rebuild(paths, &additions)?;
    let candidate = match preflight(head, command_id, paths, &revised) {
        Ok(candidate) => candidate,
        Err(error) if is_fold_error(&error) => {
            // An inserted anchor must never make an otherwise renderable path fold.
            // Keep every independent anchor that still preflights, in stable segment/
            // parameter order, so a dense real zone retains its safe crossings.
            let mut accepted = vec![Vec::new(); additions.len()];
            for group in crossing_groups(&additions) {
                let mut tentative = accepted.clone();
                for (segment, addition) in group {
                    tentative[segment].push(addition);
                }
                let tentative_revised = rebuild(paths, &tentative)?;
                match preflight(head, command_id, paths, &tentative_revised) {
                    Ok(_) => accepted = tentative,
                    Err(error) if is_fold_error(&error) => {}
                    Err(error) => return Err(error),
                }
            }
            if accepted.iter().all(Vec::is_empty) {
                return Err(
                    "Aucun point de croisement sûr à insérer ; ajuster les tracés ou leur largeur."
                        .into(),
                );
            }
            let revised = rebuild(paths, &accepted)?;
            preflight(head, command_id, paths, &revised)?
        }
        Err(error) => return Err(error),
    };
    Ok(ZoneRequest {
        command_id,
        expected_hash,
        expected_revision: head.revision,
        action: ZoneAction::edit(candidate.corrections),
    })
}

fn is_fold_error(error: &str) -> bool {
    error.contains("Path folds at a bend")
}

/// A physical crossing can add controls to more than one route. Its additions
/// must be accepted or rejected as one group: a lone inserted control would
/// falsely claim a shared anchor where the other route stayed untouched.
fn crossing_groups(additions: &[Vec<Addition>]) -> Vec<Vec<(usize, Addition)>> {
    let mut groups = Vec::<Vec<(usize, Addition)>>::new();
    for (segment, segment_additions) in additions.iter().enumerate() {
        for addition in segment_additions {
            if let Some(group) = groups
                .iter_mut()
                .find(|group| group[0].1.point == addition.point)
            {
                group.push((segment, *addition));
            } else {
                groups.push(vec![(segment, *addition)]);
            }
        }
    }
    groups
}

fn preflight(
    head: &ZoneAuthoringHead,
    command_id: CommandId,
    paths: &[AuthoredPath],
    revised: &[AuthoredPath],
) -> Result<ZoneAuthoringHead, String> {
    let mut candidate = head.clone();
    for (before, after) in paths.iter().zip(revised) {
        if before == after {
            continue;
        }
        // Use the domain's normal lock, extent, count and identity validation.
        let request = candidate.prepare_path(
            command_id,
            PathEdit::Replace {
                path: after.clone(),
            },
        )?;
        let ZoneAction::Edit(corrections) = request.action else {
            return Err("Intersection preparation requires a path edit".into());
        };
        candidate.corrections = *corrections;
        candidate.version = candidate.corrections.schema_version();
    }
    crate::path_surface::project(&candidate)?;
    let (terrain, vegetation) = candidate.effective()?;
    let vegetation = vegetation.map_or_else(|| crate::editor_vegetation::empty(&terrain), Ok)?;
    crate::path_vegetation::prepare(
        &vegetation,
        candidate.corrections.paths.as_deref().unwrap_or_default(),
    )?;
    Ok(candidate)
}

#[derive(Clone, Copy)]
struct Segment {
    path: usize,
    index: usize,
    a: PathPoint,
    b: PathPoint,
}

#[derive(Clone, Copy)]
struct Addition {
    t: f64,
    point: PathPoint,
}

fn split(paths: &[AuthoredPath]) -> Result<Vec<Vec<Addition>>, String> {
    let segments: Vec<_> = paths
        .iter()
        .enumerate()
        .flat_map(|(path, p)| {
            p.points
                .windows(2)
                .enumerate()
                .map(move |(index, pair)| Segment {
                    path,
                    index,
                    a: pair[0],
                    b: pair[1],
                })
        })
        .collect();
    let pairs = segments
        .len()
        .saturating_mul(segments.len().saturating_sub(1))
        / 2;
    if pairs > MAX_PAIRS {
        return Err(
            "Croisements : budget de 1 000 000 paires dépassé ; simplifier les tracés.".into(),
        );
    }
    let mut additions = vec![Vec::<Addition>::new(); segments.len()];
    for (i, a) in segments.iter().enumerate() {
        for (j, b) in segments.iter().enumerate().skip(i + 1) {
            if a.path == b.path && a.index.abs_diff(b.index) <= 1 {
                continue;
            }
            let Some((ta, tb, point)) = geometry::crossing(a.a, a.b, b.a, b.b)? else {
                continue;
            };
            if ta > 0.0 && ta < 1.0 {
                additions[i].push(Addition { t: ta, point });
            }
            if tb > 0.0 && tb < 1.0 {
                additions[j].push(Addition { t: tb, point });
            }
        }
    }
    Ok(additions)
}

fn rebuild(
    paths: &[AuthoredPath],
    additions: &[Vec<Addition>],
) -> Result<Vec<AuthoredPath>, String> {
    let segments: Vec<_> = paths
        .iter()
        .enumerate()
        .flat_map(|(path, p)| {
            p.points
                .windows(2)
                .enumerate()
                .map(move |(index, pair)| Segment {
                    path,
                    index,
                    a: pair[0],
                    b: pair[1],
                })
        })
        .collect();
    let mut result = paths.to_vec();
    for path in &mut result {
        path.points.clear();
        if let Some(profile) = &mut path.profile {
            profile.elevations_m.clear();
        }
    }
    for (segment, source_points) in segments.iter().zip(additions) {
        let mut points = source_points.clone();
        points.sort_by(|a, b| a.t.total_cmp(&b.t));
        points.dedup_by(|a, b| a.point == b.point);
        let source_profile = paths[segment.path].profile.as_ref();
        if segment.index == 0 {
            result[segment.path].points.push(segment.a);
            if let (Some(profile), Some(source)) =
                (&mut result[segment.path].profile, source_profile)
            {
                profile
                    .elevations_m
                    .push(source.elevations_m[segment.index]);
            }
        }
        for addition in points {
            result[segment.path].points.push(addition.point);
            if let (Some(profile), Some(source)) =
                (&mut result[segment.path].profile, source_profile)
            {
                #[allow(
                    clippy::cast_possible_truncation,
                    reason = "crossing parameters are finite and strictly inside the validated [0, 1] segment range"
                )]
                let t = addition.t as f32;
                profile.elevations_m.push(
                    source.elevations_m[segment.index] * (1.0 - t)
                        + source.elevations_m[segment.index + 1] * t,
                );
            }
        }
        result[segment.path].points.push(segment.b);
        if let (Some(profile), Some(source)) = (&mut result[segment.path].profile, source_profile) {
            profile
                .elevations_m
                .push(source.elevations_m[segment.index + 1]);
        }
        if result[segment.path].points.len() > 128 {
            return Err(format!(
                "Croisements : le chemin {} dépasserait 128 points.",
                paths[segment.path].name
            ));
        }
    }
    Ok(result)
}
