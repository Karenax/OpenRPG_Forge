use super::super::{
    PreparedProjectionMarker, PreparedVegetationProjection, VegetationMaterialHandle,
    VegetationMeshKey, VegetationPresentation, VegetationProjectionLod, VegetationProjectionStats,
    VegetationQualityRequest, prepare_vegetation_projection_with_promotions,
};
use bevy::{
    camera::{primitives::Aabb, visibility::NoAutoAabb},
    light::NotShadowCaster,
    prelude::*,
};
use forge_project::VisualQualityTier;
use forge_project::{TerrainDocument, VegetationDocument};
use forge_world::VegetationInstanceId;
use std::collections::HashMap;

#[cfg(not(target_arch = "wasm32"))]
mod background;
#[cfg(any(target_arch = "wasm32", test))]
mod incremental;
mod revision_reuse;

#[cfg(test)]
mod commit_probe;
pub(in crate::vegetation) mod staging;

#[derive(Clone, Debug, Eq, Hash, PartialEq)]
pub(in crate::vegetation) enum BindingKey {
    Placement(VegetationInstanceId, VegetationProjectionLod),
    Batch(u16, u16, VegetationProjectionLod),
}

#[derive(Resource)]
pub(crate) struct VegetationQualityBindings {
    #[cfg(any(target_arch = "wasm32", test))]
    incremental: Option<incremental::Preparation>,
    #[cfg(any(target_arch = "wasm32", test))]
    source_plan: Option<incremental::SourcePlan>,
    #[cfg(not(target_arch = "wasm32"))]
    background: bool,
    #[cfg(not(target_arch = "wasm32"))]
    pending: Option<background::Preparation>,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    paths: Vec<forge_project::zone_authoring::paths::AuthoredPath>,
    rivers: Vec<forge_project::zone_authoring::rivers::AuthoredRiver>,
    buildings: Vec<forge_project::zone_authoring::buildings::AuthoredBuilding>,
    promotions: Vec<forge_project::zone_authoring::promotion::PromotedInstance>,
    // Composite population identity for corridors and rooms; None is legacy vegetation.
    path_source_key: Option<String>,
    attempted: Option<VisualQualityTier>,
    attempted_policy: String,
    source_changed: bool,
    pub(in crate::vegetation) meshes: HashMap<VegetationMeshKey, (Handle<Mesh>, Aabb)>,
    pub(in crate::vegetation) entities: HashMap<BindingKey, Entity>,
}

impl VegetationQualityBindings {
    pub(in crate::vegetation) fn new(
        terrain: TerrainDocument,
        vegetation: VegetationDocument,
    ) -> Self {
        Self {
            #[cfg(any(target_arch = "wasm32", test))]
            incremental: None,
            #[cfg(any(target_arch = "wasm32", test))]
            source_plan: None,
            #[cfg(not(target_arch = "wasm32"))]
            background: false,
            #[cfg(not(target_arch = "wasm32"))]
            pending: None,
            terrain,
            vegetation,
            paths: Vec::new(),
            rivers: Vec::new(),
            buildings: Vec::new(),
            promotions: Vec::new(),
            path_source_key: None,
            attempted: None,
            attempted_policy: String::new(),
            source_changed: false,
            meshes: HashMap::new(),
            entities: HashMap::new(),
        }
    }

    fn record_attempt(&mut self, request: &VegetationQualityRequest) {
        self.attempted = Some(request.tier);
        self.attempted_policy.clone_from(&request.hash);
    }

    fn vegetation_key(&self) -> Result<String, String> {
        self.path_source_key.clone().map_or_else(
            || {
                self.vegetation
                    .content_hash()
                    .map(|h| h.to_string())
                    .map_err(|e| e.to_string())
            },
            Ok,
        )
    }

    #[cfg(not(target_arch = "wasm32"))]
    pub(in crate::vegetation) fn enable_background(&mut self) {
        self.background = true;
    }

    pub(in crate::vegetation) fn key(marker: &PreparedProjectionMarker) -> BindingKey {
        match marker {
            PreparedProjectionMarker::Placement(value) => {
                BindingKey::Placement(value.id, value.lod)
            }
            PreparedProjectionMarker::Batch(value) => {
                BindingKey::Batch(value.chunk_x, value.chunk_z, value.lod)
            }
        }
    }
}

pub(crate) fn apply_quality(world: &mut World) {
    let Some(request) = world.get_resource::<VegetationQualityRequest>() else {
        return;
    };
    if !world.contains_resource::<VegetationQualityBindings>() {
        return;
    }
    let request = request.clone();
    let Some(observed) = world
        .resource::<VegetationProjectionStats>()
        .quality
        .clone()
    else {
        return;
    };
    staging::discard_superseded(world, &request);
    if observed.projected_tier == request.tier
        && observed.policy_hash == request.hash
        && !world.resource::<VegetationQualityBindings>().source_changed
    {
        staging::cancel(world);
        #[cfg(target_arch = "wasm32")]
        {
            world
                .resource_mut::<VegetationQualityBindings>()
                .incremental = None;
        }
        #[cfg(not(target_arch = "wasm32"))]
        background::discard_completed(&mut world.resource_mut::<VegetationQualityBindings>());
        world
            .resource_mut::<VegetationQualityBindings>()
            .record_attempt(&request);
        if let Some(quality) = &mut world.resource_mut::<VegetationProjectionStats>().quality {
            quality.requested_tier = request.tier;
            if matches!(
                quality.status,
                "rejected_previous_cpu_projection_retained"
                    | "preparing_background_previous_projection_retained"
                    | "preparing_staged_previous_projection_retained"
                    | "uploading_staged_previous_projection_retained"
            ) {
                quality.status = "projected_cpu_not_gpu_or_global_preset";
            }
        }
        return;
    }
    let bindings = world.resource::<VegetationQualityBindings>();
    if bindings.attempted == Some(request.tier) && bindings.attempted_policy == request.hash {
        return;
    }
    if bindings
        .entities
        .values()
        .any(|entity| world.get_entity(*entity).is_err())
        || bindings
            .meshes
            .values()
            .any(|(handle, _)| !world.resource::<Assets<Mesh>>().contains(handle.id()))
    {
        staging::cancel(world);
        world
            .resource_mut::<VegetationQualityBindings>()
            .record_attempt(&request);
        if let Some(quality) = &mut world.resource_mut::<VegetationProjectionStats>().quality {
            quality.requested_tier = request.tier;
            quality.status = "rejected_missing_live_binding";
        }
        warn!(target: "forge::vegetation", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "vegetation_quality_missing_binding", "Quality change refused: owned binding is missing");
        return;
    }
    if crate::quality_cpu_trace::measure(world, crate::QualityCpuPhase::VegetationCommit, |world| {
        staging::advance(world, &request, observed.initial_tier)
    }) {
        return;
    }
    // Finish all fallible source/geometry work before touching any live binding.
    let projection_started = bevy::platform::time::Instant::now();
    let output = crate::quality_cpu_trace::measure(
        world,
        crate::QualityCpuPhase::VegetationPrepare,
        |world| prepare_requested(world, &request),
    );
    let Some((prepared, preparation_ms)) = output else {
        if let Some(quality) = &mut world.resource_mut::<VegetationProjectionStats>().quality {
            quality.requested_tier = request.tier;
            quality.status = if cfg!(target_arch = "wasm32") {
                "preparing_staged_previous_projection_retained"
            } else {
                "preparing_background_previous_projection_retained"
            };
        }
        return;
    };
    complete_preparation(
        world,
        request,
        observed.initial_tier,
        prepared,
        preparation_ms,
        projection_started,
    );
}

fn complete_preparation(
    world: &mut World,
    request: VegetationQualityRequest,
    initial: VisualQualityTier,
    prepared: Result<PreparedVegetationProjection, String>,
    preparation_ms: f64,
    projection_started: bevy::platform::time::Instant,
) {
    match prepared {
        Ok(prepared) => {
            if world.contains_resource::<staging::MeshStaging>() {
                staging::start(world, prepared, request);
                return;
            }
            world
                .resource_mut::<VegetationQualityBindings>()
                .record_attempt(&request);
            reconcile(world, prepared, initial);
            info!(target: "forge::quality", schema = "forge.runtime-event", schema_version = 1_u64,
                event_kind = "quality_projection_cpu", domain = "vegetation", tier = ?request.tier,
                preparation_ms, elapsed_ms = projection_started.elapsed().as_secs_f64() * 1000.0,
                elapsed_scope = "current_main_thread_call_excludes_background_wait",
                "vegetation quality CPU projection completed");
        }
        Err(error) => {
            staging::cancel(world);
            world
                .resource_mut::<VegetationQualityBindings>()
                .record_attempt(&request);
            if let Some(quality) = &mut world.resource_mut::<VegetationProjectionStats>().quality {
                quality.requested_tier = request.tier;
                quality.status = "rejected_previous_cpu_projection_retained";
            }
            warn!(target: "forge::vegetation", schema = "forge.runtime-event", schema_version = 1_u64,
                event_kind = "vegetation_quality_rejected", %error, "Vegetation quality retained");
        }
    }
}

fn prepare_requested(
    world: &mut World,
    request: &VegetationQualityRequest,
) -> Option<(Result<PreparedVegetationProjection, String>, f64)> {
    #[cfg(target_arch = "wasm32")]
    {
        let stream_meshes = world.contains_resource::<staging::MeshStaging>();
        let output = incremental::prepare(
            &mut world.resource_mut::<VegetationQualityBindings>(),
            request,
            stream_meshes,
        );
        if output.is_none() {
            let stream = incremental::mesh_stream(world.resource::<VegetationQualityBindings>());
            staging::receive_chunks(world, request, stream);
        }
        output
    }
    #[cfg(not(target_arch = "wasm32"))]
    {
        #[cfg(not(target_arch = "wasm32"))]
        if world.resource::<VegetationQualityBindings>().background {
            return background::prepare(
                &mut world.resource_mut::<VegetationQualityBindings>(),
                request,
            );
        }
        // Headless native callers remain synchronous. Browser polling is not a worker.
        let bindings = world.resource::<VegetationQualityBindings>();
        let started = bevy::platform::time::Instant::now();
        let result = prepare_vegetation_projection_with_promotions(
            &bindings.terrain,
            &bindings.vegetation,
            &bindings.paths,
            &bindings.rivers,
            &bindings.buildings,
            &bindings.promotions,
            Some(request),
        )
        .map_err(|error| error.to_string());
        Some((result, started.elapsed().as_secs_f64() * 1000.0))
    }
}

#[cfg(feature = "physics-rapier")]
pub(in crate::vegetation) fn restore_procedural(world: &mut World) -> Result<(), String> {
    let bindings = world
        .get_resource::<VegetationQualityBindings>()
        .ok_or("missing quality bindings for recovery")?;
    let request = world
        .get_resource::<VegetationQualityRequest>()
        .ok_or("missing quality request for recovery")?;
    if bindings
        .entities
        .values()
        .any(|entity| world.get_entity(*entity).is_err())
        || bindings
            .meshes
            .values()
            .any(|(handle, _)| !world.resource::<Assets<Mesh>>().contains(handle.id()))
    {
        return Err("missing live procedural binding during recovery".into());
    }
    let initial = world
        .resource::<VegetationProjectionStats>()
        .quality
        .as_ref()
        .ok_or("missing quality observation for recovery")?
        .initial_tier;
    let prepared = prepare_vegetation_projection_with_promotions(
        &bindings.terrain,
        &bindings.vegetation,
        &bindings.paths,
        &bindings.rivers,
        &bindings.buildings,
        &bindings.promotions,
        Some(request),
    )
    .map_err(|error| error.to_string())?;
    reconcile(world, prepared, initial);
    Ok(())
}

fn reconcile(
    world: &mut World,
    prepared: PreparedVegetationProjection,
    initial: VisualQualityTier,
) {
    crate::quality_cpu_trace::measure(world, crate::QualityCpuPhase::VegetationCommit, |world| {
        reconcile_inner(world, prepared, initial);
    });
}

fn reconcile_inner(
    world: &mut World,
    mut prepared: PreparedVegetationProjection,
    initial: VisualQualityTier,
) {
    let material = world.resource::<VegetationMaterialHandle>().0.clone();
    let mut bindings = world
        .remove_resource::<VegetationQualityBindings>()
        .expect("exclusive system owns bindings");
    #[cfg(feature = "physics-rapier")]
    if bindings.source_changed {
        super::super::collision::replace_tree_trunks(
            world,
            std::mem::take(&mut prepared.tree_colliders),
        );
    }
    // Retain handles, including temporarily unused chunks, so a return cycle reuses asset IDs.
    for value in prepared.meshes {
        if let Some((handle, bounds)) = bindings.meshes.get_mut(&value.key) {
            *world
                .resource_mut::<Assets<Mesh>>()
                .get_mut(handle.id())
                .expect("owned mesh stays resident") = value.value.mesh;
            *bounds = value.value.aabb;
        } else {
            let handle = world.resource_mut::<Assets<Mesh>>().add(value.value.mesh);
            bindings
                .meshes
                .insert(value.key, (handle, value.value.aabb));
        }
    }
    let mut previous = std::mem::take(&mut bindings.entities);
    for value in prepared.entities {
        let key = VegetationQualityBindings::key(&value.marker);
        let entity = previous
            .remove(&key)
            .unwrap_or_else(|| world.spawn_empty().id());
        let (mesh, bounds) = &bindings.meshes[&value.mesh_key];
        let mut target = world.entity_mut(entity);
        target.insert((
            Name::new(value.name),
            Mesh3d(mesh.clone()),
            MeshMaterial3d(material.clone()),
            value.transform,
            value.mesh_tag,
            value.visibility_range,
            *bounds,
            NoAutoAabb,
        ));
        match value.marker {
            PreparedProjectionMarker::Placement(marker) => {
                target.insert(marker);
            }
            PreparedProjectionMarker::Batch(marker) => {
                target.insert(marker);
            }
        }
        if value.casts_shadow {
            target.remove::<NotShadowCaster>();
        } else {
            target.insert(NotShadowCaster);
        }
        bindings.entities.insert(key, entity);
    }
    for entity in previous.into_values() {
        world.despawn(entity);
    }
    if let Some(quality) = &mut prepared.stats.quality {
        quality.initial_tier = initial;
        quality.status = "projected_cpu_not_gpu_or_global_preset";
    }
    prepared.stats.render_path = world
        .resource::<VegetationProjectionStats>()
        .render_path
        .clone();
    // A quality change does not repair a failed governed preview.
    prepared.stats.prototype_preview_error.clone_from(
        &world
            .resource::<VegetationProjectionStats>()
            .prototype_preview_error,
    );
    world.insert_resource(VegetationPresentation(prepared.profile));
    world.insert_resource(prepared.scatter);
    world.insert_resource(prepared.stats);
    bindings.source_changed = false;
    world.insert_resource(bindings);
}

/// Queue accepted authoring sources on the existing renderer. No new app or writer.
/// The caller must first synchronize the terrain projection with `terrain`.
pub fn update_vegetation_sources(
    world: &mut World,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
) -> Result<bool, String> {
    update_vegetation_sources_with_paths(world, terrain, vegetation, Vec::new())
}

/// Queue immutable authored paths separately from manual vegetation exclusions.
pub fn update_vegetation_sources_with_paths(
    world: &mut World,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    paths: Vec<forge_project::zone_authoring::paths::AuthoredPath>,
) -> Result<bool, String> {
    update_vegetation_sources_with_waterways(world, terrain, vegetation, paths, Vec::new())
}

/// Queue immutable paths and rivers; all preparation backends share their identity.
pub fn update_vegetation_sources_with_waterways(
    world: &mut World,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    paths: Vec<forge_project::zone_authoring::paths::AuthoredPath>,
    rivers: Vec<forge_project::zone_authoring::rivers::AuthoredRiver>,
) -> Result<bool, String> {
    update_vegetation_sources_with_buildings(world, terrain, vegetation, paths, rivers, Vec::new())
}

/// Queue accepted room footprints alongside corridors; never edits vegetation masks.
pub fn update_vegetation_sources_with_buildings(
    world: &mut World,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    paths: Vec<forge_project::zone_authoring::paths::AuthoredPath>,
    rivers: Vec<forge_project::zone_authoring::rivers::AuthoredRiver>,
    buildings: Vec<forge_project::zone_authoring::buildings::AuthoredBuilding>,
) -> Result<bool, String> {
    update_vegetation_sources_with_promotions(
        world,
        terrain,
        vegetation,
        paths,
        rivers,
        buildings,
        Vec::new(),
    )
}

/// Queue exact generated-origin exclusions with the same immutable source snapshot.
pub fn update_vegetation_sources_with_promotions(
    world: &mut World,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    paths: Vec<forge_project::zone_authoring::paths::AuthoredPath>,
    rivers: Vec<forge_project::zone_authoring::rivers::AuthoredRiver>,
    buildings: Vec<forge_project::zone_authoring::buildings::AuthoredBuilding>,
    promotions: Vec<forge_project::zone_authoring::promotion::PromotedInstance>,
) -> Result<bool, String> {
    let bindings = world
        .get_resource::<VegetationQualityBindings>()
        .ok_or("Vegetation renderer is not initialized")?;
    if terrain.project_id != bindings.terrain.project_id
        || terrain.area_id != bindings.terrain.area_id
    {
        return Err("Vegetation source belongs to another zone".into());
    }
    if terrain == bindings.terrain
        && vegetation == bindings.vegetation
        && paths == bindings.paths
        && rivers == bindings.rivers
        && buildings == bindings.buildings
        && promotions == bindings.promotions
    {
        return Ok(false);
    }
    let plan = forge_world::VegetationGenerationPlan::from_documents_with_buildings(
        &terrain,
        &vegetation,
        &paths,
        &rivers,
        &buildings,
    )
    .and_then(|plan| plan.with_promotions(&promotions))
    .map_err(|error| error.to_string())?;
    let path_source_key =
        if paths.is_empty() && rivers.is_empty() && buildings.is_empty() && promotions.is_empty() {
            None
        } else {
            Some(plan.source_hash().to_string())
        };
    // Keep accepted metadata current without cancelling an upload or quality transition
    // when a room edit leaves its root-exclusion footprint unchanged. Validate above
    // even for unchanged geometry; invalid doors must not enter the bindings.
    if terrain == bindings.terrain
        && vegetation == bindings.vegetation
        && paths == bindings.paths
        && rivers == bindings.rivers
        && path_source_key == bindings.path_source_key
        && promotions == bindings.promotions
    {
        world.resource_mut::<VegetationQualityBindings>().buildings = buildings;
        return Ok(false);
    }
    let request = world
        .get_resource::<VegetationQualityRequest>()
        .ok_or("Vegetation quality policy is missing")?;
    let mut policy = request.document.clone();
    policy.revision = terrain.revision;
    let request = VegetationQualityRequest::new(&terrain, policy, request.tier)
        .map_err(|error| error.to_string())?;
    request
        .validate_vegetation(&vegetation)
        .map_err(|error| error.to_string())?;
    let reuse = paths == bindings.paths
        && promotions == bindings.promotions
        && rivers == bindings.rivers
        && revision_reuse::can_reuse(world, &terrain, &vegetation, &buildings)?;
    let terrain_key = terrain
        .content_hash()
        .map_err(|error| error.to_string())?
        .to_string();
    // Discard uploads prepared from the previous sources before replacing them.
    staging::cancel(world);
    let mut bindings = world.resource_mut::<VegetationQualityBindings>();
    bindings.terrain = terrain;
    bindings.vegetation = vegetation;
    bindings.paths = paths;
    bindings.rivers = rivers;
    bindings.buildings = buildings;
    bindings.promotions = promotions;
    bindings.path_source_key = path_source_key;
    bindings.attempted = None;
    bindings.attempted_policy.clear();
    bindings.source_changed = !reuse;
    #[cfg(any(target_arch = "wasm32", test))]
    {
        bindings.incremental = None;
        bindings.source_plan = None;
    }
    // Keep at most one native worker; its existing source-hash guard rejects stale output.
    if reuse {
        bindings.record_attempt(&request);
        let mut stats = world.resource_mut::<VegetationProjectionStats>();
        stats.terrain_document_hash = terrain_key;
        stats.vegetation_document_hash = plan.source_hash().to_string();
        if let Some(quality) = &mut stats.quality {
            quality.policy_hash.clone_from(&request.hash);
            quality.status = "reused_settled_geometry_new_source_receipt";
        }
    }
    world.insert_resource(request);
    Ok(true)
}
