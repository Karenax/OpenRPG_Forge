//! Deterministic bounded `NWScript` VM for the fixture's verified opcode subset.

use crate::effects::{AppliedEffect, EffectError, EffectPolicy, RuntimeEffect};
use crate::natives::{NativeStatus, lookup};
use crate::ncs::{NcsInstruction, NcsProgram};
use forge_nwn_rules::QuestStatus;
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use std::fmt;

pub const MODULE_OBJECT: u32 = 1;
pub const PLAYER_OBJECT: u32 = 2;
pub const MAX_STEPS: usize = 100_000;
pub const MAX_STACK_VALUES: usize = 4096;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
pub enum ActionValue {
    StartConversation {
        target: u32,
        dialogue: String,
        private: bool,
        play_hello: bool,
    },
    ExecuteScript {
        resref: String,
        target: u32,
    },
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", content = "value", rename_all = "snake_case")]
pub enum NcsValue {
    Int(i32),
    Float(f32),
    String(String),
    Object(u32),
    Action(ActionValue),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum VmError {
    UnsupportedInstruction {
        offset: usize,
        opcode: u8,
    },
    UnsupportedNative {
        routine: u16,
        arguments: u8,
    },
    InvalidStack {
        offset: usize,
        detail: String,
    },
    TypeMismatch {
        instruction: usize,
        expected: &'static str,
    },
    InvalidJump {
        offset: usize,
        target: i32,
    },
    BudgetExceeded,
    DivisionByZero,
    InvalidNativeArguments {
        routine: u16,
        detail: String,
    },
    EffectRefused(String),
}

impl fmt::Display for VmError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnsupportedInstruction { offset, opcode } => {
                write!(
                    formatter,
                    "unsupported NCS instruction {opcode:#04x} at {offset}"
                )
            }
            Self::UnsupportedNative { routine, arguments } => {
                write!(
                    formatter,
                    "unsupported native {routine} with {arguments} arguments"
                )
            }
            Self::InvalidStack { offset, detail } => {
                write!(formatter, "invalid stack operation at {offset}: {detail}")
            }
            Self::TypeMismatch {
                instruction,
                expected,
            } => {
                write!(
                    formatter,
                    "type mismatch at instruction {instruction}; expected {expected}"
                )
            }
            Self::InvalidJump { offset, target } => {
                write!(formatter, "invalid jump at {offset} to {target}")
            }
            Self::BudgetExceeded => formatter.write_str("NCS execution budget exceeded"),
            Self::DivisionByZero => formatter.write_str("NCS division by zero"),
            Self::InvalidNativeArguments { routine, detail } => {
                write!(formatter, "invalid native {routine} arguments: {detail}")
            }
            Self::EffectRefused(detail) => write!(formatter, "effect refused: {detail}"),
        }
    }
}

impl std::error::Error for VmError {}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct PendingConversation {
    pub target: u32,
    pub dialogue: String,
    pub private: bool,
    pub play_hello: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct JournalEvent {
    pub quest: String,
    pub entry: u32,
    pub target: u32,
    pub all_party: bool,
    pub all_players: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct UnsupportedDiagnostic {
    pub script: String,
    pub code: String,
    pub detail: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct QueuedAction {
    pub id: u64,
    pub subject: u32,
    pub due_tick: u64,
    pub action: ActionValue,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeState {
    pub module_locals: BTreeMap<String, i32>,
    pub object_locals: BTreeMap<u32, BTreeMap<String, i32>>,
    pub messages: Vec<String>,
    pub gold: i64,
    pub inventory: BTreeSet<String>,
    pub dead: BTreeSet<u32>,
    pub quest_entries: BTreeMap<String, BTreeSet<u32>>,
    #[serde(default)]
    pub quest_statuses: BTreeMap<String, QuestStatus>,
    #[serde(default)]
    pub doors: BTreeMap<String, bool>,
    pub journal: Vec<JournalEvent>,
    pub pending_conversation: Option<PendingConversation>,
    pub unsupported: Vec<UnsupportedDiagnostic>,
    pub executed_scripts: Vec<String>,
    pub called_natives: BTreeMap<String, usize>,
    pub effects: Vec<AppliedEffect>,
    pub next_effect_sequence: u64,
    pub queued_actions: Vec<QueuedAction>,
    pub next_action_id: u64,
    pub tick: u64,
    #[serde(default)]
    pub effect_policy: EffectPolicy,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VmContext {
    pub self_object: u32,
    pub entering: Option<u32>,
    pub last_used_by: Option<u32>,
    pub first_pc: u32,
}

impl VmContext {
    #[must_use]
    pub const fn self_only(self_object: u32) -> Self {
        Self {
            self_object,
            entering: None,
            last_used_by: None,
            first_pc: PLAYER_OBJECT,
        }
    }

    #[must_use]
    pub const fn area_enter(self_object: u32, entering: u32) -> Self {
        Self {
            self_object,
            entering: Some(entering),
            last_used_by: None,
            first_pc: PLAYER_OBJECT,
        }
    }

    #[must_use]
    pub const fn object_used(self_object: u32, used_by: u32) -> Self {
        Self {
            self_object,
            entering: None,
            last_used_by: Some(used_by),
            first_pc: PLAYER_OBJECT,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct VmOutcome {
    pub result: Option<NcsValue>,
    pub steps: usize,
}

impl RuntimeState {
    #[must_use]
    pub fn local_int(&self, object: u32, name: &str) -> i32 {
        if object == MODULE_OBJECT {
            self.module_locals.get(name).copied().unwrap_or(0)
        } else {
            self.object_locals
                .get(&object)
                .and_then(|locals| locals.get(name))
                .copied()
                .unwrap_or(0)
        }
    }

    pub fn set_local_int(&mut self, object: u32, name: &str, value: i32) {
        if object == MODULE_OBJECT {
            self.module_locals.insert(name.to_owned(), value);
        } else {
            self.object_locals
                .entry(object)
                .or_default()
                .insert(name.to_owned(), value);
        }
    }

    #[must_use]
    pub fn journal_entries(&self, quest: &str) -> Vec<u32> {
        self.quest_entries
            .get(quest)
            .map(|entries| entries.iter().copied().collect())
            .unwrap_or_default()
    }

    #[must_use]
    pub fn has_journal_entry(&self, quest: &str, entry: u32) -> bool {
        self.quest_entries
            .get(quest)
            .is_some_and(|entries| entries.contains(&entry))
    }

    #[must_use]
    pub fn has_inventory(&self, item: &str) -> bool {
        self.inventory.contains(item)
    }

    /// Validate every effect before mutating anything, then apply the batch and
    /// record one receipt per effect. A refused batch leaves the state untouched.
    pub fn apply_effects(
        &mut self,
        script: &str,
        effects: impl IntoIterator<Item = RuntimeEffect>,
    ) -> Result<(), EffectError> {
        let effects = effects.into_iter().collect::<Vec<_>>();
        for effect in &effects {
            effect.validate()?;
            if !self.effect_policy.allows(effect) {
                return Err(EffectError::Denied(effect.kind().to_owned()));
            }
        }
        let count = u64::try_from(effects.len()).map_err(|_| EffectError::SequenceExhausted)?;
        if self.next_effect_sequence.checked_add(count).is_none() {
            return Err(EffectError::SequenceExhausted);
        }
        for effect in effects {
            self.apply_effect(script, effect);
        }
        Ok(())
    }

    fn apply_effect(&mut self, script: &str, effect: RuntimeEffect) {
        match &effect {
            RuntimeEffect::SetLocalInt {
                object,
                name,
                value,
            } => {
                self.set_local_int(*object, name, *value);
            }
            RuntimeEffect::AddJournalEntry {
                quest,
                entry,
                target,
                all_party,
                all_players,
            } => {
                self.quest_entries
                    .entry(quest.clone())
                    .or_default()
                    .insert(*entry);
                self.journal.push(JournalEvent {
                    quest: quest.clone(),
                    entry: *entry,
                    target: *target,
                    all_party: *all_party,
                    all_players: *all_players,
                });
            }
            RuntimeEffect::SetQuestStatus { quest, status } => {
                self.quest_statuses.insert(quest.clone(), *status);
            }
            RuntimeEffect::SetDoorState { door, open } => {
                self.doors.insert(door.clone(), *open);
            }
            RuntimeEffect::SendMessage { text, .. } => {
                self.messages.push(text.clone());
            }
            RuntimeEffect::GiveGold { amount, .. } => {
                self.gold += i64::from(*amount);
            }
            RuntimeEffect::AddInventory { item } => {
                self.inventory.insert(item.clone());
            }
            RuntimeEffect::MarkDead { object } => {
                self.dead.insert(*object);
            }
            RuntimeEffect::StartConversation {
                target,
                dialogue,
                private,
                play_hello,
            } => {
                self.pending_conversation = Some(PendingConversation {
                    target: *target,
                    dialogue: dialogue.clone(),
                    private: *private,
                    play_hello: *play_hello,
                });
            }
            RuntimeEffect::EndConversation => {
                self.pending_conversation = None;
            }
        }
        let receipt = format!("{}:{}", self.next_effect_sequence, effect.summary());
        self.effects.push(AppliedEffect {
            sequence: self.next_effect_sequence,
            script: script.to_owned(),
            receipt,
            effect,
        });
        self.next_effect_sequence += 1;
    }

    pub fn queue_action(&mut self, subject: u32, delay_ticks: u64, action: ActionValue) -> u64 {
        let id = self.next_action_id;
        self.next_action_id = self.next_action_id.saturating_add(1);
        self.queued_actions.push(QueuedAction {
            id,
            subject,
            due_tick: self.tick.saturating_add(delay_ticks),
            action,
        });
        id
    }

    /// Advance the deterministic action clock and execute every due action.
    /// Actions are ordered by `(due_tick, id)` so a resume is stable.
    pub fn advance_actions(
        &mut self,
        ticks: u64,
        host: &mut dyn ScriptHost,
    ) -> Result<Vec<ActionValue>, VmError> {
        self.tick = self.tick.saturating_add(ticks);
        let mut due = self
            .queued_actions
            .iter()
            .filter(|action| action.due_tick <= self.tick)
            .map(|action| (action.due_tick, action.id))
            .collect::<Vec<_>>();
        due.sort_unstable();
        let mut executed = Vec::new();
        for key in due {
            let Some(position) = self
                .queued_actions
                .iter()
                .position(|action| action.id == key.1)
            else {
                continue;
            };
            let queued = self.queued_actions.remove(position);
            match &queued.action {
                ActionValue::StartConversation {
                    target,
                    dialogue,
                    private,
                    play_hello,
                } => {
                    self.apply_effects(
                        "action-queue",
                        [RuntimeEffect::StartConversation {
                            target: *target,
                            dialogue: dialogue.clone(),
                            private: *private,
                            play_hello: *play_hello,
                        }],
                    )
                    .map_err(|error| VmError::EffectRefused(error.to_string()))?;
                }
                ActionValue::ExecuteScript { resref, target } => {
                    let context = VmContext {
                        self_object: *target,
                        entering: None,
                        last_used_by: None,
                        first_pc: PLAYER_OBJECT,
                    };
                    host.execute_script(resref, context, self)?;
                }
            }
            executed.push(queued.action);
        }
        Ok(executed)
    }
}

pub trait ScriptHost {
    fn execute_script(
        &mut self,
        resref: &str,
        context: VmContext,
        state: &mut RuntimeState,
    ) -> Result<Option<NcsValue>, VmError>;
}

#[derive(Debug, Default)]
pub struct NoopScriptHost;

impl ScriptHost for NoopScriptHost {
    fn execute_script(
        &mut self,
        resref: &str,
        _context: VmContext,
        state: &mut RuntimeState,
    ) -> Result<Option<NcsValue>, VmError> {
        state.unsupported.push(UnsupportedDiagnostic {
            script: resref.to_owned(),
            code: "unsupported-host".to_owned(),
            detail: "ExecuteScript requires an explicit script host".to_owned(),
        });
        Err(VmError::UnsupportedNative {
            routine: 8,
            arguments: 2,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VmContinuation {
    pub instruction_index: usize,
    pub stack: Vec<NcsValue>,
    pub calls: Vec<usize>,
    pub steps: usize,
}

#[derive(Debug, Clone, PartialEq)]
pub struct VmRun {
    pub outcome: Option<VmOutcome>,
    pub continuation: Option<VmContinuation>,
    pub steps: usize,
}

pub fn execute(
    program: &NcsProgram,
    context: VmContext,
    state: &mut RuntimeState,
) -> Result<VmOutcome, VmError> {
    let mut host = NoopScriptHost;
    let run = run_internal(program, context, state, None, MAX_STEPS, &mut host)?;
    run.outcome.ok_or(VmError::BudgetExceeded)
}

pub fn execute_with_host(
    program: &NcsProgram,
    context: VmContext,
    state: &mut RuntimeState,
    host: &mut dyn ScriptHost,
) -> Result<VmOutcome, VmError> {
    let run = run_internal(program, context, state, None, MAX_STEPS, host)?;
    run.outcome.ok_or(VmError::BudgetExceeded)
}

pub fn execute_limited(
    program: &NcsProgram,
    context: VmContext,
    state: &mut RuntimeState,
    step_budget: usize,
) -> Result<VmRun, VmError> {
    let mut host = NoopScriptHost;
    run_internal(program, context, state, None, step_budget, &mut host)
}

pub fn resume(
    program: &NcsProgram,
    context: VmContext,
    state: &mut RuntimeState,
    continuation: VmContinuation,
    step_budget: usize,
) -> Result<VmRun, VmError> {
    let mut host = NoopScriptHost;
    run_internal(
        program,
        context,
        state,
        Some(continuation),
        step_budget,
        &mut host,
    )
}

#[allow(clippy::too_many_lines)]
fn run_internal(
    program: &NcsProgram,
    context: VmContext,
    state: &mut RuntimeState,
    continuation: Option<VmContinuation>,
    step_budget: usize,
    host: &mut dyn ScriptHost,
) -> Result<VmRun, VmError> {
    let (mut index, mut stack, mut calls, mut steps) = match continuation {
        Some(continuation) => {
            if continuation.instruction_index > program.instructions.len()
                || continuation.stack.len() > MAX_STACK_VALUES
                || continuation.calls.len() > MAX_STACK_VALUES
            {
                return Err(VmError::InvalidStack {
                    offset: continuation.instruction_index,
                    detail: "invalid VM continuation bounds".to_owned(),
                });
            }
            (
                continuation.instruction_index,
                continuation.stack,
                continuation.calls,
                continuation.steps,
            )
        }
        None => (0usize, Vec::new(), Vec::new(), 0usize),
    };
    let start_steps = steps;
    let offsets = program
        .instructions
        .iter()
        .enumerate()
        .map(|(index, record)| (record.offset, index))
        .collect::<BTreeMap<_, _>>();
    while index < program.instructions.len() {
        if steps.saturating_sub(start_steps) >= step_budget {
            return Ok(VmRun {
                outcome: None,
                continuation: Some(VmContinuation {
                    instruction_index: index,
                    stack,
                    calls,
                    steps,
                }),
                steps,
            });
        }
        steps += 1;
        if steps > MAX_STEPS {
            return Err(VmError::BudgetExceeded);
        }
        let record = &program.instructions[index];
        let offset = record.offset;
        match &record.instruction {
            NcsInstruction::CopyDownSp {
                offset: delta,
                size,
            } => {
                if *size != 4 {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: format!("unsupported copy size {size}"),
                    });
                }
                let Some(top) = stack.last().cloned() else {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: "copy down from an empty stack".to_owned(),
                    });
                };
                let destination = stack_index(stack.len(), *delta, offset, "destination")?;
                if destination >= stack.len() {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: format!("destination {destination} outside the stack"),
                    });
                }
                stack[destination] = top;
            }
            NcsInstruction::Reserve { type_code } => {
                if stack.len() >= MAX_STACK_VALUES {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: "stack limit exceeded".to_owned(),
                    });
                }
                stack.push(default_value(*type_code));
            }
            NcsInstruction::CopyTopSp {
                offset: delta,
                size,
            } => {
                if *size != 4 {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: format!("unsupported copy size {size}"),
                    });
                }
                let source = stack_index(stack.len(), *delta, offset, "source")?;
                let Some(value) = stack.get(source).cloned() else {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: format!("source {source} outside the stack"),
                    });
                };
                if stack.len() >= MAX_STACK_VALUES {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: "stack limit exceeded".to_owned(),
                    });
                }
                stack.push(value);
            }
            NcsInstruction::ConstInt(value) => stack.push(NcsValue::Int(*value)),
            NcsInstruction::ConstFloat(value) => stack.push(NcsValue::Float(*value)),
            NcsInstruction::ConstString(value) => stack.push(NcsValue::String(value.clone())),
            NcsInstruction::ConstObject(value) => stack.push(NcsValue::Object(*value)),
            NcsInstruction::Action { routine, arguments } => {
                let count = usize::from(*arguments);
                if stack.len() < count {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: "action arguments exceed the stack".to_owned(),
                    });
                }
                let start = stack.len() - count;
                let mut arguments = stack.split_off(start);
                arguments.reverse();
                if let Some(value) =
                    call_native(*routine, &arguments, context, state, &program.resref, host)?
                {
                    stack.push(value);
                }
            }
            NcsInstruction::LogAnd => {
                let b = pop_int(&mut stack, index)?;
                let a = pop_int(&mut stack, index)?;
                stack.push(NcsValue::Int(i32::from(a != 0 && b != 0)));
            }
            NcsInstruction::Equal => {
                let b = pop(&mut stack, index)?;
                let a = pop(&mut stack, index)?;
                let equal = match (&a, &b) {
                    (NcsValue::Int(a), NcsValue::Int(b)) => a == b,
                    (NcsValue::Float(a), NcsValue::Float(b)) => {
                        a.partial_cmp(b) == Some(std::cmp::Ordering::Equal)
                    }
                    (NcsValue::Object(a), NcsValue::Object(b)) => a == b,
                    (NcsValue::String(a), NcsValue::String(b)) => a == b,
                    (NcsValue::Action(a), NcsValue::Action(b)) => a == b,
                    _ => false,
                };
                stack.push(NcsValue::Int(i32::from(equal)));
            }
            NcsInstruction::MoveSp(delta) => {
                let next = stack_index(stack.len(), *delta, offset, "stack pointer")?;
                if next > stack.len() {
                    return Err(VmError::InvalidStack {
                        offset,
                        detail: format!("stack growth through MOVSP {delta}"),
                    });
                }
                stack.truncate(next);
            }
            NcsInstruction::Jump(delta) => {
                index = jump_index(&offsets, offset, *delta)?;
            }
            NcsInstruction::JumpSubroutine(delta) => {
                calls.push(index + 1);
                index = jump_index(&offsets, offset, *delta)?;
            }
            NcsInstruction::JumpIfZero(delta) => {
                let value = pop_int(&mut stack, index)?;
                if value == 0 {
                    index = jump_index(&offsets, offset, *delta)?;
                } else {
                    index += 1;
                }
            }
            NcsInstruction::Return => {
                if let Some(return_index) = calls.pop() {
                    index = return_index;
                } else {
                    break;
                }
            }
            NcsInstruction::Unsupported { opcode, .. } => {
                return Err(VmError::UnsupportedInstruction {
                    offset,
                    opcode: *opcode,
                });
            }
        }
        if matches!(
            record.instruction,
            NcsInstruction::Jump(_)
                | NcsInstruction::JumpSubroutine(_)
                | NcsInstruction::JumpIfZero(_)
                | NcsInstruction::Return
        ) {
            continue;
        }
        index += 1;
    }
    state.executed_scripts.push(program.resref.clone());
    let result = stack.last().cloned();
    Ok(VmRun {
        outcome: Some(VmOutcome { result, steps }),
        continuation: None,
        steps,
    })
}

fn stack_index(len: usize, delta: i32, offset: usize, kind: &str) -> Result<usize, VmError> {
    let base = i64::try_from(len).map_err(|_| VmError::InvalidStack {
        offset,
        detail: "stack length does not fit an index".to_owned(),
    })?;
    let target = base + i64::from(delta) / 4;
    usize::try_from(target).map_err(|_| VmError::InvalidStack {
        offset,
        detail: format!("negative {kind} {delta}"),
    })
}

fn default_value(type_code: u8) -> NcsValue {
    match type_code {
        0x04 => NcsValue::Float(0.0),
        0x05 => NcsValue::String(String::new()),
        0x06 => NcsValue::Object(0),
        _ => NcsValue::Int(0),
    }
}

fn pop(stack: &mut Vec<NcsValue>, instruction: usize) -> Result<NcsValue, VmError> {
    stack.pop().ok_or(VmError::TypeMismatch {
        instruction,
        expected: "a non-empty stack",
    })
}

fn pop_int(stack: &mut Vec<NcsValue>, instruction: usize) -> Result<i32, VmError> {
    match pop(stack, instruction)? {
        NcsValue::Int(value) => Ok(value),
        NcsValue::Object(value) => i32::try_from(value).map_err(|_| VmError::TypeMismatch {
            instruction,
            expected: "object identifier fits an integer",
        }),
        _ => Err(VmError::TypeMismatch {
            instruction,
            expected: "integer",
        }),
    }
}

fn jump_index(
    offsets: &BTreeMap<usize, usize>,
    offset: usize,
    delta: i32,
) -> Result<usize, VmError> {
    let base = i64::try_from(offset).map_err(|_| VmError::InvalidJump {
        offset,
        target: delta,
    })?;
    let target = base + i64::from(delta);
    let target = usize::try_from(target).map_err(|_| VmError::InvalidJump {
        offset,
        target: delta,
    })?;
    offsets.get(&target).copied().ok_or(VmError::InvalidJump {
        offset,
        target: delta,
    })
}

#[allow(clippy::too_many_lines)]
fn call_native(
    routine: u16,
    arguments: &[NcsValue],
    context: VmContext,
    state: &mut RuntimeState,
    script: &str,
    host: &mut dyn ScriptHost,
) -> Result<Option<NcsValue>, VmError> {
    let Some(spec) = lookup(routine) else {
        state.unsupported.push(UnsupportedDiagnostic {
            script: script.to_owned(),
            code: "unknown-native".to_owned(),
            detail: format!(
                "routine {routine} with {} arguments is absent from the registry",
                arguments.len()
            ),
        });
        return Err(VmError::UnsupportedNative {
            routine,
            arguments: native_argument_count(arguments),
        });
    };
    if spec.status == NativeStatus::Unsupported {
        state.unsupported.push(UnsupportedDiagnostic {
            script: script.to_owned(),
            code: "unsupported-native".to_owned(),
            detail: format!(
                "{} (routine {routine}) is declared but not implemented",
                spec.name
            ),
        });
        return Err(VmError::UnsupportedNative {
            routine,
            arguments: native_argument_count(arguments),
        });
    }
    if arguments.len() != usize::from(spec.arguments) {
        state.unsupported.push(UnsupportedDiagnostic {
            script: script.to_owned(),
            code: "native-arity".to_owned(),
            detail: format!(
                "{} expects {} arguments but received {}",
                spec.name,
                spec.arguments,
                arguments.len()
            ),
        });
        return Err(VmError::InvalidNativeArguments {
            routine,
            detail: format!(
                "{} expects {} arguments but received {}",
                spec.name,
                spec.arguments,
                arguments.len()
            ),
        });
    }
    *state
        .called_natives
        .entry(spec.name.to_owned())
        .or_insert(0) += 1;
    match routine {
        242 => Ok(Some(NcsValue::Object(MODULE_OBJECT))),
        548 => Ok(Some(NcsValue::Object(context.first_pc))),
        25 => Ok(Some(NcsValue::Object(context.entering.unwrap_or(0)))),
        330 => Ok(Some(NcsValue::Object(context.last_used_by.unwrap_or(0)))),
        217 => {
            let object = object_argument(arguments, 0, routine)?;
            Ok(Some(NcsValue::Int(i32::from(object == context.first_pc))))
        }
        51 => {
            let object = object_argument(arguments, 0, routine)?;
            let name = string_argument(arguments, 1, routine)?;
            Ok(Some(NcsValue::Int(state.local_int(object, name))))
        }
        55 => {
            let object = object_argument(arguments, 0, routine)?;
            let name = string_argument(arguments, 1, routine)?;
            let value = int_argument(arguments, 2, routine)?;
            apply(
                state,
                script,
                RuntimeEffect::SetLocalInt {
                    object,
                    name: name.to_owned(),
                    value,
                },
            )?;
            Ok(None)
        }
        374 => {
            let target = object_argument(arguments, 0, routine)?;
            let text = string_argument(arguments, 1, routine)?;
            apply(
                state,
                script,
                RuntimeEffect::SendMessage {
                    target,
                    text: text.to_owned(),
                },
            )?;
            Ok(None)
        }
        367 => {
            let quest = string_argument(arguments, 0, routine)?;
            let entry = int_argument(arguments, 1, routine)?;
            let target = object_argument(arguments, 2, routine)?;
            let all_party = int_argument(arguments, 3, routine)? != 0;
            let all_players = int_argument(arguments, 4, routine)? != 0;
            let _allow_override = int_argument(arguments, 5, routine)?;
            let entry = u32::try_from(entry).map_err(|_| VmError::InvalidNativeArguments {
                routine,
                detail: "journal entry must be non-negative".to_owned(),
            })?;
            apply(
                state,
                script,
                RuntimeEffect::AddJournalEntry {
                    quest: quest.to_owned(),
                    entry,
                    target,
                    all_party,
                    all_players,
                },
            )?;
            Ok(None)
        }
        322 => {
            let target = object_argument(arguments, 0, routine)?;
            let amount = int_argument(arguments, 1, routine)?;
            apply(state, script, RuntimeEffect::GiveGold { target, amount })?;
            Ok(None)
        }
        204 => {
            let target = object_argument(arguments, 0, routine)?;
            let dialogue = string_argument(arguments, 1, routine)?;
            let private = int_argument(arguments, 2, routine)? != 0;
            let play_hello = int_argument(arguments, 3, routine)? != 0;
            apply(
                state,
                script,
                RuntimeEffect::StartConversation {
                    target,
                    dialogue: dialogue.to_owned(),
                    private,
                    play_hello,
                },
            )?;
            Ok(Some(NcsValue::Action(ActionValue::StartConversation {
                target,
                dialogue: dialogue.to_owned(),
                private,
                play_hello,
            })))
        }
        6 => {
            let subject = object_argument(arguments, 0, routine)?;
            let action = action_argument(arguments, 1, routine)?;
            state.queue_action(subject, 0, action);
            Ok(None)
        }
        7 => {
            let delay = float_argument(arguments, 0, routine)?;
            let action = action_argument(arguments, 1, routine)?;
            state.queue_action(context.self_object, delay_ticks(delay), action);
            Ok(None)
        }
        294 => {
            let action = action_argument(arguments, 0, routine)?;
            state.queue_action(context.self_object, 0, action);
            Ok(None)
        }
        8 => {
            let resref = string_argument(arguments, 0, routine)?;
            let target = object_argument(arguments, 1, routine)?;
            let target_context = VmContext {
                self_object: target,
                entering: None,
                last_used_by: None,
                first_pc: context.first_pc,
            };
            host.execute_script(resref, target_context, state)
        }
        _ => {
            state.unsupported.push(UnsupportedDiagnostic {
                script: script.to_owned(),
                code: "registry-mismatch".to_owned(),
                detail: format!("routine {routine} is registered but has no implementation"),
            });
            Err(VmError::UnsupportedNative {
                routine,
                arguments: native_argument_count(arguments),
            })
        }
    }
}

fn native_argument_count(arguments: &[NcsValue]) -> u8 {
    u8::try_from(arguments.len()).unwrap_or(u8::MAX)
}

fn action_argument(
    arguments: &[NcsValue],
    index: usize,
    routine: u16,
) -> Result<ActionValue, VmError> {
    match arguments.get(index) {
        Some(NcsValue::Action(value)) => Ok(value.clone()),
        _ => Err(VmError::InvalidNativeArguments {
            routine,
            detail: format!("argument {index} must be an action"),
        }),
    }
}

#[allow(clippy::cast_precision_loss)]
fn float_argument(arguments: &[NcsValue], index: usize, routine: u16) -> Result<f32, VmError> {
    match arguments.get(index) {
        Some(NcsValue::Float(value)) => Ok(*value),
        Some(NcsValue::Int(value)) => Ok(*value as f32),
        _ => Err(VmError::InvalidNativeArguments {
            routine,
            detail: format!("argument {index} must be a float"),
        }),
    }
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn delay_ticks(delay_seconds: f32) -> u64 {
    if !delay_seconds.is_finite() || delay_seconds <= 0.0 {
        0
    } else {
        (delay_seconds.ceil() as u64).max(1)
    }
}

fn apply(state: &mut RuntimeState, script: &str, effect: RuntimeEffect) -> Result<(), VmError> {
    state
        .apply_effects(script, [effect])
        .map_err(|error| VmError::EffectRefused(error.to_string()))
}

fn object_argument(arguments: &[NcsValue], index: usize, routine: u16) -> Result<u32, VmError> {
    match arguments.get(index) {
        Some(NcsValue::Object(value)) => Ok(*value),
        _ => Err(VmError::InvalidNativeArguments {
            routine,
            detail: format!("argument {index} must be an object"),
        }),
    }
}

fn int_argument(arguments: &[NcsValue], index: usize, routine: u16) -> Result<i32, VmError> {
    match arguments.get(index) {
        Some(NcsValue::Int(value)) => Ok(*value),
        Some(NcsValue::Object(value)) => {
            i32::try_from(*value).map_err(|_| VmError::InvalidNativeArguments {
                routine,
                detail: format!("argument {index} object identifier is too large"),
            })
        }
        _ => Err(VmError::InvalidNativeArguments {
            routine,
            detail: format!("argument {index} must be an integer"),
        }),
    }
}

fn string_argument(arguments: &[NcsValue], index: usize, routine: u16) -> Result<&str, VmError> {
    match arguments.get(index) {
        Some(NcsValue::String(value)) => Ok(value),
        _ => Err(VmError::InvalidNativeArguments {
            routine,
            detail: format!("argument {index} must be a string"),
        }),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn local_state_is_separate_for_module_and_objects() {
        let mut state = RuntimeState::default();
        state.set_local_int(MODULE_OBJECT, "flag", 1);
        state.set_local_int(42, "flag", 7);
        assert_eq!(state.local_int(MODULE_OBJECT, "flag"), 1);
        assert_eq!(state.local_int(42, "flag"), 7);
        assert_eq!(state.local_int(99, "flag"), 0);
    }

    #[test]
    fn denied_effect_batch_leaves_state_untouched_and_emits_no_receipt() {
        let mut state = RuntimeState::default();
        state.effect_policy.deny("giveGold");
        let error = state
            .apply_effects(
                "policy",
                [
                    RuntimeEffect::SetLocalInt {
                        object: MODULE_OBJECT,
                        name: "flag".to_owned(),
                        value: 1,
                    },
                    RuntimeEffect::GiveGold {
                        target: PLAYER_OBJECT,
                        amount: 50,
                    },
                ],
            )
            .expect_err("denied");
        assert_eq!(error, EffectError::Denied("giveGold".to_owned()));
        assert_eq!(state.gold, 0);
        assert_eq!(state.local_int(MODULE_OBJECT, "flag"), 0);
        assert!(state.effects.is_empty());
    }

    #[test]
    fn quest_status_and_door_state_are_persisted_with_receipts() {
        let mut state = RuntimeState::default();
        state
            .apply_effects(
                "quest",
                [RuntimeEffect::SetQuestStatus {
                    quest: "ab_quest".to_owned(),
                    status: QuestStatus::Failed,
                }],
            )
            .expect("quest");
        state
            .apply_effects(
                "door",
                [RuntimeEffect::SetDoorState {
                    door: "AB_GATE".to_owned(),
                    open: true,
                }],
            )
            .expect("door");
        assert_eq!(
            state.quest_statuses.get("ab_quest"),
            Some(&QuestStatus::Failed)
        );
        assert_eq!(state.doors.get("AB_GATE"), Some(&true));
        assert_eq!(state.effects.len(), 2);
        assert!(
            state.effects[0]
                .receipt
                .ends_with("SetQuestStatus(ab_quest, failed)")
        );

        let json = serde_json::to_string(&state).expect("json");
        let restored: RuntimeState = serde_json::from_str(&json).expect("restore");
        assert_eq!(
            restored.quest_statuses.get("ab_quest"),
            Some(&QuestStatus::Failed)
        );
        assert_eq!(restored.doors.get("AB_GATE"), Some(&true));
    }

    #[test]
    fn unsupported_native_is_explicit() {
        let mut state = RuntimeState::default();
        let mut host = NoopScriptHost;
        let error = call_native(
            999,
            &[],
            VmContext::self_only(MODULE_OBJECT),
            &mut state,
            "test_script",
            &mut host,
        )
        .expect_err("unsupported native");
        assert!(matches!(
            error,
            VmError::UnsupportedNative { routine: 999, .. }
        ));
        assert_eq!(state.unsupported.len(), 1);
        assert_eq!(state.unsupported[0].code, "unknown-native");
    }

    #[test]
    fn delay_command_queues_an_action_until_its_tick() {
        let mut state = RuntimeState::default();
        let mut host = NoopScriptHost;
        let action = ActionValue::StartConversation {
            target: PLAYER_OBJECT,
            dialogue: "ab_guide_dlg".to_owned(),
            private: false,
            play_hello: false,
        };
        call_native(
            7,
            &[NcsValue::Float(2.0), NcsValue::Action(action)],
            VmContext::self_only(MODULE_OBJECT),
            &mut state,
            "test_script",
            &mut host,
        )
        .expect("delay");
        assert_eq!(state.queued_actions.len(), 1);
        assert_eq!(state.queued_actions[0].due_tick, 2);
        state.advance_actions(1, &mut host).expect("tick 1");
        assert!(state.pending_conversation.is_none());
        state.advance_actions(1, &mut host).expect("tick 2");
        assert_eq!(
            state
                .pending_conversation
                .as_ref()
                .map(|pending| pending.dialogue.as_str()),
            Some("ab_guide_dlg")
        );
    }

    #[test]
    fn vm_continuation_survives_json_roundtrip() {
        let mut bytes = b"NCS V1.0".to_vec();
        bytes.push(0x42);
        bytes.extend_from_slice(&21_u32.to_be_bytes());
        bytes.extend_from_slice(&[0x04, 0x03, 0x00, 0x00, 0x00, 0x07, 0x20, 0x00]);
        let program = NcsProgram::parse("roundtrip", &bytes).expect("program");
        let context = VmContext::self_only(MODULE_OBJECT);
        let mut state = RuntimeState::default();
        let first = execute_limited(&program, context, &mut state, 1).expect("first slice");
        assert!(first.outcome.is_none());
        let continuation = first.continuation.expect("continuation");
        let json = serde_json::to_string(&continuation).expect("json");
        let restored: VmContinuation = serde_json::from_str(&json).expect("restore");
        let mut resumed_state = state.clone();
        let run = resume(&program, context, &mut resumed_state, restored, 10).expect("resume");
        let outcome = run.outcome.expect("outcome");
        assert_eq!(outcome.result, Some(NcsValue::Int(7)));
        assert_eq!(outcome.steps, 2);
    }

    #[test]
    fn execute_script_uses_the_explicit_host() {
        struct TestHost;
        impl ScriptHost for TestHost {
            fn execute_script(
                &mut self,
                resref: &str,
                _context: VmContext,
                state: &mut RuntimeState,
            ) -> Result<Option<NcsValue>, VmError> {
                state.messages.push(format!("host:{resref}"));
                Ok(Some(NcsValue::Int(1)))
            }
        }
        let mut state = RuntimeState::default();
        let mut host = TestHost;
        let value = call_native(
            8,
            &[
                NcsValue::String("ab_relic_trigger".to_owned()),
                NcsValue::Object(PLAYER_OBJECT),
            ],
            VmContext::self_only(MODULE_OBJECT),
            &mut state,
            "test_script",
            &mut host,
        )
        .expect("execute script");
        assert_eq!(value, Some(NcsValue::Int(1)));
        assert_eq!(state.messages, vec!["host:ab_relic_trigger".to_owned()]);
    }
}
