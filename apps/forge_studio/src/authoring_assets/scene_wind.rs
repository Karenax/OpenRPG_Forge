//! Apply only the accepted zone's wind to the common editor scene.
use bevy::prelude::*;
use forge_project::zone_authoring::wind::SceneWindSettings;
use std::cell::Cell;

#[derive(Clone, Copy, Default)]
enum Request {
    #[default]
    Idle,
    Apply(Option<SceneWindSettings>),
}

thread_local! {
    static REQUEST: Cell<Request> = const { Cell::new(Request::Idle) };
}

pub(super) fn observe(settings: Option<SceneWindSettings>) {
    REQUEST.with(|request| request.set(Request::Apply(settings)));
}

pub(super) fn register(app: &mut App) {
    app.add_systems(PreUpdate, apply);
}

fn apply(world: &mut World) {
    let Request::Apply(settings) = REQUEST.with(Cell::take) else {
        return;
    };
    if let Err(error) = forge_engine::source_wind::SceneWind::publish_with_frame(world, settings) {
        crate::authoring_viewport::report(format!("Vent de la carte indisponible : {error}"));
    }
}
