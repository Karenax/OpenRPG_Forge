//! Optional bounded native observations. These are physics receipts, never GPU qualification.
use crate::gameplay::GameplayRuntime;
use bevy::prelude::*;
use forge_core::ContentHash;
use std::{fs::File, io::Write, path::Path, time::Instant};

const MAX_OBSERVATION_BYTES: usize = 64 * 1024 * 1024;
#[derive(Resource)]
pub(super) struct ObservationSink {
    file: File,
    source: ContentHash,
    archive: ContentHash,
    functional: ContentHash,
    started: Instant,
    last_tick: u64,
    last_sequence: u64,
    last_ready: Option<bool>,
    bytes: usize,
    error: Option<String>,
}
impl ObservationSink {
    pub(super) fn matches_archive(&self, expected: ContentHash) -> bool {
        self.archive == expected
    }
    pub(super) fn new(
        path: &Path,
        source: ContentHash,
        archive: ContentHash,
        functional: ContentHash,
    ) -> Result<Self, String> {
        let file = std::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(path)
            .map_err(|e| e.to_string())?;
        Ok(Self {
            file,
            source,
            archive,
            functional,
            started: Instant::now(),
            last_tick: u64::MAX,
            last_sequence: u64::MAX,
            last_ready: None,
            bytes: 0,
            error: None,
        })
    }
    fn write(&mut self, value: &serde_json::Value) -> Result<(), String> {
        let mut bytes = serde_json::to_vec(value).map_err(|e| e.to_string())?;
        bytes.push(b'\n');
        if bytes.len() > MAX_OBSERVATION_BYTES.saturating_sub(self.bytes) {
            return Err("Native gameplay observation quota reached; evidence is incomplete".into());
        }
        self.file
            .write_all(&bytes)
            .and_then(|()| self.file.flush())
            .map_err(|e| e.to_string())?;
        self.bytes += bytes.len();
        Ok(())
    }
    pub(super) fn scenario(
        &mut self,
        plan_hash: ContentHash,
        step: usize,
        status: &str,
        detail: &serde_json::Value,
    ) -> Result<(), String> {
        self.write(&serde_json::json!({"schema":"forge.native-gameplay.observation","version":1,"kind":"scenario",
            "qualification":"observations_only","pid":std::process::id(),"source_hash":self.source,"archive_hash":self.archive,
            "functional_hash":self.functional,"plan_hash":plan_hash,"step":step,"status":status,"detail":detail,
            "elapsed_ms":self.started.elapsed().as_secs_f64()*1000.0}))
    }
    pub(super) fn boot(
        &mut self,
        runtime: &GameplayRuntime,
        resumed: bool,
        saved: Option<&serde_json::Value>,
        recovery: Option<&str>,
    ) -> Result<(), String> {
        self.write(&serde_json::json!({"schema":"forge.native-gameplay.observation","version":1,"kind":"boot",
            "qualification":"observations_only","pid":std::process::id(),"source_hash":self.source,
            "archive_hash":self.archive,"functional_hash":self.functional,"resumed":resumed,
            "saved":saved,"state":runtime.session().state(),"resume_refused":recovery}))
    }
    pub(super) fn command(
        &mut self,
        command: &super::NativeGameplayCommand,
        result: &Result<(), String>,
        saved: &Result<Option<serde_json::Value>, String>,
        runtime: &GameplayRuntime,
    ) {
        if self.error.is_some() {
            return;
        }
        let command = match command {
            super::NativeGameplayCommand::Action(action) => {
                serde_json::json!({"kind":"action","action":action})
            }
            super::NativeGameplayCommand::Save => serde_json::json!({"kind":"save"}),
            super::NativeGameplayCommand::ResumeNextLaunch {
                allow_compatible_edits,
            } => {
                serde_json::json!({"kind":"resume_next_launch","allow_compatible_edits":allow_compatible_edits})
            }
            super::NativeGameplayCommand::StartFresh => serde_json::json!({"kind":"start_fresh"}),
            super::NativeGameplayCommand::Quit => serde_json::json!({"kind":"quit"}),
        };
        let value = serde_json::json!({"schema":"forge.native-gameplay.observation","version":1,"kind":"command",
            "qualification":"observations_only","pid":std::process::id(),"source_hash":self.source,"archive_hash":self.archive,
            "functional_hash":self.functional,"elapsed_ms":self.started.elapsed().as_secs_f64()*1000.0,
            "tick":runtime.session().state().tick,"sequence":runtime.session().state().sequence,
            "command":command,"accepted":result.is_ok(),"error":result.as_ref().err(),"saved":saved.as_ref().ok().and_then(Option::as_ref),
            "storage_observation_error":saved.as_ref().err()});
        if let Err(error) = self.write(&value) {
            self.error = Some(error);
        }
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn record(
    runtime: Res<GameplayRuntime>,
    mut sink: ResMut<ObservationSink>,
    navigation: Res<crate::navigation::NavigationRuntime>,
    player: Res<crate::PlayerRuntimeSnapshot>,
    motion: Query<(&crate::DomainEntityId, &crate::gameplay::DangerMotionState)>,
) {
    let state = runtime.session().state();
    if sink.error.is_some() || sink.last_tick == state.tick {
        return;
    }
    sink.last_tick = state.tick;
    // Keep every event/contact, living exposure and action transition. Persistent
    // exposures on defeated actors use the idle cadence, retaining the actual
    // complete frame without filling the quota with unchanged dead-only heat.
    let causal = state
        .last_frame
        .as_ref()
        .is_some_and(|frame| frame_is_causal(frame, state));
    if !causal
        && sink.last_sequence == state.sequence
        && sink.last_ready == Some(runtime.is_ready())
        && !state.tick.is_multiple_of(10)
    {
        return;
    }
    sink.last_sequence = state.sequence;
    sink.last_ready = Some(runtime.is_ready());
    let motions = motion
        .iter()
        .map(|(id, motion)| {
            serde_json::json!({ "actor":id.0,
        "grounded":motion.grounded,"effective_translation":motion.effective_translation.to_array(),
        "requested_translation":motion.requested_translation.map(|p|p.to_array()),
        "collisions":motion.collision_count,"feedback_current":motion.feedback_current,
        "escape_target":motion.escape_target.map(|p|p.to_array()) })
        })
        .collect::<Vec<_>>();
    let value = serde_json::json!({
        "schema":"forge.native-gameplay.observation","version":1,"kind":"frame","qualification":"observations_only",
        "pid":std::process::id(),"elapsed_ms":sink.started.elapsed().as_secs_f64()*1000.0,
        "source_hash":sink.source,"archive_hash":sink.archive,"functional_hash":sink.functional,
        "tick":state.tick,"sequence":state.sequence,"ready":runtime.is_ready(),"error":runtime.error(),
        "feedback":runtime.feedback(),"pending_actions":runtime.has_queued_actions(),
        "actors":state.actors,"objects":state.objects,"quests":state.quests,
        "last_action_receipt":state.receipts.last(),"last_frame":state.last_frame,
        "last_events":runtime.last_events(),"navigation":navigation.actors(),"npc_motion":motions,
        "player":{"position":player.player_position.to_array(),"grounded":player.grounded,"last_applied_sequence":player.last_applied_sequence,
            "desired_translation":player.desired_translation.to_array(),"effective_translation":player.effective_translation.to_array(),
            "backend_identity_stable":player.backend_identity_stable}
    });
    if let Err(error) = sink.write(&value) {
        tracing::error!(%error, "native gameplay evidence stopped");
        sink.error = Some(error);
    }
}

fn frame_is_causal(
    frame: &forge_gameplay::PhysicsFrameReceipt,
    state: &forge_gameplay::GameState,
) -> bool {
    let important = |actor| {
        state
            .actors
            .get(&actor)
            .is_none_or(|actor| actor.vitality > 0)
    };
    !frame.events.is_empty()
        || !frame.frame.contacts.is_empty()
        || frame
            .frame
            .heat
            .iter()
            .any(|exposure| important(exposure.actor))
        || frame
            .frame
            .danger
            .iter()
            .any(|exposure| important(exposure.actor))
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn exit(
    mut exits: MessageReader<bevy::app::AppExit>,
    runtime: Res<GameplayRuntime>,
    mut sink: ResMut<ObservationSink>,
) {
    for exit in exits.read() {
        let value = serde_json::json!({"schema":"forge.native-gameplay.observation","version":1,"kind":"exit",
            "qualification":"observations_only","pid":std::process::id(),"source_hash":sink.source,"archive_hash":sink.archive,
            "functional_hash":sink.functional,"elapsed_ms":sink.started.elapsed().as_secs_f64()*1000.0,
            "tick":runtime.session().state().tick,"sequence":runtime.session().state().sequence,
            "exit_success":exit.is_success(),"observation_error":sink.error,"simulation_error":runtime.error()});
        // Even a quota failure has a small terminal marker. The reader must refuse it.
        let terminal = serde_json::to_vec(&value).map_err(|e| e.to_string());
        if let Ok(mut bytes) = terminal {
            bytes.push(b'\n');
            let _ = sink.file.write_all(&bytes).and_then(|()| sink.file.flush());
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::ecs::system::RunSystemOnce;
    use forge_core::{CommandId, EntityId};
    use forge_gameplay::{
        ActionObservation, ActorRole, ContactObservation, DangerObservation, GameAction,
        HeatExposure, PhysicsFrame, PhysicsFrameReceipt,
    };

    fn exposure_frame(
        source: ContentHash,
        tick: u64,
        object: EntityId,
        actor: EntityId,
    ) -> PhysicsFrame {
        PhysicsFrame {
            source_hash: source,
            tick,
            actors: vec![],
            objects: vec![],
            contacts: vec![],
            heat: vec![HeatExposure {
                object,
                actor,
                line_of_sight: true,
            }],
            danger: vec![DangerObservation {
                object,
                actor,
                line_of_sight: true,
            }],
        }
    }

    fn sampling_app(
        directory: &super::super::tests::Directory,
    ) -> (App, EntityId, EntityId, ContentHash) {
        let (_, mut document) = super::super::tests::fixture();
        let target = EntityId::deterministic("native/observation-target");
        let mut actor = document.actors[0].clone();
        actor.id = target;
        actor.role = ActorRole::Enemy;
        actor.max_vitality = 2;
        actor.flees_danger = true;
        document.actors.push(actor);
        let object = document.objects[0].id;
        document.objects[0].physics.flammable = true;
        document.objects[0].physics.heat_damage_per_tick = 1;
        document.objects[0].physics.mass_kg = 1.0;
        let source = ContentHash::from_bytes([6; 32]);
        let functional = document.functional_hash().unwrap();
        let mut runtime = GameplayRuntime::new(document, source, None, false).unwrap();
        runtime.set_enabled(true);
        let request =
            super::super::request_for_action(&runtime, GameAction::Ignite { object }).unwrap();
        runtime
            .session
            .execute(
                &request,
                ActionObservation {
                    line_of_sight: true,
                    ..default()
                },
            )
            .unwrap();
        let mut app = App::new();
        app.insert_resource(runtime)
            .insert_resource(
                ObservationSink::new(
                    &directory.root.join("observations.jsonl"),
                    source,
                    source,
                    functional,
                )
                .unwrap(),
            )
            .init_resource::<crate::navigation::NavigationRuntime>()
            .init_resource::<crate::PlayerRuntimeSnapshot>();
        (app, target, object, source)
    }

    fn sampling_tick(
        app: &mut App,
        source: ContentHash,
        tick: u64,
        contact: &ContactObservation,
    ) -> serde_json::Value {
        let mut runtime = app.world_mut().resource_mut::<GameplayRuntime>();
        let player = runtime.session.document().player;
        let object = contact.source;
        let mut frame = exposure_frame(source, tick, object, contact.target);
        if matches!(tick, 5 | 6) {
            frame.contacts.push(contact.clone());
        }
        if tick == 8 {
            frame.heat = vec![HeatExposure {
                object,
                actor: player,
                line_of_sight: false,
            }];
        }
        if tick == 9 {
            frame.danger = vec![DangerObservation {
                object,
                actor: player,
                line_of_sight: false,
            }];
        }
        runtime.session.advance_frame(&frame).unwrap();
        if tick == 7 {
            let request = super::super::request_for_action(
                &runtime,
                GameAction::SetDoor { object, open: true },
            )
            .unwrap();
            runtime
                .session
                .execute(
                    &request,
                    ActionObservation {
                        line_of_sight: true,
                        ..default()
                    },
                )
                .unwrap();
        }
        serde_json::to_value(runtime.session.state()).unwrap()
    }

    #[test]
    fn native_observations_sample_only_dead_exposures_and_keep_full_causal_frames() {
        let directory = super::super::tests::Directory::new();
        let (mut app, target, object, source) = sampling_app(&directory);
        let mut expected = std::collections::BTreeMap::new();
        let contact = ContactObservation {
            command_id: CommandId::deterministic("native/observation-contact"),
            source: object,
            target,
            initiator: None,
            normal_speed_m_s: 0.0,
        };
        for tick in 1..=12 {
            expected.insert(tick, sampling_tick(&mut app, source, tick, &contact));
            app.world_mut().run_system_once(record).unwrap();
        }
        drop(app);
        let rows: Vec<serde_json::Value> =
            std::fs::read_to_string(directory.root.join("observations.jsonl"))
                .unwrap()
                .lines()
                .map(|line| serde_json::from_str(line).unwrap())
                .collect();
        let ticks: Vec<u64> = rows
            .iter()
            .map(|row| row["tick"].as_u64().unwrap())
            .collect();
        assert_eq!(ticks, vec![1, 2, 5, 6, 7, 8, 9, 10]);
        assert_eq!(expected[&2]["actors"][target.to_string()]["vitality"], 0);
        assert!(
            !expected[&2]["last_frame"]["events"]
                .as_array()
                .unwrap()
                .is_empty()
        );
        assert!(
            expected[&6]["last_frame"]["events"]
                .as_array()
                .unwrap()
                .is_empty()
        );
        for row in rows {
            let state = &expected[&row["tick"].as_u64().unwrap()];
            for field in ["actors", "objects", "quests", "last_frame"] {
                assert_eq!(row[field], state[field]);
            }
            assert_eq!(
                &row["last_action_receipt"],
                state["receipts"].as_array().unwrap().last().unwrap()
            );
        }
    }

    #[test]
    fn native_observations_preserve_unknown_exposure_references() {
        let (_, document) = super::super::tests::fixture();
        let source = ContentHash::from_bytes([7; 32]);
        let session = forge_gameplay::GameSession::new(document.clone(), source).unwrap();
        let missing = EntityId::deterministic("native/missing-observation-actor");
        let mut receipt = PhysicsFrameReceipt {
            frame: exposure_frame(source, 1, document.objects[0].id, missing),
            events: vec![],
        };
        receipt.frame.danger.clear();
        assert!(frame_is_causal(&receipt, session.state()));
        receipt.frame.heat.clear();
        receipt.frame.danger.push(DangerObservation {
            object: document.objects[0].id,
            actor: missing,
            line_of_sight: false,
        });
        assert!(frame_is_causal(&receipt, session.state()));
    }
}
