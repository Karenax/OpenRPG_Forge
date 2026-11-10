use super::{Publication, ScopeRevision, State};
use crate::module_game::evidence_contract::GraphicsIdentity;
use bevy::{
    pbr::Material, platform::collections::HashSet, prelude::*, render::storage::ShaderBuffer,
};
use forge_core::canonical_json_sha256;
use forge_engine::source_vegetation::scene::streamed::host::HostStatus;
use forge_engine::source_vegetation::world::WorldPlantMaterial;
use forge_engine::{
    native_gameplay::{NativeGraphicsCommand, NativeGraphicsStatus},
    render_inventory,
};
use std::{collections::BTreeMap, mem};

#[cfg(feature = "qualification")]
mod staging_probe;
mod surface_prefetch;

#[derive(PartialEq)]
pub(super) struct Signature(BTreeMap<&'static str, Vec<String>>);
struct Family<M: Material> {
    meshes: Vec<Handle<Mesh>>,
    materials: Vec<Handle<M>>,
    keys: Vec<String>,
}

pub(super) fn commands(mut input: MessageReader<NativeGraphicsCommand>, mut state: ResMut<State>) {
    for command in input.read() {
        state.request(command.0);
    }
}

pub(super) fn custom_commands(
    mut input: MessageReader<super::CustomSettingsCommand>,
    mut state: ResMut<State>,
) {
    for command in input.read() {
        state.request_settings(command.tier, Some(command.settings));
    }
}

fn camera(world: &mut World) -> Result<Option<Entity>, String> {
    let mut query = world.query_filtered::<(Entity, &Camera), (
        With<Camera3d>,
        Without<forge_engine::scene_camera::OffscreenSceneCamera>,
    )>();
    let mut cameras = query.iter(world).filter(|(_, camera)| camera.is_active);
    let first = cameras.next().map(|(entity, _)| entity);
    if cameras.next().is_some() {
        return Err("Graphics settings require one active 3D camera".into());
    }
    Ok(first)
}

pub(super) fn apply(world: &mut World) {
    world.resource_scope(|world, mut state: Mut<State>| {
        if state.error.is_some() {
            return;
        }
        if state.dispatched == Some(state.requested) {
            if state.requested_custom.is_some()
                && let Err(error) = refresh_settings(world, &state)
            {
                reject(&mut state, error);
            }
            return;
        }
        match apply_settings(world, &mut state) {
            Ok(()) => {}
            Err(error) => reject(&mut state, error),
        }
    });
    report_input_readiness(world);
}

fn refresh_settings(world: &mut World, state: &State) -> Result<(), String> {
    let Some(camera) = camera(world)? else {
        return Ok(());
    };
    let effective = state
        .policy
        .resolve(state.requested, state.requested_custom)?;
    if !forge_engine::source_graphics::reference_matches(
        world,
        camera,
        effective.settings,
        effective.preset.post_process,
    ) {
        forge_engine::source_graphics::apply(
            world,
            camera,
            effective.settings,
            effective.preset.post_process,
            forge_engine::source_graphics::Pipeline::Reference,
        )?;
    }
    Ok(())
}

pub(super) fn report_input_readiness(world: &mut World) {
    let ready = world.contains_resource::<State>()
        && world.resource_scope(|world, state: Mut<State>| simulation_ready(world, &state));
    forge_engine::runtime_static_admission::observe_graphics_input_readiness(world, ready);
}

fn apply_settings(world: &mut World, state: &mut State) -> Result<(), String> {
    let Some(camera) = camera(world)? else {
        return Ok(());
    };
    let effective = state
        .policy
        .resolve(state.requested, state.requested_custom)?;
    let preset = effective.preset;
    if let Some([coarse, distant]) = preset.raster_strides {
        forge_engine::cell_streaming::request_mesh_quality(
            world,
            state.terrain,
            forge_world::cell_streaming::SampledMeshQuality::new(coarse, distant)?,
        )?;
    }
    let original_shadows = state.policy.preset(state.requested).shadows;
    let mut shadow_request = world.resource_mut::<forge_engine::ShadowQualityRequest>();
    if original_shadows.contact_shadows_enabled && !preset.shadows.contact_shadows_enabled {
        shadow_request.request_without_contact(state.requested);
    } else {
        shadow_request.request(state.requested);
    }
    let msaa = match preset.msaa_samples {
        1 => Msaa::Off,
        2 => Msaa::Sample2,
        _ => Msaa::Sample4,
    };
    forge_engine::source_graphics::apply(
        world,
        camera,
        effective.settings,
        preset.post_process,
        forge_engine::source_graphics::Pipeline::Reference,
    )?;
    world.entity_mut(camera).insert(msaa);
    if let Some(contact) = preset.contact_shadows {
        world.entity_mut(camera).insert(bevy::pbr::ContactShadows {
            linear_steps: contact.linear_steps,
            thickness: contact.thickness_m,
            length: contact.length_m,
        });
    } else {
        world
            .entity_mut(camera)
            .remove::<bevy::pbr::ContactShadows>();
    }
    state.dispatched = Some(state.requested);
    state.inventory = None;
    state.ready = false;
    state.applied = None;
    state.applied_frame = world.resource::<bevy::diagnostic::FrameCount>().0;
    state.simulation_admission = None;
    state.waiting_since = Some(world.resource::<Time<Real>>().elapsed_secs_f64());
    Ok(())
}

fn family<M: Material>(world: &mut World) -> Result<Option<Family<M>>, String> {
    let _profile = forge_engine::cpu_timing::Span::new(
        if std::any::TypeId::of::<M>()
            == std::any::TypeId::of::<forge_engine::source_terrain::lod::LodMaterial>()
        {
            "graphics.inventory.terrain"
        } else if std::any::TypeId::of::<M>()
            == std::any::TypeId::of::<forge_engine::source_water_optics::OpticalMaterial>()
        {
            "graphics.inventory.water"
        } else {
            "graphics.inventory.other"
        },
    );
    let mut result = Family {
        meshes: Vec::new(),
        materials: Vec::new(),
        keys: vec![format!("material-type:{}", std::any::type_name::<M>())],
    };
    let mut seen_meshes = HashSet::new();
    let mut seen_materials = HashSet::new();
    for (entity, mesh, material) in world
        .query::<(Entity, &Mesh3d, &MeshMaterial3d<M>)>()
        .iter(world)
    {
        if forge_engine::source_terrain::host::owns_physics_projection(world, entity)
            || forge_engine::source_terrain::host::inactive_tile(world, entity)
            || forge_engine::source_ocean_geometry::inactive_tile(world, entity)
        {
            continue;
        }
        if seen_meshes.insert(mesh.id()) {
            result.meshes.push(mesh.0.clone());
            result.keys.push(format!("mesh:{:?}", mesh.id()));
        }
        if seen_materials.insert(material.id()) {
            result.materials.push(material.0.clone());
            result.keys.push(format!("material:{:?}", material.id()));
        }
    }
    // Keep admitted sources for hidden resident tiles too. Revisiting one can
    // then reuse the current inventory, while every newly allocated mesh still
    // requires an actual GPU admission and a post-publication fence.
    #[cfg(feature = "qualification")]
    let retained_enabled = std::env::var_os("FORGE_QUALIFY_VISIBLE_TILE_INVENTORY").is_none();
    #[cfg(not(feature = "qualification"))]
    let retained_enabled = true;
    let retained = if !retained_enabled {
        None
    } else if std::any::TypeId::of::<M>()
        == std::any::TypeId::of::<forge_engine::source_terrain::lod::LodMaterial>()
    {
        forge_engine::source_terrain::host::retained_meshes(world)
    } else if std::any::TypeId::of::<M>()
        == std::any::TypeId::of::<forge_engine::source_water_optics::OpticalMaterial>()
    {
        forge_engine::source_ocean_geometry::retained_meshes(world)
    } else {
        None
    };
    if let Some(retained) = retained {
        include_retained(&mut result, retained);
    }
    let Some(materials) = world.get_resource::<Assets<M>>() else {
        return Ok(result.meshes.is_empty().then_some(result));
    };
    for handle in &result.materials {
        let Some(material) = materials.get(handle) else {
            return Ok(None);
        };
        let mut pending = false;
        let mut unsupported = false;
        render_inventory::visit_material_dependencies(material, &mut |id| {
            result.keys.push(format!("dependency:{id:?}"));
            if let Ok(id) = id.try_typed::<Image>() {
                pending |= !world
                    .get_resource::<Assets<Image>>()
                    .is_some_and(|assets| assets.contains(id));
            } else if let Ok(id) = id.try_typed::<ShaderBuffer>() {
                pending |= !world
                    .get_resource::<Assets<ShaderBuffer>>()
                    .is_some_and(|assets| assets.contains(id));
            } else {
                unsupported = true;
            }
        });
        if unsupported {
            return Err("A module material has unsupported graphics dependencies".into());
        }
        if pending {
            return Ok(None);
        }
    }
    result.keys.sort();
    Ok(Some(result))
}

fn include_retained<M: Material>(family: &mut Family<M>, retained: Vec<Handle<Mesh>>) {
    let extra: Vec<_> = retained
        .into_iter()
        .filter(|mesh| !family.meshes.contains(mesh))
        .collect();
    if family.meshes.len() + extra.len() <= render_inventory::MAX_SCOPED_MESHES {
        // `keys` describe the visible selection, not the larger GPU inventory.
        // An activation or removal must still request a later publication fence.
        family.meshes.extend(extra);
    }
}

pub(super) fn coverage(world: &mut World) -> Result<(), String> {
    let source_draws = world
        .get_resource::<forge_engine::source_graph_runtime::SourceSceneResidents>()
        .map(|residents| residents.owned_draws(world))
        .transpose()?;
    for (
        entity,
        a,
        b,
        c,
        d,
        sky,
        source_model,
        source_terrain,
        source_object,
        source_fragment,
        acquired_terrain,
        water,
        lip,
        spray,
        (fish, graph, unlit, source_vegetation),
    ) in world
        .query_filtered::<(
            Entity,
            Has<MeshMaterial3d<StandardMaterial>>,
            Has<MeshMaterial3d<forge_engine::VisualValleyTerrainMaterial>>,
            Has<MeshMaterial3d<forge_engine::VisualValleyVegetationMaterial>>,
            Has<MeshMaterial3d<WorldPlantMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_sky::SkyMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_lighting::surfaces::ModelMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_lighting::surfaces::TerrainMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_surface::SourceSurfaceMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_fragment::SourceFragmentMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_terrain::lod::LodMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_water_optics::OpticalMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_breaker_render::LipMaterial>>,
            Has<MeshMaterial3d<forge_engine::source_spray_render::SprayMaterial>>,
            (
                Has<MeshMaterial3d<forge_engine::source_fish::material::FishMaterial>>,
                Has<MeshMaterial3d<forge_engine::source_graph::SourceGraphMaterial>>,
                Has<MeshMaterial3d<forge_engine::source_graph::SourceUnlitForwardMaterial>>,
                Has<MeshMaterial3d<forge_engine::source_lighting::surfaces::VegetationMaterial>>,
            ),
        ), With<Mesh3d>>()
        .iter(world)
    {
        if forge_engine::source_refraction_capture::owns_mirror(world, entity) {
            continue;
        }
        if u8::from(a)
            + u8::from(b)
            + u8::from(c)
            + u8::from(d)
            + u8::from(sky)
            + u8::from(source_model)
            + u8::from(source_terrain)
            + u8::from(source_object)
            + u8::from(source_fragment)
            + u8::from(acquired_terrain)
            + u8::from(water)
            + u8::from(lip)
            + u8::from(spray)
            + u8::from(fish)
            + u8::from(graph)
            + u8::from(unlit)
            + u8::from(source_vegetation)
            != 1
            || (sky && !forge_engine::source_environment::host::owns_sky(world, entity))
            || ((source_model || source_terrain || source_vegetation)
                && !forge_engine::source_lighting::surfaces::host::owns(world, entity))
            || (source_object && !forge_engine::source_surface::package::host::owns(world, entity))
            || (acquired_terrain && !forge_engine::source_terrain::host::owns(world, entity))
            || (water && !forge_engine::source_water_host::owns(world, entity))
            || (lip && !forge_engine::source_breaker_resident::owns_lip(world, entity))
            || (spray && !forge_engine::source_breaker_resident::owns_spray(world, entity))
            || (fish && !forge_engine::source_fish::host::live::owns(world, entity))
            || ((graph || unlit)
                && source_draws
                    .as_ref()
                    .is_none_or(|draws| !draws.contains(&entity)))
        {
            return Err("A module mesh uses an unsupported or ambiguous graphics material".into());
        }
    }
    Ok(())
}

pub(super) fn observe(world: &mut World) {
    // Input may have been sampled after a PreUpdate or UI quality request.
    report_input_readiness(world);
    world.resource_scope(|world, mut state: Mut<State>| {
        let result = observe_inner(world, &mut state);
        match result {
            Ok(ready) => {
                state.ready = ready;
                if ready {
                    if let (Ok(identity), Some(token)) =
                        (application_identity(&state), scope_token(world, &state, "terrain"))
                    {
                        state.simulation_admission =
                            forge_engine::source_graphics::admission::AppliedProfileAdmission::observe(
                                world, identity, state.applied_frame, &token,
                            );
                    }
                    state.waiting_since = None;
                    state.applied = Some(state.requested);
                    if state.save_requested {
                        match state.store.save_preferences(super::store::Preferences {
                            tier: state.requested,
                            settings: state.requested_custom,
                        }) {
                            Ok(()) => {
                                state.saved = Some(state.requested);
                                state.saved_custom = state.requested_custom;
                                state.preference_error = None;
                            }
                            Err(error) => {
                                tracing::warn!(%error,"Graphics preference save failed");
                                state.preference_error = Some(error);
                            }
                        }
                        state.save_requested = false;
                    }
                } else if state.error.is_none() {
                    let now = world.resource::<Time<Real>>().elapsed_secs_f64();
                    let waits_for_failed_stream = state
                        .policy
                        .preset(state.requested)
                        .raster_strides
                        .is_some()
                        && world
                            .get_resource::<forge_engine::cell_streaming::CellStreamingStats>()
                            .is_some_and(|stats| stats.last_error.is_some());
                    if waits_for_failed_stream {
                        // The shared terrain adapter already reports the actionable
                        // failure. Give the graphics gate a fresh deadline once the
                        // stream is repaired; do not clear other errors or preferences.
                        state.waiting_since = Some(now);
                    } else if now - *state.waiting_since.get_or_insert(now) > 120.0 {
                        reject(
                            &mut state,
                            "Graphics application did not become ready within 120 seconds".into(),
                        );
                    }
                }
            }
            Err(error) => reject(&mut state, error),
        }
        #[cfg(feature = "qualification")]
        waiting_diagnostic(world, &state);
        #[cfg(feature = "qualification")]
        staging_probe::observe(world, &state);
        surface_prefetch::observe(world, &mut state);
        let menu_open = world.resource::<super::GraphicsUi>().open;
        world.insert_resource(NativeGraphicsStatus {
            request_sequence: state.request_sequence,
            requested: state.requested,
            applied: state.applied,
            persisted: state.saved,
            menu_open,
            ready: state.ready,
            error: state
                .error
                .clone()
                .or_else(|| state.preference_error.clone()),
        });
    });
    // A ready observation can happen without any intervening fixed step.
    report_input_readiness(world);
}

// Diagnostic observations retain the renderer's original frame; a later log
// line must not be treated as a new GPU fence or a simulation measurement.
#[cfg(feature = "qualification")]
fn waiting_diagnostic(world: &World, state: &State) {
    if state.ready || std::env::var_os("FORGE_QUALIFY_GRAPHICS_WAIT").is_none() {
        return;
    }
    let frame = world.resource::<bevy::diagnostic::FrameCount>().0;
    for name in &state.active_scopes {
        if let Some(evidence) = render_inventory::scoped_evidence(world, state.scope_owner(name)) {
            let current = scope_token(world, state, name).is_some();
            eprintln!(
                "QUALIFY_GRAPHICS_WAIT frame={frame} scope={name} observed={} completed={:?} stable={} current={current} meshes={}/{} pipelines={}/{}/{} camera={} effects={}",
                evidence.observed,
                evidence.completed,
                evidence.stable_frames,
                evidence.assets.meshes_prepared,
                evidence.assets.meshes_expected,
                evidence.pipelines_total,
                evidence.pipelines_pending,
                evidence.pipelines_failed,
                evidence.view.camera_resources_ready,
                evidence.effects.public_resources_ready,
            );
        }
    }
}

fn observe_inner(world: &mut World, state: &mut State) -> Result<bool, String> {
    if state.dispatched != Some(state.requested) || state.error.is_some() {
        return Ok(false);
    }
    {
        let _profile = forge_engine::cpu_timing::Span::new("graphics.coverage");
        coverage(world)?;
    }
    if let Some(source) = world.get_resource::<forge_engine::source_terrain::host::Status>() {
        if let Some(error) = &source.error {
            return Err(error.clone());
        }
        if source.pending {
            return Ok(false);
        }
        if world
            .get_resource::<forge_engine::source_lighting::surfaces::host::Status>()
            .is_none_or(|status| status.pending > 0)
        {
            return Ok(false);
        }
        return observe_families::<
            forge_engine::source_terrain::lod::LodMaterial,
            forge_engine::source_lighting::surfaces::ModelMaterial,
        >(world, state);
    }
    if let Some(source) =
        world.get_resource::<forge_engine::source_lighting::surfaces::host::Status>()
    {
        if source.pending > 0 {
            return Ok(false);
        }
        observe_families::<
            forge_engine::source_lighting::surfaces::TerrainMaterial,
            forge_engine::source_lighting::surfaces::ModelMaterial,
        >(world, state)
    } else {
        observe_families::<forge_engine::VisualValleyTerrainMaterial, StandardMaterial>(
            world, state,
        )
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "All typed families and their publication frames advance from one scene snapshot"
)]
fn observe_families<T: Material, M: Material>(
    world: &mut World,
    state: &mut State,
) -> Result<bool, String> {
    #[cfg(feature = "qualification")]
    let _profile = forge_engine::cpu_timing::Span::new("graphics.families_observe.total");
    let Some(mut terrain) = family::<T>(world)? else {
        return Ok(false);
    };
    let Some(mut models) = family::<M>(world)? else {
        return Ok(false);
    };
    let Some(mut vegetation) = family::<forge_engine::VisualValleyVegetationMaterial>(world)?
    else {
        return Ok(false);
    };
    let Some(mut objects) = family::<forge_engine::source_surface::SourceSurfaceMaterial>(world)?
    else {
        return Ok(false);
    };
    let Some(mut source_fragments) =
        family::<forge_engine::source_fragment::SourceFragmentMaterial>(world)?
    else {
        return Ok(false);
    };
    let Some(mut fish) = family::<forge_engine::source_fish::material::FishMaterial>(world)? else {
        return Ok(false);
    };
    let graph_enabled = world
        .contains_resource::<forge_engine::source_graph_runtime::SourceSceneResidents>()
        || state.scope_revisions.contains_key("source_graphs")
        || state.scope_revisions.contains_key("source_graph_unlit");
    let (mut source_graphs, mut source_graph_unlit) = if graph_enabled {
        let Some(graphs) = family::<forge_engine::source_graph::SourceGraphMaterial>(world)? else {
            return Ok(false);
        };
        let Some(unlit) = family::<forge_engine::source_graph::SourceUnlitForwardMaterial>(world)?
        else {
            return Ok(false);
        };
        (Some(graphs), Some(unlit))
    } else {
        (None, None)
    };
    let Some(mut water) = family::<forge_engine::source_water_optics::OpticalMaterial>(world)?
    else {
        return Ok(false);
    };
    let Some(mut lip) = family::<forge_engine::source_breaker_render::LipMaterial>(world)? else {
        return Ok(false);
    };
    let Some(mut spray) = family::<forge_engine::source_spray_render::SprayMaterial>(world)? else {
        return Ok(false);
    };
    if terrain.meshes.is_empty() {
        return Ok(false);
    }
    let signature = Signature(
        [
            ("terrain", &mut terrain.keys),
            ("models", &mut models.keys),
            ("vegetation", &mut vegetation.keys),
            ("objects", &mut objects.keys),
            ("source_fragments", &mut source_fragments.keys),
            ("water", &mut water.keys),
            ("breaker_lip", &mut lip.keys),
            ("spray", &mut spray.keys),
            ("fish", &mut fish.keys),
        ]
        .into_iter()
        .chain(
            source_graphs
                .as_mut()
                .map(|family| ("source_graphs", &mut family.keys)),
        )
        .chain(
            source_graph_unlit
                .as_mut()
                .map(|family| ("source_graph_unlit", &mut family.keys)),
        )
        .map(|(name, keys)| (name, mem::take(keys)))
        .collect(),
    );
    let families = [
        ("terrain", !terrain.meshes.is_empty()),
        ("models", !models.meshes.is_empty()),
        ("vegetation", !vegetation.meshes.is_empty()),
        ("objects", !objects.meshes.is_empty()),
        ("source_fragments", !source_fragments.meshes.is_empty()),
        ("water", !water.meshes.is_empty()),
        ("breaker_lip", !lip.meshes.is_empty()),
        ("spray", !spray.meshes.is_empty()),
        ("fish", !fish.meshes.is_empty()),
        (
            "source_graphs",
            source_graphs
                .as_ref()
                .is_some_and(|family| !family.meshes.is_empty()),
        ),
        (
            "source_graph_unlit",
            source_graph_unlit
                .as_ref()
                .is_some_and(|family| !family.meshes.is_empty()),
        ),
    ];
    let families = &families[..if graph_enabled { 11 } else { 9 }];
    let changed: [bool; 11] = std::array::from_fn(|index| {
        families.get(index).is_some_and(|(name, _)| {
            state.inventory.as_ref().is_none_or(|previous| {
                signature_family(previous, name) != signature_family(&signature, name)
            })
        })
    });
    let reusable = [
        reusable_family(world, state, "terrain", &terrain, changed[0]),
        reusable_family(world, state, "models", &models, changed[1]),
        reusable_family(world, state, "vegetation", &vegetation, changed[2]),
        reusable_family(world, state, "objects", &objects, changed[3]),
        reusable_family(
            world,
            state,
            "source_fragments",
            &source_fragments,
            changed[4],
        ),
        reusable_family(world, state, "water", &water, changed[5]),
        reusable_family(world, state, "breaker_lip", &lip, changed[6]),
        reusable_family(world, state, "spray", &spray, changed[7]),
        reusable_family(world, state, "fish", &fish, changed[8]),
        source_graphs.as_ref().is_some_and(|family| {
            reusable_family(world, state, "source_graphs", family, changed[9])
        }),
        source_graph_unlit.as_ref().is_some_and(|family| {
            reusable_family(world, state, "source_graph_unlit", family, changed[10])
        }),
    ];
    let mut dirty = [false; 11];
    for (index, (name, present)) in families.iter().enumerate() {
        dirty[index] = scope_needs_refresh(
            state.inventory.as_ref(),
            &signature,
            name,
            ScopeAvailability {
                present: *present,
                request: if !state.scope_revisions.contains_key(name) {
                    RequestState::Absent
                } else if render_inventory::scoped_sources_current(world, state.scope_owner(name)) {
                    RequestState::Current
                } else {
                    RequestState::Stale
                },
                contained: reusable[index],
            },
        );
    }
    if state.inventory.as_ref() != Some(&signature) || dirty.into_iter().any(|value| value) {
        #[cfg(feature = "qualification")]
        inventory_diagnostic(world, state, state.inventory.as_ref(), &signature, families);
        state.generation = state
            .generation
            .checked_add(1)
            .ok_or("Graphics generation overflow")?;
        let revision = ScopeRevision {
            generation: state.generation,
            required_frame: world.resource::<bevy::diagnostic::FrameCount>().0,
        };
        state.ready = false;
        let request_identity = identity(state, revision)?;
        update_family(
            world,
            state,
            "terrain",
            request_identity,
            revision,
            &terrain,
            dirty[0],
        )?;
        update_family(
            world,
            state,
            "models",
            request_identity,
            revision,
            &models,
            dirty[1],
        )?;
        update_family(
            world,
            state,
            "vegetation",
            request_identity,
            revision,
            &vegetation,
            dirty[2],
        )?;
        update_family(
            world,
            state,
            "objects",
            request_identity,
            revision,
            &objects,
            dirty[3],
        )?;
        update_family(
            world,
            state,
            "source_fragments",
            request_identity,
            revision,
            &source_fragments,
            dirty[4],
        )?;
        update_family(
            world,
            state,
            "water",
            request_identity,
            revision,
            &water,
            dirty[5],
        )?;
        update_family(
            world,
            state,
            "breaker_lip",
            request_identity,
            revision,
            &lip,
            dirty[6],
        )?;
        update_family(
            world,
            state,
            "spray",
            request_identity,
            revision,
            &spray,
            dirty[7],
        )?;
        update_family(
            world,
            state,
            "fish",
            request_identity,
            revision,
            &fish,
            dirty[8],
        )?;
        if let Some(graphs) = &source_graphs {
            update_family(
                world,
                state,
                "source_graphs",
                request_identity,
                revision,
                graphs,
                dirty[9],
            )?;
        }
        if let Some(unlit) = &source_graph_unlit {
            update_family(
                world,
                state,
                "source_graph_unlit",
                request_identity,
                revision,
                unlit,
                dirty[10],
            )?;
        }
        for (index, (name, present)) in families.iter().enumerate() {
            if !present {
                state.publications.remove(name);
            } else if changed[index] || dirty[index] {
                let current = *state
                    .scope_revisions
                    .get(name)
                    .ok_or("Graphics scope absent after registration")?;
                let scope_identity = identity(state, current)?;
                let sources = canonical_json_sha256(&signature_family(&signature, name))
                    .map_err(|error| error.to_string())?;
                let frame = revision.required_frame;
                let seal = canonical_json_sha256(&(scope_identity, frame, sources))
                    .map_err(|error| error.to_string())?;
                state.publications.insert(
                    name,
                    Publication {
                        frame,
                        sources,
                        seal,
                    },
                );
            }
        }
        state.active_scopes = families
            .iter()
            .filter(|(_, present)| *present)
            .map(|(name, _)| *name)
            .collect();
        state.inventory = Some(signature);
    }
    Ok(cpu_ready(world, state)? && gpu_ready(world, state))
}

fn reusable_family<M: Material>(
    world: &World,
    state: &State,
    name: &str,
    family: &Family<M>,
    changed: bool,
) -> bool {
    changed
        && state.inventory.is_some()
        && !family.meshes.is_empty()
        && render_inventory::scoped_contains_sources(
            world,
            state.scope_owner(name),
            &family.meshes,
            &family.materials,
        )
}

#[cfg(feature = "qualification")]
fn inventory_diagnostic(
    world: &World,
    state: &State,
    previous: Option<&Signature>,
    signature: &Signature,
    families: &[(&str, bool)],
) {
    if std::env::var_os("FORGE_QUALIFY_INVENTORY_DIAGNOSTIC").is_none() {
        return;
    }
    let Some(previous) = previous else {
        return;
    };
    let changed: Vec<_> = families
        .iter()
        .filter_map(|(name, _)| {
            (signature_family(previous, name) != signature_family(signature, name)).then_some(*name)
        })
        .collect();
    let invalid_owners: Vec<_> = families
        .iter()
        .filter_map(|(name, present)| {
            (*present && !render_inventory::scoped_sources_current(world, state.scope_owner(name)))
                .then_some(*name)
        })
        .collect();
    eprintln!(
        "QUALIFY_INVENTORY frame={} changed={changed:?} invalid_owners={invalid_owners:?}",
        world.resource::<bevy::diagnostic::FrameCount>().0
    );
}

#[derive(Clone, Copy)]
struct ScopeAvailability {
    present: bool,
    request: RequestState,
    contained: bool,
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum RequestState {
    Absent,
    Current,
    Stale,
}

fn scope_needs_refresh(
    previous: Option<&Signature>,
    current: &Signature,
    name: &str,
    availability: ScopeAvailability,
) -> bool {
    if !availability.present {
        return availability.request != RequestState::Absent;
    }
    availability.request != RequestState::Current
        || previous.is_none()
        || (previous
            .is_some_and(|old| signature_family(old, name) != signature_family(current, name))
            && !availability.contained)
}

fn signature_family<'a>(signature: &'a Signature, name: &str) -> &'a [String] {
    signature.0.get(name).map_or(&[], Vec::as_slice)
}

#[allow(
    clippy::too_many_arguments,
    reason = "One typed family and its independent GPU revision"
)]
fn update_family<M: Material>(
    world: &mut World,
    state: &mut State,
    name: &'static str,
    identity: forge_core::ContentHash,
    revision: ScopeRevision,
    family: &Family<M>,
    dirty: bool,
) -> Result<(), String> {
    if !dirty {
        return Ok(());
    }
    if family.meshes.is_empty() {
        render_inventory::release_scoped(world, state.scope_owner(name));
        state.scope_revisions.remove(name);
        state.scope_owners.remove(name);
        state.scope_leases.remove(name);
        return Ok(());
    }
    let effective = state
        .policy
        .resolve(state.requested, state.requested_custom)?;
    let preset = effective.preset;
    render_inventory::request_scoped_with_effects(
        world,
        state.scope_owner(name),
        identity,
        revision.generation,
        &family.meshes,
        &family.materials,
        render_inventory::RenderEffectRequirements::display(preset.shadows, preset.post_process)
            .with_source_final(false, effective.fxaa)
            .with_antialias(preset.temporal_aa, effective.fxaa),
    )?;
    state.scope_revisions.insert(name, revision);
    state.scope_leases.remove(name);
    Ok(())
}

pub(super) fn identity(
    state: &State,
    revision: ScopeRevision,
) -> Result<forge_core::ContentHash, String> {
    GraphicsIdentity {
        head: state.source,
        policy: state.policy.identity,
        water: state.water,
    }
    .scope_hash(
        state.requested,
        state.requested_custom.as_ref(),
        revision.generation,
        Some(revision.required_frame),
    )
}

pub(super) fn scope_token(
    world: &World,
    state: &State,
    name: &str,
) -> Option<render_inventory::RenderInventoryToken> {
    let revision = state.scope_revisions.get(name)?;
    let publication = state.publications.get(name)?;
    let token = render_inventory::acquire_scoped(
        world,
        state.scope_owner(name),
        identity(state, *revision).ok()?,
        revision.generation,
    )?;
    (token.covers_frame(world, revision.required_frame)
        && token.covers_frame(world, publication.frame)
        && token.covers_frame(world, state.applied_frame))
    .then_some(token)
}

fn water_cpu_ready(world: &World, terrain: forge_core::ContentHash) -> Result<bool, String> {
    if let Some(effects) = world.get_resource::<forge_engine::source_breaker_resident::Targets>() {
        if let Some(error) = effects.error() {
            return Err(error);
        }
        if !effects.ready.load(std::sync::atomic::Ordering::Acquire) {
            return Ok(false);
        }
        if !forge_engine::source_breaker_resident::display_ready(world) {
            return Ok(false);
        }
        if world
            .get_resource::<forge_engine::source_spray_feedback::LiveFeedback>()
            .is_none_or(|feedback| feedback.statistics().accepted_samples < 2)
        {
            return Ok(false);
        }
    }
    if let Some(water) = world.get_resource::<forge_engine::source_water_host::Status>() {
        if let Some(error) = &water.error {
            return Err(error.clone());
        }
        if !water.staged || water.document != Some(terrain) {
            return Ok(false);
        }
        let Some(optics) =
            world.get_resource::<forge_engine::source_water_optics::ResidentOptics>()
        else {
            return Ok(false);
        };
        if !forge_engine::source_water_host::owns(world, optics.entity) {
            return Ok(false);
        }
        if !world
            .get_resource::<forge_engine::source_underwater::UnderwaterObservation>()
            .is_some_and(|observation| observation.pipeline_ready())
        {
            return Ok(false);
        }
    }
    Ok(true)
}

pub(super) fn cpu_ready(world: &mut World, state: &State) -> Result<bool, String> {
    if world.contains_resource::<forge_engine::source_water_host::Status>() != state.water {
        return Ok(false);
    }
    if !water_cpu_ready(world, state.terrain)? {
        return Ok(false);
    }
    if state.water
        && world
            .get_resource::<forge_engine::amphibious_player::WaterSampleReadiness>()
            .is_none_or(|readiness| readiness.initial_step.is_none())
    {
        return Ok(false);
    }
    if let Some(source) = world.get_resource::<forge_engine::source_terrain::host::Status>() {
        if let Some(error) = &source.error {
            return Err(error.clone());
        }
        if source.pending || source.document != Some(state.terrain) || !source.normals_ready {
            return Ok(false);
        }
    }
    if let Some(objects) =
        world.get_resource::<forge_engine::source_surface::package::host::Status>()
    {
        if let Some(error) = &objects.error {
            return Err(error.clone());
        }
        if !objects.synchronized {
            return Ok(false);
        }
    }
    let effective = state
        .policy
        .resolve(state.requested, state.requested_custom)?;
    let preset = effective.preset;
    if let Some([a, b]) = preset.raster_strides
        && (forge_engine::cell_streaming::admitted_mesh_quality(world)
            != Some(forge_world::cell_streaming::SampledMeshQuality::new(a, b)?)
            || !forge_engine::cell_streaming::source_is_synchronized(world, state.terrain))
    {
        return Ok(false);
    }
    if !world
        .get_resource::<forge_engine::VisualValleyShadowStats>()
        .and_then(|stats| stats.quality.as_ref())
        .is_some_and(|quality| {
            quality.requested_tier == state.requested
                && quality.source_hash == state.policy.shadow_hash
                && quality.requested == preset.shadows
                && quality.status == "observed_cpu_projection_not_gpu_execution"
        })
    {
        return Ok(false);
    }
    let mut lights = world.query::<(&DirectionalLight, &bevy::light::CascadeShadowConfig)>();
    let Ok((light, cascades)) = lights.single(world) else {
        return Ok(false);
    };
    if light.shadow_maps_enabled != preset.shadows.enabled
        || light.contact_shadows_enabled != preset.shadows.contact_shadows_enabled
        || cascades.bounds.len() != usize::from(preset.shadows.cascade_count)
        || cascades.bounds.last().map(|v| v.to_bits())
            != Some(preset.shadows.maximum_distance_m.to_bits())
        || world
            .get_resource::<bevy::light::DirectionalLightShadowMap>()
            .map(|map| map.size)
            != Some(usize::from(preset.shadows.map_resolution))
    {
        return Ok(false);
    }
    let Some(camera) = camera(world)? else {
        return Ok(false);
    };
    let aa = world.get::<Msaa>(camera).map(Msaa::samples);
    let contact_matches = match (
        preset.contact_shadows,
        world.get::<bevy::pbr::ContactShadows>(camera),
    ) {
        (None, None) => true,
        (Some(expected), Some(actual)) => {
            actual.linear_steps == expected.linear_steps
                && actual.thickness.to_bits() == expected.thickness_m.to_bits()
                && actual.length.to_bits() == expected.length_m.to_bits()
        }
        _ => false,
    };
    Ok(aa == Some(u32::from(preset.msaa_samples))
        && contact_matches
        && world.get::<bevy::pbr::ContactShadows>(camera).is_some()
            == preset.shadows.contact_shadows_enabled
        && forge_engine::source_graphics::reference_matches(
            world,
            camera,
            effective.settings,
            preset.post_process,
        ))
}

fn gpu_ready(world: &World, state: &State) -> bool {
    if world
        .get_resource::<forge_engine::source_graph_runtime::SourceSceneResidents>()
        .is_some_and(|residents| !residents.particles_ready_for_inventory(world))
    {
        return false;
    }
    if world.contains_resource::<forge_engine::source_environment::host::Status>()
        && !forge_engine::source_environment::host::covers_frame(world, state.applied_frame)
    {
        return false;
    }
    for name in &state.active_scopes {
        if scope_token(world, state, name).is_none() {
            return false;
        }
    }
    let Some(terrain_token) = scope_token(world, state, "terrain") else {
        return false;
    };
    if world.contains_resource::<forge_engine::source_terrain::host::Status>()
        && !forge_engine::source_terrain::host::ready(world, Some(&terrain_token))
    {
        return false;
    }
    if world.contains_resource::<forge_engine::source_lighting::surfaces::live::Error>()
        && !forge_engine::source_lighting::surfaces::live::observe(world, Some(&terrain_token))
            .is_some_and(|status| status.ready && status.motion_matches)
    {
        return false;
    }
    if let Some(plants) = world.get_resource::<HostStatus>() {
        if !plants.ready {
            return false;
        }
        if plants.entities > 0 {
            let Some(evidence) = world.get_resource::<render_inventory::RenderInventoryEvidence>()
            else {
                return false;
            };
            let Some((owner, identity, generation)) = evidence
                .owner
                .zip(evidence.identity)
                .zip(evidence.generation)
                .map(|((a, b), c)| (a, b, c))
            else {
                return false;
            };
            if !render_inventory::acquire(world, owner, identity, generation)
                .is_some_and(|token| token.covers_frame(world, state.applied_frame))
            {
                return false;
            }
        }
    }
    true
}

fn reject(state: &mut State, error: String) {
    tracing::error!(%error,"Module graphics request rejected");
    state.error = Some(error);
    state.ready = false;
    state.simulation_admission = None;
}

pub(super) fn application_identity(state: &State) -> Result<forge_core::ContentHash, String> {
    canonical_json_sha256(&(
        state.source,
        state.terrain,
        state.policy.identity,
        &state.policy.shadow_hash,
        state.request_sequence,
        state.requested,
        state
            .policy
            .resolve(state.requested, state.requested_custom)?,
        state.applied_frame,
    ))
    .map_err(|error| error.to_string())
}

fn simulation_ready(world: &mut World, state: &State) -> bool {
    state.error.is_none()
        && state.dispatched == Some(state.requested)
        && application_identity(state).is_ok_and(|identity| {
            state
                .simulation_admission
                .as_ref()
                .is_some_and(|receipt| receipt.is_current(world, identity))
        })
        && cpu_ready(world, state).unwrap_or(false)
}

#[derive(Resource, Default)]
pub(super) struct GraphicsSimulationHold(forge_engine::cell_streaming::TemporarySimulationHold);

pub(super) fn release_hold(world: &mut World) {
    world.resource_scope(|world, mut hold: Mut<GraphicsSimulationHold>| hold.0.release(world));
}

pub(super) fn early(world: &mut World) {
    gate(world);
}
pub(super) fn final_gate(world: &mut World) {
    gate(world);
}
fn gate(world: &mut World) {
    let ready = world.resource_scope(|world, state: Mut<State>| simulation_ready(world, &state));
    if ready {
        return;
    }
    world.resource_scope(|world, mut hold: Mut<GraphicsSimulationHold>| hold.0.hold(world));
}

#[cfg(all(test, feature = "qualification"))]
mod diagnostic_tests {
    use super::*;
    #[test]
    fn retained_meshes_do_not_hide_visible_selection_changes_or_exceed_the_budget() {
        let mut assets = Assets::<Mesh>::default();
        let visible = assets.add(Cuboid::default());
        let cached = assets.add(Cuboid::default());
        let mut family = Family::<StandardMaterial> {
            meshes: vec![visible.clone()],
            materials: vec![],
            keys: vec!["visible-mesh".into()],
        };
        include_retained(&mut family, vec![visible.clone(), cached.clone()]);
        assert_eq!(family.meshes, vec![visible, cached]);
        assert_eq!(family.keys, vec!["visible-mesh"]);
        let too_many: Vec<_> = (0..render_inventory::MAX_SCOPED_MESHES)
            .map(|_| assets.add(Cuboid::default()))
            .collect();
        include_retained(&mut family, too_many);
        assert_eq!(family.meshes.len(), 2);
        assert_eq!(family.keys, vec!["visible-mesh"]);
    }
    #[test]
    fn changing_water_refreshes_only_water_unless_other_sources_are_stale() {
        let old = Signature(BTreeMap::from([
            ("terrain", vec!["mesh:t".into()]),
            ("water", vec!["mesh:a".into()]),
        ]));
        let new = Signature(BTreeMap::from([
            ("terrain", vec!["mesh:t".into()]),
            ("water", vec!["mesh:b".into()]),
        ]));
        let ready = ScopeAvailability {
            present: true,
            request: RequestState::Current,
            contained: false,
        };
        assert!(!scope_needs_refresh(Some(&old), &new, "terrain", ready));
        assert!(scope_needs_refresh(Some(&old), &new, "water", ready));
        assert!(scope_needs_refresh(
            Some(&old),
            &new,
            "terrain",
            ScopeAvailability {
                request: RequestState::Stale,
                ..ready
            }
        ));
        assert!(scope_needs_refresh(
            Some(&old),
            &new,
            "terrain",
            ScopeAvailability {
                request: RequestState::Absent,
                ..ready
            }
        ));
        assert!(scope_needs_refresh(
            None,
            &new,
            "terrain",
            ScopeAvailability {
                contained: true,
                ..ready
            }
        ));
        assert!(scope_needs_refresh(Some(&new), &old, "water", ready));
        assert!(!scope_needs_refresh(
            Some(&old),
            &new,
            "water",
            ScopeAvailability {
                contained: true,
                ..ready
            }
        ));
        assert!(scope_needs_refresh(
            Some(&old),
            &new,
            "water",
            ScopeAvailability {
                request: RequestState::Stale,
                contained: true,
                ..ready
            }
        ));
        assert!(scope_needs_refresh(
            Some(&old),
            &new,
            "water",
            ScopeAvailability {
                present: false,
                ..ready
            }
        ));
    }

    #[test]
    fn diagnostic_separates_resource_families_without_exposing_asset_contents() {
        let signature = Signature(BTreeMap::from([
            ("terrain", vec!["mesh:a".into()]),
            ("water", vec!["mesh:b".into(), "material:c".into()]),
        ]));
        assert_eq!(signature_family(&signature, "terrain"), &["mesh:a"]);
        assert_eq!(
            signature_family(&signature, "water"),
            &["mesh:b", "material:c"]
        );
        assert!(signature_family(&signature, "models").is_empty());
    }
}
