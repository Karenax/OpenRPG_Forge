use crate::gate1_gameplay_stutter::qpc_clock;
use bevy::prelude::Resource;
use serde::Serialize;
use std::{
    collections::{HashMap, HashSet},
    fmt::Write as _,
    fs,
    hash::{Hash, Hasher},
    io::Write as _,
    path::{Path, PathBuf},
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, AtomicU64, Ordering},
    },
};
use tracing::{
    Level, Subscriber,
    field::{Field, Visit},
    span::{Attributes, Id},
    subscriber::Interest,
};
use tracing_subscriber::{
    Layer, Registry,
    filter::{DynFilterFn, FilterFn, LevelFilter},
    layer::{Context, SubscriberExt as _},
    registry::LookupSpan,
    util::SubscriberInitExt as _,
};

pub(crate) const OUTPUT_FILE: &str = "gate1-gameplay-stutter.presentation-profile.json";
const SCHEMA: &str = "forge.gate1-gameplay-stutter-presentation-profile";
const SCHEMA_VERSION: u32 = 1;
const EVENT_CAPACITY: usize = 262_144;
const SPAN_ALIAS_CAPACITY: usize = 128;
const LIMITATION: &str = "application tracing spans only; these data must not be interpreted as DisplayedTime or screen FPS";

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
enum ExactSpan {
    MainRenderSchedule,
    PrepareWindows,
    Render,
    PresentFrames,
    SurfaceAcquire,
    SurfacePresent,
    Dx12Present,
    VulkanPresent,
}

impl ExactSpan {
    const ALL: [Self; 8] = [
        Self::MainRenderSchedule,
        Self::PrepareWindows,
        Self::Render,
        Self::PresentFrames,
        Self::SurfaceAcquire,
        Self::SurfacePresent,
        Self::Dx12Present,
        Self::VulkanPresent,
    ];

    const fn name(self) -> &'static str {
        match self {
            Self::MainRenderSchedule => "main_render_schedule",
            Self::PrepareWindows => "prepare_windows",
            Self::Render => "Render",
            Self::PresentFrames => "present_frames",
            Self::SurfaceAcquire => "Surface::get_current_texture",
            Self::SurfacePresent => "Surface::present",
            Self::Dx12Present => "IDXGISwapchain3::Present",
            Self::VulkanPresent => "vkQueuePresentKHR",
        }
    }

    fn from_name(name: &str) -> Option<Self> {
        Self::ALL.into_iter().find(|span| span.name() == name)
    }

    const fn index(self) -> usize {
        match self {
            Self::MainRenderSchedule => 0,
            Self::PrepareWindows => 1,
            Self::Render => 2,
            Self::PresentFrames => 3,
            Self::SurfaceAcquire => 4,
            Self::SurfacePresent => 5,
            Self::Dx12Present => 6,
            Self::VulkanPresent => 7,
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum Edge {
    Enter,
    Exit,
}

#[derive(Clone, Copy, Debug)]
struct RawEvent {
    sequence: u64,
    span: ExactSpan,
    edge: Edge,
    span_id: u64,
    qpc: u64,
    thread_token: u64,
}

struct CaptureState {
    active: bool,
    started: bool,
    measurement_start_qpc: u64,
    qpc_frequency_hz: u64,
    next_sequence: u64,
    event_capacity: usize,
    overflow_event_count: u64,
    events: Vec<RawEvent>,
}

impl CaptureState {
    fn new(event_capacity: usize) -> Self {
        Self {
            active: false,
            started: false,
            measurement_start_qpc: 0,
            qpc_frequency_hz: 0,
            next_sequence: 0,
            event_capacity,
            overflow_event_count: 0,
            events: Vec::with_capacity(event_capacity),
        }
    }

    fn begin(&mut self, start_qpc: u64, frequency_hz: u64) -> Result<(), String> {
        if self.active || self.started {
            return Err("presentation-profile measurement was already started".to_owned());
        }
        if start_qpc == 0 || frequency_hz == 0 {
            return Err("presentation-profile requires positive QPC boundaries".to_owned());
        }
        self.events.clear();
        self.measurement_start_qpc = start_qpc;
        self.qpc_frequency_hz = frequency_hz;
        self.next_sequence = 0;
        self.overflow_event_count = 0;
        self.started = true;
        self.active = true;
        Ok(())
    }

    fn record(&mut self, span: ExactSpan, edge: Edge, span_id: u64, qpc: u64, thread: u64) {
        if !self.active {
            return;
        }
        if self.events.len() >= self.event_capacity {
            self.overflow_event_count = self.overflow_event_count.saturating_add(1);
            return;
        }
        self.events.push(RawEvent {
            sequence: self.next_sequence,
            span,
            edge,
            span_id,
            qpc,
            thread_token: thread,
        });
        self.next_sequence = self.next_sequence.saturating_add(1);
    }
}

struct Recorder {
    output_path: Option<PathBuf>,
    active: AtomicBool,
    prepare_windows_discovered: AtomicBool,
    qpc_error_count: AtomicU64,
    lock_failure_count: AtomicU64,
    state: Mutex<CaptureState>,
    aliases: Mutex<SpanAliases>,
}

struct SpanAliases {
    overflow_count: u64,
    entries: HashMap<u64, ExactSpan>,
}

impl SpanAliases {
    fn new() -> Self {
        Self {
            overflow_count: 0,
            entries: HashMap::with_capacity(SPAN_ALIAS_CAPACITY),
        }
    }
}

#[derive(Clone, Resource)]
pub(crate) struct PresentationProfileController {
    recorder: Arc<Recorder>,
}

impl PresentationProfileController {
    pub(crate) fn disabled() -> Self {
        Self::with_output(None)
    }

    pub(crate) fn enabled(output_dir: &Path) -> Self {
        Self::with_output(Some(output_dir.join(OUTPUT_FILE)))
    }

    fn with_output(output_path: Option<PathBuf>) -> Self {
        Self {
            recorder: Arc::new(Recorder {
                output_path,
                active: AtomicBool::new(false),
                prepare_windows_discovered: AtomicBool::new(false),
                qpc_error_count: AtomicU64::new(0),
                lock_failure_count: AtomicU64::new(0),
                state: Mutex::new(CaptureState::new(EVENT_CAPACITY)),
                aliases: Mutex::new(SpanAliases::new()),
            }),
        }
    }

    pub(crate) fn is_enabled(&self) -> bool {
        self.recorder.output_path.is_some()
    }

    pub(crate) fn start(&self, start_qpc: u64, frequency_hz: u64) -> Result<(), String> {
        if !self.is_enabled() {
            return Err("presentation-profile controller is disabled".to_owned());
        }
        if !self
            .recorder
            .prepare_windows_discovered
            .load(Ordering::Acquire)
        {
            return Err(
                "presentation-profile prepare_windows span was not discovered during app setup"
                    .to_owned(),
            );
        }
        let mut state = self
            .recorder
            .state
            .lock()
            .map_err(|_| "presentation-profile capture lock is poisoned".to_owned())?;
        state.begin(start_qpc, frequency_hz)?;
        self.recorder.qpc_error_count.store(0, Ordering::Relaxed);
        self.recorder.lock_failure_count.store(0, Ordering::Relaxed);
        self.recorder
            .aliases
            .lock()
            .map_err(|_| "presentation-profile alias lock is poisoned".to_owned())?
            .overflow_count = 0;
        self.recorder.active.store(true, Ordering::Release);
        Ok(())
    }

    pub(crate) fn finish_and_write(&self, end_qpc: u64, backend: &str) -> Result<(), String> {
        if !self.recorder.active.swap(false, Ordering::AcqRel) {
            return Err("presentation-profile measurement was not active".to_owned());
        }
        let alias_overflow_count = self
            .recorder
            .aliases
            .lock()
            .map_err(|_| "presentation-profile alias lock is poisoned".to_owned())?
            .overflow_count;
        let snapshot = {
            let mut state = self
                .recorder
                .state
                .lock()
                .map_err(|_| "presentation-profile capture lock is poisoned".to_owned())?;
            state.active = false;
            if end_qpc <= state.measurement_start_qpc {
                return Err(format!(
                    "presentation-profile QPC end {end_qpc} does not follow start {}",
                    state.measurement_start_qpc
                ));
            }
            CaptureSnapshot {
                measurement_start_qpc: state.measurement_start_qpc,
                measurement_end_qpc: end_qpc,
                qpc_frequency_hz: state.qpc_frequency_hz,
                event_capacity: state.event_capacity,
                overflow_event_count: state.overflow_event_count,
                qpc_error_count: self.recorder.qpc_error_count.load(Ordering::Relaxed),
                lock_failure_count: self.recorder.lock_failure_count.load(Ordering::Relaxed),
                alias_overflow_count,
                events: state.events.clone(),
            }
        };
        let output_path = self
            .recorder
            .output_path
            .as_deref()
            .ok_or_else(|| "presentation-profile output path is absent".to_owned())?;
        let report = analyze(snapshot, backend);
        write_json_atomic(output_path, &report)
    }

    fn record(&self, span: ExactSpan, edge: Edge, span_id: u64) {
        if !self.recorder.active.load(Ordering::Acquire) {
            return;
        }
        let Ok(qpc) = qpc_clock::timestamp() else {
            self.recorder
                .qpc_error_count
                .fetch_add(1, Ordering::Relaxed);
            return;
        };
        let thread = thread_token();
        let Ok(mut state) = self.recorder.state.lock() else {
            self.recorder
                .lock_failure_count
                .fetch_add(1, Ordering::Relaxed);
            return;
        };
        state.record(span, edge, span_id, qpc, thread);
    }

    fn register_alias(&self, span_id: u64, exact_span: ExactSpan) {
        let Ok(mut aliases) = self.recorder.aliases.lock() else {
            self.recorder
                .lock_failure_count
                .fetch_add(1, Ordering::Relaxed);
            return;
        };
        if !aliases.entries.contains_key(&span_id) && aliases.entries.len() >= SPAN_ALIAS_CAPACITY {
            aliases.overflow_count = aliases.overflow_count.saturating_add(1);
            return;
        }
        aliases.entries.insert(span_id, exact_span);
    }

    fn alias(&self, span_id: u64) -> Option<ExactSpan> {
        let Ok(aliases) = self.recorder.aliases.lock() else {
            self.recorder
                .lock_failure_count
                .fetch_add(1, Ordering::Relaxed);
            return None;
        };
        aliases.entries.get(&span_id).copied()
    }

    fn remove_alias(&self, span_id: u64) {
        let Ok(mut aliases) = self.recorder.aliases.lock() else {
            self.recorder
                .lock_failure_count
                .fetch_add(1, Ordering::Relaxed);
            return;
        };
        aliases.entries.remove(&span_id);
    }

    fn is_recording(&self) -> bool {
        self.recorder.active.load(Ordering::Acquire)
    }

    fn mark_prepare_windows_discovered(&self) {
        self.recorder
            .prepare_windows_discovered
            .store(true, Ordering::Release);
    }

    fn profile_metadata_enabled(&self, metadata: &tracing::Metadata<'_>) -> bool {
        if !self.is_enabled() || !exact_span_metadata(metadata) {
            return false;
        }
        if self.is_recording() {
            return true;
        }
        metadata.name() == "system"
            && !self
                .recorder
                .prepare_windows_discovered
                .load(Ordering::Acquire)
    }

    fn layer(&self) -> PresentationProfileLayer {
        PresentationProfileLayer {
            controller: self.clone(),
        }
    }
}

fn thread_token() -> u64 {
    let mut hasher = std::collections::hash_map::DefaultHasher::new();
    std::thread::current().id().hash(&mut hasher);
    hasher.finish()
}

struct PresentationProfileLayer {
    controller: PresentationProfileController,
}

impl<S> Layer<S> for PresentationProfileLayer
where
    S: Subscriber + for<'lookup> LookupSpan<'lookup>,
{
    fn on_new_span(&self, attributes: &Attributes<'_>, id: &Id, context: Context<'_, S>) {
        let direct = ExactSpan::from_name(attributes.metadata().name());
        let mut visitor = BevySpanNameVisitor::default();
        attributes.record(&mut visitor);
        let Some(exact_span) = direct.or(visitor.exact_span) else {
            return;
        };
        match exact_span {
            ExactSpan::Render => self.controller.register_alias(id.into_u64(), exact_span),
            ExactSpan::PrepareWindows => {
                if let Some(span) = context.span(id) {
                    span.extensions_mut().insert(exact_span);
                    self.controller.mark_prepare_windows_discovered();
                }
            }
            _ => {}
        }
    }

    fn on_enter(&self, id: &Id, context: Context<'_, S>) {
        self.record(id, &context, Edge::Enter);
    }

    fn on_exit(&self, id: &Id, context: Context<'_, S>) {
        self.record(id, &context, Edge::Exit);
    }

    fn on_close(&self, id: Id, _context: Context<'_, S>) {
        self.controller.remove_alias(id.into_u64());
    }
}

impl PresentationProfileLayer {
    fn record<S>(&self, id: &Id, context: &Context<'_, S>, edge: Edge)
    where
        S: Subscriber + for<'lookup> LookupSpan<'lookup>,
    {
        if !self.controller.is_recording() {
            return;
        }
        let metadata = context.metadata(id);
        let direct = metadata.and_then(|metadata| ExactSpan::from_name(metadata.name()));
        let exact_span = direct.or_else(|| match metadata.map(tracing::Metadata::name) {
            Some("schedule") => self.controller.alias(id.into_u64()),
            Some("system") => context.span(id).and_then(|span| {
                let extensions = span.extensions();
                extensions.get::<ExactSpan>().copied()
            }),
            _ => None,
        });
        let Some(exact_span) = exact_span else {
            return;
        };
        self.controller.record(exact_span, edge, id.into_u64());
    }
}

#[derive(Default)]
struct BevySpanNameVisitor {
    exact_span: Option<ExactSpan>,
}

impl BevySpanNameVisitor {
    fn observe_name(&mut self, value: &str) {
        let normalized = value.trim_matches('"');
        self.exact_span = match normalized {
            "Render" => Some(ExactSpan::Render),
            "prepare_windows" => Some(ExactSpan::PrepareWindows),
            name if name.ends_with("::prepare_windows") => Some(ExactSpan::PrepareWindows),
            _ => self.exact_span,
        };
    }
}

impl Visit for BevySpanNameVisitor {
    fn record_str(&mut self, field: &Field, value: &str) {
        if field.name() == "name" {
            self.observe_name(value);
        }
    }

    fn record_debug(&mut self, field: &Field, value: &dyn std::fmt::Debug) {
        if field.name() == "name" {
            let mut buffer = FixedDebugName::default();
            let _ = write!(&mut buffer, "{value:?}");
            self.observe_name(buffer.as_str());
        }
    }
}

struct FixedDebugName {
    bytes: [u8; 64],
    len: usize,
}

impl Default for FixedDebugName {
    fn default() -> Self {
        Self {
            bytes: [0; 64],
            len: 0,
        }
    }
}

impl FixedDebugName {
    fn as_str(&self) -> &str {
        std::str::from_utf8(&self.bytes[..self.len]).unwrap_or("")
    }
}

impl std::fmt::Write for FixedDebugName {
    fn write_str(&mut self, value: &str) -> std::fmt::Result {
        let remaining = self.bytes.len().saturating_sub(self.len);
        let length = remaining.min(value.len());
        self.bytes[self.len..self.len + length].copy_from_slice(&value.as_bytes()[..length]);
        self.len += length;
        Ok(())
    }
}

fn exact_span_metadata(metadata: &tracing::Metadata<'_>) -> bool {
    ExactSpan::from_name(metadata.name()).is_some()
        || matches!(metadata.name(), "schedule" | "system")
}

pub(crate) fn initialize_tracing(controller: &PresentationProfileController) -> Result<(), String> {
    let dynamic_controller = controller.clone();
    let profile_filter = DynFilterFn::new(move |metadata, _context| {
        dynamic_controller.profile_metadata_enabled(metadata)
    })
    .with_callsite_filter(|metadata| {
        if exact_span_metadata(metadata) {
            Interest::sometimes()
        } else {
            Interest::never()
        }
    });
    let profile_layer = controller.layer().with_filter(profile_filter);
    let event_only_format_filter = FilterFn::new(|metadata: &tracing::Metadata<'_>| {
        metadata.is_event() && metadata.level() <= &Level::INFO
    })
    .with_max_level_hint(LevelFilter::INFO);
    let format_layer = tracing_subscriber::fmt::layer()
        .json()
        .flatten_event(true)
        .with_current_span(false)
        .with_span_list(false)
        .with_target(true)
        .with_thread_ids(true)
        .with_file(true)
        .with_line_number(true)
        .with_writer(std::io::stderr)
        .with_filter(event_only_format_filter);
    Registry::default()
        .with(profile_layer)
        .with(format_layer)
        .try_init()
        .map_err(|error| error.to_string())
}

struct CaptureSnapshot {
    measurement_start_qpc: u64,
    measurement_end_qpc: u64,
    qpc_frequency_hz: u64,
    event_capacity: usize,
    overflow_event_count: u64,
    alias_overflow_count: u64,
    qpc_error_count: u64,
    lock_failure_count: u64,
    events: Vec<RawEvent>,
}

#[derive(Clone, Copy)]
struct OpenSpan {
    span: ExactSpan,
    start_qpc: u64,
    start_thread_token: u64,
}

#[derive(Default)]
struct SpanAccumulator {
    starts_qpc: Vec<u64>,
    ends_qpc: Vec<u64>,
    durations_qpc_ticks: Vec<u64>,
    completed_pair_count: u64,
    missing_start_count: u64,
    missing_end_count: u64,
    duplicate_start_count: u64,
    duplicate_end_count: u64,
    cross_thread_pair_count: u64,
    invalid_nesting_count: u64,
    non_monotonic_pair_count: u64,
}

#[derive(Serialize)]
struct PresentationProfileReport {
    schema: &'static str,
    schema_version: u32,
    qualifying: bool,
    display_timing_observed: bool,
    limitation: &'static str,
    source_commit: Option<&'static str>,
    build_profile: &'static str,
    backend: String,
    instrumentation: InstrumentationReport,
    clock: ClockReport,
    buffer: BufferReport,
    integrity: IntegrityReport,
    spans: Vec<SpanReport>,
}

#[derive(Serialize)]
struct InstrumentationReport {
    format_layer_events_only: bool,
    profile_event_recording_outside_measurement: bool,
    prepare_windows_span_discovered_before_measurement: bool,
}

#[derive(Serialize)]
struct ClockReport {
    source: &'static str,
    frequency_hz: u64,
    measurement_start_qpc: u64,
    measurement_end_qpc: u64,
}

#[derive(Serialize)]
struct BufferReport {
    preallocated_event_capacity: usize,
    preallocated_span_alias_capacity: usize,
    recorded_event_count: usize,
    overflow_event_count: u64,
    span_alias_overflow_count: u64,
    recorder_allocation_or_serialization_per_frame: bool,
}

#[derive(Default, Serialize)]
struct IntegrityReport {
    out_of_window_event_count: u64,
    missing_start_count: u64,
    missing_end_count: u64,
    duplicate_start_count: u64,
    duplicate_end_count: u64,
    cross_thread_pair_count: u64,
    invalid_nesting_count: u64,
    non_monotonic_pair_count: u64,
    span_kinds_without_complete_pair: u64,
    qpc_error_count: u64,
    recorder_lock_failure_count: u64,
}

#[derive(Serialize)]
struct SpanReport {
    name: &'static str,
    start_count: usize,
    end_count: usize,
    completed_pair_count: u64,
    missing_start_count: u64,
    missing_end_count: u64,
    duplicate_start_count: u64,
    duplicate_end_count: u64,
    cross_thread_pair_count: u64,
    invalid_nesting_count: u64,
    non_monotonic_pair_count: u64,
    starts_qpc: Vec<u64>,
    ends_qpc: Vec<u64>,
    start_to_start_interval_qpc_ticks: Vec<u64>,
    duration_qpc_ticks: Vec<u64>,
}

#[allow(
    clippy::too_many_lines,
    reason = "the post-measurement analyzer keeps pairing and integrity accounting together"
)]
fn analyze(mut snapshot: CaptureSnapshot, backend: &str) -> PresentationProfileReport {
    snapshot.events.sort_by_key(|event| event.sequence);
    let recorded_event_count = snapshot.events.len();
    let mut accumulators: Vec<SpanAccumulator> = ExactSpan::ALL
        .into_iter()
        .map(|_| SpanAccumulator::default())
        .collect();
    let mut integrity = IntegrityReport {
        qpc_error_count: snapshot.qpc_error_count,
        recorder_lock_failure_count: snapshot.lock_failure_count,
        ..Default::default()
    };
    let mut open = HashMap::<u64, OpenSpan>::new();
    let mut started_span_ids = HashSet::<u64>::new();
    let mut thread_stacks = HashMap::<u64, Vec<u64>>::new();

    for event in snapshot.events {
        if event.qpc < snapshot.measurement_start_qpc || event.qpc > snapshot.measurement_end_qpc {
            integrity.out_of_window_event_count =
                integrity.out_of_window_event_count.saturating_add(1);
            continue;
        }
        let accumulator = &mut accumulators[event.span.index()];
        match event.edge {
            Edge::Enter => {
                accumulator.starts_qpc.push(event.qpc);
                if open.contains_key(&event.span_id) {
                    accumulator.duplicate_start_count =
                        accumulator.duplicate_start_count.saturating_add(1);
                    continue;
                }
                started_span_ids.insert(event.span_id);
                open.insert(
                    event.span_id,
                    OpenSpan {
                        span: event.span,
                        start_qpc: event.qpc,
                        start_thread_token: event.thread_token,
                    },
                );
                thread_stacks
                    .entry(event.thread_token)
                    .or_default()
                    .push(event.span_id);
            }
            Edge::Exit => {
                accumulator.ends_qpc.push(event.qpc);
                let Some(start) = open.remove(&event.span_id) else {
                    if started_span_ids.contains(&event.span_id) {
                        accumulator.duplicate_end_count =
                            accumulator.duplicate_end_count.saturating_add(1);
                    } else {
                        accumulator.missing_start_count =
                            accumulator.missing_start_count.saturating_add(1);
                    }
                    continue;
                };
                let start_accumulator = &mut accumulators[start.span.index()];
                start_accumulator.completed_pair_count =
                    start_accumulator.completed_pair_count.saturating_add(1);
                if event.qpc >= start.start_qpc {
                    start_accumulator
                        .durations_qpc_ticks
                        .push(event.qpc - start.start_qpc);
                } else {
                    start_accumulator.non_monotonic_pair_count =
                        start_accumulator.non_monotonic_pair_count.saturating_add(1);
                }
                if event.thread_token != start.start_thread_token {
                    start_accumulator.cross_thread_pair_count =
                        start_accumulator.cross_thread_pair_count.saturating_add(1);
                    remove_from_thread_stack(
                        &mut thread_stacks,
                        start.start_thread_token,
                        event.span_id,
                    );
                } else if !pop_nested_span(
                    &mut thread_stacks,
                    start.start_thread_token,
                    event.span_id,
                ) {
                    start_accumulator.invalid_nesting_count =
                        start_accumulator.invalid_nesting_count.saturating_add(1);
                }
            }
        }
    }

    for start in open.into_values() {
        accumulators[start.span.index()].missing_end_count = accumulators[start.span.index()]
            .missing_end_count
            .saturating_add(1);
    }

    let spans = ExactSpan::ALL
        .into_iter()
        .zip(accumulators)
        .map(|(span, accumulator)| {
            let intervals = accumulator
                .starts_qpc
                .windows(2)
                .map(|pair| pair[1].saturating_sub(pair[0]))
                .collect();
            integrity.missing_start_count = integrity
                .missing_start_count
                .saturating_add(accumulator.missing_start_count);
            integrity.missing_end_count = integrity
                .missing_end_count
                .saturating_add(accumulator.missing_end_count);
            integrity.duplicate_start_count = integrity
                .duplicate_start_count
                .saturating_add(accumulator.duplicate_start_count);
            integrity.duplicate_end_count = integrity
                .duplicate_end_count
                .saturating_add(accumulator.duplicate_end_count);
            integrity.cross_thread_pair_count = integrity
                .cross_thread_pair_count
                .saturating_add(accumulator.cross_thread_pair_count);
            integrity.invalid_nesting_count = integrity
                .invalid_nesting_count
                .saturating_add(accumulator.invalid_nesting_count);
            integrity.non_monotonic_pair_count = integrity
                .non_monotonic_pair_count
                .saturating_add(accumulator.non_monotonic_pair_count);
            if accumulator.completed_pair_count == 0 {
                integrity.span_kinds_without_complete_pair =
                    integrity.span_kinds_without_complete_pair.saturating_add(1);
            }
            SpanReport {
                name: span.name(),
                start_count: accumulator.starts_qpc.len(),
                end_count: accumulator.ends_qpc.len(),
                completed_pair_count: accumulator.completed_pair_count,
                missing_start_count: accumulator.missing_start_count,
                missing_end_count: accumulator.missing_end_count,
                duplicate_start_count: accumulator.duplicate_start_count,
                duplicate_end_count: accumulator.duplicate_end_count,
                cross_thread_pair_count: accumulator.cross_thread_pair_count,
                invalid_nesting_count: accumulator.invalid_nesting_count,
                non_monotonic_pair_count: accumulator.non_monotonic_pair_count,
                starts_qpc: accumulator.starts_qpc,
                ends_qpc: accumulator.ends_qpc,
                start_to_start_interval_qpc_ticks: intervals,
                duration_qpc_ticks: accumulator.durations_qpc_ticks,
            }
        })
        .collect();

    PresentationProfileReport {
        schema: SCHEMA,
        schema_version: SCHEMA_VERSION,
        qualifying: false,
        display_timing_observed: false,
        limitation: LIMITATION,
        source_commit: option_env!("FORGE_SOURCE_COMMIT"),
        build_profile: env!("FORGE_BUILD_PROFILE"),
        backend: backend.to_owned(),
        instrumentation: InstrumentationReport {
            format_layer_events_only: true,
            profile_event_recording_outside_measurement: false,
            prepare_windows_span_discovered_before_measurement: true,
        },
        clock: ClockReport {
            source: "windows_query_performance_counter",
            frequency_hz: snapshot.qpc_frequency_hz,
            measurement_start_qpc: snapshot.measurement_start_qpc,
            measurement_end_qpc: snapshot.measurement_end_qpc,
        },
        buffer: BufferReport {
            preallocated_event_capacity: snapshot.event_capacity,
            preallocated_span_alias_capacity: SPAN_ALIAS_CAPACITY,
            recorded_event_count,
            overflow_event_count: snapshot.overflow_event_count,
            span_alias_overflow_count: snapshot.alias_overflow_count,
            recorder_allocation_or_serialization_per_frame: false,
        },
        integrity,
        spans,
    }
}

fn pop_nested_span(stacks: &mut HashMap<u64, Vec<u64>>, thread_token: u64, span_id: u64) -> bool {
    let Some(stack) = stacks.get_mut(&thread_token) else {
        return false;
    };
    if stack.last() == Some(&span_id) {
        stack.pop();
        return true;
    }
    if let Some(position) = stack.iter().rposition(|candidate| *candidate == span_id) {
        stack.remove(position);
    }
    false
}

fn remove_from_thread_stack(stacks: &mut HashMap<u64, Vec<u64>>, thread_token: u64, span_id: u64) {
    let Some(stack) = stacks.get_mut(&thread_token) else {
        return;
    };
    if let Some(position) = stack.iter().rposition(|candidate| *candidate == span_id) {
        stack.remove(position);
    }
}

fn write_json_atomic(path: &Path, report: &PresentationProfileReport) -> Result<(), String> {
    if path.exists() {
        return Err(format!(
            "refusing to replace existing presentation-profile output {}",
            path.display()
        ));
    }
    let file_name = path
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or_else(|| format!("presentation-profile path is not UTF-8: {}", path.display()))?;
    let temporary = path.with_file_name(format!(".{file_name}.tmp"));
    let json = serde_json::to_vec_pretty(report)
        .map_err(|error| format!("cannot serialize presentation-profile JSON: {error}"))?;
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temporary)
        .map_err(|error| format!("cannot create {}: {error}", temporary.display()))?;
    if let Err(error) = file.write_all(&json).and_then(|()| file.sync_all()) {
        drop(file);
        let _ = fs::remove_file(&temporary);
        return Err(format!("cannot write {}: {error}", temporary.display()));
    }
    drop(file);
    if let Err(error) = fs::hard_link(&temporary, path) {
        let _ = fs::remove_file(&temporary);
        return Err(format!(
            "cannot atomically publish {} as {}: {error}",
            temporary.display(),
            path.display()
        ));
    }
    fs::remove_file(&temporary)
        .map_err(|error| format!("cannot remove {}: {error}", temporary.display()))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn event(
        sequence: u64,
        span: ExactSpan,
        edge: Edge,
        span_id: u64,
        qpc: u64,
        thread_token: u64,
    ) -> RawEvent {
        RawEvent {
            sequence,
            span,
            edge,
            span_id,
            qpc,
            thread_token,
        }
    }

    fn report(events: Vec<RawEvent>) -> PresentationProfileReport {
        analyze(
            CaptureSnapshot {
                measurement_start_qpc: 100,
                measurement_end_qpc: 1_000,
                qpc_frequency_hz: 10_000_000,
                event_capacity: 32,
                overflow_event_count: 0,
                alias_overflow_count: 0,
                qpc_error_count: 0,
                lock_failure_count: 0,
                events,
            },
            "Vulkan",
        )
    }

    #[test]
    fn normal_pairs_keep_start_intervals_separate_from_durations() {
        let report = report(vec![
            event(0, ExactSpan::SurfacePresent, Edge::Enter, 1, 110, 7),
            event(1, ExactSpan::SurfacePresent, Edge::Exit, 1, 120, 7),
            event(2, ExactSpan::SurfacePresent, Edge::Enter, 2, 140, 7),
            event(3, ExactSpan::SurfacePresent, Edge::Exit, 2, 155, 7),
        ]);
        let span = &report.spans[ExactSpan::SurfacePresent.index()];
        assert_eq!(span.start_to_start_interval_qpc_ticks, vec![30]);
        assert_eq!(span.duration_qpc_ticks, vec![10, 15]);
        assert_eq!(span.completed_pair_count, 2);
        assert!(!report.qualifying);
        assert!(!report.display_timing_observed);
    }

    #[test]
    fn missing_start_and_end_are_both_reported() {
        let report = report(vec![
            event(0, ExactSpan::SurfaceAcquire, Edge::Enter, 1, 110, 7),
            event(1, ExactSpan::SurfaceAcquire, Edge::Exit, 2, 120, 7),
        ]);
        assert_eq!(report.integrity.missing_start_count, 1);
        assert_eq!(report.integrity.missing_end_count, 1);
    }

    #[test]
    fn duplicate_edges_are_counted_without_inventing_a_second_pair() {
        let report = report(vec![
            event(0, ExactSpan::Dx12Present, Edge::Enter, 1, 110, 7),
            event(1, ExactSpan::Dx12Present, Edge::Enter, 1, 111, 7),
            event(2, ExactSpan::Dx12Present, Edge::Exit, 1, 120, 7),
            event(3, ExactSpan::Dx12Present, Edge::Exit, 1, 121, 7),
        ]);
        let span = &report.spans[ExactSpan::Dx12Present.index()];
        assert_eq!(span.duplicate_start_count, 1);
        assert_eq!(span.duplicate_end_count, 1);
        assert_eq!(span.completed_pair_count, 1);
    }

    #[test]
    fn sequential_reentries_of_the_same_span_id_are_distinct_valid_pairs() {
        let report = report(vec![
            event(0, ExactSpan::Render, Edge::Enter, 1, 110, 7),
            event(1, ExactSpan::Render, Edge::Exit, 1, 120, 7),
            event(2, ExactSpan::Render, Edge::Enter, 1, 140, 7),
            event(3, ExactSpan::Render, Edge::Exit, 1, 155, 7),
        ]);
        let span = &report.spans[ExactSpan::Render.index()];
        assert_eq!(span.completed_pair_count, 2);
        assert_eq!(span.duplicate_start_count, 0);
        assert_eq!(span.duplicate_end_count, 0);
        assert_eq!(span.duration_qpc_ticks, vec![10, 15]);
    }

    #[test]
    fn sequence_order_preserves_non_monotonic_qpc_pairs_for_integrity_reporting() {
        let report = report(vec![
            event(0, ExactSpan::SurfacePresent, Edge::Enter, 1, 120, 7),
            event(1, ExactSpan::SurfacePresent, Edge::Exit, 1, 110, 7),
        ]);
        let span = &report.spans[ExactSpan::SurfacePresent.index()];
        assert_eq!(span.completed_pair_count, 1);
        assert_eq!(span.non_monotonic_pair_count, 1);
        assert!(span.duration_qpc_ticks.is_empty());
        assert_eq!(report.integrity.non_monotonic_pair_count, 1);
    }

    #[test]
    fn measurement_refuses_to_start_before_prepare_windows_discovery() {
        let controller = PresentationProfileController::enabled(Path::new("."));
        assert!(
            controller
                .start(100, 10_000_000)
                .unwrap_err()
                .contains("prepare_windows span was not discovered")
        );
        controller.mark_prepare_windows_discovered();
        controller
            .start(100, 10_000_000)
            .expect("measurement starts after the one-shot discovery");
    }

    #[test]
    fn invalid_same_thread_nesting_is_visible() {
        let report = report(vec![
            event(0, ExactSpan::PresentFrames, Edge::Enter, 1, 110, 7),
            event(1, ExactSpan::SurfacePresent, Edge::Enter, 2, 115, 7),
            event(2, ExactSpan::PresentFrames, Edge::Exit, 1, 120, 7),
            event(3, ExactSpan::SurfacePresent, Edge::Exit, 2, 125, 7),
        ]);
        assert_eq!(report.integrity.invalid_nesting_count, 1);
    }

    #[test]
    fn cross_thread_pair_is_not_silently_treated_as_nested() {
        let report = report(vec![
            event(0, ExactSpan::VulkanPresent, Edge::Enter, 1, 110, 7),
            event(1, ExactSpan::VulkanPresent, Edge::Exit, 1, 120, 8),
        ]);
        assert_eq!(report.integrity.cross_thread_pair_count, 1);
        assert_eq!(report.integrity.invalid_nesting_count, 0);
    }

    #[test]
    fn bounded_buffer_reports_overflow_without_growing() {
        let mut state = CaptureState::new(2);
        state.begin(100, 10_000_000).expect("capture starts");
        let capacity = state.events.capacity();
        state.record(ExactSpan::PresentFrames, Edge::Enter, 1, 110, 7);
        state.record(ExactSpan::PresentFrames, Edge::Exit, 1, 120, 7);
        state.record(ExactSpan::PresentFrames, Edge::Enter, 2, 130, 7);
        assert_eq!(state.events.len(), 2);
        assert_eq!(state.events.capacity(), capacity);
        assert_eq!(state.overflow_event_count, 1);
    }
}
