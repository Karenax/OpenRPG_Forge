#![deny(unsafe_code)]
//! Bounded `NWScript` compatibility runtime for the synthetic Aurora bridge scenario.

pub mod effects;
pub mod m4;
pub mod m5;
pub mod natives;
pub mod ncs;
pub mod scenario;
pub mod scheduler;
pub mod trace;
pub mod vm;

pub use effects::{AppliedEffect, EffectError, EffectPolicy, RuntimeEffect};
pub use m4::{M4Error, M4Report, M4Save, M4Scenario};
pub use m5::{
    M5Diff, M5Divergence, M5Error, M5Migration, M5Report, M5Save, M5SaveMetadata, compare_m5,
    run_m5,
};
pub use natives::{
    NativeSpec, NativeStatus, REGISTRY as NATIVE_REGISTRY,
    REGISTRY_VERSION as NATIVE_REGISTRY_VERSION, lookup as native_lookup,
};
pub use ncs::{NcsError, NcsInstruction, NcsInstructionRecord, NcsProgram};
pub use scenario::{DialogueTurn, M3Error, M3Report, M3Save, M3Scenario, ScenarioEvent};
pub use scheduler::{
    SCHEDULER_SCHEMA, ScheduledExecution, ScheduledJob, SchedulerError, SchedulerSnapshot,
    ScriptScheduler,
};
pub use trace::{NwnTrace, TRACE_SCHEMA, TraceEntry, TraceError};
pub use vm::{
    JournalEvent, MAX_STEPS, MODULE_OBJECT, NcsValue, PLAYER_OBJECT, PendingConversation,
    RuntimeState, UnsupportedDiagnostic, VmContext, VmContinuation, VmError, VmOutcome, VmRun,
    execute, execute_limited, resume,
};
