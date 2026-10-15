//! Native presentation and persistence for the existing authored gameplay adapter.
//!
//! The window supplies commands only. Rapier, `GameplayPlugin` and `GameSession`
//! remain the sole authorities for movement, observations and consequences.
use crate::gameplay::{GameplayPlugin, GameplayRuntime, GameplaySet};
use bevy::prelude::*;
use forge_core::{CommandId, ContentHash};
use forge_gameplay::{GameAction, GameRequest, GameplayDocument};
use forge_project::{
    authoring::playtest::PlaytestSnapshot, zone_authoring::session::ZoneEditSession,
};
use std::path::Path;

mod observations;
mod presentation;
mod qualification;
mod report;
mod storage;
#[cfg(test)]
mod tests;

/// Inputs to normal native commands, also usable by bounded qualification harnesses.
#[derive(Message, Clone, Debug, PartialEq)]
pub enum NativeGameplayCommand {
    Action(GameAction),
    Save,
    /// The next process restores this exact saved revision; the live player is not teleported.
    ResumeNextLaunch {
        allow_compatible_edits: bool,
    },
    /// Explicit recovery after a refused resume; the saved party remains intact.
    StartFresh,
    Quit,
}

#[derive(Clone, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum NativeGameplayUiSet {
    Input,
}

#[derive(Resource, Default)]
struct CommandResult {
    sequence: u64,
    last: Option<(NativeGameplayCommand, Result<(), String>)>,
    last_error: Option<String>,
    pending_action: Option<GameRequest>,
    completed_action: Option<(GameRequest, Result<(), String>)>,
}

#[derive(Resource)]
pub struct NativeGameplayUi {
    open: bool,
    feedback: String,
    last_view: Option<presentation::GameView>,
    recovery: Option<String>,
}
impl NativeGameplayUi {
    #[must_use]
    pub const fn is_open(&self) -> bool {
        self.open
    }
    pub fn close(&mut self) {
        if self.recovery.is_none() {
            self.open = false;
        }
    }
}

/// A validated immutable launch, prepared before player startup.
pub struct PreparedNativeGameplay {
    runtime: GameplayRuntime,
    storage: storage::GameStorage,
    observations: Option<observations::ObservationSink>,
    resumed: bool,
    recovery: Option<String>,
    report: Option<report::ReportSink>,
}
impl PreparedNativeGameplay {
    /// A zone without RPG definitions stays a walk. No document or authored file is changed.
    pub fn from_archive(
        archive: &[u8],
        save_root: &Path,
        observation_path: Option<&Path>,
    ) -> Result<Option<Self>, String> {
        let zone = ZoneEditSession::from_archive_bytes(archive)?;
        let source = zone.head().content_hash()?;
        let snapshot = PlaytestSnapshot::from_zone(zone.head(), source)?;
        let Some(document) = snapshot.gameplay().cloned() else {
            return Ok(None);
        };
        Self::from_document(
            document,
            source,
            forge_core::bytes_sha256(archive),
            save_root,
            observation_path,
        )
        .map(Some)
    }

    fn from_document(
        document: GameplayDocument,
        source: ContentHash,
        archive_hash: ContentHash,
        save_root: &Path,
        observation_path: Option<&Path>,
    ) -> Result<Self, String> {
        document.validate().map_err(|e| e.to_string())?;
        let functional = document.functional_hash().map_err(|e| e.to_string())?;
        let mut storage = storage::GameStorage::open(save_root, &document)?;
        let (save, compatible) = storage.requested_resume(&document)?;
        let (runtime, resumed, recovery) =
            match GameplayRuntime::new(document.clone(), source, save.as_deref(), compatible) {
                Ok(runtime) => (runtime, save.is_some(), None),
                Err(error) if save.is_some() => (
                    GameplayRuntime::new(document, source, None, false)?,
                    false,
                    Some(error),
                ),
                Err(error) => return Err(error),
            };
        let mut observations = observation_path
            .map(|path| observations::ObservationSink::new(path, source, archive_hash, functional))
            .transpose()?;
        let report = std::env::var_os("FORGE_GAME_REPORT_PATH")
            .map(|path| report::ReportSink::new(Path::new(&path), source, archive_hash, functional))
            .transpose()?;
        if let Some(sink) = observations.as_mut() {
            sink.boot(
                &runtime,
                resumed,
                storage.metadata()?.as_ref(),
                recovery.as_deref(),
            )?;
        }
        Ok(Self {
            runtime,
            storage,
            observations,
            resumed,
            recovery,
            report,
        })
    }

    /// Call after configuring the existing `PlayerControllerPlugin`, before `App::run`.
    pub fn install(self, app: &mut App) -> Result<(), String> {
        if self.resumed {
            let position = self.runtime.session().state().actors
                [&self.runtime.session().document().player]
                .position;
            crate::PlayerSpawnPoint::rebase_before_start(
                app.world_mut(),
                Vec3::new(position.x, position.y, position.z),
            )?;
        }
        let feedback = if self.resumed {
            "Sauvegarde reprise. G : actions et dialogue · F5 : sauvegarder."
        } else {
            "Nouvelle partie. G : actions et dialogue · F5 : sauvegarder · F9 : demander une reprise."
        };
        app.insert_resource(self.runtime)
            .insert_resource(self.storage)
            .insert_resource(NativeGameplayUi {
                open: self.recovery.is_some(),
                feedback: self.recovery.as_ref().map_or_else(|| feedback.into(), |error| format!("Reprise refusée : {error}. Choisissez une reprise compatible, une nouvelle partie ou Quitter. La simulation reste suspendue.")),
                last_view: None,
                recovery: self.recovery,
            })
            .add_message::<NativeGameplayCommand>()
            .init_resource::<CommandResult>()
            .add_plugins(GameplayPlugin)
            .add_systems(PostStartup, presentation::spawn)
            .add_systems(
                Update,
                presentation::input.in_set(NativeGameplayUiSet::Input),
            )
            .add_systems(
                Update,
                (presentation::scroll, presentation::refresh)
                    .chain()
                    .after(NativeGameplayUiSet::Input),
            )
            .add_systems(
                FixedUpdate,
                readiness
                    .before(crate::PlayerControllerSet::Drive)
                    .before(GameplaySet::Drive),
            )
            .add_systems(
                FixedUpdate,
                process_commands.after(crate::navigation::NavigationSet::Refresh),
            );
        if let Some(observations) = self.observations {
            app.insert_resource(observations)
                .add_systems(FixedUpdate, observations::record.after(process_commands));
        }
        install_exit_observers(app, self.report);
        qualification::install(app)?;
        Ok(())
    }
}

#[derive(bevy::ecs::schedule::ScheduleLabel, Clone, Debug, PartialEq, Eq, Hash)]
struct NativeGameplayExitReports;

fn install_exit_observers(app: &mut App, report: Option<report::ReportSink>) {
    let observations = app
        .world()
        .contains_resource::<observations::ObservationSink>();
    if !observations && report.is_none() {
        return;
    }
    // Some producers, including WindowPlugin, emit AppExit during Last. Finish
    // every Last producer before recording the exit, without another app update.
    app.init_schedule(NativeGameplayExitReports);
    app.world_mut()
        .resource_mut::<bevy::app::MainScheduleOrder>()
        .insert_after(Last, NativeGameplayExitReports);
    if observations {
        app.add_systems(NativeGameplayExitReports, observations::exit);
    }
    if let Some(report) = report {
        app.insert_resource(report)
            .add_systems(NativeGameplayExitReports, report::exit);
    }
}

/// Player/terrain readiness is supplied by the same walk adapter, never by the HUD.
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn readiness(
    mut ready: ResMut<crate::PlayerSimulationReady>,
    projection: Option<Res<crate::gameplay::GameplayProjectionStatus>>,
    mut runtime: ResMut<GameplayRuntime>,
    mut storage: ResMut<storage::GameStorage>,
    ui: Res<NativeGameplayUi>,
    mut input: ResMut<crate::PendingPlayerControlCommand>,
    mut focus: ResMut<crate::PlayerInputFocus>,
) {
    let previous_enabled = runtime.enabled;
    let projection_ready = projection.is_some_and(|projection| projection.ready);
    if ui.recovery.is_some() {
        ready.0 = false;
        focus.0 = false;
        input.cancel_input();
        runtime.set_enabled(false);
    } else {
        let enabled = ready.0 && projection_ready && runtime.error().is_none();
        runtime.set_enabled(enabled);
        if enabled && let Err(error) = storage.consume_resume_request() {
            runtime.error = Some(error);
            runtime.set_enabled(false);
        }
    }
    if previous_enabled != runtime.enabled {
        tracing::debug!(target: "forge::gameplay", tick = runtime.session().state().tick,
            player_ready = ready.0, projection_ready, enabled = runtime.enabled,
            "native gameplay readiness changed");
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn process_commands(
    mut messages: MessageReader<NativeGameplayCommand>,
    mut runtime: ResMut<GameplayRuntime>,
    mut storage: ResMut<storage::GameStorage>,
    mut ui: ResMut<NativeGameplayUi>,
    mut observations: Option<ResMut<observations::ObservationSink>>,
    mut last_result: ResMut<CommandResult>,
    mut exits: MessageWriter<bevy::app::AppExit>,
) {
    observe_action_completion(&mut last_result, &runtime);
    for command in messages.read() {
        let request = match command {
            NativeGameplayCommand::Action(action) => {
                request_for_action(&runtime, action.clone()).ok()
            }
            _ => None,
        };
        let result = match command {
            NativeGameplayCommand::Action(action) => queue_action(&mut runtime, action.clone()),
            NativeGameplayCommand::Save => {
                runtime.save_bytes().and_then(|bytes| storage.save(&bytes))
            }
            NativeGameplayCommand::ResumeNextLaunch {
                allow_compatible_edits,
            } => storage.request_resume(
                runtime.session().document(),
                runtime.session().source_hash(),
                *allow_compatible_edits,
            ),
            NativeGameplayCommand::StartFresh => {
                if ui.recovery.is_some() {
                    storage
                        .cancel_resume(runtime.session().document())
                        .map(|()| {
                            ui.recovery = None;
                            ui.open = false;
                        })
                } else {
                    Err("La partie est déjà active. Une nouvelle partie se lance dans une nouvelle fenêtre.".into())
                }
            }
            NativeGameplayCommand::Quit => {
                exits.write(bevy::app::AppExit::Success);
                Ok(())
            }
        };
        if let Some(sink) = observations.as_deref_mut() {
            sink.command(command, &result, &storage.metadata(), &runtime);
        }
        last_result.sequence = last_result.sequence.saturating_add(1);
        if let Err(error) = &result {
            last_result.last_error = Some(error.clone());
        } else if let Some(request) = request {
            // A refused retry can have the same deterministic ID. Its previous
            // completion must not acknowledge or reject the newly queued action.
            last_result.completed_action = None;
            last_result.pending_action = Some(request);
        }
        last_result.last = Some((command.clone(), result.clone()));
        ui.feedback = match result {
            Ok(()) => match command {
                NativeGameplayCommand::Action(_) => "Action transmise à la simulation.".into(),
                NativeGameplayCommand::Save => {
                    "Partie sauvegardée. F9 demande la reprise au prochain lancement.".into()
                }
                NativeGameplayCommand::ResumeNextLaunch { .. } => {
                    "Reprise demandée. Fermez puis relancez le jeu pour charger cette sauvegarde."
                        .into()
                }
                NativeGameplayCommand::StartFresh => {
                    "Nouvelle partie choisie. La sauvegarde précédente est conservée.".into()
                }
                NativeGameplayCommand::Quit => "Fermeture du jeu.".into(),
            },
            Err(error) => error,
        };
    }
}
fn observe_action_completion(result: &mut CommandResult, runtime: &GameplayRuntime) {
    if runtime.has_queued_actions() {
        return;
    }
    let Some(request) = result.pending_action.take() else {
        return;
    };
    let completion = if runtime
        .session()
        .state()
        .receipts
        .iter()
        .any(|receipt| receipt.request == request)
    {
        Ok(())
    } else {
        Err(runtime.feedback().to_owned())
    };
    if let Err(error) = &completion {
        result.last_error = Some(error.clone());
    }
    result.completed_action = Some((request, completion));
}

fn queue_action(runtime: &mut GameplayRuntime, action: GameAction) -> Result<(), String> {
    if runtime.has_queued_actions() {
        return Err("Attendez la fin de l’action précédente.".into());
    }
    let request = request_for_action(runtime, action)?;
    runtime.queue(request)
}
fn request_for_action(
    runtime: &GameplayRuntime,
    action: GameAction,
) -> Result<GameRequest, String> {
    let session = runtime.session();
    let sequence = session.state().sequence;
    let actor = session.document().player;
    let key = serde_json::to_vec(&("native-gameplay", session.source_hash(), sequence, &action))
        .map_err(|e| e.to_string())?;
    Ok(GameRequest {
        command_id: CommandId::deterministic(key),
        expected_sequence: sequence,
        actor,
        action,
    })
}
