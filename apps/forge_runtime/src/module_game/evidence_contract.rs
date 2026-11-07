//! RPG projection evidence. Process exit success alone is never a ready verdict.
use forge_core::{AreaId, ContentHash, EntityId, ModuleId, Transform};
use forge_project::module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin};
use serde::{Deserialize, Serialize};
#[path = "evidence_graphics.rs"]
pub mod graphics;
pub use graphics::{GraphicsIdentity, GraphicsObservation};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct HostIdentity {
    #[serde(default)]
    pub source_lighting: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub grass: Option<ContentHash>,
    pub projection: ContentHash,
    pub head: ContentHash,
    pub terrain: ContentHash,
    pub instances: usize,
    pub colliders: usize,
}

#[derive(Default, Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct HostAdmission {
    pub render_ready: bool,
    pub collisions_ready: bool,
    pub frame_ready: bool,
}

#[derive(Default, Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct GrassStatus {
    pub identity: Option<ContentHash>,
    pub ready: bool,
    pub counts: [usize; 3],
    pub generation: u64,
    pub sequence: u64,
    pub publication_frame: u32,
    pub gpu_epoch: u64,
    pub gpu_observed: u32,
    pub gpu_completed: Option<u32>,
    pub gpu_meshes: [usize; 2],
    pub gpu_materials: [usize; 2],
    pub gpu_images: [usize; 2],
    pub gpu_buffers: [usize; 2],
    pub pipelines: [usize; 3],
}

#[derive(Default, Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct HostStatus {
    #[serde(default)]
    pub surfaces: SurfaceStatus,
    #[serde(default)]
    pub grass: GrassStatus,
    pub identity: Option<ContentHash>,
    pub head: Option<ContentHash>,
    pub terrain: Option<ContentHash>,
    pub ready: bool,
    pub admission: HostAdmission,
    pub colliders: usize,
    pub canonical_instances: usize,
    pub canonical_colliders: usize,
    pub entities: usize,
    #[serde(default, skip_serializing_if = "is_zero_count")]
    pub replacement_primitives: usize,
    pub generation: u64,
    pub sequence: u64,
    pub detail: usize,
    pub grouped: usize,
    pub selected_triangles: usize,
    pub blocked_ticks: u64,
    pub error: Option<String>,
}

#[allow(
    clippy::trivially_copy_pass_by_ref,
    reason = "Serde supplies a reference"
)]
fn is_zero_count(value: &usize) -> bool {
    *value == 0
}

#[derive(Default, Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct SurfaceStatus {
    #[serde(default)]
    pub world_receivers: usize,
    #[serde(default)]
    pub model_receivers: usize,
    #[serde(default)]
    pub terrain_receivers: usize,
    pub active: bool,
    pub ready: bool,
    pub generation: u64,
    pub opaque_casters: usize,
    pub plant_batches: usize,
    pub encoded_frames: u64,
    pub cascade_layers: [u64; 3],
    pub encoded_frame: Option<u32>,
    pub gpu_completed: Option<u32>,
    pub motion_matches: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct SpeciesIdentity {
    pub population_source_hash: ContentHash,
    pub catalog_sha256: ContentHash,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
pub struct ObservedSpeciesStatus {
    pub ready: bool,
    pub cpu_ready: bool,
    pub collisions_ready: bool,
    pub error: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct ObservedSpecies {
    #[serde(default, skip_serializing_if = "is_false")]
    pub empty: bool,
    pub identity: SpeciesIdentity,
    pub projected_population_hash: Option<String>,
    pub projected_catalog_sha256: Option<ContentHash>,
    pub status: ObservedSpeciesStatus,
    pub gpu_generation: Option<u64>,
    pub meshes_expected: usize,
    pub meshes_prepared: usize,
    pub materials_expected: usize,
    pub materials_bound: usize,
    pub images_expected: usize,
    pub images_prepared: usize,
}

impl ObservedSpecies {
    pub fn ready(&self) -> bool {
        self.status.ready
            && self.status.cpu_ready
            && self.status.collisions_ready
            && self.status.error.is_none()
            && self.projected_population_hash.as_deref()
                == Some(self.identity.population_source_hash.to_string().as_str())
            && self.projected_catalog_sha256 == Some(self.identity.catalog_sha256)
            && if self.empty {
                self.gpu_generation.is_none()
                    && self.meshes_expected == 0
                    && self.materials_expected == 0
                    && self.images_expected == 0
            } else {
                self.gpu_generation.is_some_and(|generation| generation > 0)
            }
            && self.meshes_prepared == self.meshes_expected
            && self.materials_bound == self.materials_expected
            && self.images_prepared == self.images_expected
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedManual {
    pub entity_id: EntityId,
    pub asset: VisualAssetPin,
    pub transform: Transform,
    #[serde(default, skip_serializing_if = "is_false")]
    pub non_colliding: bool,
    /// This placement belongs to the admitted fish host, independently of its
    /// collision policy. Other animated objects may also be non-colliding.
    #[serde(default, skip_serializing_if = "is_false")]
    pub fish: bool,
    /// Exact authored physics companion; ordinary placements keep the v1 shape.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_physics:
        Option<Box<forge_project::zone_authoring::placements::PhysicsPackageReference>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_particles:
        Option<Box<forge_project::zone_authoring::placements::ParticlePackageReference>>,
}

#[allow(
    clippy::trivially_copy_pass_by_ref,
    reason = "Serde supplies a reference"
)]
fn is_false(value: &bool) -> bool {
    !value
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct EnvironmentIdentity {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub solar_clock: Option<forge_project::module_package::ModuleSolarClock>,
    pub identity: ContentHash,
    pub live_programs: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct EnvironmentObservation {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub solar: Option<ObservedSolarClock>,
    pub identity: Option<ContentHash>,
    pub ready: bool,
    pub bootstrap_steps: u32,
    pub cloud_steps: u32,
    pub cloud_submissions: u32,
    pub live_submissions: u32,
    pub programs_expected: bool,
    pub programs_prepared: usize,
    pub hdr_publication: Option<u64>,
    pub publication_frame: Option<u32>,
    pub gpu_observed: u32,
    pub gpu_completed: Option<u32>,
    pub gpu_images: [usize; 2],
    pub gpu_materials: [usize; 2],
    pub error: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ObservedSolarClock {
    pub start_minutes: u16,
    pub hours_bits: Option<u64>,
    pub night_bits: Option<u32>,
    pub ready: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedProjection {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub local_lights: Option<ExpectedLocalLights>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub environment: Option<EnvironmentIdentity>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub graphics: Option<GraphicsIdentity>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub imported: Option<HostIdentity>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub species: Option<SpeciesIdentity>,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub source_hash: ContentHash,
    pub functional_hash: ContentHash,
    pub visuals: Vec<GameplayVisualBinding>,
    pub manual: Vec<ExpectedManual>,
    #[serde(default)]
    pub building_count: usize,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub streamed_terrain: Option<ExpectedStreamedTerrain>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub procedural_streaming: Option<ExpectedProceduralTerrain>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedStreamedTerrain {
    pub document_hash: ContentHash,
    pub raster_hash: ContentHash,
    pub raster_bytes: u64,
    pub maximum_cells: u16,
    pub maximum_cpu_mesh_bytes: u64,
    pub dimensions: [u16; 2],
    pub maximum_staging_cpu_mesh_bytes: u64,
}

/// Qualification of a procedural source; no raster identity is implied.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedProceduralTerrain {
    pub document_hash: ContentHash,
    pub maximum_cells: u16,
    pub maximum_cpu_mesh_bytes: u64,
    pub dimensions: [u16; 2],
    pub maximum_staging_cpu_mesh_bytes: u64,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedTerrainHlod {
    pub tiles: usize,
    pub aggregated_tiles: usize,
    pub covered_base_cells: usize,
    pub maximum_level: u8,
    pub cpu_mesh_bytes: u64,
    pub resident_triangles: usize,
    pub peak_staging_mesh_bytes: u64,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedStreamedTerrain {
    pub identity: ExpectedStreamedTerrain,
    pub resident_cells: usize,
    pub detailed_cells: usize,
    pub coarse_cells: usize,
    pub cpu_mesh_bytes: u64,
    pub resident_triangles: usize,
    pub committed_transitions: u64,
    pub maximum_cells: u16,
    pub maximum_cpu_mesh_bytes: u64,
    pub pending: bool,
    pub physics_ready: bool,
    pub error: Option<String>,
    pub source_synchronized: bool,
    pub hlod: ObservedTerrainHlod,
}

/// CPU residency and physics observation; rendering still needs independent GPU evidence.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedProceduralTerrain {
    pub identity: ExpectedProceduralTerrain,
    pub resident_cells: usize,
    pub detailed_cells: usize,
    pub coarse_cells: usize,
    pub cpu_mesh_bytes: u64,
    pub resident_triangles: usize,
    pub committed_transitions: u64,
    pub pending: bool,
    pub physics_ready: bool,
    pub error: Option<String>,
    pub source_synchronized: bool,
    pub hlod: ObservedTerrainHlod,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedStaticHlod {
    pub enabled: bool,
    pub generation: u64,
    pub source_objects: usize,
    pub source_primitives: usize,
    pub covered_primitives: usize,
    pub source_triangles: usize,
    pub selected_triangles: usize,
    pub desired_batches: usize,
    pub rendered_batches: usize,
    pub replaced_primitives: usize,
    pub retained_source_mesh_bytes: u64,
    pub source_snapshot_bytes: u64,
    pub derived_mesh_bytes: u64,
    pub peak_tracked_buffer_bound_bytes: u64,
    pub maximum_position_error_m: f64,
    pub maximum_attribute_deltas: std::collections::BTreeMap<String, f64>,
    pub minimum_face_alignment: Option<f64>,
    pub committed_transitions: u64,
    pub discarded_jobs: u64,
    pub pending: bool,
    pub error: Option<String>,
    pub gpu_upload_ready: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedVisual {
    pub entity_id: EntityId,
    pub asset: VisualAssetPin,
    pub transform: Transform,
    pub parent_identity: Option<EntityId>,
    pub parent_is_gameplay_body: bool,
    pub parent_has_collider: bool,
    pub scene_ready: bool,
    pub mesh_primitives: usize,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedManual {
    pub identity: ExpectedManual,
    pub mesh_primitives: usize,
    pub registered_primitives: usize,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_physics: Option<ObservedSourcePhysics>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_particles: Option<ObservedSourceParticles>,
}

/// Encoded source work and owned output binding; GPU completion is separately
/// required by the renderer inventory and report's renderer observation.
#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedSourceParticles {
    pub owners: usize,
    pub initialized_owners: usize,
    pub current_owners: usize,
    pub producer_steps: u64,
    pub projection_frames: u64,
    pub primitives: usize,
    pub pending: bool,
    pub error: Option<String>,
}

impl ObservedSourceParticles {
    fn ready(&self) -> bool {
        !self.pending
            && self.error.is_none()
            && self.owners > 0
            && self.owners == self.initialized_owners
            && self.owners == self.current_owners
            && self.projection_frames > 0
            && self.primitives > 0
    }
}

#[cfg(test)]
mod particle_admission_tests {
    use super::*;

    #[test]
    fn particle_report_requires_owned_output_and_keeps_effect_geometry_nonphysical() {
        let hash = forge_core::bytes_sha256(b"original particle carrier");
        let expected = ExpectedManual {
            entity_id: EntityId::deterministic("particle placement"),
            asset: VisualAssetPin {
                asset_id: forge_core::AssetId::deterministic("particle asset"),
                sha256: hash,
            },
            transform: Transform::IDENTITY,
            non_colliding: false,
            fish: false,
            source_physics: None,
            source_particles: Some(Box::new(
                forge_project::zone_authoring::placements::ParticlePackageReference {
                    schema_version: 1,
                    carrier_sha256: hash,
                    manifest_sha256: hash,
                    package_sha256: hash,
                },
            )),
        };
        let actual = ObservedManual {
            identity: expected.clone(),
            mesh_primitives: 1,
            registered_primitives: 0,
            source_physics: None,
            source_particles: Some(ObservedSourceParticles {
                owners: 1,
                initialized_owners: 1,
                current_owners: 1,
                producer_steps: 0,
                projection_frames: 1,
                primitives: 1,
                pending: false,
                error: None,
            }),
        };
        let verify = |row: &ObservedManual| {
            verify_manual(
                std::slice::from_ref(&expected),
                &std::collections::BTreeMap::from([(expected.entity_id, row)]),
            )
        };
        // A stopped emitter may have no producer steps; its admitted initial
        // buffer and encoded projection still need the actual renderer fence.
        verify(&actual).unwrap();
        for case in 0..8 {
            let mut bad = actual.clone();
            let effect = bad.source_particles.as_mut().unwrap();
            match case {
                0 => effect.initialized_owners = 0,
                1 => effect.current_owners = 0,
                2 => effect.projection_frames = 0,
                3 => effect.pending = true,
                4 => effect.error = Some("kernel failed".into()),
                5 => effect.primitives = 2,
                6 => bad.registered_primitives = 1,
                _ => bad.source_particles = None,
            }
            assert!(verify(&bad).is_err(), "case {case}");
        }
    }
}

/// Fresh backend observations for the source-owned collision batch, not the
/// number of displayed meshes or a claim of source-engine equivalence.
#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedSourcePhysics {
    pub active_solids: usize,
    pub registered_solids: usize,
    pub active_sensors: usize,
    pub registered_sensors: usize,
    pub disabled: usize,
    pub disabled_synchronized: usize,
    pub unassigned: usize,
    pub unassigned_clear: usize,
    pub pending: bool,
    pub error: Option<String>,
}

impl ObservedSourcePhysics {
    fn ready(&self) -> bool {
        !self.pending
            && self.error.is_none()
            && (self.active_solids != 0 || self.active_sensors != 0)
            && self.active_solids == self.registered_solids
            && self.active_sensors == self.registered_sensors
            && self.disabled == self.disabled_synchronized
            && self.unassigned == self.unassigned_clear
    }
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RendererObservation {
    pub observed: bool,
    pub frame: u32,
    pub pipelines: usize,
    pub pending: usize,
    pub failed: usize,
}

/// Actual values seen by the running shared scene, recorded only when authored.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedSceneWind {
    pub direction_xz: [f64; 2],
    pub speed_mps: f64,
    pub surface: Option<[f32; 4]>,
    pub cloud_direction_xz: Option<[f64; 2]>,
    pub cloud_speed_mps: Option<f64>,
    pub cloud_step_xz: Option<[f32; 2]>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProjectionObservation {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub wind: Option<ObservedSceneWind>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub local_lights: Option<ObservedLocalLights>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub environment: Option<EnvironmentObservation>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub graphics: Option<GraphicsObservation>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub imported: Option<HostStatus>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub species: Option<ObservedSpecies>,
    pub pid: u32,
    pub frame: u32,
    pub module_id: Option<ModuleId>,
    pub area_id: Option<AreaId>,
    pub source_hash: Option<ContentHash>,
    pub functional_hash: Option<ContentHash>,
    pub gameplay_ready: bool,
    pub tick: u64,
    pub simulation_error: Option<String>,
    pub visual_error: Option<String>,
    pub visual_expected: usize,
    pub visual_spawned: usize,
    pub visual_ready: usize,
    pub manual_projection_ready: bool,
    pub visuals: Vec<ObservedVisual>,
    pub manual: Vec<ObservedManual>,
    #[serde(default)]
    pub building_source_hash: Option<ContentHash>,
    #[serde(default)]
    pub building_count: usize,
    #[serde(default)]
    pub building_parts: usize,
    #[serde(default)]
    pub registered_building_parts: usize,
    #[serde(default)]
    pub building_projection_ready: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub streamed_terrain: Option<ObservedStreamedTerrain>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub procedural_streaming: Option<ObservedProceduralTerrain>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub static_hlod: Option<ObservedStaticHlod>,
    pub renderer: RendererObservation,
}

/// Procedural and source-physics fields extend the strict v1 shape, even on failure.
#[must_use]
pub fn report_version(
    expected: &ExpectedProjection,
    final_observation: Option<&ProjectionObservation>,
    capture_observation: Option<&ProjectionObservation>,
) -> u8 {
    if expected.imported.is_some() && expected.species.is_some()
        || [final_observation, capture_observation]
            .into_iter()
            .flatten()
            .any(|observation| {
                observation
                    .imported
                    .as_ref()
                    .is_some_and(|plants| plants.replacement_primitives > 0)
            })
    {
        return 4;
    }
    if expected
        .manual
        .iter()
        .any(|row| row.source_particles.is_some())
        || [final_observation, capture_observation]
            .into_iter()
            .flatten()
            .any(|observation| {
                observation.manual.iter().any(|row| {
                    row.identity.source_particles.is_some() || row.source_particles.is_some()
                })
            })
    {
        return 3;
    }
    if expected.procedural_streaming.is_some()
        || expected
            .manual
            .iter()
            .any(|row| row.source_physics.is_some())
        || final_observation.is_some_and(|observation| observation.procedural_streaming.is_some())
        || capture_observation.is_some_and(|observation| observation.procedural_streaming.is_some())
        || [final_observation, capture_observation]
            .into_iter()
            .flatten()
            .any(|observation| {
                observation.manual.iter().any(|row| {
                    row.identity.source_physics.is_some() || row.source_physics.is_some()
                })
            })
    {
        2
    } else {
        1
    }
}

fn verify_world_projection(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    verify_local_lights(
        expected.local_lights.as_ref(),
        observed.local_lights.as_ref(),
        observed.frame,
    )?;
    if expected.local_lights.is_some() && expected.environment.is_none() {
        return Err(
            "Source local lights require their declared shared environment receivers".into(),
        );
    }
    verify_buildings(expected, observed)?;
    if expected.environment.is_some()
        && observed
            .graphics
            .as_ref()
            .is_none_or(|graphics| graphics.source_surfaces.is_none())
    {
        return Err("An admitted source environment requires common surface evidence even without vegetation".into());
    }
    graphics::verify(
        expected.graphics.as_ref(),
        observed.graphics.as_ref(),
        observed.frame,
        expected.imported.is_some(),
        expected.manual.iter().any(|placement| placement.fish),
    )?;
    verify_species(expected, observed)?;
    verify_imported_mode(
        expected.imported.as_ref(),
        observed.imported.as_ref(),
        expected.species.is_some(),
    )?;
    if (expected.streamed_terrain.is_some() && expected.procedural_streaming.is_some())
        || (observed.streamed_terrain.is_some() && observed.procedural_streaming.is_some())
    {
        return Err("A terrain cannot be both sampled and procedural".into());
    }
    verify_streamed_terrain(expected, observed)?;
    verify_procedural_streaming(
        expected.procedural_streaming.as_ref(),
        observed.procedural_streaming.as_ref(),
    )?;
    verify_static_hlod(expected, observed)
}

/// Scene preparation and a dated queue write; this does not claim mesh drawing or GPU completion.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedLocalLights {
    pub identity: Option<ContentHash>,
    pub placements: usize,
    pub emitters: usize,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedLocalLights {
    pub identity: Option<ContentHash>,
    pub placements: usize,
    pub emitters: usize,
    pub ready: bool,
    pub camera: Option<u64>,
    pub queued_camera: Option<u64>,
    pub queued_frame: Option<u32>,
    pub queued_writes: u64,
    pub queued_lights: i32,
    pub error: Option<String>,
    pub queue_error: Option<String>,
}

fn verify_local_lights(
    expected: Option<&ExpectedLocalLights>,
    actual: Option<&ObservedLocalLights>,
    frame: u32,
) -> Result<(), String> {
    match (expected, actual) {
        (None, None) => Ok(()),
        (Some(expected), Some(actual))
            if expected.identity.is_some()
                && expected.placements > 0
                && expected.emitters > 0
                && actual.identity == expected.identity
                && actual.placements == expected.placements
                && actual.emitters == expected.emitters
                && actual.ready
                && actual.error.is_none()
                && actual.queue_error.is_none()
                && actual.camera.is_some()
                && actual.camera == actual.queued_camera
                && actual.queued_writes > 0
                && (0..=8).contains(&actual.queued_lights)
                && actual
                    .queued_frame
                    .is_some_and(|queued| queued <= frame && frame - queued <= 8) =>
        {
            Ok(())
        }
        _ => Err(
            "Declared source lights have no matching complete scene and fresh queue observation"
                .into(),
        ),
    }
}

#[cfg(test)]
mod local_light_tests {
    use super::*;
    #[test]
    fn local_lights_require_the_exact_scene_and_fresh_same_camera_queue_without_claiming_draws() {
        let identity = Some(forge_core::bytes_sha256(b"admitted-light-placements"));
        let expected = ExpectedLocalLights {
            identity,
            placements: 2,
            emitters: 3,
        };
        let actual = ObservedLocalLights {
            identity,
            placements: 2,
            emitters: 3,
            ready: true,
            camera: Some(7),
            queued_camera: Some(7),
            queued_frame: Some(99),
            queued_writes: 8,
            queued_lights: 0,
            error: None,
            queue_error: None,
        };
        verify_local_lights(Some(&expected), Some(&actual), 100).unwrap();
        assert!(verify_local_lights(Some(&expected), None, 100).is_err());
        assert!(verify_local_lights(None, Some(&actual), 100).is_err());
        for case in 0..12 {
            let mut bad = actual.clone();
            match case {
                0 => bad.identity = None,
                1 => bad.placements = 1,
                2 => bad.emitters = 2,
                3 => bad.ready = false,
                4 => bad.camera = None,
                5 => bad.queued_camera = Some(8),
                6 => bad.queued_frame = Some(91),
                7 => bad.queued_frame = Some(101),
                8 => bad.queued_writes = 0,
                9 => bad.queued_lights = 9,
                10 => bad.error = Some("projection".into()),
                _ => bad.queue_error = Some("queue".into()),
            }
            assert!(
                verify_local_lights(Some(&expected), Some(&bad), 100).is_err(),
                "case {case}"
            );
        }
    }
}

#[cfg(test)]
fn verify_imported(
    expected: Option<&HostIdentity>,
    observed: Option<&HostStatus>,
) -> Result<(), String> {
    verify_imported_mode(expected, observed, false)
}

fn verify_imported_mode(
    expected: Option<&HostIdentity>,
    observed: Option<&HostStatus>,
    species: bool,
) -> Result<(), String> {
    match (expected, observed) {
        (None, None) => Ok(()),
        (Some(expected), Some(actual)) if actual.identity == Some(expected.projection)
            && actual.head == Some(expected.head) && actual.terrain == Some(expected.terrain)
            && actual.ready && actual.admission.render_ready && actual.admission.collisions_ready
            && actual.admission.frame_ready && actual.error.is_none()
            && verify_surfaces(expected.source_lighting, &actual.surfaces)
            && verify_grass_mode(expected.grass, &actual.grass, expected.source_lighting)
            && actual.canonical_instances == expected.instances
            && actual.detail.checked_add(actual.grouped)
                .is_some_and(|parts| parts <= expected.instances.saturating_mul(2)
                    && parts.checked_add(actual.replacement_primitives) == Some(actual.entities))
            && (actual.replacement_primitives == 0 || species)
            && actual.canonical_colliders == expected.colliders
            && actual.colliders <= actual.canonical_colliders && actual.generation > 0 && actual.sequence > 0 => Ok(()),
        _ => Err("Imported plants require the exact authored projection, shared frame, GPU publication and backend collisions".into()),
    }
}

#[cfg(test)]
fn verify_grass(expected: Option<ContentHash>, actual: &GrassStatus) -> bool {
    verify_grass_mode(expected, actual, false)
}

fn verify_surfaces(expected: bool, actual: &SurfaceStatus) -> bool {
    if !expected {
        return actual == &SurfaceStatus::default();
    }
    actual.world_receivers > 0
        && actual.active
        && actual.ready
        && actual.motion_matches
        && actual.generation > 0
        && actual.opaque_casters > 0
        && actual.plant_batches == 5
        && actual.encoded_frames > 0
        && actual.cascade_layers.iter().all(|count| *count > 0)
        && actual
            .encoded_frame
            .zip(actual.gpu_completed)
            .is_some_and(|(encoded, completed)| completed >= encoded)
}

fn verify_grass_mode(expected: Option<ContentHash>, actual: &GrassStatus, source: bool) -> bool {
    match expected {
        None => actual == &GrassStatus::default(),
        Some(expected) => {
            actual.identity == Some(expected)
                && actual.ready
                && actual.generation >= 2
                && actual.sequence > 0
                && actual.gpu_epoch > 0
                && actual.gpu_completed.is_some_and(|frame| {
                    frame >= actual.publication_frame && frame <= actual.gpu_observed
                })
                && actual.gpu_meshes == [3, 3]
                && actual.gpu_materials == [3, 3]
                && actual.gpu_images == if source { [11, 11] } else { [8, 8] }
                && actual.gpu_buffers == if source { [8, 8] } else { [6, 6] }
                && actual.pipelines[0] > 0
                && actual.pipelines[1..] == [0, 0]
                && actual
                    .counts
                    .iter()
                    .zip([48, 160, 420])
                    .all(|(&count, capacity)| count <= capacity)
        }
    }
}

fn verify_species(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    let imported = expected.imported.is_some();
    match (&expected.species, &observed.species) {
        (None, None) => Ok(()),
        (Some(expected), Some(actual)) if &actual.identity == expected && actual.ready()
            && (!actual.empty || imported) => Ok(()),
        _ => Err("Installed species require the exact authored population, catalog, backend collisions and GPU inventory".into()),
    }
}

pub fn verify(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    if observed.pid == 0
        || observed.frame == 0
        || observed.module_id != Some(expected.module_id)
        || observed.area_id != Some(expected.area_id)
        || observed.source_hash != Some(expected.source_hash)
        || observed.functional_hash != Some(expected.functional_hash)
    {
        return Err(
            "The observed native process/document identity differs from the admitted module".into(),
        );
    }
    verify_environment(expected.environment.as_ref(), observed.environment.as_ref())?;
    if !observed.gameplay_ready
        || observed.tick == 0
        || observed.simulation_error.is_some()
        || observed.visual_error.is_some()
        || !observed.manual_projection_ready
    {
        return Err("The native simulation or physical/visual projections are not ready".into());
    }
    verify_world_projection(expected, observed)?;
    let renderer = &observed.renderer;
    if !renderer.observed
        || renderer.pipelines == 0
        || renderer.pending != 0
        || renderer.failed != 0
        || renderer.frame > observed.frame
        || observed.frame - renderer.frame > 8
    {
        return Err("A fresh, complete renderer pipeline observation is required".into());
    }
    let count = expected.visuals.len();
    if observed.visual_expected != count
        || observed.visual_spawned != count
        || observed.visual_ready != count
        || observed.visuals.len() != count
        || observed.manual.len() != expected.manual.len()
    {
        return Err("Native model/placement inventories differ from the canonical module".into());
    }
    let mut seen = std::collections::BTreeSet::new();
    let visual_rows = observed
        .visuals
        .iter()
        .map(|row| (row.entity_id, row))
        .collect::<std::collections::BTreeMap<_, _>>();
    let manual_rows = observed
        .manual
        .iter()
        .map(|row| (row.identity.entity_id, row))
        .collect::<std::collections::BTreeMap<_, _>>();
    if visual_rows.len() != observed.visuals.len() || manual_rows.len() != observed.manual.len() {
        return Err("Duplicate observed model or manual-placement identity".into());
    }
    for expected in &expected.visuals {
        if expected.area_id != observed.area_id.ok_or("Observed area is absent")?
            || !seen.insert(expected.entity_id)
        {
            return Err("Invalid expected visual inventory".into());
        }
        let row = visual_rows
            .get(&expected.entity_id)
            .ok_or("A canonical visual has no observed scene")?;
        if row.asset != expected.asset
            || row.transform != expected.local_transform
            || row.parent_identity != Some(expected.entity_id)
            || !row.parent_is_gameplay_body
            || !row.parent_has_collider
            || !row.scene_ready
            || row.mesh_primitives == 0
        {
            return Err(
                "A governed model's pin, transform, parent, collider or loaded mesh differs".into(),
            );
        }
        row.transform.validate().map_err(|e| e.to_string())?;
    }
    verify_manual(&expected.manual, &manual_rows)
}

fn verify_manual(
    expected: &[ExpectedManual],
    rows: &std::collections::BTreeMap<EntityId, &ObservedManual>,
) -> Result<(), String> {
    let mut seen = std::collections::BTreeSet::new();
    for expected in expected {
        if !seen.insert(expected.entity_id) {
            return Err("Duplicate expected manual placement".into());
        }
        let row = rows
            .get(&expected.entity_id)
            .ok_or("A canonical manual placement has no observed scene")?;
        let collision_ready = match (&expected.source_physics, &row.source_physics) {
            (Some(pin), Some(physics)) => {
                !expected.non_colliding
                    && !expected.fish
                    && pin.schema_version == 1
                    && pin.carrier_sha256 == expected.asset.sha256
                    && row.registered_primitives == 0
                    && physics.ready()
            }
            (None, None) if expected.source_particles.is_some() => {
                row.source_particles.as_ref().is_some_and(|particles| {
                    particles.primitives <= row.mesh_primitives
                        && row.registered_primitives == row.mesh_primitives - particles.primitives
                })
            }
            (None, None) if expected.non_colliding => row.registered_primitives == 0,
            (None, None) => row.registered_primitives == row.mesh_primitives,
            _ => false,
        };
        let particles_ready = match (&expected.source_particles, &row.source_particles) {
            (None, None) => true,
            (Some(pin), Some(particles)) => {
                pin.schema_version == 1
                    && pin.carrier_sha256 == expected.asset.sha256
                    && particles.ready()
                    && particles.primitives <= row.mesh_primitives
                    && !expected.fish
            }
            _ => false,
        };
        if expected.fish && !expected.non_colliding
            || row.identity != *expected
            || row.mesh_primitives == 0
            || !collision_ready
            || !particles_ready
        {
            return Err(
                "A manual placement's identity, transform or static colliders differ".into(),
            );
        }
        row.identity
            .transform
            .validate()
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

fn verify_buildings(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    if observed.building_count != expected.building_count
        || observed.building_parts != expected.building_count.saturating_mul(8)
        || observed.registered_building_parts != observed.building_parts
        || (expected.building_count != 0
            && (observed.building_source_hash != Some(expected.source_hash)
                || !observed.building_projection_ready))
    {
        return Err(
            "Canonical buildings require the exact source and every registered room part".into(),
        );
    }
    Ok(())
}

fn verify_streamed_terrain(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    match (&expected.streamed_terrain, &observed.streamed_terrain) {
        (None, None) => Ok(()),
        (Some(expected), Some(actual)) if actual.identity == *expected
            && actual.resident_cells > 0 && actual.detailed_cells > 0
            && actual.detailed_cells.checked_add(actual.coarse_cells) == Some(actual.resident_cells)
            && actual.maximum_cells == expected.maximum_cells
            && actual.maximum_cpu_mesh_bytes == expected.maximum_cpu_mesh_bytes
            && actual.resident_cells <= usize::from(expected.maximum_cells)
            && actual.cpu_mesh_bytes > 0 && actual.cpu_mesh_bytes <= expected.maximum_cpu_mesh_bytes
            && actual.resident_triangles > 0 && actual.committed_transitions > 0
            && !actual.pending && actual.physics_ready && actual.error.is_none()
            && actual.source_synchronized
            && actual.hlod.covered_base_cells.checked_add(actual.resident_cells)
                == Some(usize::from(expected.dimensions[0]) * usize::from(expected.dimensions[1]))
            && actual.hlod.tiles <= forge_world::terrain_hlod::MAX_TERRAIN_HLOD_TILES
            && actual.hlod.aggregated_tiles <= actual.hlod.tiles && actual.hlod.maximum_level <= 7
            && (actual.hlod.covered_base_cells == 0
                || (actual.hlod.tiles > 0 && actual.hlod.cpu_mesh_bytes > 0 && actual.hlod.resident_triangles > 0))
            && actual.cpu_mesh_bytes.checked_add(actual.hlod.cpu_mesh_bytes)
                .is_some_and(|bytes| bytes <= expected.maximum_cpu_mesh_bytes)
            && actual.hlod.peak_staging_mesh_bytes <= expected.maximum_staging_cpu_mesh_bytes => Ok(()),
        _ => Err("Streamed terrain requires the admitted source, bounded residency and synchronized physics".into()),
    }
}

fn verify_procedural_streaming(
    expected: Option<&ExpectedProceduralTerrain>,
    actual: Option<&ObservedProceduralTerrain>,
) -> Result<(), String> {
    match (expected, actual) {
        (None, None) => Ok(()),
        (Some(expected), Some(actual)) if actual.identity == *expected
            && actual.resident_cells > 0 && actual.detailed_cells > 0
            && actual.detailed_cells.checked_add(actual.coarse_cells) == Some(actual.resident_cells)
            && actual.resident_cells <= usize::from(expected.maximum_cells)
            && actual.cpu_mesh_bytes > 0 && actual.cpu_mesh_bytes <= expected.maximum_cpu_mesh_bytes
            && actual.resident_triangles > 0 && actual.committed_transitions > 0
            && !actual.pending && actual.physics_ready && actual.error.is_none()
            && actual.source_synchronized
            && actual.hlod.covered_base_cells.checked_add(actual.resident_cells)
                == Some(usize::from(expected.dimensions[0]) * usize::from(expected.dimensions[1]))
            && actual.hlod.tiles <= forge_world::terrain_hlod::MAX_TERRAIN_HLOD_TILES
            && actual.hlod.aggregated_tiles <= actual.hlod.tiles && actual.hlod.maximum_level <= 3
            && (actual.hlod.covered_base_cells == 0
                || (actual.hlod.tiles > 0 && actual.hlod.cpu_mesh_bytes > 0 && actual.hlod.resident_triangles > 0))
            && actual.cpu_mesh_bytes.checked_add(actual.hlod.cpu_mesh_bytes)
                .is_some_and(|bytes| bytes <= expected.maximum_cpu_mesh_bytes)
            && actual.hlod.peak_staging_mesh_bytes <= expected.maximum_staging_cpu_mesh_bytes => Ok(()),
        _ => Err("Procedural streaming requires its pinned source, bounded residency and synchronized physics".into()),
    }
}

#[cfg(test)]
mod procedural_streaming_tests {
    use super::*;

    #[test]
    fn procedural_evidence_requires_its_own_source_coverage_and_physics() {
        let expected = ExpectedProceduralTerrain {
            document_hash: forge_core::bytes_sha256(b"procedural terrain"),
            maximum_cells: 64,
            maximum_cpu_mesh_bytes: 6_400_000,
            dimensions: [8, 8],
            maximum_staging_cpu_mesh_bytes: 12_800_000,
        };
        let actual = ObservedProceduralTerrain {
            identity: expected.clone(),
            resident_cells: 25,
            detailed_cells: 9,
            coarse_cells: 16,
            cpu_mesh_bytes: 1_000_000,
            resident_triangles: 10_000,
            committed_transitions: 1,
            pending: false,
            physics_ready: true,
            error: None,
            source_synchronized: true,
            hlod: ObservedTerrainHlod {
                tiles: 15,
                aggregated_tiles: 3,
                covered_base_cells: 39,
                maximum_level: 2,
                cpu_mesh_bytes: 500_000,
                resident_triangles: 2_000,
                peak_staging_mesh_bytes: 2_000_000,
            },
        };
        assert!(verify_procedural_streaming(Some(&expected), Some(&actual)).is_ok());
        assert!(verify_procedural_streaming(Some(&expected), None).is_err());
        assert!(verify_procedural_streaming(None, Some(&actual)).is_err());
        for case in 0..5 {
            let mut bad = actual.clone();
            match case {
                0 => bad.identity.document_hash = forge_core::bytes_sha256(b"other"),
                1 => bad.physics_ready = false,
                2 => bad.hlod.covered_base_cells -= 1,
                3 => bad.hlod.peak_staging_mesh_bytes = expected.maximum_staging_cpu_mesh_bytes + 1,
                _ => bad.source_synchronized = false,
            }
            assert!(verify_procedural_streaming(Some(&expected), Some(&bad)).is_err());
        }
    }
}

fn verify_static_hlod(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    if expected.streamed_terrain.is_none() && expected.procedural_streaming.is_none() {
        return Ok(());
    }
    let actual = observed
        .static_hlod
        .as_ref()
        .ok_or("Streamed module static render coverage is missing")?;
    let objects = expected
        .manual
        .iter()
        .filter(|placement| !placement.non_colliding)
        .count()
        .checked_add(expected.building_count)
        .ok_or("Static source count overflow")?;
    let parts = observed
        .manual
        .iter()
        .filter(|row| !row.identity.non_colliding)
        .try_fold(
            expected
                .building_count
                .checked_mul(8)
                .ok_or("Static room count overflow")?,
            |sum, source| {
                sum.checked_add(source.mesh_primitives)
                    .ok_or("Static primitive count overflow")
            },
        )?;
    if actual.source_objects != objects
        || actual.source_primitives != parts
        || actual.source_primitives != actual.covered_primitives
        || (objects != 0
            && (actual.source_primitives < objects
                || actual.source_triangles == 0
                || actual.selected_triangles == 0
                || actual.retained_source_mesh_bytes == 0))
        || actual.selected_triangles > actual.source_triangles
        || (actual.rendered_batches == 0 && actual.selected_triangles != actual.source_triangles)
        || actual.source_primitives > 4096
        || actual.source_snapshot_bytes > 64 * 1024 * 1024
        || actual.derived_mesh_bytes > 32 * 1024 * 1024
        || actual.rendered_batches != actual.desired_batches
        || (!actual.enabled && (actual.desired_batches != 0 || actual.replaced_primitives != 0))
        || actual.replaced_primitives > actual.source_primitives
        || (actual.rendered_batches > 0
            && (!actual.gpu_upload_ready
                || actual.replaced_primitives == 0
                || actual.derived_mesh_bytes == 0
                || actual.committed_transitions == 0))
        || !actual.maximum_position_error_m.is_finite()
        || !(0.0..=1.0).contains(&actual.maximum_position_error_m)
        || actual.maximum_attribute_deltas.iter().any(|(name, delta)| {
            let maximum = match name.as_str() {
                "Vertex_Uv" | "Vertex_Uv_1" => f64::from(0.01_f32),
                "Vertex_Normal" | "Vertex_Tangent" => f64::from(0.1_f32),
                _ => 0.0,
            };
            !delta.is_finite() || !(0.0..=maximum).contains(delta)
        })
        || (actual.rendered_batches > 0 && actual.minimum_face_alignment.is_none())
        || (actual.rendered_batches == 0
            && (actual.replaced_primitives != 0
                || actual.derived_mesh_bytes != 0
                || actual.gpu_upload_ready))
        || actual
            .minimum_face_alignment
            .is_some_and(|value| !value.is_finite() || value < 0.5)
        || actual.pending
        || actual.error.is_some()
    {
        return Err("Static HLOD requires complete current source coverage and GPU-acknowledged replacement".into());
    }
    Ok(())
}

fn verify_environment(
    expected: Option<&EnvironmentIdentity>,
    actual: Option<&EnvironmentObservation>,
) -> Result<(), String> {
    match (expected, actual) {
        (None, None) => Ok(()),
        (Some(expected), Some(actual))
            if actual.identity == Some(expected.identity)
                && solar_matches(expected.solar_clock, actual.solar.as_ref())
                && actual.ready
                && actual.error.is_none()
                && actual.bootstrap_steps > 0
                && actual.cloud_steps > actual.bootstrap_steps
                && actual.cloud_submissions >= actual.bootstrap_steps
                && actual.live_submissions > 0
                && actual.programs_expected == expected.live_programs
                && actual.gpu_images == [5, 5]
                && actual.gpu_materials == [2, 2]
                && actual
                    .gpu_completed
                    .zip(actual.publication_frame)
                    .is_some_and(|(done, frame)| done >= frame)
                && actual
                    .publication_frame
                    .is_some_and(|frame| actual.gpu_observed >= frame)
                && if expected.live_programs {
                    actual.programs_prepared == 22
                        && actual.hdr_publication.is_some_and(|id| id > 0)
                } else {
                    actual.programs_prepared == 0 && actual.hdr_publication.is_none()
                } =>
        {
            Ok(())
        }
        _ => Err("The declared environment has no matching completed renderer resources".into()),
    }
}

#[cfg(test)]
mod imported_tests {
    use super::*;

    #[test]
    fn source_surfaces_require_completed_casters_and_the_visible_motion() {
        let status = SurfaceStatus {
            world_receivers: 4,
            model_receivers: 3,
            terrain_receivers: 1,
            active: true,
            ready: true,
            generation: 2,
            opaque_casters: 8,
            plant_batches: 5,
            encoded_frames: 9,
            cascade_layers: [9, 5, 2],
            encoded_frame: Some(30),
            gpu_completed: Some(32),
            motion_matches: true,
        };
        assert!(verify_surfaces(true, &status));
        assert!(!verify_surfaces(false, &status));
        assert!(!verify_surfaces(true, &SurfaceStatus::default()));
        for fault in 0..8 {
            let mut invalid = status.clone();
            match fault {
                0 => invalid.motion_matches = false,
                1 => invalid.gpu_completed = Some(29),
                2 => invalid.encoded_frames = 0,
                3 => invalid.cascade_layers[2] = 0,
                4 => invalid.plant_batches = 0,
                5 => invalid.active = false,
                6 => invalid.opaque_casters = 0,
                _ => invalid.world_receivers = 0,
            }
            assert!(!verify_surfaces(true, &invalid));
        }
    }

    #[test]
    fn grass_requires_its_exact_fields_and_a_completed_gpu_inventory() {
        let identity = forge_core::bytes_sha256(b"grass fields");
        let status = GrassStatus {
            identity: Some(identity),
            ready: true,
            counts: [7, 12, 0],
            generation: 2,
            sequence: 1,
            publication_frame: 30,
            gpu_epoch: 4,
            gpu_observed: 35,
            gpu_completed: Some(32),
            gpu_meshes: [3, 3],
            gpu_materials: [3, 3],
            gpu_images: [8, 8],
            gpu_buffers: [6, 6],
            pipelines: [1, 0, 0],
        };
        assert!(verify_grass(Some(identity), &status));
        let mut source = status.clone();
        source.gpu_images = [11, 11];
        source.gpu_buffers = [8, 8];
        assert!(verify_grass_mode(Some(identity), &source, true));
        for buffers in [[7, 7], [7, 8], [8, 7], [9, 9]] {
            let mut invalid = source.clone();
            invalid.gpu_buffers = buffers;
            assert!(!verify_grass_mode(Some(identity), &invalid, true));
        }
        for buffers in [[5, 5], [5, 6], [6, 5], [7, 7]] {
            let mut invalid = status.clone();
            invalid.gpu_buffers = buffers;
            assert!(!verify_grass(Some(identity), &invalid));
        }
        assert!(!verify_grass(None, &status));
        for index in 0..7 {
            let mut invalid = status.clone();
            match index {
                0 => invalid.identity = None,
                1 => invalid.gpu_completed = Some(29),
                2 => invalid.gpu_images[1] -= 1,
                3 => invalid.pipelines[1] = 1,
                4 => invalid.sequence = 0,
                5 => invalid.counts[0] = 49,
                _ => invalid.ready = false,
            }
            assert!(!verify_grass(Some(identity), &invalid));
        }
    }

    #[test]
    fn imported_evidence_requires_all_admissions_and_exact_sources() {
        let expected = HostIdentity {
            source_lighting: false,
            grass: None,
            projection: forge_core::bytes_sha256(b"projection"),
            head: forge_core::bytes_sha256(b"head"),
            terrain: forge_core::bytes_sha256(b"raster"),
            instances: 12,
            colliders: 12,
        };
        let observed = HostStatus {
            identity: Some(expected.projection),
            head: Some(expected.head),
            terrain: Some(expected.terrain),
            ready: true,
            admission: HostAdmission {
                render_ready: true,
                collisions_ready: true,
                frame_ready: true,
            },
            colliders: 12,
            canonical_instances: 12,
            canonical_colliders: 12,
            entities: 12,
            detail: 4,
            grouped: 8,
            generation: 2,
            sequence: 1,
            ..Default::default()
        };
        assert!(verify_imported(Some(&expected), Some(&observed)).is_ok());
        let mut models = observed.clone();
        models.replacement_primitives = 100;
        models.entities += 100;
        assert!(verify_imported(Some(&expected), Some(&models)).is_err());
        assert!(verify_imported_mode(Some(&expected), Some(&models), true).is_ok());
        models.entities += 1;
        assert!(verify_imported_mode(Some(&expected), Some(&models), true).is_err());
        models.entities = 125;
        models.detail = 17;
        assert!(verify_imported_mode(Some(&expected), Some(&models), true).is_err());
        let mut distant = observed.clone();
        distant.detail = 0;
        distant.grouped = 5;
        distant.entities = 5;
        distant.colliders = 2;
        assert!(verify_imported(Some(&expected), Some(&distant)).is_ok());
        let mut overlap = observed.clone();
        overlap.detail = 8;
        overlap.grouped = 12;
        overlap.entities = 20;
        assert!(verify_imported(Some(&expected), Some(&overlap)).is_ok());
        for index in 0..13 {
            let mut invalid = observed.clone();
            match index {
                0 => invalid.identity = None,
                1 => invalid.head = Some(expected.terrain),
                2 => invalid.terrain = Some(expected.head),
                3 => invalid.admission.render_ready = false,
                4 => invalid.admission.collisions_ready = false,
                5 => invalid.admission.frame_ready = false,
                6 => invalid.canonical_colliders -= 1,
                7 => invalid.grouped -= 1,
                8 => invalid.sequence = 0,
                9 => invalid.canonical_instances -= 1,
                10 => invalid.entities += 1,
                11 => invalid.colliders += 1,
                _ => invalid.error = Some("backend changed".into()),
            }
            assert!(verify_imported(Some(&expected), Some(&invalid)).is_err());
        }
        assert!(verify_imported(Some(&expected), None).is_err());
        assert!(verify_imported(None, Some(&observed)).is_err());
    }
}

#[cfg(test)]
mod environment_tests {
    use super::*;
    #[test]
    fn environment_requires_its_exact_fenced_resources_and_live_graph_when_declared() {
        let hash = forge_core::bytes_sha256(b"environment");
        let expected = EnvironmentIdentity {
            solar_clock: None,
            identity: hash,
            live_programs: false,
        };
        let actual = EnvironmentObservation {
            solar: None,
            identity: Some(hash),
            ready: true,
            bootstrap_steps: 120,
            cloud_steps: 121,
            cloud_submissions: 121,
            live_submissions: 1,
            programs_expected: false,
            programs_prepared: 0,
            hdr_publication: None,
            publication_frame: Some(140),
            gpu_observed: 142,
            gpu_completed: Some(141),
            gpu_images: [5, 5],
            gpu_materials: [2, 2],
            error: None,
        };
        verify_environment(Some(&expected), Some(&actual)).unwrap();
        assert!(verify_environment(Some(&expected), None).is_err());
        assert!(verify_environment(None, Some(&actual)).is_err());
        for mutation in 0..10 {
            let mut bad = actual.clone();
            match mutation {
                0 => bad.identity = Some(forge_core::bytes_sha256(b"other")),
                1 => bad.ready = false,
                2 => bad.cloud_steps = 120,
                3 => bad.live_submissions = 0,
                4 => bad.gpu_completed = None,
                5 => bad.gpu_completed = Some(139),
                6 => bad.gpu_images = [4, 4],
                7 => bad.gpu_materials = [1, 1],
                8 => bad.error = Some("failed".into()),
                _ => bad.hdr_publication = Some(1),
            }
            assert!(
                verify_environment(Some(&expected), Some(&bad)).is_err(),
                "mutation {mutation}"
            );
        }
        let expected = EnvironmentIdentity {
            solar_clock: None,
            live_programs: true,
            ..expected
        };
        let mut hdr = EnvironmentObservation {
            solar: None,
            programs_expected: true,
            programs_prepared: 22,
            hdr_publication: Some(1),
            ..actual
        };
        verify_environment(Some(&expected), Some(&hdr)).unwrap();
        hdr.hdr_publication = None;
        assert!(verify_environment(Some(&expected), Some(&hdr)).is_err());
        hdr.hdr_publication = Some(1);
        hdr.programs_prepared = 21;
        assert!(verify_environment(Some(&expected), Some(&hdr)).is_err());
    }
}

fn solar_matches(
    expected: Option<forge_project::module_package::ModuleSolarClock>,
    actual: Option<&ObservedSolarClock>,
) -> bool {
    match (expected, actual) {
        (None, None) => true,
        (Some(expected), Some(actual)) => {
            actual.start_minutes == expected.start_minutes
                && actual.ready
                && actual.hours_bits.is_some_and(|bits| {
                    let hours = f64::from_bits(bits);
                    hours.is_finite()
                        && (0.0..=24.0).contains(&hours)
                        && (expected.day_length_seconds.is_some()
                            || bits == (f64::from(expected.start_minutes) / 60.0).to_bits())
                })
                && actual.night_bits.is_some_and(|bits| {
                    let night = f32::from_bits(bits);
                    night.is_finite() && (0.0..=1.0).contains(&night)
                })
        }
        _ => false,
    }
}

#[cfg(test)]
mod solar_observation_tests {
    use super::*;
    #[test]
    fn declared_paused_solar_clock_requires_ready_matching_observation() {
        let expected = forge_project::module_package::ModuleSolarClock {
            captured_minutes: 972,
            start_minutes: 0,
            azimuth_degrees: 0,
            day_length_seconds: None,
        };
        let actual = ObservedSolarClock {
            start_minutes: 0,
            hours_bits: Some(0.0_f64.to_bits()),
            night_bits: Some(1.0_f32.to_bits()),
            ready: true,
        };
        assert!(solar_matches(Some(expected), Some(&actual)));
        assert!(!solar_matches(Some(expected), None));
        assert!(!solar_matches(None, Some(&actual)));
        for case in 0..6 {
            let mut bad = actual.clone();
            match case {
                0 => bad.ready = false,
                1 => bad.start_minutes = 1,
                2 => bad.hours_bits = None,
                3 => bad.hours_bits = Some(12.0_f64.to_bits()),
                4 => bad.night_bits = None,
                _ => bad.night_bits = Some(f32::NAN.to_bits()),
            }
            assert!(!solar_matches(Some(expected), Some(&bad)), "case {case}");
        }
    }
}
