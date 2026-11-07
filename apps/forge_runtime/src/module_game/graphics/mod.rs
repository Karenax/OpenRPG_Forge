//! Module display controls, independent from authoring and party persistence.
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};
use forge_project::VisualQualityTier;
mod evidence;
mod policy;
mod runtime;
mod store;
mod ui;
pub use evidence::{expected, observe};
#[cfg(test)]
mod tests;
use policy::Policy;
pub use ui::GraphicsUi;
pub(super) use ui::GraphicsUiInput;

/// Current renderer inventories have been observed for the module frame.
#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub(super) struct ModuleGraphicsObserved;

#[derive(Message, Clone, Copy)]
struct CustomSettingsCommand {
    tier: VisualQualityTier,
    settings: forge_engine::source_graphics::Settings,
}

#[derive(Resource)]
pub struct State {
    policy: Policy,
    store: store::Store,
    pub requested: VisualQualityTier,
    requested_custom: Option<forge_engine::source_graphics::Settings>,
    pub applied: Option<VisualQualityTier>,
    pub saved: Option<VisualQualityTier>,
    saved_custom: Option<forge_engine::source_graphics::Settings>,
    pub error: Option<String>,
    pub preference_error: Option<String>,
    dispatched: Option<VisualQualityTier>,
    save_requested: bool,
    source: ContentHash,
    terrain: ContentHash,
    water: bool,
    generation: u64,
    request_sequence: u64,
    applied_frame: u32,
    simulation_admission: Option<forge_engine::source_graphics::admission::AppliedProfileAdmission>,
    inventory: Option<runtime::Signature>,
    active_scopes: Vec<&'static str>,
    scope_revisions: std::collections::BTreeMap<&'static str, ScopeRevision>,
    scope_owners: std::collections::BTreeMap<&'static str, EntityId>,
    scope_leases: std::collections::BTreeMap<&'static str, Vec<bevy::asset::UntypedHandle>>,
    publications: std::collections::BTreeMap<&'static str, Publication>,
    waiting_since: Option<f64>,
    pub ready: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct ScopeRevision {
    generation: u64,
    required_frame: u32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct Publication {
    frame: u32,
    sources: ContentHash,
    seal: ContentHash,
}

#[expect(
    clippy::too_many_lines,
    reason = "Install the existing graphics resources and scheduling in dependency order."
)]
pub fn install(
    app: &mut App,
    loaded: &forge_delivery::module_runtime::LoadedModuleZone,
    save_root: &std::path::Path,
    default_quality: Option<VisualQualityTier>,
    procedural_streaming: bool,
) -> Result<(), String> {
    let terrain = loaded.zone.head().effective()?.0;
    let policy = if procedural_streaming {
        Policy::with_procedural_streaming(&terrain, true)?
    } else {
        Policy::new(&terrain)?
    };
    let store = store::Store::new(save_root)?;
    let (preferences, preference_error) = match store.load_preferences() {
        Ok(preferences) => (preferences, None),
        Err(error) => {
            tracing::warn!(%error,"Graphics preference load rejected");
            (None, Some(error))
        }
    };
    let saved = preferences.map(|preferences| preferences.tier);
    let saved_custom = preferences.and_then(|preferences| preferences.settings);
    let requested = initial_quality(saved, default_quality);
    app.add_plugins(
        forge_engine::VisualValleyShadowQualityPlugin::new(
            &terrain,
            policy.shadow_source.clone(),
            requested,
        )
        .map_err(|e| e.to_string())?,
    );
    if !app.is_plugin_added::<forge_engine::render_inventory::RenderInventoryPlugin>() {
        app.add_plugins(forge_engine::render_inventory::RenderInventoryPlugin);
    }
    forge_engine::source_graphics::install(app)?;
    let open = preference_error.is_some();
    app.insert_resource(State {
        policy,
        store,
        requested,
        requested_custom: saved_custom,
        applied: None,
        saved,
        saved_custom,
        error: None,
        save_requested: saved.is_none() && preference_error.is_none(),
        preference_error,
        dispatched: None,
        source: loaded.zone.head().content_hash()?,
        terrain: terrain.content_hash().map_err(|e| e.to_string())?,
        water: loaded
            .manifest
            .ocean_inputs
            .iter()
            .any(|binding| binding.area_id == terrain.area_id),
        generation: 0,
        request_sequence: 0,
        applied_frame: 0,
        simulation_admission: None,
        inventory: None,
        active_scopes: Vec::new(),
        scope_revisions: std::collections::BTreeMap::new(),
        scope_owners: std::collections::BTreeMap::new(),
        scope_leases: std::collections::BTreeMap::new(),
        publications: std::collections::BTreeMap::new(),
        waiting_since: None,
        ready: false,
    })
    .insert_resource(GraphicsUi::new(requested, open))
    .add_systems(Startup, ui::spawn)
    .add_message::<forge_engine::native_gameplay::NativeGraphicsCommand>()
    .add_message::<CustomSettingsCommand>()
    .add_systems(
        PreUpdate,
        (runtime::commands, runtime::custom_commands, runtime::apply).chain(),
    )
    .add_systems(PostUpdate, ui::sync_terrain.before(runtime::observe))
    .add_systems(
        PostUpdate,
        runtime::observe
            .in_set(ModuleGraphicsObserved)
            .after(forge_engine::source_particle_compute::SourceParticleProjectionUpdate)
            .after(forge_engine::source_water_host::WaterHostSet)
            .after(forge_engine::source_refraction::Synchronize)
            .after(forge_engine::source_terrain::host::TerrainHostSet)
            .after(bevy::transform::TransformSystems::Propagate)
            .after(forge_engine::source_graph_runtime::SourceSceneSystems::Placements)
            .after(forge_engine::source_lighting::surfaces::live::SurfaceHostSet)
            .after(forge_engine::source_fish::host::live::FishHostSet)
            .after(forge_engine::source_surface::package::host::ObjectHostSet),
    )
    .add_systems(
        Update,
        (ui::input, ui::scroll, ui::update)
            .chain()
            .in_set(GraphicsUiInput)
            .after(forge_engine::native_gameplay::NativeGameplayUiSet::Input),
    )
    .add_systems(
        PostUpdate,
        ui::keep_focus_visible.after(bevy::ui::UiSystems::Layout),
    )
    .add_systems(FixedFirst, runtime::release_hold)
    .init_resource::<runtime::GraphicsSimulationHold>()
    .add_systems(
        FixedUpdate,
        runtime::early
            .after(forge_engine::cell_streaming::PlayerCellStreamingSet::Request)
            .before(forge_engine::native_gameplay::NativeGameplayReadinessSet),
    )
    .add_systems(
        FixedUpdate,
        runtime::final_gate
            .after(forge_engine::cell_streaming::PlayerCellStreamingSet::Enforce)
            .after(forge_engine::physics::imported_vegetation::ImportedPlantActivationSet)
            .before(bevy_rapier3d::prelude::PhysicsSet::SyncBackend),
    );
    Ok(())
}

fn initial_quality(
    saved: Option<VisualQualityTier>,
    default_quality: Option<VisualQualityTier>,
) -> VisualQualityTier {
    saved.or(default_quality).unwrap_or(VisualQualityTier::High)
}

fn owner(family: &str) -> EntityId {
    EntityId::deterministic(format!("module-graphics/{family}"))
}

fn prepared_owner(family: &str) -> EntityId {
    EntityId::deterministic(format!("module-graphics/{family}/prepared"))
}

impl State {
    fn scope_owner(&self, family: &str) -> EntityId {
        self.scope_owners
            .get(family)
            .copied()
            .unwrap_or_else(|| owner(family))
    }
    fn request(&mut self, tier: VisualQualityTier) {
        self.request_settings(tier, None);
    }

    fn request_settings(
        &mut self,
        tier: VisualQualityTier,
        custom: Option<forge_engine::source_graphics::Settings>,
    ) {
        let custom = match custom {
            Some(settings) => match self.policy.resolve(tier, Some(settings)) {
                Ok(effective) => Some(effective.settings),
                Err(error) => {
                    self.error = Some(error);
                    self.ready = false;
                    return;
                }
            },
            None => None,
        };
        let Some(sequence) = self.request_sequence.checked_add(1) else {
            self.error = Some("Graphics request sequence exhausted".into());
            self.ready = false;
            return;
        };
        self.request_sequence = sequence;
        self.simulation_admission = None;
        self.requested = tier;
        self.requested_custom = custom;
        self.dispatched = None;
        self.save_requested = true;
        self.preference_error = None;
        self.error = None;
        self.ready = false;
        self.waiting_since = None;
    }
}
