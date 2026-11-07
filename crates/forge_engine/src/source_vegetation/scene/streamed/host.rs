//! Playable raster host: one immutable projected snapshot, render selection and physics gate.
use super::{Admission, Prepared, Resident, Selection, WorldEnvelopes};
use crate::source_lighting::frame::Frame;
use crate::source_lighting::frame::MAX_WIND_SPEED;
use crate::source_vegetation::Params;
use crate::source_vegetation::textures::PreparedTextures;
use crate::source_vegetation::world::WorldPlantMaterial;
use crate::{
    PlayerControllerSet, PlayerSimulationReady,
    cell_streaming::PlayerCellStreamingSet,
    physics::imported_vegetation::{
        self as physical, ImportedPlantActivationSet, ImportedPlantCollisionPlugin,
    },
    render_inventory::RenderInventoryPlugin,
};
use bevy::{pbr::MaterialPlugin, prelude::*};
use forge_assets::source_vegetation_package::{PlantPackage, geometry::PlantGeometry};
use forge_core::{ContentHash, EntityId};
use forge_project::{VegetationPresentationProfile, zone_authoring::ZoneAuthoringHead};
use forge_world::imported_vegetation::{
    ImportedPopulation,
    streaming::{ImportedTerrainSnapshot, PlantView},
};
pub mod picking;
mod runtime;
mod species;
use runtime::{State, early, final_gate, update_frame};
pub use species::Status as SpeciesStatus;
use std::sync::Arc;

pub struct PreparedHost {
    species: Option<species::Config>,
    surfaces: Option<crate::source_lighting::surfaces::scene::Prepared>,
    source_lighting: bool,
    grass: Option<crate::source_vegetation::grass::fields::PreparedFields>,
    projection: Prepared,
    bounds: WorldEnvelopes,
    population: Arc<ImportedPopulation>,
    snapshot: ImportedTerrainSnapshot,
    textures: PreparedTextures,
    shader: String,
    presentation: VegetationPresentationProfile,
    owner: EntityId,
    head: ContentHash,
    terrain: ContentHash,
}

impl PreparedHost {
    /// All CPU decoding, reprojection and bounds finish before installation.
    /// The package comes from the ordinary closed module reader, without fallback.
    pub fn new(
        package: &PlantPackage,
        mut geometry: PlantGeometry,
        population: impl Into<Arc<ImportedPopulation>>,
        head: &ZoneAuthoringHead,
        heights: &[u8],
        owner: EntityId,
    ) -> Result<Self, String> {
        let population = population.into();
        if owner.as_uuid().is_nil() {
            return Err("Imported plant host requires a real owner".into());
        }
        let terrain = head
            .effective()?
            .0
            .content_hash()
            .map_err(|e| e.to_string())?;
        let grass = population
            .document()
            .imported_source
            .as_ref()
            .and_then(|source| source.grass.as_ref())
            .map(|grass| {
                if grass.profile_manifest_sha256 != package.manifest_sha256() {
                    return Err("Authored grass profile differs from the admitted package".into());
                }
                let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
                    &head.effective()?.0,
                    heights,
                )
                .map_err(|e| e.to_string())?;
                crate::source_vegetation::grass::fields::PreparedFields::new(
                    package,
                    std::mem::take(&mut geometry.grass),
                    &plan,
                )
            })
            .transpose()?;
        let projection = Prepared::new(package, geometry, &population, head, heights, terrain)?;
        let view = population.terrain_view(head, heights)?;
        let bounds = projection.world_envelopes(&population, &view)?;
        let snapshot = view.into_snapshot();
        let shader = std::str::from_utf8(
            package
                .file("plant.wgsl")
                .ok_or("Installed plant shader absent")?,
        )
        .map_err(|e| e.to_string())?
        .to_owned();
        let textures = PreparedTextures::from_package(package)?;
        let presentation = population.document().presentation;
        Ok(Self {
            species: None,
            surfaces: None,
            source_lighting: false,
            grass,
            projection,
            bounds,
            population,
            snapshot,
            textures,
            shader,
            presentation,
            owner,
            head: head.content_hash()?,
            terrain,
        })
    }

    #[must_use]
    pub fn matches(&self, head: ContentHash, terrain: ContentHash) -> bool {
        self.head == head && self.terrain == terrain
    }

    /// Explicit source-lighting admission from the same closed plant and terrain inputs.
    pub fn with_source_lighting(
        mut self,
        package: &PlantPackage,
        terrain: &forge_world::sampled_terrain::SampledTerrainPlan,
    ) -> Result<Self, String> {
        if terrain.document_hash() != self.terrain {
            return Err("Source surfaces require their exact authored terrain".into());
        }
        self.surfaces = Some(
            crate::source_lighting::surfaces::scene::Prepared::new(terrain)?.with_plants(
                crate::source_vegetation::scene::shadow::Source::from_vegetation(
                    &self.projection.scene,
                    package,
                )?,
            ),
        );
        self.source_lighting = true;
        Ok(self)
    }

    /// Keep model choice separate from the imported population and its physics.
    /// The caller installs the ordinary pinned prototype loader in this App.
    pub fn with_species(
        mut self,
        head: &ZoneAuthoringHead,
        catalog: ContentHash,
    ) -> Result<Self, String> {
        if head.content_hash()? != self.head || !self.source_lighting {
            return Err("Imported species require this head and its source environment".into());
        }
        self.species = Some(species::Config {
            vegetation: head
                .effective()?
                .1
                .ok_or("Imported species vegetation absent")?,
            catalog,
        });
        Ok(self)
    }
    #[must_use]
    pub fn identity(&self) -> ContentHash {
        self.bounds.identity()
    }

    #[must_use]
    pub fn expected(&self) -> HostIdentity {
        let colliders = self
            .snapshot
            .layers()
            .filter(|(id, _)| {
                self.population
                    .document()
                    .layers
                    .iter()
                    .any(|layer| layer.id.as_str() == *id && layer.collision.is_some())
            })
            .map(|(_, instances)| instances.len())
            .sum();
        HostIdentity {
            source_lighting: self.source_lighting,
            grass: self.grass.as_ref().map(|grass| grass.identity),
            projection: self.identity(),
            head: self.head,
            terrain: self.terrain,
            instances: self.projection.pins.len(),
            colliders,
        }
    }

    pub fn install(mut self, app: &mut App) -> Result<(), String> {
        if app.world().contains_resource::<Pending>() || app.world().contains_resource::<State>() {
            return Err("Imported plants require their exclusive admitted raster host".into());
        }
        admit_terrain(app.world(), self.terrain)?;
        if self.species.is_some() || app.is_plugin_added::<crate::VegetationPrototypeLoaderPlugin>()
        {
            // An editor can select replacement models after opening an imported
            // raster. Its installed loader needs the authored wind profile and
            // material resources even before the first selection.
            let profile = self
                .species
                .as_ref()
                .map_or(self.presentation, |species| species.vegetation.presentation);
            crate::vegetation::install_imported_model_presentation(app, profile);
        }
        if !app.is_plugin_added::<MaterialPlugin<WorldPlantMaterial>>() {
            app.add_plugins(MaterialPlugin::<WorldPlantMaterial>::default());
        }
        if !app.is_plugin_added::<RenderInventoryPlugin>() {
            app.add_plugins(RenderInventoryPlugin);
        }
        if !app.is_plugin_added::<ImportedPlantCollisionPlugin>() {
            app.add_plugins(ImportedPlantCollisionPlugin);
        }
        if let Some(scene) = self.surfaces.take() {
            crate::source_lighting::surfaces::live::PreparedHost {
                scene,
                wind_speed: MAX_WIND_SPEED * self.presentation.wind_strength,
                wind_frequency: self.presentation.wind_speed_hz,
            }
            .install(app)?;
        }
        app.insert_resource(Pending(Some(self)))
            .init_resource::<HostStatus>()
            .add_systems(
                PostStartup,
                start
                    .after(crate::source_environment::host::EnvironmentHostSet)
                    .after(crate::source_lighting::surfaces::live::SurfaceHostSet),
            )
            .add_systems(
                PostUpdate,
                update_frame
                    .in_set(PlantFrameSet)
                    .after(bevy::transform::TransformSystems::Propagate)
                    .after(crate::source_environment::host::EnvironmentHostSet)
                    .after(crate::source_lighting::surfaces::live::SurfaceHostSet),
            )
            .configure_sets(
                FixedUpdate,
                ImportedPlantActivationSet
                    .after(PlayerControllerSet::Drive)
                    .after(crate::gameplay::GameplaySet::NpcDrive)
                    .after(PlayerCellStreamingSet::Enforce)
                    .before(HostSet::Final),
            )
            .add_systems(
                FixedUpdate,
                final_gate
                    .in_set(HostSet::Final)
                    .after(PlayerCellStreamingSet::Enforce)
                    .before(bevy_rapier3d::prelude::PhysicsSet::SyncBackend),
            )
            .insert_resource(PlayerSimulationReady(false));
        let guard = early
            .after(PlayerCellStreamingSet::Request)
            .before(PlayerControllerSet::Drive);
        #[cfg(not(target_arch = "wasm32"))]
        let guard = guard.before(crate::native_gameplay::NativeGameplayReadinessSet);
        app.add_systems(FixedUpdate, guard);
        Ok(())
    }

    /// Reuse the installed host for an accepted authoring head. All CPU inputs
    /// are prepared before retiring its owned render and collision generation.
    pub fn replace(mut self, world: &mut World, expected_head: ContentHash) -> Result<(), String> {
        let current = world
            .get_resource::<State>()
            .ok_or("Imported plant host absent")?;
        if current.head != expected_head
            || current.authoring_target != Some((self.head, self.terrain))
            || current.owner != self.owner
            || current.source_lighting != self.source_lighting
            || world
                .get_resource::<crate::cell_streaming::ResidentSampledTerrain>()
                .is_none_or(|terrain| terrain.plan().document_hash() != self.terrain)
        {
            return Err("Imported plant authoring replacement is stale or foreign".into());
        }
        if !current.render.projection_intact(world) {
            return Err("Imported plant ownership changed before authoring replacement".into());
        }
        if let Some(scene) = self.surfaces.take() {
            crate::source_lighting::surfaces::live::replace_scene(world, scene)?;
        }
        let old = world
            .remove_resource::<State>()
            .ok_or("Imported plant host disappeared")?;
        let retired = (|| {
            if let Some(species) = old.species.and_then(|species| species.candidate) {
                species.retire(world);
            }
            if let Some(physical) = old.physical {
                physical.retire(world)?;
            }
            if let Some(grass) = old.grass {
                grass.retire(world);
            }
            old.render.retire(world)
        })();
        if let Err(error) = retired {
            runtime::fail(world, error.clone());
            return Err(error);
        }
        *world.resource_mut::<HostStatus>() = HostStatus::default();
        match stage(world, self) {
            Ok(next) => {
                world.insert_resource(next);
                Ok(())
            }
            Err(error) => {
                runtime::fail(world, error.clone());
                Err(error)
            }
        }
    }
}

#[derive(Resource)]
struct Pending(Option<PreparedHost>);

pub(super) fn admit_terrain(world: &World, expected: ContentHash) -> Result<(), String> {
    if world
        .get_resource::<crate::cell_streaming::ResidentSampledTerrain>()
        .is_none_or(|terrain| terrain.plan().document_hash() != expected)
    {
        return Err("Imported plants require their exclusive admitted raster host".into());
    }
    Ok(())
}

/// Announce an authoring transition before the raster controller can swap cells.
/// The immutable game path still rejects every unannounced terrain replacement.
pub fn suspend_authoring(
    world: &mut World,
    expected_head: ContentHash,
    head: &ZoneAuthoringHead,
) -> Result<(), String> {
    let next = (
        head.content_hash()?,
        head.effective()?
            .0
            .content_hash()
            .map_err(|e| e.to_string())?,
    );
    let mut state = world
        .get_resource_mut::<State>()
        .ok_or("Imported plant host absent")?;
    let source = state.population.document();
    if state.head != expected_head
        || source.project_id != head.base.terrain.project_id
        || source.area_id != head.base.terrain.area_id
    {
        return Err("Imported plant authoring transition is stale or foreign".into());
    }
    state.authoring_target = Some(next);
    state.frame_ready = false;
    let mut status = world.resource_mut::<HostStatus>();
    status.ready = false;
    status.admission.frame_ready = false;
    Ok(())
}
/// Completion of the current source frame, including selected model uniforms.
#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct PlantFrameSet;

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
enum HostSet {
    Final,
}

#[derive(Default, Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct HostAdmission {
    pub render_ready: bool,
    pub collisions_ready: bool,
    pub frame_ready: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
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

#[derive(Resource, Default, Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct HostStatus {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub species: Option<SpeciesStatus>,
    #[serde(default)]
    pub surfaces: crate::source_lighting::surfaces::scene::Status,
    #[serde(default)]
    pub grass: crate::source_vegetation::grass::host::Status,
    pub identity: Option<ContentHash>,
    pub head: Option<ContentHash>,
    pub terrain: Option<ContentHash>,
    pub ready: bool,
    /// Physical source plus admitted resources, independent of new visibility.
    #[serde(default)]
    pub simulation_ready: bool,
    pub admission: HostAdmission,
    pub entities: usize,
    /// Actual drawn model primitives, separate from source plant LOD parts.
    #[serde(default, skip_serializing_if = "is_zero")]
    pub replacement_primitives: usize,
    /// Complete admitted authored population, independent of visible overlapping LODs.
    pub canonical_instances: usize,
    pub canonical_colliders: usize,
    pub colliders: usize,
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
fn is_zero(value: &usize) -> bool {
    *value == 0
}

fn start(world: &mut World) {
    let Some(mut pending) = world.remove_resource::<Pending>() else {
        return;
    };
    let Some(inputs) = pending.0.take() else {
        return;
    };
    match stage(world, inputs) {
        Ok(state) => {
            world.insert_resource(state);
        }
        Err(error) => runtime::fail(world, error),
    }
}

#[expect(
    clippy::too_many_lines,
    reason = "Stages one coherent plant, grass and source-surface generation before publication"
)]
fn stage(world: &mut World, inputs: PreparedHost) -> Result<State, String> {
    if let Some(species) = &inputs.species {
        world.insert_resource(crate::vegetation::VegetationPresentation(
            species.vegetation.presentation,
        ));
    }
    let (_, camera) = runtime::camera(world)?;
    crate::source_vegetation::world::stage_shader(world, &inputs.shader)?;
    let textures = inputs
        .textures
        .stage(&mut world.resource_mut::<Assets<Image>>());
    let speed = MAX_WIND_SPEED * inputs.presentation.wind_strength;
    let direction = bevy::math::DVec2::new(0.82, 0.57).normalize();
    let wind = world.get_resource::<crate::source_wind::SceneWind>();
    let current_direction = wind.map_or(direction, |wind| {
        bevy::math::DVec2::from_array(wind.wind().direction)
    });
    let current_speed = wind.map_or(speed, |wind| wind.speed_mps());
    let clock = crate::source_wind::vegetation::Clock::from_acquired(
        0.0,
        0,
        [0.0; 2],
        current_direction.to_array(),
        f64::from(current_speed),
    )?;
    let (frame, frame_handle, surface_generation) = if inputs.source_lighting {
        let shared = crate::source_lighting::surfaces::live::snapshot(world)
            .ok_or("Source surface scene must stage before its vegetation subscriber")?;
        (shared.frame, shared.handle, shared.generation)
    } else {
        let frame = Frame::new(
            camera,
            current_direction
                .as_vec2()
                .extend(current_speed)
                .extend(0.0),
            Vec4::ZERO,
        )?;
        let handle = frame.stage(world)?;
        (frame, handle, 0)
    };
    let mut base = WorldPlantMaterial {
        params: Params {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
            wind: Vec4::ZERO,
            gust_lod: Vec4::ZERO,
            sun_night: Vec4::ZERO,
            lod_end_frame: Vec4::ZERO,
        },
        detail: textures.detail,
        leaf: textures.leaf,
        impostor_a: textures.impostors[0].clone(),
        impostor_b: textures.impostors[1].clone(),
        instances: default(),
        models: default(),
        frame: frame_handle.clone(),
        grass_heights: None,
        grass_mask: None,
        grass_deltas: None,
        grass_field: Vec4::ZERO,
        scene_lighting: frame_handle.clone(),
        live_shadows: frame_handle.clone(),
        local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
        environment: None,
        hill_shadow: None,
        object_shadow: Some(crate::source_vegetation::world::PBR_DEPTH_BINDING),
        object_settings: crate::source_shadows::ObjectShadowSettings::default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        source_lighting: false,
        hill_domain: bevy::prelude::Vec4::new(-1024.0, -1024.0, 2048.0, 0.0),
        kind: 0,
    };
    if inputs.source_lighting {
        crate::source_lighting::surfaces::live::bind_plant(world, &mut base)?;
    }
    let render = Resident::stage(world, inputs.projection, &base, inputs.owner)?;
    let grass_owner = EntityId::deterministic(format!("source-grass:{}", inputs.owner));
    let grass = inputs
        .grass
        .map(|grass| {
            crate::source_vegetation::grass::host::Host::stage(
                world,
                grass,
                &base,
                textures.grass_mask,
                grass_owner,
            )
        })
        .transpose()?;
    let mut status = world.resource_mut::<HostStatus>();
    status.identity = Some(render.identity());
    status.head = Some(inputs.head);
    status.terrain = Some(inputs.terrain);
    Ok(State {
        species: inputs.species.map(species::State::new),
        source_lighting: inputs.source_lighting,
        surface_generation,
        grass,
        render,
        physical: None,
        physical_source: None,
        bounds: Arc::new(inputs.bounds),
        population: inputs.population,
        snapshot: Arc::new(inputs.snapshot),
        bound_sources: None,
        owner: inputs.owner,
        head: inputs.head,
        terrain: inputs.terrain,
        presentation: inputs.presentation,
        frame,
        frame_handle,
        direction,
        clock,
        partition: None,
        admitted: None,
        selected_camera: None,
        pending: None,
        published: false,
        frame_ready: false,
        wait_started: None,
        authoring_target: None,
        simulation_admission: None,
    })
}
