//! Presentation bridge. The canvas owns physical observations, never the DOM.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::{CommandId, ContentHash};
use forge_engine::gameplay::GameplayRuntime;
use forge_engine::navigation::{
    ActorNavigationObservation, NavigationPolicy, NavigationRuntime, NavigationStatus,
};
use forge_gameplay::{GameAction, GameRequest};
use leptos::prelude::Update;
use leptos::prelude::*;
use std::{
    cell::{Cell, RefCell},
    collections::VecDeque,
    fmt::Write as _,
};

mod editor;
mod hud;
mod timing;
pub(super) use editor::GameplayEditor;
pub(super) use hud::GameplayControls;
pub(super) use timing::{TimingPublication, observe as observe_timing};

#[derive(Clone, Default, PartialEq)]
struct GameView {
    run: u64,
    ready: bool,
    pending: bool,
    source: Option<ContentHash>,
    player: Option<forge_core::EntityId>,
    sequence: u64,
    vitality: u32,
    stamina: u32,
    inventory: Vec<String>,
    quests: Vec<String>,
    actions: Vec<(String, GameAction)>,
    dialogue: String,
    feedback: String,
    navigation_summary: Vec<String>,
    navigation_diagnostic: String,
    timing_state: Option<forge_engine::playtest_timing::TimingState>,
    timing_json: std::sync::Arc<str>,
}

struct Resume {
    source: ContentHash,
    json: String,
    compatible: bool,
}

enum Pending {
    Action(u64, ContentHash, GameRequest),
    Save(u64, ContentHash),
}

type SaveCapture = Result<(u64, ContentHash, String), String>;
#[derive(Resource)]
pub(super) struct BridgeRun(u64);
impl BridgeRun {
    pub(super) const fn id(&self) -> u64 {
        self.0
    }
}
thread_local! {
    static GENERATION: Cell<u64> = const {Cell::new(0)};
    static RESUME: RefCell<Option<Resume>> = const {RefCell::new(None)};
    static VIEW: RefCell<Option<RwSignal<GameView>>> = const {RefCell::new(None)};
    static SAVE: RefCell<Option<RwSignal<Option<SaveCapture>>>> = const {RefCell::new(None)};
    static PENDING: RefCell<VecDeque<Pending>> = const {RefCell::new(VecDeque::new())};
}

fn generation() -> u64 {
    GENERATION.with(Cell::get)
}

pub(super) fn invalidate_run() {
    GENERATION.with(|run| {
        run.set(
            run.get()
                .checked_add(1)
                .expect("playtest generation exhausted"),
        );
    });
    let cancelling_save = PENDING.with(|queue| {
        queue
            .borrow()
            .iter()
            .any(|p| matches!(p, Pending::Save(..)))
    });
    PENDING.with(|queue| queue.borrow_mut().clear());
    timing::clear_request();
    VIEW.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            let _ = signal.try_set(GameView::default());
        }
    });
    SAVE.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            let _ = signal.try_set(cancelling_save.then(|| {
                Err("La partie a changé ; relancer la sauvegarde dans la session active.".into())
            }));
        }
    });
}

pub(super) fn begin_run() -> BridgeRun {
    invalidate_run();
    BridgeRun(generation())
}

pub(super) fn take_resume(source: ContentHash) -> Result<Option<(String, bool)>, String> {
    RESUME.with(|slot| {
        slot.borrow_mut()
            .take()
            .map(|resume| {
                if resume.source != source {
                    return Err("La sauvegarde préparée appartient à une autre révision.".into());
                }
                Ok((resume.json, resume.compatible))
            })
            .transpose()
    })
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows the system resources"
)]
pub(super) fn bridge(
    mut runtime: ResMut<GameplayRuntime>,
    run: Res<BridgeRun>,
    navigation: Res<NavigationRuntime>,
    policy: Res<NavigationPolicy>,
) {
    if run.0 != generation() {
        return;
    }
    PENDING.with(|slot| {
        let mut queue = slot.borrow_mut();
        if runtime.has_queued_actions() {
            return;
        }
        while let Some(pending) = queue.pop_front() {
            match pending {
                Pending::Action(token, source, request) => {
                    if token == run.0 && source == runtime.session().source_hash() {
                        if !runtime.is_ready() {
                            queue.push_front(Pending::Action(token, source, request));
                            break;
                        }
                        if let Err(error) = runtime.queue(request) {
                            runtime.report_interaction_error(error);
                        }
                    } else {
                        runtime.report_interaction_error(
                            "La commande appartient à une ancienne partie.".into(),
                        );
                    }
                }
                Pending::Save(token, source) => {
                    let captured = if token == run.0 && source == runtime.session().source_hash() {
                        runtime
                            .save_bytes()
                            .and_then(|bytes| String::from_utf8(bytes).map_err(|e| e.to_string()))
                            .map(|json| (token, source, json))
                    } else {
                        Err("La partie a changé avant la sauvegarde.".into())
                    };
                    SAVE.with(|slot| {
                        if let Some(signal) = *slot.borrow() {
                            let _ = signal.try_set(Some(captured));
                        }
                    });
                }
            }
            if runtime.has_queued_actions() {
                break;
            }
        }
    });
    let mut view = game_view(&runtime);
    view.run = run.0;
    view.ready = runtime.is_ready();
    view.pending = runtime.has_queued_actions() || PENDING.with(|q| !q.borrow().is_empty());
    let actors = navigation.actors();
    view.navigation_summary = navigation_summary(&runtime, &actors);
    view.navigation_diagnostic=serde_json::to_string(&serde_json::json!({
        "schema":"forge.navigation-runtime-observation","version":1,"run":run.0,
        "simulation_ready":runtime.is_ready(),"simulation_error":runtime.error(),"feedback_phase":"post_writeback",
        "gameplay_source_hash":runtime.session().source_hash(),"sequence":view.sequence,"tick":runtime.session().state().tick,
        "provider_source_hash":forge_engine::navigation::PROVIDER_SOURCE_SHA256,
        "profile":navigation.profile(),"policy_source_hash":policy.source_hash(),"policy_revision":policy.revision(),
        "policy_kind":if policy.source_hash().is_empty() {"empty"} else {"canonical"},
        "policy_area_count":policy.areas().len(),"actors":actors,"tiles":navigation.tiles(),
        "active_jobs":navigation.active_jobs(),"pending_jobs":navigation.pending_jobs(),
        "completed_builds":navigation.completed_builds(),"rejected_stale_results":navigation.rejected_stale_results()
    })).unwrap_or_else(|error|error.to_string());
    VIEW.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            if let Some(previous) = signal.try_get_untracked() {
                view.timing_state = previous.timing_state;
                view.timing_json = previous.timing_json;
            }
            if signal.try_get_untracked().as_ref() != Some(&view) {
                let _ = signal.try_set(view);
            }
        }
    });
}

fn navigation_summary(
    runtime: &GameplayRuntime,
    actors: &[ActorNavigationObservation],
) -> Vec<String> {
    actors
        .iter()
        .map(|observation| {
            let name = runtime
                .session()
                .document()
                .actor(observation.actor)
                .map_or("Personnage", |actor| actor.name.as_str());
            let label = match observation.status {
                NavigationStatus::Moving => "en marche",
                NavigationStatus::Reached => "destination atteinte",
                NavigationStatus::NoPath => "aucun chemin",
                NavigationStatus::OutsideMesh => "destination hors des surfaces disponibles",
                NavigationStatus::Disabled => "en pause",
                NavigationStatus::Ready => "prêt",
                NavigationStatus::BudgetExceeded => "trajet trop étendu",
                NavigationStatus::InvalidGeometry => "surface invalide",
                _ => "préparation du trajet",
            };
            format!("{name} : {label}")
        })
        .collect()
}

fn publish_error(error: String) {
    VIEW.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            let _ = signal.try_update(|view| view.feedback = error);
        }
    });
}

fn game_view(runtime: &GameplayRuntime) -> GameView {
    let session = runtime.session();
    let document = session.document();
    let state = session.state();
    let player = &state.actors[&document.player];
    let mut view = GameView {
        source: Some(session.source_hash()),
        player: Some(document.player),
        sequence: state.sequence,
        vitality: player.vitality,
        stamina: player.stamina,
        feedback: runtime.error().unwrap_or(runtime.feedback()).to_owned(),
        ..Default::default()
    };
    view.inventory = player
        .inventory
        .iter()
        .map(|(id, n)| {
            format!(
                "{} × {n}",
                document.item(*id).map_or("Objet", |i| i.name.as_str())
            )
        })
        .collect();
    view.quests = document
        .quests
        .iter()
        .map(|quest| format!("{} · {:?}", quest.name, state.quests[&quest.id]))
        .collect();
    actor_actions(session, player, &mut view);
    object_actions(session, player, &mut view);
    view
}

fn actor_actions(
    session: &forge_gameplay::GameSession,
    player: &forge_gameplay::ActorState,
    view: &mut GameView,
) {
    let document = session.document();
    let state = session.state();
    for actor in &document.actors {
        if actor.id == document.player || state.actors[&actor.id].vitality == 0 {
            continue;
        }
        if let Ok(dialogue) = session.dialogue_view(document.player, actor.id) {
            if !view.dialogue.is_empty() {
                view.dialogue.push('\n');
            }
            let _ = write!(view.dialogue, "{} : {}", actor.name, dialogue.text);
            view.actions
                .extend(dialogue.choices.into_iter().map(|choice| {
                    (
                        choice.text,
                        GameAction::ChooseDialogue {
                            speaker: actor.id,
                            node: dialogue.node,
                            choice: choice.id,
                        },
                    )
                }));
        }
        if actor.role == forge_gameplay::ActorRole::Enemy
            && distance_squared(player.position, state.actors[&actor.id].position)
                <= document.profile.attack_range_m.powi(2)
        {
            view.actions.push((
                format!("Frapper {}", actor.name),
                GameAction::Attack { target: actor.id },
            ));
        }
    }
}

fn object_actions(
    session: &forge_gameplay::GameSession,
    player: &forge_gameplay::ActorState,
    view: &mut GameView,
) {
    let document = session.document();
    let state = session.state();
    for object in &document.objects {
        let current = &state.objects[&object.id];
        if current.consumed
            || current.destroyed
            || distance_squared(player.position, current.position)
                > document.profile.interaction_range_m.powi(2)
        {
            continue;
        }
        match object.kind {
            forge_gameplay::ObjectKind::Pickup { .. } => view.actions.push((
                format!("Ramasser {}", object.name),
                GameAction::Pickup { object: object.id },
            )),
            forge_gameplay::ObjectKind::Door { .. } => view.actions.push((
                format!(
                    "{} {}",
                    if current.open { "Fermer" } else { "Ouvrir" },
                    object.name
                ),
                GameAction::SetDoor {
                    object: object.id,
                    open: !current.open,
                },
            )),
            forge_gameplay::ObjectKind::Prop => (),
        }
        let delta = Vec3::new(
            current.position.x - player.position.x,
            0.0,
            current.position.z - player.position.z,
        )
        .normalize_or_zero();
        let impulse = |speed: f32, up: f32| {
            forge_core::Vec3::new(
                delta.x * object.physics.mass_kg * speed,
                object.physics.mass_kg * up,
                delta.z * object.physics.mass_kg * speed,
            )
        };
        if object.physics.pushable {
            view.actions.push((
                format!("Pousser {}", object.name),
                GameAction::Push {
                    object: object.id,
                    impulse: impulse(2.0, 0.0),
                },
            ));
        }
        if object.physics.throwable {
            view.actions.push((
                format!("Lancer {}", object.name),
                GameAction::Throw {
                    object: object.id,
                    impulse: impulse(5.0, 3.0),
                },
            ));
        }
        if object.physics.flammable {
            view.actions.push((
                format!(
                    "{} {}",
                    if current.burning {
                        "Éteindre"
                    } else {
                        "Allumer"
                    },
                    object.name
                ),
                if current.burning {
                    GameAction::Extinguish { object: object.id }
                } else {
                    GameAction::Ignite { object: object.id }
                },
            ));
        }
    }
}

fn distance_squared(a: forge_core::Vec3, b: forge_core::Vec3) -> f32 {
    (a.x - b.x).powi(2) + (a.y - b.y).powi(2) + (a.z - b.z).powi(2)
}

fn enqueue(view: &GameView, action: GameAction) {
    if !view.ready || view.pending || view.run != generation() {
        return;
    }
    if let (Some(source), Some(actor)) = (view.source, view.player) {
        let key = serde_json::to_string(&(source, view.sequence, &action));
        match key {
            Ok(key) => PENDING.with(|slot| {
                let mut queue = slot.borrow_mut();
                if queue.is_empty() {
                    queue.push_back(Pending::Action(
                        view.run,
                        source,
                        GameRequest {
                            command_id: CommandId::deterministic(key),
                            expected_sequence: view.sequence,
                            actor,
                            action,
                        },
                    ));
                } else {
                    publish_error("Trop de commandes en attente.".into());
                }
            }),
            Err(error) => publish_error(error.to_string()),
        }
    }
}
