//! Projected world handles stay readable over meshes, without a second GPU pass.
use super::{Axis, axis_name};
use bevy::prelude::*;
use forge_core::EntityId;
use leptos::prelude::*;
use serde_json::{Value, json};
use std::cell::RefCell;

#[derive(Clone, Copy)]
pub(super) enum Plane {
    XY,
    XZ,
    YZ,
}
impl Plane {
    pub(super) fn axes(self) -> (Axis, Axis) {
        match self {
            Self::XY => (Axis::X, Axis::Y),
            Self::XZ => (Axis::X, Axis::Z),
            Self::YZ => (Axis::Y, Axis::Z),
        }
    }
    pub(super) fn normal(self) -> Axis {
        match self {
            Self::XY => Axis::Z,
            Self::XZ => Axis::Y,
            Self::YZ => Axis::X,
        }
    }
    pub(super) fn name(self) -> &'static str {
        match self {
            Self::XY => "xy",
            Self::XZ => "xz",
            Self::YZ => "yz",
        }
    }
}
#[derive(Clone, Copy)]
pub(super) enum Hit {
    Axis(Axis),
    Plane(Plane),
}
struct Screen {
    id: EntityId,
    editable: bool,
    pivot: Vec2,
    size: Vec2,
    axes: Vec<(Axis, Vec2)>,
    planes: Vec<(Plane, [Vec2; 4])>,
}
thread_local! {
    static SCREEN: RefCell<Option<Screen>> = const { RefCell::new(None) };
    static LAST_SVG: RefCell<String> = const { RefCell::new(String::new()) };
}

#[component]
pub(crate) fn HandlesHud() -> impl IntoView {
    view! {
        <svg id="forge-selection-handles" class="selection-handles" style="display:none"
             aria-label="Repère de déplacement : axes X Y Z et plans XY XZ YZ">
            <g class="handle-axis"><line id="handle-axis-x"/><polygon id="handle-arrow-x"/><text id="handle-label-x">"X"</text></g>
            <g class="handle-axis"><line id="handle-axis-y"/><polygon id="handle-arrow-y"/><text id="handle-label-y">"Y"</text></g>
            <g class="handle-axis"><line id="handle-axis-z"/><polygon id="handle-arrow-z"/><text id="handle-label-z">"Z"</text></g>
            <g class="handle-plane"><polygon id="handle-plane-xy"/><text id="handle-plane-label-xy">"XY"</text></g>
            <g class="handle-plane"><polygon id="handle-plane-xz"/><text id="handle-plane-label-xz">"XZ"</text></g>
            <g class="handle-plane"><polygon id="handle-plane-yz"/><text id="handle-plane-label-yz">"YZ"</text></g>
        </svg>
    }
}
fn set(document: &web_sys::Document, id: &str, key: &str, value: &str) {
    if let Some(element) = document.get_element_by_id(id) {
        let _ = element.set_attribute(key, value);
    }
}
pub(super) fn clear() {
    SCREEN.with(|s| s.borrow_mut().take());
    LAST_SVG.with(|s| s.borrow_mut().clear());
    if let Some(document) = web_sys::window().and_then(|w| w.document()) {
        set(
            &document,
            "forge-selection-handles",
            "style",
            "display:none",
        );
    }
}
fn cross(a: Vec2, b: Vec2) -> f32 {
    a.x * b.y - a.y * b.x
}
fn inside(point: Vec2, quad: &[Vec2; 4]) -> bool {
    let mut positive = false;
    let mut negative = false;
    for i in 0..4 {
        let side = cross(quad[(i + 1) % 4] - quad[i], point - quad[i]);
        positive |= side > 0.01;
        negative |= side < -0.01;
    }
    let area = cross(quad[1] - quad[0], quad[3] - quad[0]).abs();
    area >= 30.0 && !(positive && negative)
}
pub(super) fn hit(normalized: Vec2) -> Option<(EntityId, Hit)> {
    SCREEN.with(|s| {
        let slot = s.borrow();
        let s = slot.as_ref()?;
        if !s.editable {
            return None;
        }
        let point = normalized * s.size;
        // The filled squares are explicit two-axis targets, including in a
        // foreshortened view where a neighbouring shaft crosses their pixels.
        if let Some((plane, _)) = s.planes.iter().find(|(_, quad)| inside(point, quad)) {
            return Some((s.id, Hit::Plane(*plane)));
        }
        let axis = s
            .axes
            .iter()
            .filter_map(|(axis, tip)| {
                let segment = *tip - s.pivot;
                if segment.length() < 20.0 {
                    return None;
                }
                let along =
                    ((point - s.pivot).dot(segment) / segment.length_squared()).clamp(0.25, 1.0);
                let distance = (point - (s.pivot + segment * along)).length();
                (distance <= 12.0).then_some((*axis, distance))
            })
            .min_by(|a, b| a.1.total_cmp(&b.1));
        if let Some((axis, _)) = axis {
            return Some((s.id, Hit::Axis(axis)));
        }
        None
    })
}
pub(super) fn planes_json() -> Value {
    SCREEN.with(|s|s.borrow().as_ref().map_or_else(||json!([]),|s| {
        json!(s.planes.iter().map(|(plane,quad)|{
            let center=quad.iter().copied().sum::<Vec2>()/4.0/s.size;
            json!({"plane":plane.name(),"screen_normalized":{"x":center.x,"y":center.y},"pickable":cross(quad[1]-quad[0],quad[3]-quad[0]).abs()>=30.0})
        }).collect::<Vec<_>>())
    }))
}
#[derive(Clone, Copy)]
pub(super) struct Geometry {
    pub pivot: Vec3,
    pub vectors: [Vec3; 3],
    pub length: f32,
}

pub(super) fn show(
    id: EntityId,
    editable: bool,
    camera: &Camera,
    transform: &GlobalTransform,
    size: Vec2,
    geometry: Geometry,
) {
    let Geometry {
        pivot,
        vectors,
        length,
    } = geometry;
    let Ok(center) = camera.world_to_viewport(transform, pivot) else {
        clear();
        return;
    };
    let axes = [Axis::X, Axis::Y, Axis::Z]
        .into_iter()
        .zip(vectors)
        .filter_map(|(axis, v)| {
            camera
                .world_to_viewport(transform, pivot + v * length)
                .ok()
                .map(|p| (axis, p))
        })
        .collect::<Vec<_>>();
    let vector = |axis| {
        vectors[match axis {
            Axis::X => 0,
            Axis::Y => 1,
            Axis::Z => 2,
        }]
    };
    let planes = [Plane::XY, Plane::XZ, Plane::YZ]
        .into_iter()
        .filter_map(|plane| {
            let (a, b) = plane.axes();
            let a = vector(a) * length;
            let b = vector(b) * length;
            let mut quad = [Vec2::ZERO; 4];
            for (i, (x, y)) in [(0.19, 0.19), (0.43, 0.19), (0.43, 0.43), (0.19, 0.43)]
                .into_iter()
                .enumerate()
            {
                quad[i] = camera
                    .world_to_viewport(transform, pivot + a * x + b * y)
                    .ok()?;
            }
            Some((plane, quad))
        })
        .collect::<Vec<_>>();
    let signature = format!(
        "{id}:{editable}:{size:?}:{center:?}:{axes:?}:{}",
        json!(
            planes
                .iter()
                .map(|(p, q)| (p.name(), q.iter().map(|p| [p.x, p.y]).collect::<Vec<_>>()))
                .collect::<Vec<_>>()
        )
    );
    let changed = LAST_SVG.with(|s| {
        let mut s = s.borrow_mut();
        if *s == signature {
            false
        } else {
            *s = signature;
            true
        }
    });
    if changed && let Some(document) = web_sys::window().and_then(|w| w.document()) {
        render_svg(&document, id, editable, size, center, &axes, &planes);
    }
    SCREEN.with(|s| {
        s.replace(Some(Screen {
            id,
            editable,
            pivot: center,
            size,
            axes,
            planes,
        }))
    });
}

fn render_svg(
    document: &web_sys::Document,
    id: EntityId,
    editable: bool,
    size: Vec2,
    center: Vec2,
    axes: &[(Axis, Vec2)],
    planes: &[(Plane, [Vec2; 4])],
) {
    set(
        document,
        "forge-selection-handles",
        "style",
        "display:block",
    );
    set(
        document,
        "forge-selection-handles",
        "viewBox",
        &format!("0 0 {} {}", size.x, size.y),
    );
    set(
        document,
        "forge-selection-handles",
        "data-placement-id",
        &id.to_string(),
    );
    set(
        document,
        "forge-selection-handles",
        "data-editable",
        if editable { "true" } else { "false" },
    );
    for name in ["x", "y", "z"] {
        for prefix in ["axis", "arrow", "label"] {
            set(
                document,
                &format!("handle-{prefix}-{name}"),
                "style",
                "display:none",
            );
        }
    }
    for name in ["xy", "xz", "yz"] {
        set(
            document,
            &format!("handle-plane-{name}"),
            "style",
            "display:none",
        );
        set(
            document,
            &format!("handle-plane-label-{name}"),
            "style",
            "display:none",
        );
    }
    render_axes(document, center, axes);
    for (plane, quad) in planes {
        let name = plane.name();
        let label = quad.iter().copied().sum::<Vec2>() / 4.0;
        set(document, &format!("handle-plane-{name}"), "style", "");
        set(document, &format!("handle-plane-label-{name}"), "style", "");
        set(
            document,
            &format!("handle-plane-{name}"),
            "points",
            &quad
                .iter()
                .map(|p| format!("{},{}", p.x, p.y))
                .collect::<Vec<_>>()
                .join(" "),
        );
        set(
            document,
            &format!("handle-plane-label-{name}"),
            "x",
            &label.x.to_string(),
        );
        set(
            document,
            &format!("handle-plane-label-{name}"),
            "y",
            &label.y.to_string(),
        );
    }
}

fn render_axes(document: &web_sys::Document, center: Vec2, axes: &[(Axis, Vec2)]) {
    for (axis, tip) in axes {
        let name = axis_name(*axis);
        let v = (*tip - center).normalize_or_zero();
        let perpendicular = Vec2::new(-v.y, v.x);
        for prefix in ["axis", "arrow", "label"] {
            set(document, &format!("handle-{prefix}-{name}"), "style", "");
        }
        let base = *tip - v * 13.0;
        let label = *tip + v * 13.0;
        let line = format!("handle-axis-{name}");
        for (k, value) in [
            ("x1", center.x),
            ("y1", center.y),
            ("x2", tip.x),
            ("y2", tip.y),
        ] {
            set(document, &line, k, &value.to_string());
        }
        let q = [*tip, base + perpendicular * 6.0, base - perpendicular * 6.0];
        set(
            document,
            &format!("handle-arrow-{name}"),
            "points",
            &q.iter()
                .map(|p| format!("{},{}", p.x, p.y))
                .collect::<Vec<_>>()
                .join(" "),
        );
        set(
            document,
            &format!("handle-label-{name}"),
            "x",
            &label.x.to_string(),
        );
        set(
            document,
            &format!("handle-label-{name}"),
            "y",
            &label.y.to_string(),
        );
    }
}
