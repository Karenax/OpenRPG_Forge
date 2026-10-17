//! Runtime adapter for authored RPG definitions; state and consequences live in `forge_gameplay`.
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};
use forge_gameplay::{GameRequest, GameSession, GameplayDocument, GameplayEvent};
use std::collections::{BTreeMap, VecDeque};

mod danger_motion;
mod projection;
mod simulation;
pub mod visuals;
pub use danger_motion::{DangerMotionActor, DangerMotionState};
pub use projection::GameplayProjectionStatus;

/// Runtime-owned identities never enter authored documents or game saves.
#[derive(Component, Default)]
pub struct GameplayBody {
    pub previous_velocity: Vec3,
}

#[derive(Resource)]
pub struct GameplayRuntime {
    pub(crate) session: GameSession,
    pub(crate) entities: BTreeMap<EntityId, Entity>,
    pub(crate) pending: VecDeque<GameRequest>,
    pub(crate) last_events: Vec<GameplayEvent>,
    pub(crate) effects: Vec<GameplayEvent>,
    pub(crate) error: Option<String>,
    pub(crate) enabled: bool,
    pub(crate) player_settled: bool,
    pub(crate) feedback: String,
}

impl GameplayRuntime {
    pub fn new(
        document: GameplayDocument,
        source_hash: ContentHash,
        save: Option<&[u8]>,
        compatible: bool,
    ) -> Result<Self, String> {
        let session = match save {
            Some(bytes) => GameSession::resume(document, source_hash, bytes, compatible),
            None => GameSession::new(document, source_hash),
        }
        .map_err(|e| e.to_string())?;
        Ok(Self {
            session,
            entities: BTreeMap::new(),
            pending: VecDeque::new(),
            last_events: Vec::new(),
            effects: Vec::new(),
            error: None,
            enabled: false,
            player_settled: false,
            feedback: "Approchez un objet ou un personnage pour interagir.".into(),
        })
    }
    #[must_use]
    pub const fn session(&self) -> &GameSession {
        &self.session
    }
    #[must_use]
    pub fn error(&self) -> Option<&str> {
        self.error.as_deref()
    }
    #[must_use]
    pub fn feedback(&self) -> &str {
        &self.feedback
    }
    pub fn report_interaction_error(&mut self, error: String) {
        self.feedback = error;
    }
    #[must_use]
    pub const fn is_ready(&self) -> bool {
        self.enabled && self.error.is_none()
    }
    #[must_use]
    pub fn has_queued_actions(&self) -> bool {
        !self.pending.is_empty()
    }
    pub fn queue(&mut self, request: GameRequest) -> Result<(), String> {
        if request.actor != self.session.document().player {
            return Err(
                "The player interaction adapter cannot supply another actor's observations".into(),
            );
        }
        if !self.enabled {
            return Err("Gameplay projection is not ready".into());
        }
        if self.pending.len() >= 32 {
            return Err("Gameplay command queue is full".into());
        }
        self.pending.push_back(request);
        Ok(())
    }
    pub fn save_bytes(&self) -> Result<Vec<u8>, String> {
        if !self.pending.is_empty() || !self.effects.is_empty() {
            return Err("Wait for queued actions and consequences before saving".into());
        }
        if !self.enabled || self.error.is_some() {
            return Err("Wait for a valid stable simulation before saving".into());
        }
        if !self.player_settled {
            return Err("Arrêtez le personnage sur un appui stable avant de sauvegarder.".into());
        }
        self.session.save_bytes().map_err(|e| e.to_string())
    }
    pub fn set_enabled(&mut self, enabled: bool) {
        self.enabled = enabled;
        if !enabled {
            self.pending.clear();
            self.player_settled = false;
        }
    }
    #[must_use]
    pub fn last_events(&self) -> &[GameplayEvent] {
        &self.last_events
    }
}

#[derive(Clone, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum GameplaySet {
    Project,
    Drive,
    NpcDrive,
    Observe,
}

pub struct GameplayPlugin;
impl Plugin for GameplayPlugin {
    fn build(&self, app: &mut App) {
        use bevy_rapier3d::prelude::PhysicsSet;
        app.init_resource::<crate::navigation::NavigationRuntime>()
            .init_resource::<visuals::GameplayVisualPlan>()
            .init_resource::<visuals::GameplayVisualStatus>()
            .init_resource::<crate::navigation::NavigationPolicy>()
            .add_systems(Startup, projection::spawn)
            .add_systems(Startup, visuals::attach.after(projection::spawn))
            .add_systems(PostUpdate, visuals::observe)
            .configure_sets(
                FixedUpdate,
                GameplaySet::Drive
                    .after(PhysicsSet::SyncBackend)
                    .before(PhysicsSet::StepSimulation),
            )
            .configure_sets(
                FixedUpdate,
                GameplaySet::Observe
                    .after(PhysicsSet::Writeback)
                    .after(crate::PlayerControllerSet::Observe),
            )
            .add_systems(
                FixedUpdate,
                simulation::remember_velocity.in_set(GameplaySet::Drive),
            )
            // NPC fleeing is a kinematic actuator.  It must publish its
            // requested translation before Rapier rebuilds its backend, and
            // therefore deliberately does not belong to the gameplay Drive
            // set (that set observes the post-backend physics state).
            .add_systems(
                FixedUpdate,
                danger_motion::drive
                    .in_set(GameplaySet::NpcDrive)
                    .after(crate::PlayerControllerSet::Drive)
                    .before(PhysicsSet::SyncBackend),
            )
            .add_systems(
                FixedUpdate,
                crate::navigation::guard
                    .in_set(crate::navigation::NavigationSet::Guard)
                    .after(crate::PlayerControllerSet::Drive)
                    .before(danger_motion::drive),
            )
            .add_systems(
                FixedUpdate,
                (
                    danger_motion::observe,
                    simulation::observe,
                    projection::synchronize,
                )
                    .chain()
                    .in_set(GameplaySet::Observe),
            )
            .add_systems(
                FixedUpdate,
                crate::navigation::refresh
                    .in_set(crate::navigation::NavigationSet::Refresh)
                    .after(GameplaySet::Observe),
            );
    }
}

pub(crate) fn neutral(v: Vec3) -> forge_core::Vec3 {
    forge_core::Vec3::new(v.x, v.y, v.z)
}
pub(crate) fn engine(v: forge_core::Vec3) -> Vec3 {
    Vec3::new(v.x, v.y, v.z)
}
