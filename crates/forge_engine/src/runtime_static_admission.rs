//! Shared player admission while module statics and gameplay bodies become ready.
use bevy::prelude::*;
use forge_engine::{
    PendingPlayerControlCommand, PlayerControllerSet, PlayerSimulationReady,
    cell_streaming::{PlayerCellStreamingSet, PlayerCellStreamingStatus},
    native_gameplay::NativeGameplayReadinessSet,
};
use forge_project::zone_authoring::placements::ManualPlacement;

#[derive(Resource)]
struct StaticAdmission {
    placements: Vec<ManualPlacement>,
    species_required: bool,
}

/// The host publishes a fresh species observation on every fixed step.
/// Absence is distinct from `false` and cannot satisfy a required observation.
#[derive(Resource, Default)]
pub struct SpeciesReadiness(Option<bool>);

impl SpeciesReadiness {
    pub fn publish(&mut self, ready: bool) {
        self.0 = Some(ready);
    }

    #[must_use]
    pub const fn observed(&self) -> Option<bool> {
        self.0
    }
}

#[derive(Clone, Copy, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum StaticAdmissionSet {
    Begin,
    PublishSpecies,
    Constrain,
}

#[derive(Resource, Default)]
struct InputBoundary {
    last_fixed_ready: bool,
    graphics_waiting: bool,
}

pub fn install_static_admission(app: &mut App, placements: Vec<ManualPlacement>) {
    install_static_admission_with_species(app, placements, false);
}

pub fn install_static_admission_with_species(
    app: &mut App,
    placements: Vec<ManualPlacement>,
    species_required: bool,
) {
    app.insert_resource(StaticAdmission {
        placements,
        species_required,
    })
    .init_resource::<SpeciesReadiness>()
    .init_resource::<InputBoundary>()
    .configure_sets(
        FixedUpdate,
        (
            StaticAdmissionSet::Begin,
            StaticAdmissionSet::PublishSpecies,
            StaticAdmissionSet::Constrain,
        )
            .chain(),
    )
    .add_systems(FixedUpdate, reset_species.in_set(StaticAdmissionSet::Begin))
    .add_systems(
        FixedUpdate,
        hold_until_static_ready
            .in_set(StaticAdmissionSet::Constrain)
            .after(PlayerCellStreamingSet::Request)
            .before(NativeGameplayReadinessSet)
            .before(PlayerControllerSet::Drive)
            .before(forge_engine::gameplay::GameplaySet::Drive),
    )
    .add_systems(
        FixedUpdate,
        input_before_drive
            .after(NativeGameplayReadinessSet)
            .after(StaticAdmissionSet::Constrain)
            .before(PlayerControllerSet::Drive)
            .before(forge_engine::gameplay::GameplaySet::Drive),
    )
    .add_systems(PostUpdate, discard_paused_frame_input);
}

fn reset_species(world: &mut World) {
    if let Some(mut species) = world.get_resource_mut::<SpeciesReadiness>() {
        species.0 = None;
    }
}

fn persistent_source_errors_absent(world: &World) -> bool {
    world
        .get_resource::<forge_engine::source_lighting::surfaces::live::Error>()
        .is_none_or(|error| error.0.is_none())
        && world
            .get_resource::<forge_engine::source_terrain::host::Status>()
            .is_none_or(|status| status.error.is_none())
        && world
            .get_resource::<forge_engine::source_water_host::Status>()
            .is_none_or(|status| status.error.is_none())
        && world
            .get_resource::<forge_engine::source_vegetation::scene::streamed::host::HostStatus>()
            .is_none_or(|status| status.error.is_none())
}

fn hold_until_static_ready(world: &mut World) {
    if !world.resource::<PlayerSimulationReady>().0 {
        return;
    }
    let (manual_ready, species_required) =
        world.resource_scope(|world, admission: Mut<StaticAdmission>| {
            (
                forge_engine::manual_placements_ready(world, &admission.placements),
                admission.species_required,
            )
        });
    let terrain_ready = world
        .get_resource::<PlayerCellStreamingStatus>()
        .is_some_and(|state| state.ready && state.error.is_none());
    let species_ready = !species_required
        || world
            .get_resource::<SpeciesReadiness>()
            .is_some_and(|state| state.0 == Some(true));
    let ready = terrain_ready
        && species_ready
        && persistent_source_errors_absent(world)
        && forge_engine::runtime_buildings::observe(world).is_some_and(|state| state.ready)
        && manual_ready
        && world
            .get_resource::<forge_engine::gameplay::GameplayProjectionStatus>()
            .is_none_or(|state| state.ready)
        && world
            .get_resource::<forge_engine::gameplay::GameplayRuntime>()
            .is_none_or(|runtime| runtime.error().is_none());
    if !ready {
        world.resource_mut::<PlayerSimulationReady>().0 = false;
    }
}

// Commands sampled during a hold must not be replayed on its first ready step.
// A continuously ready player keeps commands sampled normally in PreUpdate.
fn input_before_drive(world: &mut World) {
    let ready = world.resource::<PlayerSimulationReady>().0;
    let boundary = world.resource::<InputBoundary>();
    let admitted = ready && boundary.last_fixed_ready && !boundary.graphics_waiting;
    if !admitted && let Some(mut pending) = world.get_resource_mut::<PendingPlayerControlCommand>()
    {
        pending.cancel_input();
    }
    world.resource_mut::<InputBoundary>().last_fixed_ready = ready;
}

// This also runs on rendered frames with no fixed step. A building's PreUpdate
// release must not turn input collected during an earlier hold into a new action.
fn discard_paused_frame_input(world: &mut World) {
    let ready = world.resource::<PlayerSimulationReady>().0;
    let boundary = world.resource::<InputBoundary>();
    let admitted = ready && boundary.last_fixed_ready && !boundary.graphics_waiting;
    if !admitted && let Some(mut pending) = world.get_resource_mut::<PendingPlayerControlCommand>()
    {
        pending.cancel_input();
    }
    if !ready {
        world.resource_mut::<InputBoundary>().last_fixed_ready = false;
    }
}

/// Record a graphics wait even if no fixed simulation step occurs in the frame.
/// Only the streamed host installs the boundary; other hosts retain their own policy.
/// A transition back to ready discards the last inputs collected during the wait,
/// but leaves later inputs from an already ready player untouched.
pub fn observe_graphics_input_readiness(world: &mut World, ready: bool) {
    let discard = {
        let Some(mut boundary) = world.get_resource_mut::<InputBoundary>() else {
            return;
        };
        let discard = !ready || boundary.graphics_waiting;
        boundary.graphics_waiting = !ready;
        discard
    };
    if discard && let Some(mut pending) = world.get_resource_mut::<PendingPlayerControlCommand>() {
        pending.cancel_input();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_completed_cell_request_cannot_release_missing_statics_or_gameplay_bodies() {
        let terrain = forge_project::authoring::new_terrain("stream-host", "camp", "Camp").unwrap();
        let zone =
            forge_project::zone_authoring::session::ZoneEditSession::from_initial_terrain(terrain)
                .unwrap();
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .init_resource::<Assets<Mesh>>()
            .init_resource::<Assets<StandardMaterial>>()
            .insert_resource(PlayerSimulationReady(true))
            .insert_resource(PlayerCellStreamingStatus {
                ready: true,
                ..default()
            });
        forge_engine::runtime_buildings::install(&mut app, zone.head()).unwrap();
        install_static_admission(&mut app, vec![]);
        app.add_systems(
            FixedUpdate,
            (|mut ready: ResMut<PlayerSimulationReady>| ready.0 = true)
                .in_set(PlayerCellStreamingSet::Request),
        );
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(app.world().resource::<PlayerSimulationReady>().0);
        app.insert_resource(forge_engine::gameplay::GameplayProjectionStatus { ready: false });
        app.world_mut().run_schedule(FixedUpdate);
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
        app.world_mut()
            .resource_mut::<forge_engine::gameplay::GameplayProjectionStatus>()
            .ready = true;
        app.world_mut().run_schedule(FixedUpdate);
        assert!(app.world().resource::<PlayerSimulationReady>().0);

        // An authored placement without its pinned loaded scene/colliders must
        // hold the player even when the terrain and gameplay bodies are ready.
        app.world_mut()
            .resource_mut::<StaticAdmission>()
            .placements
            .push(ManualPlacement {
                local_lights: None,
                id: forge_core::EntityId::deterministic("missing-static"),
                name: "Cairn".into(),
                asset_id: forge_core::AssetId::deterministic("cairn"),
                asset_hash: forge_core::bytes_sha256(b"cairn"),
                transform: forge_core::Transform::IDENTITY,
                physics: None,
                particles: None,
                locked: false,
            });
        app.world_mut().run_schedule(FixedUpdate);
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
        // The host never promotes a pause set by another subsystem.
        app.world_mut()
            .resource_mut::<StaticAdmission>()
            .placements
            .clear();
        hold_until_static_ready(app.world_mut());
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
    }

    #[derive(Resource)]
    struct TestSources {
        terrain: bool,
        species: Option<bool>,
    }

    fn report_terrain(world: &mut World) {
        let ready = world.resource::<TestSources>().terrain;
        world.resource_mut::<PlayerCellStreamingStatus>().ready = ready;
        world.resource_mut::<PlayerSimulationReady>().0 = ready;
    }

    fn report_species(world: &mut World) {
        if let Some(ready) = world.resource::<TestSources>().species
            && let Some(mut publication) = world.get_resource_mut::<SpeciesReadiness>()
        {
            publication.publish(ready);
        }
    }

    #[test]
    fn both_publication_orders_require_fresh_terrain_species_and_buildings() {
        for terrain_first in [true, false] {
            let terrain =
                forge_project::authoring::new_terrain("stream-host", "camp", "Camp").unwrap();
            let zone =
                forge_project::zone_authoring::session::ZoneEditSession::from_initial_terrain(
                    terrain,
                )
                .unwrap();
            let mut app = App::new();
            app.add_plugins(MinimalPlugins)
                .init_resource::<Assets<Mesh>>()
                .init_resource::<Assets<StandardMaterial>>()
                .insert_resource(PlayerSimulationReady(false))
                .init_resource::<PlayerCellStreamingStatus>()
                .insert_resource(TestSources {
                    terrain: false,
                    species: Some(true),
                });
            forge_engine::runtime_buildings::install(&mut app, zone.head()).unwrap();
            install_static_admission_with_species(&mut app, vec![], true);
            app.add_systems(
                FixedUpdate,
                (
                    report_terrain.in_set(PlayerCellStreamingSet::Request),
                    report_species.in_set(StaticAdmissionSet::PublishSpecies),
                ),
            );
            if terrain_first {
                app.configure_sets(
                    FixedUpdate,
                    PlayerCellStreamingSet::Request.before(StaticAdmissionSet::PublishSpecies),
                );
            } else {
                app.configure_sets(
                    FixedUpdate,
                    PlayerCellStreamingSet::Request.after(StaticAdmissionSet::PublishSpecies),
                );
            }
            app.update();
            app.world_mut().run_schedule(FixedUpdate);
            assert!(!app.world().resource::<PlayerSimulationReady>().0);
            app.world_mut().resource_mut::<TestSources>().terrain = true;
            app.world_mut().resource_mut::<TestSources>().species = None;
            app.world_mut().run_schedule(FixedUpdate);
            assert!(!app.world().resource::<PlayerSimulationReady>().0);
            app.world_mut().resource_mut::<TestSources>().species = Some(true);
            app.world_mut().run_schedule(FixedUpdate);
            assert!(app.world().resource::<PlayerSimulationReady>().0);
            app.world_mut()
                .insert_resource(forge_engine::source_lighting::surfaces::live::Error(Some(
                    "fatal".into(),
                )));
            app.world_mut().run_schedule(FixedUpdate);
            assert!(!app.world().resource::<PlayerSimulationReady>().0);
            app.world_mut()
                .remove_resource::<forge_engine::source_lighting::surfaces::live::Error>();
            app.world_mut().run_schedule(FixedUpdate);
            assert!(app.world().resource::<PlayerSimulationReady>().0);
            // A formerly ready publication is never reused when its producer disappears.
            app.world_mut().resource_mut::<TestSources>().species = None;
            app.world_mut().run_schedule(FixedUpdate);
            assert!(!app.world().resource::<PlayerSimulationReady>().0);
            app.world_mut().remove_resource::<SpeciesReadiness>();
            app.world_mut().resource_mut::<TestSources>().species = Some(true);
            app.world_mut().run_schedule(FixedUpdate);
            assert!(!app.world().resource::<PlayerSimulationReady>().0);
        }
    }

    #[test]
    fn source_errors_stay_closed_and_ready_input_is_not_discarded() {
        let mut world = World::new();
        world.insert_resource(PlayerSimulationReady(true));
        world.insert_resource(InputBoundary {
            last_fixed_ready: true,
            graphics_waiting: false,
        });
        world.init_resource::<PendingPlayerControlCommand>();
        let sequence = world
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(forge_engine::PlayerControlCommand {
                move_axis: Vec2::X,
                jump: true,
                ..default()
            })
            .unwrap();
        input_before_drive(&mut world);
        assert_eq!(
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .drain_tick()
                .0,
            Some(sequence)
        );
        let sequence = world
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(forge_engine::PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            })
            .unwrap();
        // A rendered frame with no fixed step must retain a ready player's input.
        discard_paused_frame_input(&mut world);
        assert_eq!(
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .drain_tick()
                .0,
            Some(sequence)
        );
        world.insert_resource(forge_engine::source_lighting::surfaces::live::Error(Some(
            "surface failed".into(),
        )));
        assert!(!persistent_source_errors_absent(&world));
        world.remove_resource::<forge_engine::source_lighting::surfaces::live::Error>();
        world.insert_resource(forge_engine::source_terrain::host::Status {
            error: Some("terrain failed".into()),
            ..default()
        });
        assert!(!persistent_source_errors_absent(&world));
        world.remove_resource::<forge_engine::source_terrain::host::Status>();
        world.insert_resource(forge_engine::source_water_host::Status {
            error: Some("water failed".into()),
            ..default()
        });
        assert!(!persistent_source_errors_absent(&world));
        world.remove_resource::<forge_engine::source_water_host::Status>();
        world.insert_resource(
            forge_engine::source_vegetation::scene::streamed::host::HostStatus {
                error: Some("vegetation failed".into()),
                ..default()
            },
        );
        assert!(!persistent_source_errors_absent(&world));
    }
}
