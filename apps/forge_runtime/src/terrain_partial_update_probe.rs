use bevy::{app::AppExit, prelude::*};
use forge_core::{ContentHash, Revision};
use forge_engine::{
    PendingTerrainPartialUpdate, TerrainRuntimeBindingSnapshot, TerrainRuntimeBindings,
    TerrainRuntimeState, TerrainRuntimeUpdateOutcome, TerrainRuntimeUpdateReport,
    TerrainRuntimeUpdateSet,
};
use forge_project::{TerrainDocument, TerrainHeightDelta};
use forge_world::{
    GeneratedTerrain, TERRAIN_CHUNK_COUNT, TerrainGenerationPlan, TerrainHeightSampleCoordinate,
    TerrainPartialUpdateRequest,
};
use std::{mem, time::Instant};
use tracing::{error, info};

const PROBE_SAMPLE_X: u16 = 31;
const PROBE_SAMPLE_Z: u16 = 31;
const PROBE_DELTA_M: f32 = 5.0;
const PROBE_WARMUP_FIXED_TICKS: u32 = 2;
const PROBE_OBSERVATION_FIXED_TICKS: u32 = 2;
const PROBE_TIMEOUT_SECONDS: u64 = 30;

pub(crate) struct TerrainPartialUpdateProbePlugin;

impl TerrainPartialUpdateProbePlugin {
    #[must_use]
    pub(crate) const fn new() -> Self {
        Self
    }
}

impl Plugin for TerrainPartialUpdateProbePlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(TerrainPartialUpdateProbeState {
            started_at: Instant::now(),
            fixed_ticks: 0,
            phase: TerrainPartialUpdateProbePhase::WarmingUp,
        })
        .add_systems(
            FixedUpdate,
            drive_terrain_partial_update_probe.before(TerrainRuntimeUpdateSet::Apply),
        )
        .add_systems(Update, watchdog_terrain_partial_update_probe);
    }
}

#[derive(Resource)]
struct TerrainPartialUpdateProbeState {
    started_at: Instant,
    fixed_ticks: u32,
    phase: TerrainPartialUpdateProbePhase,
}

enum TerrainPartialUpdateProbePhase {
    WarmingUp,
    Submitted(SubmittedProbe),
    Applied {
        submission: Box<SubmittedProbe>,
        report: TerrainRuntimeUpdateReport,
        applied_fixed_tick: u32,
    },
    Completed,
    Failed,
}

struct SubmittedProbe {
    submitted_fixed_tick: u32,
    outcome_processed_before_submit: u64,
    request_sequence: u64,
    source_document_hash: ContentHash,
    target_document_hash: ContentHash,
    expected_cache_hash: ContentHash,
    source_revision: Revision,
    target_revision: Revision,
    expected_cache: GeneratedTerrain,
    bindings_before: Vec<TerrainRuntimeBindingSnapshot>,
}

#[derive(Clone, Copy)]
struct PreservedIdentityCounts {
    render_entities: usize,
    mesh_assets: usize,
    collider_entities: usize,
}

struct VerifiedProbe {
    identities: PreservedIdentityCounts,
    target_cache_hash: ContentHash,
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "the fixed-step probe observes independent runtime resources without owning them"
)]
fn drive_terrain_partial_update_probe(
    runtime: Res<TerrainRuntimeState>,
    bindings: Res<TerrainRuntimeBindings>,
    mut pending: ResMut<PendingTerrainPartialUpdate>,
    outcome: Res<TerrainRuntimeUpdateOutcome>,
    mut probe: ResMut<TerrainPartialUpdateProbeState>,
    mut exit: MessageWriter<AppExit>,
) {
    probe.fixed_ticks = probe.fixed_ticks.saturating_add(1);
    let current_fixed_tick = probe.fixed_ticks;
    let current_phase = mem::replace(&mut probe.phase, TerrainPartialUpdateProbePhase::Failed);
    let was_terminal = matches!(
        &current_phase,
        TerrainPartialUpdateProbePhase::Completed | TerrainPartialUpdateProbePhase::Failed
    );
    let advanced = advance_probe(
        current_phase,
        current_fixed_tick,
        &runtime,
        &bindings,
        &mut pending,
        &outcome,
    );

    let next_phase = match advanced {
        Ok(phase) => phase,
        Err(reason) => {
            fail_probe(current_fixed_tick, &reason, &mut exit);
            TerrainPartialUpdateProbePhase::Failed
        }
    };
    if !was_terminal && matches!(&next_phase, TerrainPartialUpdateProbePhase::Completed) {
        exit.write(AppExit::Success);
    }
    probe.phase = next_phase;
}

fn watchdog_terrain_partial_update_probe(
    mut probe: ResMut<TerrainPartialUpdateProbeState>,
    mut exit: MessageWriter<AppExit>,
) {
    if matches!(
        &probe.phase,
        TerrainPartialUpdateProbePhase::Completed | TerrainPartialUpdateProbePhase::Failed
    ) || probe.started_at.elapsed().as_secs() < PROBE_TIMEOUT_SECONDS
    {
        return;
    }

    fail_probe(
        probe.fixed_ticks,
        "terrain partial-update probe exceeded its 30 second real-time watchdog",
        &mut exit,
    );
    probe.phase = TerrainPartialUpdateProbePhase::Failed;
}

fn advance_probe(
    phase: TerrainPartialUpdateProbePhase,
    current_fixed_tick: u32,
    runtime: &TerrainRuntimeState,
    bindings: &TerrainRuntimeBindings,
    pending: &mut PendingTerrainPartialUpdate,
    outcome: &TerrainRuntimeUpdateOutcome,
) -> Result<TerrainPartialUpdateProbePhase, String> {
    match phase {
        TerrainPartialUpdateProbePhase::WarmingUp
            if current_fixed_tick <= PROBE_WARMUP_FIXED_TICKS =>
        {
            Ok(TerrainPartialUpdateProbePhase::WarmingUp)
        }
        TerrainPartialUpdateProbePhase::WarmingUp => {
            submit_probe(current_fixed_tick, runtime, bindings, pending, outcome)
                .map(TerrainPartialUpdateProbePhase::Submitted)
        }
        TerrainPartialUpdateProbePhase::Submitted(submission) => {
            if outcome.sequence() != Some(submission.request_sequence) {
                if outcome.processed() > submission.outcome_processed_before_submit {
                    return Err(
                        "terrain update outcome published an unexpected sequence".to_owned()
                    );
                }
                return Ok(TerrainPartialUpdateProbePhase::Submitted(submission));
            }
            if outcome.projection_failed() {
                return Err(format!(
                    "terrain runtime projection proof failed: {}",
                    outcome.error().unwrap_or("unspecified projection failure")
                ));
            }
            if let Some(update_error) = outcome.error() {
                return Err(format!(
                    "terrain runtime rejected the probe transaction: {update_error}"
                ));
            }
            let report = outcome
                .report()
                .ok_or_else(|| {
                    "processed terrain update has neither verified report nor error".to_owned()
                })?
                .clone();
            Ok(TerrainPartialUpdateProbePhase::Applied {
                submission: Box::new(submission),
                report,
                applied_fixed_tick: current_fixed_tick,
            })
        }
        TerrainPartialUpdateProbePhase::Applied {
            submission,
            report,
            applied_fixed_tick,
        } => {
            let observed_fixed_ticks = current_fixed_tick.saturating_sub(applied_fixed_tick);
            if observed_fixed_ticks < PROBE_OBSERVATION_FIXED_TICKS {
                return Ok(TerrainPartialUpdateProbePhase::Applied {
                    submission,
                    report,
                    applied_fixed_tick,
                });
            }

            let verified = verify_probe(&submission, &report, runtime, bindings)?;
            let identities_preserved = verified.identities.render_entities == TERRAIN_CHUNK_COUNT
                && verified.identities.mesh_assets == TERRAIN_CHUNK_COUNT
                && verified.identities.collider_entities == TERRAIN_CHUNK_COUNT;
            info!(
                target: "forge::terrain",
                schema = "forge.runtime-event",
                schema_version = 1_u64,
                event_kind = "terrain_partial_update_probe_completed",
                success = true,
                request_sequence = submission.request_sequence,
                sample_x = u64::from(PROBE_SAMPLE_X),
                sample_z = u64::from(PROBE_SAMPLE_Z),
                delta_m = f64::from(PROBE_DELTA_M),
                submitted_fixed_tick = submission.submitted_fixed_tick,
                applied_fixed_tick,
                verified_fixed_tick = current_fixed_tick,
                observed_fixed_ticks,
                source_revision = submission.source_revision.get(),
                target_revision = submission.target_revision.get(),
                source_document_hash = %submission.source_document_hash,
                target_document_hash = %submission.target_document_hash,
                target_cache_hash = %verified.target_cache_hash,
                changed_samples = report.changed_samples().len(),
                mesh_chunks = report.mesh_chunks().len(),
                collider_chunks = report.collider_chunks().len(),
                mesh_assets_verified = report.mesh_assets_verified(),
                collider_components_verified = report.collider_components_verified(),
                rapier_backends_verified = report.rapier_backends_verified(),
                bindings = report.binding_count(),
                render_entities_preserved = verified.identities.render_entities,
                mesh_assets_preserved = verified.identities.mesh_assets,
                collider_entities_preserved = verified.identities.collider_entities,
                identities_preserved,
                "terrain partial-update probe passed after backend proof and two later fixed ticks"
            );
            Ok(TerrainPartialUpdateProbePhase::Completed)
        }
        TerrainPartialUpdateProbePhase::Completed => Ok(TerrainPartialUpdateProbePhase::Completed),
        TerrainPartialUpdateProbePhase::Failed => Ok(TerrainPartialUpdateProbePhase::Failed),
    }
}

fn submit_probe(
    current_fixed_tick: u32,
    runtime: &TerrainRuntimeState,
    bindings: &TerrainRuntimeBindings,
    pending: &mut PendingTerrainPartialUpdate,
    outcome: &TerrainRuntimeUpdateOutcome,
) -> Result<SubmittedProbe, String> {
    let source_document = runtime.document();
    let source_document_hash = source_document
        .content_hash()
        .map_err(|error| format!("cannot hash probe source terrain: {error}"))?;
    let source_revision = source_document.revision;
    let target_document = build_probe_target_document(source_document)?;
    let target_document_hash = target_document
        .content_hash()
        .map_err(|error| format!("cannot hash probe target terrain: {error}"))?;
    let target_revision = target_document.revision;
    let expected_cache = TerrainGenerationPlan::from_document(&target_document)
        .map_err(|error| format!("cannot plan full probe rebuild: {error}"))?
        .generate()
        .map_err(|error| format!("cannot generate full probe rebuild: {error}"))?;
    let expected_cache_hash = expected_cache
        .content_hash()
        .map_err(|error| format!("cannot hash full probe rebuild: {error}"))?;
    let bindings_before = bindings
        .snapshots()
        .map_err(|error| format!("cannot snapshot terrain bindings before probe: {error}"))?;
    let outcome_processed_before_submit = outcome.processed();
    let request_sequence = pending
        .submit(TerrainPartialUpdateRequest {
            base_revision: source_revision,
            base_document_hash: source_document_hash,
            target_document,
        })
        .map_err(|error| format!("cannot submit terrain partial-update probe: {error}"))?;

    info!(
        target: "forge::terrain",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "terrain_partial_update_probe_submitted",
        request_sequence,
        sample_x = u64::from(PROBE_SAMPLE_X),
        sample_z = u64::from(PROBE_SAMPLE_Z),
        delta_m = f64::from(PROBE_DELTA_M),
        submitted_fixed_tick = current_fixed_tick,
        source_revision = source_revision.get(),
        target_revision = target_revision.get(),
        source_document_hash = %source_document_hash,
        target_document_hash = %target_document_hash,
        expected_cache_hash = %expected_cache_hash,
        bindings = bindings_before.len(),
        "terrain partial-update probe submitted on the ordered fixed schedule"
    );

    Ok(SubmittedProbe {
        submitted_fixed_tick: current_fixed_tick,
        outcome_processed_before_submit,
        request_sequence,
        source_document_hash,
        target_document_hash,
        expected_cache_hash,
        source_revision,
        target_revision,
        expected_cache,
        bindings_before,
    })
}

fn build_probe_target_document(source: &TerrainDocument) -> Result<TerrainDocument, String> {
    let mut target = source
        .migrate_to_latest()
        .map_err(|error| format!("cannot migrate probe terrain to the current schema: {error}"))?;
    target.revision = source
        .revision
        .next()
        .map_err(|error| format!("cannot advance probe terrain revision: {error}"))?;

    let key = (PROBE_SAMPLE_Z, PROBE_SAMPLE_X);
    match target
        .height_deltas
        .binary_search_by_key(&key, |delta| delta.sort_key())
    {
        Ok(index) => {
            let updated_delta = target.height_deltas[index].delta_m + PROBE_DELTA_M;
            if updated_delta == 0.0 {
                target.height_deltas.remove(index);
            } else {
                target.height_deltas[index].delta_m = updated_delta;
            }
        }
        Err(index) => target.height_deltas.insert(
            index,
            TerrainHeightDelta::new(PROBE_SAMPLE_X, PROBE_SAMPLE_Z, PROBE_DELTA_M),
        ),
    }
    target
        .validate()
        .map_err(|error| format!("probe target terrain is invalid: {error}"))?;
    Ok(target)
}

fn verify_probe(
    submission: &SubmittedProbe,
    report: &TerrainRuntimeUpdateReport,
    runtime: &TerrainRuntimeState,
    bindings: &TerrainRuntimeBindings,
) -> Result<VerifiedProbe, String> {
    if report.sequence() != submission.request_sequence
        || report.source_document_hash() != submission.source_document_hash
        || report.target_document_hash() != submission.target_document_hash
        || report.target_cache_hash() != submission.expected_cache_hash
        || report.source_revision() != submission.source_revision
        || report.target_revision() != submission.target_revision
    {
        return Err(
            "terrain update report differs from the submitted probe or full rebuild".to_owned(),
        );
    }
    if report.changed_samples()
        != [TerrainHeightSampleCoordinate::new(
            PROBE_SAMPLE_X,
            PROBE_SAMPLE_Z,
        )]
    {
        return Err("terrain update report does not contain the exact probe sample".to_owned());
    }
    if report.mesh_chunks().len() != 3
        || report.collider_chunks().len() != 1
        || report.mesh_assets_verified() != 3
        || report.collider_components_verified() != 1
        || report.rapier_backends_verified() != 1
    {
        return Err(format!(
            "terrain update expected 3 verified meshes, 1 verified collider component and 1 verified Rapier backend; observed {}/{}/{}/{}/{}",
            report.mesh_chunks().len(),
            report.collider_chunks().len(),
            report.mesh_assets_verified(),
            report.collider_components_verified(),
            report.rapier_backends_verified()
        ));
    }
    if report.binding_count() != TERRAIN_CHUNK_COUNT {
        return Err(format!(
            "terrain update observed {} bindings instead of {TERRAIN_CHUNK_COUNT}",
            report.binding_count()
        ));
    }

    let current_document_hash = runtime
        .document()
        .content_hash()
        .map_err(|error| format!("cannot hash terrain document after probe: {error}"))?;
    let target_cache_hash = runtime
        .cache()
        .content_hash()
        .map_err(|error| format!("cannot hash terrain cache after probe: {error}"))?;
    if current_document_hash != submission.target_document_hash
        || runtime.document().revision != submission.target_revision
        || runtime.cache() != &submission.expected_cache
        || target_cache_hash != submission.expected_cache_hash
    {
        return Err("terrain runtime cache differs from the independent full rebuild".to_owned());
    }

    let bindings_after = bindings
        .snapshots()
        .map_err(|error| format!("cannot snapshot terrain bindings after probe: {error}"))?;
    let identities = preserved_identity_counts(&submission.bindings_before, &bindings_after);
    if identities.render_entities != TERRAIN_CHUNK_COUNT
        || identities.mesh_assets != TERRAIN_CHUNK_COUNT
        || identities.collider_entities != TERRAIN_CHUNK_COUNT
    {
        return Err(format!(
            "terrain runtime preserved {}/{} render entities, {}/{} mesh assets and {}/{} collider entities",
            identities.render_entities,
            TERRAIN_CHUNK_COUNT,
            identities.mesh_assets,
            TERRAIN_CHUNK_COUNT,
            identities.collider_entities,
            TERRAIN_CHUNK_COUNT
        ));
    }

    Ok(VerifiedProbe {
        identities,
        target_cache_hash,
    })
}

fn preserved_identity_counts(
    before: &[TerrainRuntimeBindingSnapshot],
    after: &[TerrainRuntimeBindingSnapshot],
) -> PreservedIdentityCounts {
    let mut counts = PreservedIdentityCounts {
        render_entities: 0,
        mesh_assets: 0,
        collider_entities: 0,
    };
    for source in before {
        let Some(target) = after.iter().find(|candidate| {
            candidate.id == source.id && candidate.coordinate == source.coordinate
        }) else {
            continue;
        };
        if target.entity() == source.entity() {
            counts.render_entities += 1;
        }
        if target.mesh_asset_id() == source.mesh_asset_id() {
            counts.mesh_assets += 1;
        }
        if target.collider_entity() == source.collider_entity() {
            counts.collider_entities += 1;
        }
    }
    counts
}

fn fail_probe(fixed_tick: u32, reason: &str, exit: &mut MessageWriter<AppExit>) {
    error!(
        target: "forge::terrain",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "terrain_partial_update_probe_failed",
        success = false,
        fixed_tick,
        error = reason,
        "terrain partial-update probe failed"
    );
    exit.write(AppExit::error());
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::TERRAIN_DOCUMENT_SCHEMA_VERSION;
    use forge_world::embedded_visual_valley_terrain_document;

    #[test]
    fn probe_target_is_a_canonical_next_revision_five_metre_delta() {
        let source = embedded_visual_valley_terrain_document().unwrap();
        let target = build_probe_target_document(&source).unwrap();

        assert_eq!(target.schema_version, TERRAIN_DOCUMENT_SCHEMA_VERSION);
        assert_eq!(target.revision, source.revision.next().unwrap());
        assert_eq!(
            target.height_deltas,
            vec![TerrainHeightDelta::new(
                PROBE_SAMPLE_X,
                PROBE_SAMPLE_Z,
                PROBE_DELTA_M
            )]
        );
        assert_ne!(
            target.content_hash().unwrap(),
            source.content_hash().unwrap()
        );
    }

    #[test]
    fn probe_delta_is_additive_and_keeps_canonical_order() {
        let source = embedded_visual_valley_terrain_document().unwrap();
        let mut source = source.migrate_to_latest().unwrap();
        source.height_deltas = vec![
            TerrainHeightDelta::new(30, 31, 1.0),
            TerrainHeightDelta::new(PROBE_SAMPLE_X, PROBE_SAMPLE_Z, -2.0),
            TerrainHeightDelta::new(32, 31, 1.0),
        ];
        let target = build_probe_target_document(&source).unwrap();

        assert_eq!(target.height_deltas[1].delta_m.to_bits(), 3.0_f32.to_bits());
        assert!(
            target
                .height_deltas
                .windows(2)
                .all(|pair| pair[0].sort_key() < pair[1].sort_key())
        );
    }
}
