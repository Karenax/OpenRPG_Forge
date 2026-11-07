//! Pinned transient physics payload for the same source preview host.
use bevy::prelude::IntoScheduleConfigs;
pub(super) use forge_engine::source_graph_mount::SourcePhysicsPayload;
pub(super) use forge_engine::source_graph_placement::SceneContactSettings;
use forge_project::zone_authoring::physics::PhysicsSceneSettings;
use std::cell::RefCell;

enum Request {
    Set(Box<PhysicsSceneSettings>),
    Clear,
}
thread_local! { static REQUEST: RefCell<Option<Request>> = const { RefCell::new(None) }; }

pub(super) fn observe(settings: Option<Box<PhysicsSceneSettings>>) {
    REQUEST.with(|request| request.replace(Some(settings.map_or(Request::Clear, Request::Set))));
}

pub(super) fn register(app: &mut bevy::prelude::App) {
    app.add_systems(
        bevy::prelude::PostUpdate,
        update.before(super::preview::PreviewSystems::Requests),
    );
}

fn update(world: &mut bevy::prelude::World) {
    let Some(request) = REQUEST.with(|request| request.borrow_mut().take()) else {
        return;
    };
    let settings = match request {
        Request::Set(settings) => Some(settings),
        Request::Clear => None,
    };
    let current = world
        .get_resource::<SceneContactSettings>()
        .map(|value| &value.0);
    if current == settings.as_deref() {
        return;
    }
    super::preview::cancel();
    match settings {
        Some(settings) if settings.validate().is_ok() => {
            world.insert_resource(SceneContactSettings(*settings));
        }
        _ => {
            world.remove_resource::<SceneContactSettings>();
        }
    }
}
