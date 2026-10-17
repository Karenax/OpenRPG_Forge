//! Governed model children share their existing gameplay body's identity and physics.
use super::GameplayBody;
use crate::DomainEntityId;
use bevy::{
    asset::LoadState,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use forge_core::EntityId;
use forge_gameplay::GameplayDocument;
use forge_project::module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin};
use std::collections::BTreeMap;

pub struct GameplayVisualModel {
    pub pin: VisualAssetPin,
    pub asset_path: String,
}

#[derive(Component, Clone, Debug)]
pub struct GovernedGameplayVisual {
    pub target: EntityId,
    pub pin: VisualAssetPin,
}

#[derive(Resource, Default)]
pub struct GameplayVisualStatus {
    pub expected: usize,
    pub spawned: usize,
    pub ready: usize,
    pub error: Option<String>,
}

#[derive(Resource, Default)]
pub(super) struct GameplayVisualPlan {
    entries: BTreeMap<EntityId, ProjectedVisual>,
}

struct ProjectedVisual {
    pin: VisualAssetPin,
    scene: Handle<WorldAsset>,
    transform: Transform,
}

/// Configure `AssetPlugin.file_path` to the admitted package root before this call.
/// Model paths are relative GLBs; only the module adapter supplies them.
pub fn install(
    app: &mut App,
    document: &GameplayDocument,
    bindings: &[GameplayVisualBinding],
    models: &[GameplayVisualModel],
) -> Result<(), String> {
    let pins = models.iter().map(|model| model.pin).collect::<Vec<_>>();
    forge_project::module_gameplay_visuals::validate_for_document(bindings, document, &pins)?;
    let mut plan = GameplayVisualPlan::default();
    if !bindings.is_empty() {
        let server = app
            .world()
            .get_resource::<AssetServer>()
            .ok_or("AssetPlugin is required for governed gameplay models")?;
        for binding in bindings {
            let mut matching = models.iter().filter(|model| model.pin == binding.asset);
            let model = matching.next().ok_or("Governed gameplay model is absent")?;
            if matching.next().is_some() {
                return Err("Duplicate governed gameplay model pin".into());
            }
            let path = &model.asset_path;
            if !path.is_ascii()
                || path.contains(['\\', ':'])
                || path.chars().any(char::is_control)
                || path
                    .split('/')
                    .any(|p| p.is_empty() || p == "." || p == "..")
                || !std::path::Path::new(path)
                    .extension()
                    .is_some_and(|extension| extension.eq_ignore_ascii_case("glb"))
            {
                return Err("Governed gameplay model requires a relative GLB path".into());
            }
            let local = binding.local_transform;
            plan.entries.insert(
                binding.entity_id,
                ProjectedVisual {
                    pin: binding.asset,
                    scene: server.load(GltfAssetLabel::Scene(0).from_asset(path.clone())),
                    transform: Transform {
                        translation: Vec3::new(
                            local.translation_m.x,
                            local.translation_m.y,
                            local.translation_m.z,
                        ),
                        rotation: Quat::from_xyzw(
                            local.rotation.x,
                            local.rotation.y,
                            local.rotation.z,
                            local.rotation.w,
                        ),
                        scale: Vec3::new(local.scale.x, local.scale.y, local.scale.z),
                    },
                },
            );
        }
    }
    app.insert_resource(GameplayVisualStatus {
        expected: plan.entries.len(),
        ..default()
    })
    .insert_resource(plan);
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
pub(super) fn attach(
    mut commands: Commands,
    plan: Res<GameplayVisualPlan>,
    mut status: ResMut<GameplayVisualStatus>,
    bodies: Query<(Entity, &DomainEntityId), With<GameplayBody>>,
) {
    status.expected = plan.entries.len();
    let mut targets = BTreeMap::new();
    for (entity, id) in &bodies {
        if plan.entries.contains_key(&id.0) && targets.insert(id.0, entity).is_some() {
            status.error = Some(format!("Duplicate gameplay body for visual: {}", id.0));
            return;
        }
    }
    if targets.len() != plan.entries.len() {
        status.error = Some("A governed visual has no projected gameplay body".into());
        return;
    }
    for (id, visual) in &plan.entries {
        let body = targets[id];
        // Keep identity, material, collider and physical pose on the authoritative body.
        commands.entity(body).remove::<Mesh3d>();
        commands.spawn((
            Name::new(format!("Governed gameplay visual · {id}")),
            WorldAssetRoot(visual.scene.clone()),
            visual.transform,
            ChildOf(body),
            GovernedGameplayVisual {
                target: *id,
                pin: visual.pin,
            },
        ));
        status.spawned += 1;
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
pub(super) fn observe(
    mut status: ResMut<GameplayVisualStatus>,
    server: Option<Res<AssetServer>>,
    spawner: Option<Res<WorldInstanceSpawner>>,
    visuals: Query<(&WorldAssetRoot, Option<&WorldInstance>), With<GovernedGameplayVisual>>,
    meshes: Query<(), With<Mesh3d>>,
) {
    if status.expected == 0 || status.error.is_some() {
        return;
    }
    let (Some(server), Some(spawner)) = (server, spawner) else {
        status.error = Some("Governed gameplay scene services are absent".into());
        return;
    };
    let mut ready = 0;
    for (root, instance) in &visuals {
        if let Some(LoadState::Failed(error)) = server.get_load_state(root.0.id()) {
            status.error = Some(format!("Governed gameplay model load failed: {error}"));
            return;
        }
        let Some(instance) = instance else {
            continue;
        };
        if spawner.instance_is_ready(**instance) && server.is_loaded_with_dependencies(root.0.id())
        {
            if !spawner
                .iter_instance_entities(**instance)
                .any(|entity| meshes.contains(entity))
            {
                status.error = Some("Governed gameplay model contains no rendered mesh".into());
                return;
            }
            ready += 1;
        }
    }
    status.ready = ready;
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::Collider;

    fn fixture(bound: bool) -> (App, Entity, EntityId, VisualAssetPin) {
        let id = EntityId::deterministic("governed-body");
        let pin = VisualAssetPin {
            asset_id: forge_core::AssetId::deterministic("governed-model"),
            sha256: forge_core::bytes_sha256(b"model"),
        };
        let mut app = App::new();
        app.add_plugins(MinimalPlugins);
        let body = app
            .world_mut()
            .spawn((
                DomainEntityId(id),
                GameplayBody::default(),
                Mesh3d(Handle::default()),
                Collider::cuboid(0.4, 0.5, 0.6),
                Transform::from_xyz(2.0, 3.0, 4.0),
            ))
            .id();
        let mut plan = GameplayVisualPlan::default();
        if bound {
            plan.entries.insert(
                id,
                ProjectedVisual {
                    pin,
                    scene: Handle::default(),
                    transform: Transform::from_xyz(0.0, -0.5, 0.0),
                },
            );
        }
        app.insert_resource(plan)
            .init_resource::<GameplayVisualStatus>()
            .add_systems(Startup, attach);
        (app, body, id, pin)
    }

    #[test]
    fn governed_child_replaces_only_marker_mesh_preserving_gameplay_body_and_transform() {
        let (mut app, body, id, pin) = fixture(true);
        app.update();
        let entity = app.world().entity(body);
        assert_eq!(entity.get::<DomainEntityId>().unwrap().0, id);
        assert!(entity.get::<GameplayBody>().is_some());
        assert!(entity.get::<Collider>().is_some());
        assert_eq!(
            entity.get::<Transform>().unwrap().translation,
            Vec3::new(2.0, 3.0, 4.0)
        );
        assert!(entity.get::<Mesh3d>().is_none());
        let mut query = app.world_mut().query::<(
            &GovernedGameplayVisual,
            &ChildOf,
            &Transform,
            &WorldAssetRoot,
        )>();
        let (visual, parent, transform, _) = query.single(app.world()).unwrap();
        assert_eq!(visual.target, id);
        assert_eq!(visual.pin, pin);
        assert_eq!(parent.parent(), body);
        assert_eq!(transform.translation, Vec3::new(0.0, -0.5, 0.0));
    }

    #[test]
    fn unbound_gameplay_body_keeps_its_existing_marker() {
        let (mut app, body, _, _) = fixture(false);
        app.update();
        assert!(app.world().entity(body).get::<Mesh3d>().is_some());
    }

    #[test]
    fn missing_gameplay_projection_is_an_error_instead_of_an_unbound_model() {
        let (mut app, body, _, _) = fixture(true);
        app.world_mut().despawn(body);
        app.update();
        assert!(
            app.world()
                .resource::<GameplayVisualStatus>()
                .error
                .is_some()
        );
    }
}
