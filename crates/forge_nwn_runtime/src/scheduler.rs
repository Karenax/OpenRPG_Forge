//! Deterministic bounded scheduler for delayed script jobs.
//!
//! The `NWScript` action system (`AssignCommand`, `DelayCommand`, `ExecuteScript`,
//! `ActionDoCommand`) is not implemented. This scheduler is the explicit host
//! boundary used by the M3 scenario: jobs are typed, ordered by due tick and
//! identifier, and persisted with the save so a resume never repeats them.

use crate::ncs::NcsProgram;
use crate::vm::{RuntimeState, VmContext, VmError, execute};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use std::fmt;

pub const SCHEDULER_SCHEMA: &str = "forge.nwn-scheduler@1";
pub const MAX_SCHEDULED_JOBS: usize = 4096;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ScheduledJob {
    pub id: u64,
    pub due_tick: u64,
    pub script: String,
    pub context: VmContext,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ScheduledExecution {
    pub id: u64,
    pub tick: u64,
    pub script: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct SchedulerSnapshot {
    pub schema: String,
    pub tick: u64,
    pub next_id: u64,
    pub jobs: Vec<ScheduledJob>,
    pub completed: Vec<u64>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SchedulerError {
    MissingScript { script: String },
    Script { script: String, detail: String },
    InvalidSnapshot(String),
    Full(usize),
}

impl fmt::Display for SchedulerError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::MissingScript { script } => write!(formatter, "script {script} is absent"),
            Self::Script { script, detail } => {
                write!(formatter, "script {script} failed: {detail}")
            }
            Self::InvalidSnapshot(detail) => {
                write!(formatter, "invalid scheduler snapshot: {detail}")
            }
            Self::Full(count) => write!(formatter, "scheduler job limit exceeded ({count})"),
        }
    }
}

impl std::error::Error for SchedulerError {}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ScriptScheduler {
    tick: u64,
    next_id: u64,
    jobs: BTreeMap<(u64, u64), ScheduledJob>,
    completed: BTreeSet<u64>,
}

impl ScriptScheduler {
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    #[must_use]
    pub const fn tick(&self) -> u64 {
        self.tick
    }

    #[must_use]
    pub fn pending(&self) -> usize {
        self.jobs.len()
    }

    pub fn schedule(
        &mut self,
        delay_ticks: u64,
        script: impl Into<String>,
        context: VmContext,
    ) -> Result<u64, SchedulerError> {
        if self.jobs.len() >= MAX_SCHEDULED_JOBS {
            return Err(SchedulerError::Full(self.jobs.len()));
        }
        if self.next_id == u64::MAX {
            return Err(SchedulerError::Full(self.jobs.len()));
        }
        let id = self.next_id;
        self.next_id += 1;
        let due_tick = self.tick.saturating_add(delay_ticks);
        self.jobs.insert(
            (due_tick, id),
            ScheduledJob {
                id,
                due_tick,
                script: script.into(),
                context,
            },
        );
        Ok(id)
    }

    pub fn advance(
        &mut self,
        ticks: u64,
        programs: &BTreeMap<String, NcsProgram>,
        state: &mut RuntimeState,
    ) -> Result<Vec<ScheduledExecution>, SchedulerError> {
        let target = self.tick.saturating_add(ticks);
        let mut executed = Vec::new();
        while self.tick < target {
            self.tick += 1;
            let due = self
                .jobs
                .keys()
                .filter(|(due_tick, _)| *due_tick <= self.tick)
                .copied()
                .collect::<Vec<_>>();
            for key in due {
                let Some(job) = self.jobs.remove(&key) else {
                    continue;
                };
                let program =
                    programs
                        .get(&job.script)
                        .ok_or_else(|| SchedulerError::MissingScript {
                            script: job.script.clone(),
                        })?;
                execute(program, job.context, state).map_err(|error| SchedulerError::Script {
                    script: job.script.clone(),
                    detail: describe_vm_error(&error),
                })?;
                self.completed.insert(job.id);
                executed.push(ScheduledExecution {
                    id: job.id,
                    tick: self.tick,
                    script: job.script,
                });
            }
        }
        Ok(executed)
    }

    #[must_use]
    pub fn snapshot(&self) -> SchedulerSnapshot {
        SchedulerSnapshot {
            schema: SCHEDULER_SCHEMA.to_owned(),
            tick: self.tick,
            next_id: self.next_id,
            jobs: self.jobs.values().cloned().collect(),
            completed: self.completed.iter().copied().collect(),
        }
    }

    pub fn restore(snapshot: SchedulerSnapshot) -> Result<Self, SchedulerError> {
        if snapshot.schema != SCHEDULER_SCHEMA {
            return Err(SchedulerError::InvalidSnapshot(format!(
                "unsupported schema {}",
                snapshot.schema
            )));
        }
        let mut jobs = BTreeMap::new();
        let mut completed = BTreeSet::new();
        for id in snapshot.completed {
            if !completed.insert(id) {
                return Err(SchedulerError::InvalidSnapshot(format!(
                    "duplicate completed job {id}"
                )));
            }
        }
        let mut seen = BTreeSet::new();
        for job in snapshot.jobs {
            if job.id >= snapshot.next_id {
                return Err(SchedulerError::InvalidSnapshot(format!(
                    "job {} is not below next_id {}",
                    job.id, snapshot.next_id
                )));
            }
            if !seen.insert(job.id) {
                return Err(SchedulerError::InvalidSnapshot(format!(
                    "duplicate pending job {}",
                    job.id
                )));
            }
            if completed.contains(&job.id) {
                return Err(SchedulerError::InvalidSnapshot(format!(
                    "job {} is both completed and pending",
                    job.id
                )));
            }
            jobs.insert((job.due_tick, job.id), job);
        }
        Ok(Self {
            tick: snapshot.tick,
            next_id: snapshot.next_id,
            jobs,
            completed,
        })
    }
}

fn describe_vm_error(error: &VmError) -> String {
    error.to_string()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::vm::{MODULE_OBJECT, PLAYER_OBJECT};
    use std::path::PathBuf;

    fn programs() -> BTreeMap<String, NcsProgram> {
        let root = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1/scripts");
        let mut programs = BTreeMap::new();
        for entry in std::fs::read_dir(root).expect("scripts directory") {
            let entry = entry.expect("entry");
            let path = entry.path();
            if !path
                .extension()
                .and_then(|value| value.to_str())
                .is_some_and(|value| value.eq_ignore_ascii_case("ncs"))
            {
                continue;
            }
            let resref = path
                .file_stem()
                .and_then(|value| value.to_str())
                .expect("resref")
                .to_owned();
            let bytes = std::fs::read(&path).expect("ncs bytes");
            programs.insert(
                resref.clone(),
                NcsProgram::parse(resref, &bytes).expect("parse NCS"),
            );
        }
        programs
    }

    #[test]
    fn jobs_run_in_due_then_insertion_order() {
        let programs = programs();
        let mut state = RuntimeState::default();
        execute(
            programs.get("ab_mod_load").expect("mod load"),
            VmContext::self_only(MODULE_OBJECT),
            &mut state,
        )
        .expect("module load");
        let mut scheduler = ScriptScheduler::new();
        scheduler
            .schedule(
                2,
                "ab_camp_enter",
                VmContext::area_enter(MODULE_OBJECT, PLAYER_OBJECT),
            )
            .expect("schedule camp");
        scheduler
            .schedule(2, "ab_guard_death", VmContext::self_only(MODULE_OBJECT))
            .expect("schedule guard");
        let first = scheduler
            .advance(1, &programs, &mut state)
            .expect("advance");
        assert!(first.is_empty());
        let second = scheduler
            .advance(1, &programs, &mut state)
            .expect("advance");
        assert_eq!(second.len(), 2);
        assert_eq!(second[0].script, "ab_camp_enter");
        assert_eq!(second[1].script, "ab_guard_death");
        assert_eq!(state.local_int(MODULE_OBJECT, "ab_combat_won"), 1);
    }

    #[test]
    fn snapshot_restore_does_not_repeat_completed_jobs() {
        let programs = programs();
        let mut state = RuntimeState::default();
        execute(
            programs.get("ab_mod_load").expect("mod load"),
            VmContext::self_only(MODULE_OBJECT),
            &mut state,
        )
        .expect("module load");
        execute(
            programs.get("ab_chest_dist").expect("chest"),
            VmContext::self_only(MODULE_OBJECT),
            &mut state,
        )
        .expect("chest");
        let mut scheduler = ScriptScheduler::new();
        scheduler
            .schedule(1, "ab_reward", VmContext::self_only(MODULE_OBJECT))
            .expect("schedule reward");
        let executed = scheduler
            .advance(1, &programs, &mut state)
            .expect("advance");
        assert_eq!(executed.len(), 1);
        assert_eq!(state.gold, 50);

        let snapshot = scheduler.snapshot();
        let mut restored = ScriptScheduler::restore(snapshot.clone()).expect("restore");
        assert_eq!(restored.snapshot(), snapshot);
        restored
            .advance(5, &programs, &mut state)
            .expect("advance after restore");
        assert_eq!(state.gold, 50, "the reward must not be repeated");
        assert_eq!(
            state
                .executed_scripts
                .iter()
                .filter(|name| name.as_str() == "ab_reward")
                .count(),
            1
        );
    }
}
