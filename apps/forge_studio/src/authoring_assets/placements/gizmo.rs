//! Transient translation handles. Canonical mutation is submitted only after release.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::{EntityId, Transform as ForgeTransform, Vec3 as ForgeVec3};
use forge_world::{placement_axis, placement_transform::Axis};
use leptos::prelude::{Callable, Callback};
use serde_json::{Value, json};
use std::cell::RefCell;
use wasm_bindgen::JsCast;
mod rotation;

#[derive(Clone, Copy)]
pub(crate) enum Mode {
    Translation,
    LocalTranslation {
        step_m: Option<f32>,
    },
    Rotation {
        step_degrees: Option<f32>,
        space: forge_world::placement_transform::RotationSpace,
        pivot: Option<ForgeVec3>,
    },
    Scale,
}

#[derive(Component)]
pub(crate) struct PlacementRoot(pub EntityId, pub ForgeTransform);

#[derive(Clone, Copy)]
pub(crate) enum Phase {
    Down,
    Move,
    Up,
}
struct Drag {
    axis: Axis,
    start: f32,
    extent: f32,
}
struct Tool {
    mode: Mode,
    id: EntityId,
    batched: bool,
    source: ForgeTransform,
    current: ForgeTransform,
    drag: Option<Drag>,
    alignment: Option<crate::authoring_raster::Alignment>,
    callback: Callback<Result<ForgeTransform, String>>,
    phase: Option<Phase>,
    click_normalized: Option<Vec2>,
}
#[derive(Clone, Copy)]
enum DiagnosticState {
    Idle,
    Active,
    Cancelled,
}
thread_local! {
    static TOOL: RefCell<Option<Tool>> = const { RefCell::new(None) };
    static EVENTS: RefCell<Vec<(Phase, Vec2)>> = const { RefCell::new(Vec::new()) };
    static RESTORE: RefCell<Option<(EntityId, ForgeTransform)>> = const { RefCell::new(None) };
    static DIAGNOSTIC_STATE: RefCell<DiagnosticState> = const { RefCell::new(DiagnosticState::Idle) };
}

pub(crate) fn arm(
    mode: Mode,
    id: EntityId,
    source: ForgeTransform,
    alignment: Option<crate::authoring_raster::Alignment>,
    callback: Callback<Result<ForgeTransform, String>>,
) {
    cancel();
    let batched = crate::authoring_imported_population::current_head()
        .is_some_and(|head| super::super::objects::is_batched(&head, id));
    TOOL.with(|slot| {
        slot.replace(Some(Tool {
            mode,
            id,
            batched,
            source,
            current: source,
            drag: None,
            alignment,
            callback,
            phase: None,
            click_normalized: None,
        }))
    });
    DIAGNOSTIC_STATE.with(|slot| slot.replace(DiagnosticState::Active));
}

pub(crate) fn can_arm() -> bool {
    source_current() && super::proposal_ready() == Ok(true)
}

fn source_current() -> bool {
    crate::authoring_viewport::can_preview_asset()
        && !super::PREVIEW_ACTIVE.with(std::cell::Cell::get)
        && super::DESIRED.with(|desired| {
            super::ACCEPTED.with(|accepted| *desired.borrow() == *accepted.borrow())
        })
}
pub(crate) fn active() -> bool {
    TOOL.with(|slot| slot.borrow().is_some())
}
pub(crate) fn cancel() {
    if let Some(tool) = TOOL.with(|slot| slot.borrow_mut().take()) {
        RESTORE.with(|slot| slot.replace(Some((tool.id, tool.source))));
        DIAGNOSTIC_STATE.with(|slot| slot.replace(DiagnosticState::Cancelled));
    }
    EVENTS.with(|slot| slot.borrow_mut().clear());
}

#[allow(
    clippy::cast_precision_loss,
    reason = "Browser pixel dimensions fit f32"
)]
pub(crate) fn pointer(event: &web_sys::MouseEvent, phase: Phase) {
    if !TOOL.with(|slot| slot.borrow().is_some())
        || (!matches!(phase, Phase::Move) && event.button() != 0)
    {
        return;
    }
    let Some(target) = event
        .current_target()
        .and_then(|t| t.dyn_into::<web_sys::HtmlElement>().ok())
    else {
        return;
    };
    let (width, height) = (target.client_width(), target.client_height());
    if width <= 0 || height <= 0 {
        return;
    }
    let point = Vec2::new(
        event.offset_x() as f32 / width as f32,
        event.offset_y() as f32 / height as f32,
    );
    let overflow = EVENTS.with(|slot| {
        let mut events = slot.borrow_mut();
        if events.len() >= 64 {
            return true;
        }
        events.push((phase, point));
        false
    });
    if overflow {
        cancel();
        super::report("Geste annulé : trop d’événements en attente.");
    }
}

fn forge(value: Vec3) -> ForgeVec3 {
    ForgeVec3::new(value.x, value.y, value.z)
}
fn bevy(value: ForgeVec3) -> Vec3 {
    Vec3::new(value.x, value.y, value.z)
}

fn project(source: ForgeTransform) -> Transform {
    Transform {
        translation: bevy(source.translation_m),
        rotation: Quat::from_xyzw(
            source.rotation.x,
            source.rotation.y,
            source.rotation.z,
            source.rotation.w,
        ),
        scale: bevy(source.scale),
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn update(
    cameras: Query<
        (&Camera, &GlobalTransform),
        forge_engine::tidewater_sky::preview::SceneCameraFilter,
    >,
    mut roots: Query<(&PlacementRoot, &mut Transform, &Visibility)>,
    mut gizmos: Gizmos,
) {
    if let Some((id, original)) = RESTORE.with(|slot| slot.borrow_mut().take()) {
        for (root, mut transform, _) in &mut roots {
            if root.0 == id && root.1 == original {
                *transform = project(original);
            }
        }
    }
    let Some(mut tool) = TOOL.with(|slot| slot.borrow_mut().take()) else {
        write_diagnostic(&idle_diagnostic());
        return;
    };
    // A live preview intentionally differs from the accepted pose. Physics readiness
    // gates starting a gesture and playtest, not each intermediate drag position.
    if !source_current() {
        RESTORE.with(|slot| slot.replace(Some((tool.id, tool.source))));
        DIAGNOSTIC_STATE.with(|slot| slot.replace(DiagnosticState::Cancelled));
        write_diagnostic(&cancelled_diagnostic());
        return;
    }
    let ready = tool.batched
        || roots
            .iter()
            .any(|(root, _, visibility)| root.0 == tool.id && *visibility != Visibility::Hidden);
    let events = EVENTS.with(|slot| std::mem::take(&mut *slot.borrow_mut()));
    let mut result = None;
    if ready
        && let Ok((camera, camera_transform)) = cameras.single()
        && let Some(size) = camera.logical_viewport_size()
    {
        let pivot = if matches!(tool.mode, Mode::Rotation { .. }) {
            rotation::pivot(&tool)
        } else {
            bevy(tool.current.translation_m)
        };
        let length = (camera_transform.translation().distance(pivot) * 0.12).clamp(0.5, 20.0);
        if matches!(tool.mode, Mode::Rotation { .. }) {
            rotation::draw(&mut gizmos, &tool, length);
        } else {
            for (axis, color) in [
                (Axis::X, Color::srgb(1.0, 0.2, 0.2)),
                (Axis::Y, Color::srgb(0.3, 1.0, 0.3)),
                (Axis::Z, Color::srgb(0.3, 0.5, 1.0)),
            ] {
                let tip = pivot + axis_vector(&tool, axis) * length;
                gizmos.line(pivot, tip, color);
                if matches!(tool.mode, Mode::Scale) {
                    gizmos.cube(
                        Transform {
                            translation: tip,
                            rotation: project(tool.source).rotation,
                            scale: Vec3::splat(length * 0.14),
                        },
                        color,
                    );
                } else {
                    gizmos.sphere(tip, length * 0.07, color);
                }
            }
        }
        for (phase, position) in events {
            tool.phase = Some(phase);
            tool.click_normalized = Some(position);
            match step(
                &mut tool,
                phase,
                position * size,
                camera,
                camera_transform,
                length,
            ) {
                Ok(true) => {
                    result = Some(Ok(tool.current));
                    break;
                }
                Ok(false) => {}
                Err(error) => {
                    result = Some(Err(error));
                    break;
                }
            }
        }
        for (root, mut transform, _) in &mut roots {
            if root.0 == tool.id {
                *transform = project(tool.current);
            }
        }
        write_diagnostic(&active_diagnostic(
            &tool,
            camera,
            camera_transform,
            size,
            pivot,
            length,
        ));
    }
    if let Some(result) = result {
        RESTORE.with(|slot| slot.replace(Some((tool.id, tool.source))));
        tool.callback.run(result);
        DIAGNOSTIC_STATE.with(|slot| slot.replace(DiagnosticState::Idle));
    } else {
        TOOL.with(|slot| slot.replace(Some(tool)));
    }
}

fn axis_name(axis: Axis) -> &'static str {
    match axis {
        Axis::X => "x",
        Axis::Y => "y",
        Axis::Z => "z",
    }
}

fn phase_name(phase: Option<Phase>) -> &'static str {
    match phase {
        Some(Phase::Down) => "down",
        Some(Phase::Move) => "move",
        Some(Phase::Up) => "up",
        None => "idle",
    }
}

fn mode_name(mode: Mode) -> &'static str {
    match mode {
        Mode::Translation => "translation",
        Mode::LocalTranslation { .. } => "local_translation",
        Mode::Rotation { .. } => "rotation",
        Mode::Scale => "scale",
    }
}

fn rotation_space(mode: Mode) -> Option<&'static str> {
    match mode {
        Mode::Rotation {
            space: forge_world::placement_transform::RotationSpace::Local,
            ..
        } => Some("local"),
        Mode::Rotation { .. } => Some("world"),
        _ => None,
    }
}

fn transform_json(transform: ForgeTransform) -> Value {
    json!({
        "translation_m": [transform.translation_m.x, transform.translation_m.y, transform.translation_m.z],
        "rotation": [transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w],
        "scale": [transform.scale.x, transform.scale.y, transform.scale.z],
    })
}

fn normalized(point: Vec2, size: Vec2) -> Option<Value> {
    (size.x > 0.0 && size.y > 0.0).then(|| json!({"x": point.x / size.x, "y": point.y / size.y}))
}

fn axis_vector(tool: &Tool, axis: Axis) -> Vec3 {
    let vector = match axis {
        Axis::X => Vec3::X,
        Axis::Y => Vec3::Y,
        Axis::Z => Vec3::Z,
    };
    if matches!(tool.mode, Mode::Scale | Mode::LocalTranslation { .. }) {
        project(tool.source).rotation * vector
    } else {
        vector
    }
}

fn axis_handles(
    tool: &Tool,
    camera: &Camera,
    camera_transform: &GlobalTransform,
    size: Vec2,
    pivot: Vec3,
    length: f32,
) -> Value {
    Value::Array([Axis::X, Axis::Y, Axis::Z].into_iter().map(|axis| {
        let tip = pivot + axis_vector(tool, axis) * length;
        json!({"axis": axis_name(axis), "screen_normalized": camera.world_to_viewport(camera_transform, tip).ok().and_then(|point| normalized(point, size))})
    }).collect())
}

fn active_diagnostic(
    tool: &Tool,
    camera: &Camera,
    camera_transform: &GlobalTransform,
    size: Vec2,
    pivot: Vec3,
    length: f32,
) -> String {
    let rings = if matches!(tool.mode, Mode::Rotation { .. }) {
        rotation::projected_candidates(tool, camera, camera_transform, size, length)
    } else {
        Value::Array(Vec::new())
    };
    json!({
        "schema": "forge-placement-gizmo-diagnostic", "version": 1,
        "state": "active", "placement_id": tool.id.to_string(), "mode": mode_name(tool.mode),
        "rotation_space": rotation_space(tool.mode), "source_transform": transform_json(tool.source),
        "current_transform": transform_json(tool.current), "pivot_world": [pivot.x, pivot.y, pivot.z],
        "logical_viewport_size": {"width": size.x, "height": size.y}, "phase": phase_name(tool.phase),
        "active_axis": tool.drag.as_ref().map(|drag| axis_name(drag.axis)),
        "click_normalized": tool.click_normalized.map(|point| json!({"x": point.x, "y": point.y})),
        "axis_handle_screen_normalized": axis_handles(tool, camera, camera_transform, size, pivot, length),
        "rotation_ring_candidates_screen_normalized": rings,
    }).to_string()
}

fn idle_diagnostic() -> String {
    let state = DIAGNOSTIC_STATE.with(|slot| *slot.borrow());
    let state = match state {
        DiagnosticState::Cancelled => "cancelled",
        DiagnosticState::Active | DiagnosticState::Idle => "idle",
    };
    json!({"schema":"forge-placement-gizmo-diagnostic","version":1,"state":state,"placement_id":Value::Null,"mode":Value::Null,"rotation_space":Value::Null,"source_transform":Value::Null,"current_transform":Value::Null,"pivot_world":Value::Null,"logical_viewport_size":Value::Null,"phase":state,"active_axis":Value::Null,"click_normalized":Value::Null,"axis_handle_screen_normalized":[],"rotation_ring_candidates_screen_normalized":[]}).to_string()
}

fn cancelled_diagnostic() -> String {
    idle_diagnostic()
}

fn write_diagnostic(json: &str) {
    let Some(document) = web_sys::window().and_then(|window| window.document()) else {
        return;
    };
    let Some(node) = document.get_element_by_id("forge-placement-gizmo-diagnostic") else {
        return;
    };
    if node.text_content().as_deref() != Some(json) {
        node.set_text_content(Some(json));
    }
}

fn step(
    tool: &mut Tool,
    phase: Phase,
    point: Vec2,
    camera: &Camera,
    camera_transform: &GlobalTransform,
    length: f32,
) -> Result<bool, String> {
    if matches!(tool.mode, Mode::Rotation { .. }) {
        return rotation::step(tool, phase, point, camera, camera_transform, length);
    }
    let pivot = bevy(tool.source.translation_m);
    if matches!(phase, Phase::Down) {
        let selected = [Axis::X, Axis::Y, Axis::Z]
            .into_iter()
            .filter_map(|axis| {
                let vector = axis_vector(tool, axis);
                let start = camera.world_to_viewport(camera_transform, pivot).ok()?;
                let end = camera
                    .world_to_viewport(camera_transform, pivot + vector * length)
                    .ok()?;
                let segment = end - start;
                if segment.length() < 20.0 {
                    return None;
                }
                let fraction =
                    ((point - start).dot(segment) / segment.length_squared()).clamp(0.25, 1.0);
                let distance = (point - (start + segment * fraction)).length();
                (distance <= 12.0).then_some((axis, distance))
            })
            .min_by(|a, b| a.1.total_cmp(&b.1));
        if let Some((axis, _)) = selected {
            tool.drag = Some(Drag {
                axis,
                start: parameter(tool, axis, point, camera, camera_transform)?,
                extent: length,
            });
        }
        return Ok(false);
    }
    let Some(drag) = &tool.drag else {
        return Ok(false);
    };
    let end = parameter(tool, drag.axis, point, camera, camera_transform)?;
    if matches!(tool.mode, Mode::Scale) {
        tool.current = forge_world::placement_transform::scale_axis(
            tool.source,
            drag.axis,
            1.0 + (end - drag.start) / drag.extent,
        )?;
    } else if let Mode::LocalTranslation { step_m } = tool.mode {
        tool.current =
            placement_axis::translate_local(tool.source, drag.axis, end - drag.start, step_m)?;
    } else {
        tool.current = placement_axis::translate(tool.source, drag.axis, end - drag.start)?;
        if let Some(alignment) = &tool.alignment {
            tool.current = alignment.resolve_axis(tool.current, drag.axis)?;
        }
    }
    Ok(matches!(phase, Phase::Up))
}

fn parameter(
    tool: &Tool,
    axis: Axis,
    point: Vec2,
    camera: &Camera,
    transform: &GlobalTransform,
) -> Result<f32, String> {
    let ray = camera
        .viewport_to_world(transform, point)
        .map_err(|e| e.to_string())?;
    if matches!(tool.mode, Mode::Scale | Mode::LocalTranslation { .. }) {
        let inverse = project(tool.source).rotation.inverse();
        placement_axis::ray_parameter(
            ForgeVec3::ZERO,
            axis,
            forge(inverse * (ray.origin - bevy(tool.source.translation_m))),
            forge(inverse * *ray.direction),
        )
    } else {
        placement_axis::ray_parameter(
            tool.source.translation_m,
            axis,
            forge(ray.origin),
            forge(*ray.direction),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn idle_diagnostic_has_a_stable_read_only_contract() {
        DIAGNOSTIC_STATE.with(|slot| slot.replace(DiagnosticState::Idle));
        let value: Value = serde_json::from_str(&idle_diagnostic()).unwrap();
        assert_eq!(value["schema"], "forge-placement-gizmo-diagnostic");
        assert_eq!(value["version"], 1);
        assert_eq!(value["state"], "idle");
        for field in [
            "placement_id",
            "mode",
            "rotation_space",
            "source_transform",
            "current_transform",
            "pivot_world",
            "logical_viewport_size",
            "active_axis",
            "click_normalized",
        ] {
            assert!(
                value[field].is_null(),
                "{field} must remain null while idle"
            );
        }
        assert_eq!(value["phase"], "idle");
        assert_eq!(value["axis_handle_screen_normalized"], json!([]));
        assert_eq!(
            value["rotation_ring_candidates_screen_normalized"],
            json!([])
        );
    }

    #[test]
    fn transform_serialization_keeps_vector_and_quaternion_order() {
        let value = transform_json(ForgeTransform::IDENTITY);
        assert_eq!(value["translation_m"], json!([0.0, 0.0, 0.0]));
        assert_eq!(value["rotation"], json!([0.0, 0.0, 0.0, 1.0]));
        assert_eq!(value["scale"], json!([1.0, 1.0, 1.0]));
    }
}
