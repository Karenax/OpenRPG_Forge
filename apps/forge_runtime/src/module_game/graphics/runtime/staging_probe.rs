//! Opt-in GPU qualification of the common staged inventory, without scene publication.
use super::{State, family, scope_token};
use bevy::{diagnostic::FrameCount, prelude::*};
use forge_core::{EntityId, canonical_json_sha256};
use forge_engine::{
    render_inventory::{self, ActivatedScopedInventory, StagedScopedInventory},
    source_terrain::lod::LodMaterial,
};

#[derive(Resource, Default)]
struct Probe {
    phase: Phase,
    deadline: Option<FrameDeadline>,
}

/// A conservative limit in processed render frames, not elapsed wall time.
#[derive(Clone, Copy)]
pub(super) struct FrameDeadline(u32);

impl FrameDeadline {
    pub(super) const fn start(frame: u32) -> Self {
        Self(frame)
    }
    pub(super) fn expired(self, frame: u32) -> bool {
        frame.checked_sub(self.0).is_none_or(|age| age >= 120)
    }
}

#[derive(Default)]
enum Phase {
    #[default]
    Start,
    Waiting(StagedScopedInventory<LodMaterial>),
    LaterFence(Box<ActivatedScopedInventory<LodMaterial>>, u32),
    Done,
}

pub(super) fn observe(world: &mut World, state: &State) {
    if std::env::var_os("FORGE_QUALIFY_STAGED_RENDER_INVENTORY").is_none() {
        return;
    }
    world.init_resource::<Probe>();
    world.resource_scope(|world, mut probe: Mut<Probe>| {
        let phase = std::mem::replace(&mut probe.phase, Phase::Done);
        let frame = world.resource::<FrameCount>().0;
        if matches!(&phase, Phase::Waiting(_) | Phase::LaterFence(..))
            && probe.deadline.is_some_and(|deadline| deadline.expired(frame))
        {
            cancel_timed_out(world, phase, frame);
            probe.deadline = None;
            return;
        }
        let mut started = None;
        let next = match phase {
            Phase::Start => match begin(world, state) {
                Ok(Some(staged)) => { started = Some(frame); Phase::Waiting(staged) },
                Ok(None) => Phase::Start,
                Err(error) => {
                    record("failed", serde_json::json!({"error": error}));
                    Phase::Done
                }
            },
            Phase::Waiting(mut staged) => {
                record(
                    "waiting",
                    serde_json::json!({
                        "frame": world.resource::<FrameCount>().0,
                        "current_live": scope_token(world, state, "terrain").is_some(),
                        "graphics_ready": state.ready,
                        "simulation_ready": world.get_resource::<forge_engine::PlayerSimulationReady>().map(|ready| ready.0),
                    }),
                );
                match staged.try_activate(world) {
                Ok(Some(active)) => {
                    let frame = world.resource::<FrameCount>().0;
                    let current = scope_token(world, state, "terrain").is_some();
                    let evidence = render_inventory::scoped_evidence(world, active.owner);
                    record(
                        "activated",
                        serde_json::json!({
                            "frame": frame,
                            "registration_frame": active.registration_frame,
                            "current_live": current,
                            "observed": evidence.map(|e| e.observed),
                            "completed": evidence.and_then(|e| e.completed),
                            "stable_frames": evidence.map(|e| e.stable_frames),
                            "candidate_meshes": active.meshes.len(),
                            "covers_marker_immediately": active.token.covers_frame(world, frame),
                        }),
                    );
                    Phase::LaterFence(Box::new(active), frame)
                }
                Ok(None) => Phase::Waiting(staged),
                Err(error) => {
                    let cancellation = staged.cancel(world).err();
                    record(
                        "failed",
                        serde_json::json!({"error": error, "cancellation": cancellation}),
                    );
                    Phase::Done
                }
                }
            }
            Phase::LaterFence(active, marker) => {
                let current = scope_token(world, state, "terrain").is_some();
                if !active.request_is_current(world)
                    || !active.previous_request_is_current(world, state.scope_owner("terrain"))
                {
                    let cancellation = active.cancel(world).err();
                    record("failed", serde_json::json!({"error": "Candidate or current request was replaced", "cancellation": cancellation}));
                    Phase::Done
                } else if current && active.token.covers_frame(world, marker) {
                    let evidence = render_inventory::scoped_evidence(world, active.owner);
                    record(
                        "later_fence",
                        serde_json::json!({
                            "frame": world.resource::<FrameCount>().0,
                            "marker": marker,
                            "current_live": current,
                            "observed": evidence.map(|e| e.observed),
                            "completed": evidence.and_then(|e| e.completed),
                            "scene_published_by_probe": false,
                        }),
                    );
                    if let Err(error) = active.cancel(world) {
                        record("failed", serde_json::json!({"error": error}));
                    }
                    Phase::Done
                } else {
                    Phase::LaterFence(active, marker)
                }
            }
            Phase::Done => Phase::Done,
        };
        probe.phase = next;
        if let Some(frame) = started { probe.deadline = Some(FrameDeadline::start(frame)); }
        if matches!(&probe.phase, Phase::Done) { probe.deadline = None; }
    });
}

fn cancel_timed_out(world: &mut World, phase: Phase, frame: u32) {
    let cancellation = match phase {
        Phase::Waiting(mut staged) => staged.cancel(world).err(),
        Phase::LaterFence(active, _) => active.cancel(world).err(),
        _ => None,
    };
    record(
        "failed",
        serde_json::json!({"error": "Probe timed out in processed frames", "frame": frame, "cancellation": cancellation}),
    );
}

fn begin(
    world: &mut World,
    state: &State,
) -> Result<Option<StagedScopedInventory<LodMaterial>>, String> {
    if !state.ready
        || !world.contains_resource::<forge_engine::source_terrain::host::Status>()
        || world
            .get_resource::<forge_engine::gameplay::GameplayRuntime>()
            .is_none_or(|game| game.session().state().tick < 120)
        || scope_token(world, state, "terrain").is_none()
    {
        return Ok(None);
    }
    let Some(current) = family::<LodMaterial>(world)? else {
        return Ok(None);
    };
    if current.meshes.is_empty()
        || current.meshes.len() >= render_inventory::MAX_SCOPED_MESHES
        || current.materials.is_empty()
    {
        return Err("Qualification terrain cannot fit an additional staged mesh".into());
    }
    let mesh = world
        .resource::<Assets<Mesh>>()
        .get(&current.meshes[0])
        .ok_or("Qualification source mesh disappeared")?
        .clone();
    let extra = world.resource_mut::<Assets<Mesh>>().add(mesh);
    let mut meshes = current.meshes;
    meshes.push(extra);
    let identity = canonical_json_sha256(&(state.source, state.terrain, "staged-inventory-probe"))
        .map_err(|error| error.to_string())?;
    let staged = StagedScopedInventory::begin(
        world,
        state.scope_owner("terrain"),
        EntityId::deterministic("qualification/staged-terrain-inventory"),
        identity,
        1,
        &meshes,
        &current.materials,
    )?;
    record(
        "registered",
        serde_json::json!({"frame": world.resource::<FrameCount>().0, "candidate_meshes": meshes.len()}),
    );
    Ok(Some(staged))
}

fn record(status: &str, detail: serde_json::Value) {
    eprintln!(
        "QUALIFY_STAGED_INVENTORY {}",
        serde_json::json!({"status": status, "detail": detail})
    );
}
#[cfg(test)]
mod deadline_tests {
    use super::FrameDeadline;

    #[test]
    fn timeout_is_bounded_and_refuses_a_restarted_or_wrapped_clock() {
        let deadline = FrameDeadline::start(100);
        assert!(!deadline.expired(100));
        assert!(!deadline.expired(219));
        assert!(deadline.expired(220));
        assert!(deadline.expired(99));
        assert!(FrameDeadline::start(u32::MAX - 1).expired(0));
    }
}
