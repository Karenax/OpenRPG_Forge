//! Pinned transient physics payload for the same source preview host.
use bevy::ecs as bevy_ecs;
use bevy::prelude::IntoScheduleConfigs;
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
#[derive(Clone)]
pub(super) struct SourcePhysicsPayload {
    pub wire: std::sync::Arc<[u8]>,
    pub carrier_sha256: forge_core::ContentHash,
    pub manifest_sha256: forge_core::ContentHash,
    pub package_sha256: forge_core::ContentHash,
}

impl SourcePhysicsPayload {
    /// Keep the complete saved identity through async reads and until mounting.
    pub(super) fn decode_for_carrier(
        &self,
        carrier: forge_core::ContentHash,
    ) -> Result<forge_assets::source_physics_package::SourcePhysicsPackage, String> {
        if self.carrier_sha256 != carrier {
            return Err("Paquet physique différent du modèle à monter".into());
        }
        forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
            &self.wire,
            self.carrier_sha256,
            self.manifest_sha256,
            self.package_sha256,
        )
    }
}

/// Explicit settings owned by the authoring scene, shared by every source root.
#[derive(bevy::prelude::Resource, Clone)]
pub(super) struct SceneContactSettings(pub PhysicsSceneSettings);

impl SceneContactSettings {
    pub(super) fn resolved(
        &self,
    ) -> Result<forge_engine::source_physics_contacts::ContactSceneSettings, String> {
        self.0.validate()?;
        Ok(
            forge_engine::source_physics_contacts::ContactSceneSettings {
                layer_matrix: self.0.layer_matrix,
                contact_skin_m: self.0.contact_skin_m,
                evidence: self.0.evidence.clone(),
            },
        )
    }
}
