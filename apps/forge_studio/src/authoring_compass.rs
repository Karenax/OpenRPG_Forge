//! Read-only camera bearing and terrain-hover coordinates for the editor HUD.
use bevy::prelude::*;
use leptos::prelude::*;
use std::cell::Cell;

thread_local! {
    static HEADING: Cell<Option<RwSignal<f32>>> = const { Cell::new(None) };
    static HIT: Cell<Option<RwSignal<Option<[f32; 3]>>>> = const { Cell::new(None) };
    static AXES: Cell<Option<RwSignal<[AxisEnd; 3]>>> = const { Cell::new(None) };
}

#[derive(Clone, Copy, PartialEq)]
struct AxisEnd {
    x: f32,
    y: f32,
}

fn project_axes(transform: &Transform) -> [AxisEnd; 3] {
    [Vec3::X, Vec3::Y, Vec3::Z].map(|axis| {
        let local = transform.rotation.inverse() * axis;
        let quantize = |value: f32| (value * 2.0).round() / 2.0;
        // An oblique depth cue keeps the axis facing the camera visible too.
        // A flat screen projection would collapse it onto the origin.
        AxisEnd {
            x: quantize(52.0 + local.x * 30.0 - local.z * 12.0),
            y: quantize(52.0 - local.y * 30.0 + local.z * 12.0),
        }
    })
}

#[component]
pub(super) fn CompassHud() -> impl IntoView {
    let heading = RwSignal::new(0.0_f32);
    let hit = RwSignal::new(None::<[f32; 3]>);
    HEADING.with(|slot| slot.set(Some(heading)));
    HIT.with(|slot| slot.set(Some(hit)));
    on_cleanup(|| {
        HEADING.with(|slot| slot.set(None));
        HIT.with(|slot| slot.set(None));
    });
    view! {
        <aside class="forge-compass" aria-label="Boussole et coordonnées du terrain">
            <div class="forge-compass-rose" aria-hidden="true">
                <div class="forge-compass-cardinals" style=move || format!("transform:rotate({}deg)", -heading.get())>
                    <span class="north">"N"</span><span class="east">"E"</span>
                    <span class="south">"S"</span><span class="west">"O"</span>
                </div>
                <span class="forge-compass-forward">"▲"</span>
            </div>
            <div class="forge-compass-readout">
                <strong>"Repère du terrain"</strong>
                <span>{move || format!("Vue {:.0}°", heading.get())}</span>
                <output aria-live="off">{move || match hit.get() {
                    Some([x, y, z]) => format!("X {x:.1} · Y {y:.1} · Z {z:.1} m"),
                    None => "Survolez le terrain pour lire X · Y · Z".into(),
                }}</output>
            </div>
        </aside>
    }
}

#[component]
pub(super) fn AxisHud() -> impl IntoView {
    let axes = RwSignal::new(project_axes(&Transform::IDENTITY));
    AXES.with(|slot| slot.set(Some(axes)));
    on_cleanup(|| AXES.with(|slot| slot.set(None)));
    view! {
        <aside class="forge-axis-hud" aria-label="Repère des trois axes X Y Z">
            <strong>"Axes du monde"</strong>
            <svg viewBox="0 0 104 104" role="img" aria-label="X rouge, Y vert vertical, Z bleu">
                {[('X', "#d65247"), ('Y', "#418b5b"), ('Z', "#4c77c0")].into_iter().enumerate().map(move |(index, (label, color))| view! {
                    <g>
                        <line x1="52" y1="52" x2=move || (104.0 - axes.get()[index].x).to_string()
                            y2=move || (104.0 - axes.get()[index].y).to_string() stroke=color stroke-width="2" opacity="0.35"/>
                        <line x1="52" y1="52" x2=move || axes.get()[index].x.to_string()
                            y2=move || axes.get()[index].y.to_string() stroke=color stroke-width="3" stroke-linecap="round"/>
                        <circle cx=move || axes.get()[index].x.to_string()
                            cy=move || axes.get()[index].y.to_string() r="4" fill=color/>
                        <text x=move || (axes.get()[index].x + 6.0).clamp(6.0, 95.0).to_string()
                            y=move || (axes.get()[index].y - 5.0).clamp(10.0, 98.0).to_string()
                            fill=color>{label.to_string()}</text>
                    </g>
                }).collect_view()}
                <circle cx="52" cy="52" r="3" fill="#423b2e"/>
            </svg>
            <span>"X horizontal · Y hauteur · Z profondeur"</span>
        </aside>
    }
}

pub(super) fn publish_camera(transform: &Transform) {
    let forward = transform.forward();
    let heading = forward
        .x
        .atan2(-forward.z)
        .to_degrees()
        .rem_euclid(360.0)
        .round()
        .rem_euclid(360.0);
    HEADING.with(|slot| {
        if let Some(signal) = slot.get()
            && (signal.get_untracked() - heading).abs() >= 1.0
        {
            signal.set(heading);
        }
    });
    let projected = project_axes(transform);
    AXES.with(|slot| {
        if let Some(signal) = slot.get()
            && signal.get_untracked() != projected
        {
            signal.set(projected);
        }
    });
}

pub(super) fn publish_hit(hit: Option<Vec3>) {
    let value = hit
        .filter(|point| point.is_finite())
        .map(|point| [point.x, point.y, point.z].map(|axis| (axis * 10.0).round() / 10.0));
    HIT.with(|slot| {
        if let Some(signal) = slot.get()
            && signal.get_untracked() != value
        {
            signal.set(value);
        }
    });
}
