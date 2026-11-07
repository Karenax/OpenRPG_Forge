//! Playable host for admitted terrain cells, shared renderer and Rapier guard.
use crate::{
    PlayerControllerPlugin, ProofSceneStats, RuntimeAutomation, RuntimeEvidencePlugin,
    TerrainLightingPlugin,
    cell_streaming::{
        CellStreamingStats, PlayerCellStreamingPlugin, ProceduralCellStreamingPlugin,
        SampledCellStreamingPlugin,
    },
};
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};
use forge_project::PlayerControllerProfileDocument;
use forge_world::{
    TerrainGenerationPlan, cell_streaming::CellStreamingPolicy, sampled_terrain::SampledTerrainPlan,
};
use std::sync::Arc;

#[derive(Resource, Clone, Debug)]
pub struct StreamedTerrainIdentity {
    pub document_hash: ContentHash,
    pub raster_hash: ContentHash,
    pub raster_bytes: u64,
    pub policy: CellStreamingPolicy,
}

/// Uses Forge's authored PBR palette, shared sun, player and capture lifecycle.
/// No complete terrain mesh or complete terrain collider is generated at startup.
pub struct StreamedTerrainPlugin {
    terrain: StreamedTerrainSource,
    policy: CellStreamingPolicy,
    session_id: EntityId,
    profile: PlayerControllerProfileDocument,
    spawn: Vec3,
    camera_target: Option<Vec3>,
    automation: RuntimeAutomation,
}

enum StreamedTerrainSource {
    Sampled(Arc<SampledTerrainPlan>),
    Procedural(Arc<TerrainGenerationPlan>),
}

impl StreamedTerrainSource {
    fn document_hash(&self) -> ContentHash {
        match self {
            Self::Sampled(plan) => plan.document_hash(),
            Self::Procedural(plan) => plan.document_hash(),
        }
    }

    fn material_layers(&self) -> usize {
        match self {
            Self::Sampled(plan) => plan.document().material_layers.len(),
            Self::Procedural(plan) => plan.document().material_layers.len(),
        }
    }
}

impl StreamedTerrainPlugin {
    pub fn new(
        terrain: Arc<SampledTerrainPlan>,
        policy: CellStreamingPolicy,
        session_id: EntityId,
        profile: PlayerControllerProfileDocument,
        spawn: Vec3,
    ) -> Result<Self, String> {
        Self::from_source(
            StreamedTerrainSource::Sampled(terrain),
            policy,
            session_id,
            profile,
            spawn,
        )
    }

    /// Prepare the same host for the finite procedural plan without a whole-map cache.
    pub fn new_procedural(
        terrain: Arc<TerrainGenerationPlan>,
        policy: CellStreamingPolicy,
        session_id: EntityId,
        profile: PlayerControllerProfileDocument,
        spawn: Vec3,
    ) -> Result<Self, String> {
        Self::from_source(
            StreamedTerrainSource::Procedural(terrain),
            policy,
            session_id,
            profile,
            spawn,
        )
    }

    /// The supported procedural grid is currently fixed at eight cells per axis.
    pub fn procedural_policy(
        terrain: &TerrainGenerationPlan,
    ) -> Result<CellStreamingPolicy, String> {
        let count = forge_world::TERRAIN_CHUNKS_PER_AXIS;
        let per_cell = 100_000_u64;
        let max_resident_cells = count * count;
        let max_resident_bytes = u64::from(max_resident_cells) * per_cell;
        let policy = CellStreamingPolicy {
            origin_xz_m: terrain.origin_xz_m().map(f64::from),
            cell_size_m: f64::from(forge_world::TERRAIN_CHUNK_INTERVALS)
                * f64::from(forge_world::TERRAIN_SAMPLE_SPACING_M),
            dimensions: [count; 2],
            detail_radius: 1,
            far_radius: 2,
            max_resident_cells,
            full_cell_bytes: per_cell,
            hlod_cell_bytes: per_cell,
            max_resident_bytes,
            max_staging_bytes: 2 * max_resident_bytes,
        };
        policy.validate().map_err(|error| error.to_string())?;
        Ok(policy)
    }

    fn from_source(
        terrain: StreamedTerrainSource,
        policy: CellStreamingPolicy,
        session_id: EntityId,
        profile: PlayerControllerProfileDocument,
        spawn: Vec3,
    ) -> Result<Self, String> {
        profile.validate().map_err(|error| error.to_string())?;
        if !spawn.is_finite() || terrain.material_layers() != 3 {
            return Err("Streamed player pose or the three-role terrain palette is invalid".into());
        }
        let position = [f64::from(spawn.x), f64::from(spawn.z)];
        match &terrain {
            StreamedTerrainSource::Sampled(plan) => {
                SampledCellStreamingPlugin::new(
                    plan.clone(),
                    policy.clone(),
                    session_id,
                    StandardMaterial::default(),
                    position,
                )?;
            }
            StreamedTerrainSource::Procedural(plan) => {
                ProceduralCellStreamingPlugin::new(
                    plan.clone(),
                    policy.clone(),
                    session_id,
                    StandardMaterial::default(),
                    position,
                )?;
            }
        }
        Ok(Self {
            terrain,
            policy,
            session_id,
            profile,
            spawn,
            camera_target: None,
            automation: RuntimeAutomation::default(),
        })
    }

    #[must_use]
    pub fn with_automation(mut self, automation: RuntimeAutomation) -> Self {
        self.automation = automation;
        self
    }

    #[must_use]
    pub const fn policy(&self) -> &CellStreamingPolicy {
        &self.policy
    }

    pub fn with_camera_target(mut self, target: Vec3) -> Result<Self, String> {
        if !target.is_finite() || target == self.spawn {
            return Err("Streamed player camera direction is invalid".into());
        }
        self.camera_target = Some(target);
        Ok(self)
    }
}

impl Plugin for StreamedTerrainPlugin {
    fn build(&self, app: &mut App) {
        if let StreamedTerrainSource::Sampled(terrain) = &self.terrain {
            let source = terrain.raster().source();
            app.insert_resource(StreamedTerrainIdentity {
                document_hash: terrain.document_hash(),
                raster_hash: source.samples_sha256,
                raster_bytes: source.byte_length().expect("Validated raster") as u64,
                policy: self.policy.clone(),
            });
        }
        app.insert_resource(ProofSceneStats {
            source_entities: 1,
            source_mesh_instances: 0,
            source_hash: self.terrain.document_hash().to_string(),
        })
        .insert_resource(ClearColor(Color::srgb(0.32, 0.48, 0.66)));
        crate::sampled_terrain_physics::register(app);
        let material = StandardMaterial {
            base_color: Color::WHITE,
            ..default()
        };
        let position = [f64::from(self.spawn.x), f64::from(self.spawn.z)];
        app.add_plugins(TerrainLightingPlugin);
        match &self.terrain {
            StreamedTerrainSource::Sampled(terrain) => {
                app.add_plugins(
                    SampledCellStreamingPlugin::new(
                        terrain.clone(),
                        self.policy.clone(),
                        self.session_id,
                        material,
                        position,
                    )
                    .expect("validated sampled cell adapter")
                    .with_authored_material_layers()
                    .with_terrain_hlod(),
                );
            }
            StreamedTerrainSource::Procedural(terrain) => {
                app.add_plugins(
                    ProceduralCellStreamingPlugin::new(
                        terrain.clone(),
                        self.policy.clone(),
                        self.session_id,
                        material,
                        position,
                    )
                    .expect("validated procedural cell adapter")
                    .with_authored_material_layers()
                    .with_terrain_hlod(),
                );
            }
        }
        let mut player = PlayerControllerPlugin::new(self.profile.clone(), self.spawn);
        if let Some(target) = self.camera_target {
            player = player.with_initial_camera_target(target);
        }
        app.add_plugins((
            player,
            PlayerCellStreamingPlugin,
            RuntimeEvidencePlugin::new(self.automation.clone()),
        ))
        .add_systems(PostUpdate, observe_residency);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn observe_residency(cells: Res<CellStreamingStats>, mut stats: ResMut<ProofSceneStats>) {
    stats.source_mesh_instances = cells.resident_cells;
}

#[cfg(test)]
mod tests {
    use super::*;

    fn policy() -> CellStreamingPolicy {
        CellStreamingPolicy {
            origin_xz_m: [-128.0; 2],
            cell_size_m: 32.0,
            dimensions: [8; 2],
            detail_radius: 1,
            far_radius: 3,
            max_resident_cells: 81,
            full_cell_bytes: 100_000,
            hlod_cell_bytes: 100_000,
            max_resident_bytes: 8_100_000,
            max_staging_bytes: 16_200_000,
        }
    }

    #[test]
    fn installed_host_refuses_mismatched_grids_and_unsafe_initial_poses_without_source_mutation() {
        let source = crate::cell_streaming::tests::terrain_fixture();
        let hash = source.document_hash();
        for fault in 0..4 {
            let mut policy = policy();
            let mut spawn = Vec3::Y * 2.0;
            match fault {
                0 => policy.origin_xz_m[0] += 1.0,
                1 => policy.cell_size_m *= 2.0,
                2 => spawn.x = 128.0,
                _ => spawn.y = f32::NAN,
            }
            assert!(
                StreamedTerrainPlugin::new(
                    source.clone(),
                    policy,
                    EntityId::deterministic("streamed-host"),
                    forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
                    spawn
                )
                .is_err()
            );
        }
        let plugin = StreamedTerrainPlugin::new(
            source.clone(),
            policy(),
            EntityId::deterministic("streamed-host"),
            forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
            Vec3::Y * 2.0,
        )
        .unwrap();
        let StreamedTerrainSource::Sampled(admitted) = &plugin.terrain else {
            panic!("sampled host changed source type");
        };
        assert!(Arc::ptr_eq(admitted, &source));
        assert_eq!(source.document_hash(), hash);
        assert!(plugin.with_camera_target(Vec3::splat(f32::NAN)).is_err());
    }

    #[test]
    fn procedural_host_reuses_the_pinned_plan_and_rejects_invalid_grid_or_spawn() {
        let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let source = Arc::new(TerrainGenerationPlan::from_document(&document).unwrap());
        let policy = StreamedTerrainPlugin::procedural_policy(&source).unwrap();
        let profile = forge_world::embedded_visual_valley_player_controller_profile().unwrap();
        let plugin = StreamedTerrainPlugin::new_procedural(
            source.clone(),
            policy.clone(),
            EntityId::deterministic("procedural-host"),
            profile.clone(),
            Vec3::Y * 2.0,
        )
        .unwrap();
        let StreamedTerrainSource::Procedural(admitted) = &plugin.terrain else {
            panic!("procedural host changed source type");
        };
        assert!(Arc::ptr_eq(admitted, &source));
        assert_eq!(plugin.policy(), &policy);
        let mut invalid = policy.clone();
        invalid.cell_size_m += 1.0;
        assert!(
            StreamedTerrainPlugin::new_procedural(
                source.clone(),
                invalid,
                EntityId::deterministic("procedural-host"),
                profile.clone(),
                Vec3::Y * 2.0,
            )
            .is_err()
        );
        assert!(
            StreamedTerrainPlugin::new_procedural(
                source,
                policy,
                EntityId::deterministic("procedural-host"),
                profile,
                Vec3::new(256.0, 2.0, 0.0),
            )
            .is_err()
        );
    }
}
