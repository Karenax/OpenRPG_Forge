//! Rotation rings share the parent's transient gesture lifecycle and release callback.
use super::{Drag, Mode, Phase, Tool, bevy, forge, project};
use bevy::prelude::*;
use forge_world::{
    placement_pivot::rotate_about,
    placement_rotation,
    placement_transform::{Axis, RotationSpace},
};
use serde_json::{Value, json};

fn ring_point(axis: Axis, angle: f32) -> Vec3 {
    let (sine, cosine) = angle.sin_cos();
    match axis {
        Axis::X => Vec3::new(0.0, cosine, sine),
        Axis::Y => Vec3::new(sine, 0.0, cosine),
        Axis::Z => Vec3::new(cosine, sine, 0.0),
    }
}
fn segments(
    axis: Axis,
    pivot: Vec3,
    radius: f32,
    frame: Quat,
) -> impl Iterator<Item = (Vec3, Vec3)> {
    (0_u16..48).map(move |index| {
        let angle = f32::from(index) * std::f32::consts::TAU / 48.0;
        (
            pivot + frame * ring_point(axis, angle) * radius,
            pivot + frame * ring_point(axis, angle + std::f32::consts::TAU / 48.0) * radius,
        )
    })
}
// Freeze the interaction frame at activation. Rendering, picking and the ray
// use this same frame; rotating the derived preview must not feed back into it.
fn frame(tool: &Tool) -> Quat {
    match tool.mode {
        Mode::Rotation {
            space: RotationSpace::Local,
            ..
        } => project(tool.source).rotation,
        _ => Quat::IDENTITY,
    }
}

pub(super) fn pivot(tool: &Tool) -> Vec3 {
    match tool.mode {
        Mode::Rotation {
            pivot: Some(pivot), ..
        } => bevy(pivot),
        _ => bevy(tool.source.translation_m),
    }
}

pub(super) fn draw(gizmos: &mut Gizmos, tool: &Tool, radius: f32) {
    for (axis, color) in [
        (Axis::X, Color::srgb(1.0, 0.2, 0.2)),
        (Axis::Y, Color::srgb(0.3, 1.0, 0.3)),
        (Axis::Z, Color::srgb(0.3, 0.5, 1.0)),
    ] {
        for (start, end) in segments(axis, pivot(tool), radius, frame(tool)) {
            gizmos.line(start, end, color);
        }
    }
}

/// Screen candidates use the exact ring segments used by drawing and picking.
pub(super) fn projected_candidates(
    tool: &Tool,
    camera: &Camera,
    camera_transform: &GlobalTransform,
    size: Vec2,
    radius: f32,
) -> Value {
    let point = |world: Vec3| {
        camera
            .world_to_viewport(camera_transform, world)
            .ok()
            .and_then(|screen| {
                (size.x > 0.0 && size.y > 0.0)
                    .then(|| json!({"x": screen.x / size.x, "y": screen.y / size.y}))
            })
    };
    Value::Array(
        [Axis::X, Axis::Y, Axis::Z]
            .into_iter()
            .map(|axis| {
                let segments = segments(axis, pivot(tool), radius, frame(tool))
                    .map(|(start, end)| json!({"start": point(start), "end": point(end)}))
                    .collect::<Vec<_>>();
                let axis = match axis {
                    Axis::X => "x",
                    Axis::Y => "y",
                    Axis::Z => "z",
                };
                json!({"axis": axis, "segments": segments})
            })
            .collect(),
    )
}

pub(super) fn step(
    tool: &mut Tool,
    phase: Phase,
    point: Vec2,
    camera: &Camera,
    camera_transform: &GlobalTransform,
    radius: f32,
) -> Result<bool, String> {
    if matches!(phase, Phase::Down) {
        let selected = [Axis::X, Axis::Y, Axis::Z]
            .into_iter()
            .filter_map(|axis| {
                let distance = segments(axis, pivot(tool), radius, frame(tool))
                    .filter_map(|(start, end)| {
                        let start = camera.world_to_viewport(camera_transform, start).ok()?;
                        let end = camera.world_to_viewport(camera_transform, end).ok()?;
                        let vector = end - start;
                        if vector.length_squared() < 0.01 {
                            return None;
                        }
                        let fraction =
                            ((point - start).dot(vector) / vector.length_squared()).clamp(0.0, 1.0);
                        Some((point - (start + fraction * vector)).length())
                    })
                    .min_by(f32::total_cmp)?;
                (distance <= 12.0).then_some((axis, distance))
            })
            .min_by(|a, b| a.1.total_cmp(&b.1));
        if let Some((axis, _)) = selected {
            tool.drag = Some(Drag {
                axis,
                start: angle(tool, axis, point, camera, camera_transform)?,
                extent: radius,
            });
        }
        return Ok(false);
    }
    let Some(drag) = &tool.drag else {
        return Ok(false);
    };
    let delta = placement_rotation::delta_degrees(
        drag.start,
        angle(tool, drag.axis, point, camera, camera_transform)?,
    )?;
    let Mode::Rotation {
        step_degrees,
        space,
        ..
    } = tool.mode
    else {
        return Err("Rotation tool is not active".into());
    };
    let delta = placement_rotation::snap_degrees(delta, step_degrees)?;
    tool.current = rotate_about(tool.source, drag.axis, delta, space, forge(pivot(tool)))?;
    Ok(matches!(phase, Phase::Up))
}

fn angle(
    tool: &Tool,
    axis: Axis,
    point: Vec2,
    camera: &Camera,
    transform: &GlobalTransform,
) -> Result<f32, String> {
    let ray = camera
        .viewport_to_world(transform, point)
        .map_err(|e| e.to_string())?;
    let inverse = frame(tool).inverse();
    placement_rotation::ray_angle(
        forge_core::Vec3::ZERO,
        axis,
        forge(inverse * (ray.origin - pivot(tool))),
        forge(inverse * *ray.direction),
    )
}
