//! Derived draped path ribbons; no changes to terrain, materials or collision sources.
use crate::TerrainGenerationPlan;
use forge_core::{CommandId, ContentHash, EntityId};
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    paths::{AuthoredPath, PathEdit},
    session::{ZoneAction, ZoneReceipt, ZoneRequest},
};
const MAX_VERTICES: usize = 250_000;
mod union;
/// Derive a reviewed paths-only proposal over the still-accepted terrain.
pub fn preview(
    head: &ZoneAuthoringHead,
    receipt: &ZoneReceipt,
) -> Result<PathSurfaceProjection, String> {
    if receipt.request.expected_hash != head.content_hash()?
        || receipt.request.expected_revision != head.revision
        || receipt.inverse != head.corrections
        || receipt.base_change.is_some()
        || receipt.resulting_revision != head.revision.next().map_err(|e| e.to_string())?
        || receipt.request.action != ZoneAction::edit(receipt.applied.clone())
    {
        return Err("Path preview receipt is stale or inconsistent".into());
    }
    let mut unchanged = receipt.applied.clone();
    unchanged.paths.clone_from(&head.corrections.paths);
    if unchanged != head.corrections || receipt.applied.paths == head.corrections.paths {
        return Err("Path preview requires a paths-only change".into());
    }
    let mut candidate = head.clone();
    candidate.corrections = receipt.applied.clone();
    candidate.version = candidate.corrections.schema_version();
    candidate.revision = receipt.resulting_revision;
    if candidate.content_hash()? != receipt.resulting_hash {
        return Err("Path preview result hash mismatch".into());
    }
    let mut derived = project(&candidate)?; // A paths-only proposal does not change the accepted terrain's geometry.
    derived.terrain_hash = crate::river_projection::project(head)?
        .terrain
        .content_hash()
        .map_err(|e| e.to_string())?;
    Ok(derived)
}
/// Prepare the normal domain command and preflight its derived representation.
/// This is not execution, a GPU acknowledgement or a replacement for host preview.
pub fn prepare_edit(
    head: &ZoneAuthoringHead,
    command_id: CommandId,
    edit: PathEdit,
) -> Result<ZoneRequest, String> {
    let needs_projection = matches!(edit, PathEdit::Insert { .. } | PathEdit::Replace { .. });
    let request = head.prepare_path(command_id, edit)?; // Unlock/removal must remain available to repair a previously unrenderable zone.
    if needs_projection {
        let ZoneAction::Edit(corrections) = &request.action else {
            return Err("Path preparation did not produce an edit".into());
        };
        let mut candidate = head.clone();
        candidate.corrections = (**corrections).clone(); // Path edits must retain any newer authoring domain's required head version.
        candidate.version = candidate.corrections.schema_version();
        project(&candidate)?;
        let (terrain, vegetation) = candidate.effective()?;
        let source = vegetation.map_or_else(|| crate::editor_vegetation::empty(&terrain), Ok)?;
        crate::path_vegetation::prepare(
            &source,
            candidate.corrections.paths.as_deref().unwrap_or_default(),
        )?;
    }
    Ok(request)
}
/// Shared cross sections, expressed per metre of half-width. Reject unbounded
/// miters instead of clamping them into a silently narrowed or folded path.
pub(crate) fn sections(path: &AuthoredPath) -> Result<Vec<[f32; 2]>, String> {
    let directions = path
        .points
        .windows(2)
        .map(|p| {
            let dx = p[1].x_m - p[0].x_m;
            let dz = p[1].z_m - p[0].z_m;
            let length = dx.hypot(dz);
            if !length.is_finite() || length < 0.001 {
                return Err("Path segment is shorter than 1 mm or non-finite".into());
            }
            Ok([dx / length, dz / length])
        })
        .collect::<Result<Vec<_>, String>>()?;
    let Some(first) = directions.first() else {
        return Err("Path requires at least two points".into());
    };
    let mut result = vec![[-first[1], first[0]]];
    for pair in directions.windows(2) {
        let denominator = 1.0 + pair[0][0] * pair[1][0] + pair[0][1] * pair[1][1]; // Miter length <= 4 half-widths; reversals have no finite intersection.
        if denominator < 0.125 {
            return Err("Path turn is too sharp; spread the bend over more points".into());
        }
        result.push([
            -(pair[0][1] + pair[1][1]) / denominator,
            (pair[0][0] + pair[1][0]) / denominator,
        ]);
    }
    let last = directions[directions.len() - 1];
    result.push([-last[1], last[0]]);
    Ok(result)
}
#[derive(Clone, Debug, PartialEq)]
pub struct PathSurfaceMesh {
    pub id: EntityId,
    /// True only when an accepted authored vertical profile changed this mesh's height.
    pub profiled: bool,
    pub positions: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub colors: Vec<[f32; 4]>,
    pub indices: Vec<u32>,
}
#[derive(Clone, Debug, PartialEq)]
pub struct PathSurfaceProjection {
    pub source_hash: ContentHash,
    pub terrain_hash: ContentHash,
    pub meshes: Vec<PathSurfaceMesh>,
}
/// Presentation-only lift (2 cm); terrain collision remains the walking surface.
/// Shared miter rows weld adjacent segments; coverage union removes overlap at junctions.
pub fn project(head: &ZoneAuthoringHead) -> Result<PathSurfaceProjection, String> {
    let source_hash = head.content_hash()?;
    let terrain = crate::river_projection::project(head)?.terrain;
    let terrain_hash = terrain.content_hash().map_err(|e| e.to_string())?;
    let plan = TerrainGenerationPlan::from_document(&terrain).map_err(|e| e.to_string())?;
    let mut meshes = Vec::new();
    let mut total_vertices = 0;
    for path in head.corrections.paths.as_deref().unwrap_or_default() {
        meshes.push(build_mesh(path, &plan, &mut total_vertices)?);
    }
    union::merge(&mut meshes, &plan)?;
    Ok(PathSurfaceProjection {
        source_hash,
        terrain_hash,
        meshes,
    })
}
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss,
    reason = "finite validated terrain bounds, subdivisions and vertex budget checked before allocation"
)]
fn build_mesh(
    path: &AuthoredPath,
    plan: &TerrainGenerationPlan,
    total_vertices: &mut usize,
) -> Result<PathSurfaceMesh, String> {
    let mut mesh = PathSurfaceMesh {
        id: path.id,
        profiled: path.profile.is_some(),
        positions: Vec::new(),
        normals: Vec::new(),
        colors: Vec::new(),
        indices: Vec::new(),
    };
    let outer = path.width_m * 0.5 + path.shoulder_m;
    let sections = sections(path)?;
    let stretch = sections
        .iter()
        .map(|v| v[0].hypot(v[1]))
        .fold(1.0_f32, f32::max);
    let columns = (outer * 2.0 * stretch).ceil().max(2.0) as usize;
    for (segment, pair) in path.points.windows(2).enumerate() {
        let start = pair[0];
        let end = pair[1];
        let dx = end.x_m - start.x_m;
        let dz = end.z_m - start.z_m;
        let length = dx.hypot(dz);
        if length < 0.001 {
            return Err("Path segment is shorter than 1 mm".into());
        }
        let start_section = sections[segment];
        let end_section = sections[segment + 1];
        let edge_length = [-outer, outer]
            .into_iter()
            .map(|offset| {
                (dx + (end_section[0] - start_section[0]) * offset)
                    .hypot(dz + (end_section[1] - start_section[1]) * offset)
            })
            .fold(length, f32::max);
        let rows = edge_length.ceil() as usize;
        let first_row = usize::from(segment != 0);
        let vertices = (rows + 1 - first_row) * (columns + 1);
        *total_vertices += vertices;
        if *total_vertices > MAX_VERTICES {
            return Err("Path projection exceeds 250000 vertices; simplify the paths".into());
        }
        let base = (mesh.positions.len() - first_row * (columns + 1)) as u32;
        for row in first_row..=rows {
            let t = row as f32 / rows as f32;
            let centre_x = start.x_m + dx * t;
            let centre_z = start.z_m + dz * t;
            let profile_height = profile_height(path, segment, t, plan, centre_x, centre_z)?;
            for column in 0..=columns {
                let offset = (column as f32 / columns as f32 * 2.0 - 1.0) * outer;
                let x = start.x_m
                    + dx * t
                    + (start_section[0] * (1.0 - t) + end_section[0] * t) * offset;
                let z = start.z_m
                    + dz * t
                    + (start_section[1] * (1.0 - t) + end_section[1] * t) * offset;
                let (surface, normal) = plan
                    .surface_with_shading_normal_at(x, z)
                    .map_err(|e| format!("Path {}: {e}", path.name))?;
                let alpha = if path.shoulder_m == 0.0 {
                    1.0
                } else {
                    ((outer - offset.abs()) / path.shoulder_m).clamp(0.0, 1.0)
                };
                let height = profile_height.map_or(surface.height_m, |centre| {
                    centre
                        + path
                            .profile
                            .as_ref()
                            .map_or(0.0, |profile| profile.crossfall_ratio * offset)
                });
                mesh.positions.push([x, height + 0.02, z]);
                mesh.normals.push(normal);
                mesh.colors.push([1.0, 1.0, 1.0, alpha]);
            }
        }
        let stride = (columns + 1) as u32;
        for row in 0..rows as u32 {
            for column in 0..columns as u32 {
                let i = base + row * stride + column;
                mesh.indices.extend_from_slice(&[
                    i,
                    i + 1,
                    i + stride,
                    i + 1,
                    i + stride + 1,
                    i + stride,
                ]);
            }
        }
    }
    validate_triangles(&mesh)?;
    Ok(mesh)
}
fn profile_height(
    path: &AuthoredPath,
    segment: usize,
    t: f32,
    plan: &TerrainGenerationPlan,
    x_m: f32,
    z_m: f32,
) -> Result<Option<f32>, String> {
    let Some(profile) = &path.profile else {
        return Ok(None);
    };
    let height = profile.elevations_m[segment] * (1.0 - t) + profile.elevations_m[segment + 1] * t;
    let terrain = plan
        .surface_height_at(x_m, z_m)
        .map_err(|error| format!("Path {}: {error}", path.name))?;
    if (height - terrain).abs() > profile.maximum_cut_fill_m {
        return Err(format!("Path {} exceeds its cut/fill limit", path.name));
    }
    Ok(Some(height))
}
fn validate_triangles(mesh: &PathSurfaceMesh) -> Result<(), String> {
    for triangle in mesh.indices.as_chunks::<3>().0 {
        let [a, b, c] = [
            mesh.positions[triangle[0] as usize],
            mesh.positions[triangle[1] as usize],
            mesh.positions[triangle[2] as usize],
        ];
        let area = (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]);
        if !area.is_finite() || area <= 0.0 {
            return Err("Path folds at a bend; increase point spacing or reduce width".into());
        }
    }
    Ok(())
}
