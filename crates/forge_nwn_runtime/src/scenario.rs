//! Synthetic M3 vertical slice: events, NCS, variables, dialogue, journal, item,
//! door, combat and save/resume.

use crate::effects::RuntimeEffect;
use crate::natives::REGISTRY_VERSION;
use crate::ncs::NcsProgram;
use crate::scheduler::{SchedulerSnapshot, ScriptScheduler};
use crate::vm::{
    MODULE_OBJECT, NcsValue, PLAYER_OBJECT, PendingConversation, RuntimeState, ScriptHost,
    VmContext, VmContinuation, VmError, execute, execute_with_host,
};
use forge_aurora_bridge::{ImportedModule, load_bundle};
use forge_aurora_runtime::{ModuleSession, ProjectedModule, SessionSnapshot, project_module};
use forge_core::{ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::path::Path;

const PLAYER_OBJECT_ID: u32 = PLAYER_OBJECT;
const CHEST_OBJECT_ID: u32 = 100;
const GUARD_OBJECT_ID: u32 = 101;
const DOOR_OBJECT_ID: u32 = 102;
const TRIGGER_OBJECT_ID: u32 = 103;
const GUIDE_OBJECT_ID: u32 = 104;

#[derive(Debug, thiserror::Error)]
pub enum M3Error {
    #[error("bundle load failed: {0}")]
    Bundle(String),
    #[error("projection failed: {0}")]
    Projection(String),
    #[error("script {script} is absent")]
    MissingScript { script: String },
    #[error("script {script} failed: {detail}")]
    Script { script: String, detail: String },
    #[error("session failed: {0}")]
    Session(String),
    #[error("dialogue {dialogue} failed: {detail}")]
    Dialogue { dialogue: String, detail: String },
    #[error("scheduler failed: {0}")]
    Scheduler(String),
    #[error("effect refused: {0}")]
    Effect(String),
    #[error("I/O error at {path}: {detail}")]
    Io { path: String, detail: String },
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ScenarioEvent {
    pub step: String,
    pub detail: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct DialogueTurn {
    pub node: String,
    pub text: String,
    pub available_replies: Vec<DialogueReplyView>,
    pub chosen_reply: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct DialogueReplyView {
    pub node: String,
    pub text: String,
    pub condition_script: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct M3Save {
    pub schema: String,
    pub session: SessionSnapshot,
    pub vm: RuntimeState,
    pub scheduler: SchedulerSnapshot,
    pub dialogue_transcript: Vec<DialogueTurn>,
    pub completed_steps: Vec<String>,
    #[serde(default)]
    pub continuation: Option<VmContinuation>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct M3Report {
    pub schema: String,
    pub module: String,
    pub entry_area: String,
    pub final_area: String,
    pub quest_status: String,
    pub transitions: usize,
    pub visited_areas: Vec<String>,
    pub doors_opened: usize,
    pub ticks: u64,
    pub distance_m: f32,
    pub natives: Vec<String>,
    pub native_registry: String,
    pub effects: usize,
    pub journal_entries: Vec<u32>,
    pub gold: i64,
    pub inventory: Vec<String>,
    pub messages: Vec<String>,
    pub dialogue_transcript: Vec<DialogueTurn>,
    pub events: Vec<ScenarioEvent>,
    pub executed_scripts: Vec<String>,
    pub unsupported: Vec<String>,
    pub save_sha256: ContentHash,
    pub resume_idempotent: bool,
}

struct ProgramHost<'a> {
    programs: &'a BTreeMap<String, NcsProgram>,
}

impl ScriptHost for ProgramHost<'_> {
    fn execute_script(
        &mut self,
        resref: &str,
        context: VmContext,
        state: &mut RuntimeState,
    ) -> Result<Option<NcsValue>, VmError> {
        let program = self
            .programs
            .get(resref)
            .cloned()
            .ok_or(VmError::UnsupportedNative {
                routine: 8,
                arguments: 2,
            })?;
        execute_with_host(&program, context, state, self).map(|outcome| outcome.result)
    }
}

pub struct M3Scenario {
    projected: ProjectedModule,
    session: ModuleSession,
    programs: BTreeMap<String, NcsProgram>,
    dialogues: BTreeMap<String, DialogueDocument>,
    state: RuntimeState,
    scheduler: ScriptScheduler,
    transcript: Vec<DialogueTurn>,
    events: Vec<ScenarioEvent>,
    completed_steps: Vec<String>,
    continuation: Option<VmContinuation>,
}

impl M3Scenario {
    pub fn load(bundle: &Path) -> Result<Self, M3Error> {
        let imported = load_bundle(bundle).map_err(|error| M3Error::Bundle(error.to_string()))?;
        let projected =
            project_module(&imported).map_err(|error| M3Error::Projection(error.to_string()))?;
        let entry = projected.entry_area.clone();
        let session = ModuleSession::new(projected.clone(), &entry).map_err(M3Error::Session)?;
        let programs = load_programs(&imported)?;
        let dialogues = load_dialogues(&imported)?;
        Ok(Self {
            projected,
            session,
            programs,
            dialogues,
            state: RuntimeState::default(),
            scheduler: ScriptScheduler::new(),
            transcript: Vec::new(),
            events: Vec::new(),
            completed_steps: Vec::new(),
            continuation: None,
        })
    }

    #[must_use]
    pub fn module_name(&self) -> &str {
        &self.projected.name
    }

    #[must_use]
    pub(crate) fn runtime(&self) -> &RuntimeState {
        &self.state
    }

    #[must_use]
    pub(crate) fn session(&self) -> &ModuleSession {
        &self.session
    }

    #[must_use]
    pub(crate) fn transcript(&self) -> &[DialogueTurn] {
        &self.transcript
    }

    #[must_use]
    pub(crate) fn scenario_events(&self) -> &[ScenarioEvent] {
        &self.events
    }

    pub fn run(&mut self) -> Result<M3Report, M3Error> {
        self.start_module()?;
        self.enter_camp()?;
        self.talk_to_guide(0)?;
        self.talk_to_guide(0)?;
        self.enter_crypt()?;
        self.schedule_relic_trigger()?;
        self.run_due_jobs(1)?;
        self.disturb_chest()?;
        self.kill_guard()?;
        self.return_to_camp()?;
        self.talk_to_guide(0)?;
        self.talk_to_guide(0)?;
        self.verify_completion()?;
        let save = self.save();
        let save_hash =
            canonical_json_sha256(&save).map_err(|error| M3Error::Session(error.to_string()))?;
        let resumed = self.resume_and_check(&save)?;
        let visit = self.session.report();
        Ok(M3Report {
            schema: "forge.aurora-m3-report@1".to_owned(),
            module: self.projected.name.clone(),
            entry_area: self.projected.entry_area.clone(),
            final_area: self.session.current_area().to_owned(),
            quest_status: self.quest_status(),
            transitions: visit.transitions,
            visited_areas: visit.zones_visited,
            doors_opened: visit.doors_opened,
            ticks: visit.ticks,
            distance_m: visit.distance_m,
            natives: self
                .state
                .called_natives
                .iter()
                .map(|(name, count)| format!("{name} x{count}"))
                .collect(),
            native_registry: REGISTRY_VERSION.to_owned(),
            effects: self.state.effects.len(),
            journal_entries: self.state.journal_entries("ab_quest"),
            gold: self.state.gold,
            inventory: self.state.inventory.iter().cloned().collect(),
            messages: self.state.messages.clone(),
            dialogue_transcript: self.transcript.clone(),
            events: self.events.clone(),
            executed_scripts: self.state.executed_scripts.clone(),
            unsupported: self
                .state
                .unsupported
                .iter()
                .map(|diagnostic| format!("{}: {}", diagnostic.code, diagnostic.detail))
                .collect(),
            save_sha256: save_hash,
            resume_idempotent: resumed,
        })
    }

    pub(crate) fn start_module(&mut self) -> Result<(), M3Error> {
        self.run_script("ab_mod_load", VmContext::self_only(MODULE_OBJECT))?;
        self.event("module_load", "ab_mod_load exécuté");
        Ok(())
    }

    pub(crate) fn enter_camp(&mut self) -> Result<(), M3Error> {
        self.run_script(
            "ab_camp_enter",
            VmContext::area_enter(MODULE_OBJECT, PLAYER_OBJECT_ID),
        )?;
        self.event("camp_enter", "ab_camp_enter exécuté");
        Ok(())
    }

    pub(crate) fn enter_crypt(&mut self) -> Result<(), M3Error> {
        self.travel_to("ab_crypt")?;
        self.run_script(
            "ab_crypt_enter",
            VmContext::area_enter(MODULE_OBJECT, PLAYER_OBJECT_ID),
        )?;
        self.event("crypt_enter", "ab_crypt_enter exécuté");
        Ok(())
    }

    pub(crate) fn return_to_camp(&mut self) -> Result<(), M3Error> {
        self.travel_to("ab_camp")?;
        self.run_script(
            "ab_camp_enter",
            VmContext::area_enter(MODULE_OBJECT, PLAYER_OBJECT_ID),
        )?;
        self.event("camp_return", "retour au camp");
        Ok(())
    }

    fn travel_to(&mut self, destination: &str) -> Result<(), M3Error> {
        if self.session.current_area() == destination {
            return Ok(());
        }
        let door = self
            .session
            .zone()
            .doors
            .iter()
            .find(|door| door.destination.as_deref() == Some(destination))
            .cloned()
            .ok_or_else(|| M3Error::Session(format!("no door to {destination}")))?;
        let from = self.session.player().position;
        let approach = approach_point(door.position, from, 1.2);
        self.session.walk_to(approach).map_err(M3Error::Session)?;
        if !self.session.is_door_open(&door) {
            self.session.interact().map_err(M3Error::Session)?;
            self.fire_door_event(&door, true)?;
        }
        self.run_script(
            "ab_gate_used",
            VmContext::object_used(DOOR_OBJECT_ID, PLAYER_OBJECT_ID),
        )?;
        if self
            .session
            .try_transition()
            .map_err(M3Error::Session)?
            .is_none()
        {
            self.session
                .walk_to(door.position)
                .map_err(M3Error::Session)?;
            if self
                .session
                .try_transition()
                .map_err(M3Error::Session)?
                .is_none()
            {
                return Err(M3Error::Session(format!(
                    "transition to {destination} did not trigger"
                )));
            }
        }
        self.event("transition", format!("transition vers {destination}"));
        self.fire_door_event(&door, false)?;
        Ok(())
    }

    fn fire_door_event(
        &mut self,
        door: &forge_aurora_runtime::ProjectedDoor,
        open: bool,
    ) -> Result<(), M3Error> {
        let label = door
            .tag
            .clone()
            .filter(|tag| !tag.trim().is_empty())
            .unwrap_or_else(|| format!("door:{}", door.position.x));
        self.state
            .apply_effects(
                "scenario",
                [RuntimeEffect::SetDoorState {
                    door: label.clone(),
                    open,
                }],
            )
            .map_err(|error| M3Error::Effect(error.to_string()))?;
        let script = if open {
            "ab_gate_open"
        } else {
            "ab_gate_close"
        };
        if !self.programs.contains_key(script) {
            return Ok(());
        }
        self.run_script(
            script,
            VmContext::object_used(DOOR_OBJECT_ID, PLAYER_OBJECT_ID),
        )?;
        self.event(
            if open { "door_open" } else { "door_close" },
            format!("{label} : {script} exécuté"),
        );
        Ok(())
    }

    pub(crate) fn schedule_relic_trigger(&mut self) -> Result<(), M3Error> {
        self.scheduler
            .schedule(
                1,
                "ab_relic_trigger",
                VmContext::area_enter(TRIGGER_OBJECT_ID, PLAYER_OBJECT_ID),
            )
            .map_err(|error| M3Error::Scheduler(error.to_string()))?;
        self.event(
            "relic_trigger_scheduled",
            "ab_relic_trigger planifié au tick suivant",
        );
        Ok(())
    }

    pub(crate) fn run_due_jobs(&mut self, ticks: u64) -> Result<(), M3Error> {
        let executed = self
            .scheduler
            .advance(ticks, &self.programs, &mut self.state)
            .map_err(|error| M3Error::Scheduler(error.to_string()))?;
        for job in executed {
            self.event(
                "timer",
                format!("{} exécuté au tick {}", job.script, job.tick),
            );
        }
        Ok(())
    }

    pub(crate) fn disturb_chest(&mut self) -> Result<(), M3Error> {
        self.state
            .apply_effects(
                "scenario",
                [RuntimeEffect::AddInventory {
                    item: "AB_RELIC".to_owned(),
                }],
            )
            .map_err(|error| M3Error::Effect(error.to_string()))?;
        self.run_script(
            "ab_chest_dist",
            VmContext::object_used(CHEST_OBJECT_ID, PLAYER_OBJECT_ID),
        )?;
        self.event("chest", "coffre disturbé, relique récupérée");
        Ok(())
    }

    pub(crate) fn kill_guard(&mut self) -> Result<(), M3Error> {
        self.state
            .apply_effects(
                "scenario",
                [RuntimeEffect::MarkDead {
                    object: GUARD_OBJECT_ID,
                }],
            )
            .map_err(|error| M3Error::Effect(error.to_string()))?;
        self.run_script("ab_guard_death", VmContext::self_only(GUARD_OBJECT_ID))?;
        self.event("combat", "gardien vaincu");
        Ok(())
    }

    pub(crate) fn talk_to_guide(&mut self, reply_index: usize) -> Result<(), M3Error> {
        self.run_script(
            "ab_guide_talk",
            VmContext::object_used(GUIDE_OBJECT_ID, PLAYER_OBJECT_ID),
        )?;
        let pending = self
            .state
            .pending_conversation
            .clone()
            .ok_or_else(|| M3Error::Dialogue {
                dialogue: "ab_guide_dlg".to_owned(),
                detail: "ab_guide_talk did not start a conversation".to_owned(),
            })?;
        self.run_dialogue(&pending, reply_index)?;
        self.state
            .apply_effects("scenario", [RuntimeEffect::EndConversation])
            .map_err(|error| M3Error::Effect(error.to_string()))?;
        Ok(())
    }

    fn run_dialogue(
        &mut self,
        pending: &PendingConversation,
        reply_index: usize,
    ) -> Result<(), M3Error> {
        let document = self
            .dialogues
            .get(&pending.dialogue)
            .cloned()
            .ok_or_else(|| M3Error::Dialogue {
                dialogue: pending.dialogue.clone(),
                detail: "dialogue document is absent".to_owned(),
            })?;
        let start = document
            .links
            .iter()
            .find(|link| link.source.is_none())
            .ok_or_else(|| M3Error::Dialogue {
                dialogue: document.resref.clone(),
                detail: "no start link".to_owned(),
            })?;
        let mut current = start.target.clone();
        loop {
            let node = document
                .nodes
                .iter()
                .find(|node| node.id == current)
                .ok_or_else(|| M3Error::Dialogue {
                    dialogue: document.resref.clone(),
                    detail: format!("missing node {current}"),
                })?;
            let mut replies = Vec::new();
            for link in &document.links {
                if link.source.as_deref() != Some(node.id.as_str()) {
                    continue;
                }
                let Some(reply) = document
                    .nodes
                    .iter()
                    .find(|candidate| candidate.id == link.target)
                else {
                    continue;
                };
                if reply.kind != "reply" {
                    continue;
                }
                let condition = link.condition_script.as_deref();
                if let Some(condition) = condition
                    && !self.eval_condition(condition)?
                {
                    continue;
                }
                replies.push(DialogueReplyView {
                    node: reply.id.clone(),
                    text: reply.display_text.clone(),
                    condition_script: condition.map(str::to_owned),
                });
            }
            let chosen = replies
                .get(reply_index.min(replies.len().saturating_sub(1)))
                .cloned();
            self.transcript.push(DialogueTurn {
                node: node.id.clone(),
                text: node.display_text.clone(),
                available_replies: replies,
                chosen_reply: chosen.as_ref().map(|reply| reply.text.clone()),
            });
            let Some(chosen) = chosen else { break };
            let reply = document
                .nodes
                .iter()
                .find(|candidate| candidate.id == chosen.node)
                .ok_or_else(|| M3Error::Dialogue {
                    dialogue: document.resref.clone(),
                    detail: format!("selected reply {} is absent", chosen.node),
                })?;
            if let Some(script) = &reply.action_script {
                self.run_script(
                    script,
                    VmContext::object_used(GUIDE_OBJECT_ID, PLAYER_OBJECT_ID),
                )?;
            }
            let next = document
                .links
                .iter()
                .find(|link| link.source.as_deref() == Some(reply.id.as_str()));
            let Some(next) = next else { break };
            current.clone_from(&next.target);
        }
        Ok(())
    }

    fn eval_condition(&mut self, script: &str) -> Result<bool, M3Error> {
        let program = self
            .programs
            .get(script)
            .ok_or_else(|| M3Error::MissingScript {
                script: script.to_owned(),
            })?;
        let mut host = ProgramHost {
            programs: &self.programs,
        };
        let outcome = execute_with_host(
            program,
            VmContext::self_only(MODULE_OBJECT),
            &mut self.state,
            &mut host,
        )
        .map_err(|error| M3Error::Script {
            script: script.to_owned(),
            detail: error.to_string(),
        })?;
        Ok(matches!(outcome.result, Some(NcsValue::Int(value)) if value != 0))
    }

    fn run_script(&mut self, script: &str, context: VmContext) -> Result<(), M3Error> {
        let program = self
            .programs
            .get(script)
            .ok_or_else(|| M3Error::MissingScript {
                script: script.to_owned(),
            })?;
        let mut host = ProgramHost {
            programs: &self.programs,
        };
        execute_with_host(program, context, &mut self.state, &mut host).map_err(|error| {
            M3Error::Script {
                script: script.to_owned(),
                detail: error.to_string(),
            }
        })?;
        Ok(())
    }

    pub(crate) fn verify_completion(&mut self) -> Result<(), M3Error> {
        if !self.eval_condition("ab_cond_done")? {
            return Err(M3Error::Dialogue {
                dialogue: "ab_quest".to_owned(),
                detail: "quest is not complete".to_owned(),
            });
        }
        self.event("quest_complete", "ab_cond_done vrai");
        Ok(())
    }

    pub(crate) fn quest_status(&self) -> String {
        if let Some(status) = self.state.quest_statuses.get("ab_quest") {
            return status.label().to_owned();
        }
        if self.state.has_journal_entry("ab_quest", 30) {
            "completed".to_owned()
        } else if self.state.has_journal_entry("ab_quest", 20) {
            "active".to_owned()
        } else if self.state.has_journal_entry("ab_quest", 10) {
            "available".to_owned()
        } else {
            "not_started".to_owned()
        }
    }

    pub(crate) fn save(&self) -> M3Save {
        M3Save {
            schema: "forge.aurora-m3-save@1".to_owned(),
            session: self.session.snapshot(),
            vm: self.state.clone(),
            scheduler: self.scheduler.snapshot(),
            dialogue_transcript: self.transcript.clone(),
            completed_steps: self.completed_steps.clone(),
            continuation: self.continuation.clone(),
        }
    }

    pub(crate) fn resume_and_check(&mut self, save: &M3Save) -> Result<bool, M3Error> {
        let before_gold = self.state.gold;
        let before_entries = self.state.journal_entries("ab_quest");
        let restored_session = ModuleSession::restore(self.projected.clone(), save.session.clone())
            .map_err(M3Error::Session)?;
        let mut restored_state = save.vm.clone();
        let mut restored_scheduler = ScriptScheduler::restore(save.scheduler.clone())
            .map_err(|error| M3Error::Scheduler(error.to_string()))?;
        let relic_runs_before = restored_state
            .executed_scripts
            .iter()
            .filter(|name| name.as_str() == "ab_relic_trigger")
            .count();
        let program = self
            .programs
            .get("ab_mod_load")
            .ok_or_else(|| M3Error::MissingScript {
                script: "ab_mod_load".to_owned(),
            })?;
        execute(
            program,
            VmContext::self_only(MODULE_OBJECT),
            &mut restored_state,
        )
        .map_err(|error| M3Error::Script {
            script: "ab_mod_load".to_owned(),
            detail: error.to_string(),
        })?;
        let program = self
            .programs
            .get("ab_reward")
            .ok_or_else(|| M3Error::MissingScript {
                script: "ab_reward".to_owned(),
            })?;
        execute(
            program,
            VmContext::self_only(MODULE_OBJECT),
            &mut restored_state,
        )
        .map_err(|error| M3Error::Script {
            script: "ab_reward".to_owned(),
            detail: error.to_string(),
        })?;
        restored_scheduler
            .advance(5, &self.programs, &mut restored_state)
            .map_err(|error| M3Error::Scheduler(error.to_string()))?;
        let relic_runs_after = restored_state
            .executed_scripts
            .iter()
            .filter(|name| name.as_str() == "ab_relic_trigger")
            .count();
        let idempotent = restored_state.gold == before_gold
            && restored_state.journal_entries("ab_quest") == before_entries
            && restored_session.current_area() == self.session.current_area()
            && restored_scheduler.pending() == 0
            && relic_runs_after == relic_runs_before;
        self.event("save_resume", format!("reprise idempotente={idempotent}"));
        Ok(idempotent)
    }

    pub(crate) fn event(&mut self, step: impl Into<String>, detail: impl Into<String>) {
        let step = step.into();
        self.completed_steps.push(step.clone());
        self.events.push(ScenarioEvent {
            step,
            detail: detail.into(),
        });
    }
}

#[derive(Debug, Clone)]
struct DialogueDocument {
    resref: String,
    nodes: Vec<DialogueNode>,
    links: Vec<DialogueLink>,
}

#[derive(Debug, Clone)]
struct DialogueNode {
    id: String,
    kind: String,
    display_text: String,
    action_script: Option<String>,
}

#[derive(Debug, Clone)]
struct DialogueLink {
    source: Option<String>,
    target: String,
    condition_script: Option<String>,
}

fn has_extension(path: &str, extension: &str) -> bool {
    path.rsplit_once('.')
        .is_some_and(|(_, value)| value.eq_ignore_ascii_case(extension))
}

fn load_programs(imported: &ImportedModule) -> Result<BTreeMap<String, NcsProgram>, M3Error> {
    let mut programs = BTreeMap::new();
    for entry in &imported.manifest.files {
        if entry.path.starts_with("scripts/") && has_extension(&entry.path, "ncs") {
            let resref = entry
                .path
                .strip_prefix("scripts/")
                .and_then(|path| path.rsplit_once('.').map(|(stem, _)| stem.to_owned()))
                .unwrap_or_default();
            let path = imported.root.join(&entry.path);
            let bytes = std::fs::read(&path).map_err(|error| M3Error::Io {
                path: path.display().to_string(),
                detail: error.to_string(),
            })?;
            let program = NcsProgram::parse(&resref, &bytes).map_err(|error| M3Error::Script {
                script: resref.clone(),
                detail: error.to_string(),
            })?;
            programs.insert(resref, program);
        }
    }
    Ok(programs)
}

fn load_dialogues(
    imported: &ImportedModule,
) -> Result<BTreeMap<String, DialogueDocument>, M3Error> {
    let mut dialogues = BTreeMap::new();
    for entry in &imported.manifest.files {
        if entry.path.starts_with("dialogs/") && has_extension(&entry.path, "json") {
            let path = imported.root.join(&entry.path);
            let bytes = std::fs::read(&path).map_err(|error| M3Error::Io {
                path: path.display().to_string(),
                detail: error.to_string(),
            })?;
            let value: serde_json::Value =
                serde_json::from_slice(&bytes).map_err(|error| M3Error::Dialogue {
                    dialogue: entry.path.clone(),
                    detail: error.to_string(),
                })?;
            let resref = value["resref"].as_str().unwrap_or_default().to_owned();
            if resref.is_empty() {
                continue;
            }
            let nodes = value["nodes"]
                .as_array()
                .into_iter()
                .flatten()
                .map(|node| DialogueNode {
                    id: node["id"].as_str().unwrap_or_default().to_owned(),
                    kind: node["kind"].as_str().unwrap_or_default().to_owned(),
                    display_text: node["displayText"].as_str().unwrap_or_default().to_owned(),
                    action_script: node["actionScript"].as_str().map(str::to_owned),
                })
                .collect();
            let links = value["links"]
                .as_array()
                .into_iter()
                .flatten()
                .map(|link| DialogueLink {
                    source: link["source"].as_str().map(str::to_owned),
                    target: link["target"].as_str().unwrap_or_default().to_owned(),
                    condition_script: link["conditionScript"].as_str().map(str::to_owned),
                })
                .collect();
            dialogues.insert(
                resref.clone(),
                DialogueDocument {
                    resref,
                    nodes,
                    links,
                },
            );
        }
    }
    Ok(dialogues)
}

fn approach_point(
    door: forge_core::Vec3,
    from: forge_core::Vec3,
    distance: f32,
) -> forge_core::Vec3 {
    let dx = from.x - door.x;
    let dz = from.z - door.z;
    let length = (dx * dx + dz * dz).sqrt();
    if length <= 1.0e-4 {
        forge_core::Vec3::new(door.x, door.y, door.z + distance)
    } else {
        forge_core::Vec3::new(
            door.x + dx / length * distance,
            door.y,
            door.z + dz / length * distance,
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_nwn_rules::QuestStatus;
    use std::path::PathBuf;

    #[test]
    fn report_schema_is_stable() {
        assert_eq!("forge.aurora-m3-report@1", "forge.aurora-m3-report@1");
    }

    #[test]
    fn dialogue_document_parser_reads_the_fixture() {
        let imported = load_bundle(
            &PathBuf::from(env!("CARGO_MANIFEST_DIR"))
                .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1"),
        )
        .expect("bundle");
        let dialogues = load_dialogues(&imported).expect("dialogues");
        let dialogue = dialogues.get("ab_guide_dlg").expect("guide dialogue");
        assert_eq!(dialogue.nodes.len(), 9);
        assert_eq!(dialogue.links.len(), 9);
    }

    #[test]
    fn door_events_apply_state_and_run_optional_scripts() {
        let bundle = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1");
        let mut scenario = M3Scenario::load(&bundle).expect("scenario");
        let mut bytes = b"NCS V1.0".to_vec();
        bytes.push(0x42);
        bytes.extend_from_slice(&15_u32.to_be_bytes());
        bytes.extend_from_slice(&[0x20, 0x00]);
        let program = NcsProgram::parse("ab_gate_open", &bytes).expect("ncs");
        scenario.programs.insert("ab_gate_open".to_owned(), program);
        let door = scenario
            .session
            .zone()
            .doors
            .first()
            .cloned()
            .expect("door");
        scenario.fire_door_event(&door, true).expect("open event");
        assert_eq!(scenario.state.doors.get("AB_GATE"), Some(&true));
        assert!(
            scenario
                .state
                .executed_scripts
                .iter()
                .any(|script| script == "ab_gate_open")
        );
        assert!(
            scenario
                .events
                .iter()
                .any(|event| event.step == "door_open")
        );
    }

    #[test]
    fn quest_status_effect_overrides_the_journal_projection() {
        let bundle = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1");
        let mut scenario = M3Scenario::load(&bundle).expect("scenario");
        scenario
            .state
            .apply_effects(
                "test",
                [RuntimeEffect::SetQuestStatus {
                    quest: "ab_quest".to_owned(),
                    status: QuestStatus::Failed,
                }],
            )
            .expect("quest status");
        assert_eq!(scenario.quest_status(), "failed");
        let save = scenario.save();
        let json = serde_json::to_string(&save).expect("json");
        let restored: M3Save = serde_json::from_str(&json).expect("restore");
        assert_eq!(
            restored.vm.quest_statuses.get("ab_quest"),
            Some(&QuestStatus::Failed)
        );
        let mut save = save;
        save.continuation = Some(VmContinuation {
            instruction_index: 0,
            stack: Vec::new(),
            calls: Vec::new(),
            steps: 0,
        });
        let json = serde_json::to_string(&save).expect("json");
        let restored: M3Save = serde_json::from_str(&json).expect("restore");
        assert!(restored.continuation.is_some());
    }
}
