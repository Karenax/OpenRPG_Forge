use crate::{TerrainRuntimeState, VisualValleyTerrainChunk, VisualValleyTerrainMaterial};
use bevy::{
    asset::RenderAssetUsages,
    camera::{primitives::Aabb, visibility::VisibilityRange},
    core_pipeline::prepass::NormalPrepass,
    mesh::{
        Indices,
        morph::{MeshMorphWeights, MorphAttributes},
    },
    prelude::*,
    render::render_resource::PrimitiveTopology,
};
use forge_core::{ContentHash, canonical_json_sha256};
use forge_project::{
    TerrainDocument, TerrainLodQualitySettings, VisualQualityTier,
    VisualValleyQualityProfileDocument, VisualValleyQualityProfileError,
};
use forge_world::{TerrainChunkId, TerrainChunkMesh, TerrainLodMesh, TerrainLodTopology};
use serde::Serialize;
use std::collections::{BTreeMap, BTreeSet};

#[cfg(not(target_arch = "wasm32"))]
mod background;
mod bank_constraints;
#[cfg(any(target_arch = "wasm32", test))]
mod incremental;

#[cfg(test)]
mod tests;

#[derive(Resource)]
pub struct TerrainQualityRequest {
    document: VisualValleyQualityProfileDocument,
    source_hash: String,
    tier: VisualQualityTier,
}
impl TerrainQualityRequest {
    pub(crate) fn policy(&self) -> (&VisualValleyQualityProfileDocument, &str) {
        (&self.document, &self.source_hash)
    }

    pub const fn request(&mut self, tier: VisualQualityTier) {
        self.tier = tier;
    }
}

#[derive(Clone, Debug, Default, Resource, Serialize)]
pub struct TerrainLodProjectionStats {
    pub policy_hash: String,
    pub terrain_hash: String,
    pub requested_tier: Option<VisualQualityTier>,
    pub projected_tier: Option<VisualQualityTier>,
    pub canonical_chunks: usize,
    pub derived_meshes: usize,
    pub derived_vertices: usize,
    pub derived_triangles: usize,
    pub last_rebuilt_chunks: usize,
    pub bank_constrained_chunks: usize,
    pub bank_constraints: Option<forge_project::effective_quality::TerrainQualityConstraints>,
    pub status: &'static str,
}

struct LodVariant {
    entity: Entity,
    mesh: Handle<Mesh>,
    vertices: usize,
    triangles: usize,
}

impl TerrainLodProjectionStats {
    fn await_preparation(&mut self) {
        self.status = if cfg!(target_arch = "wasm32") {
            "preparing_staged_previous_projection_retained"
        } else {
            "preparing_background_previous_projection_retained"
        };
    }

    fn begin_request(&mut self, request: &TerrainQualityRequest) {
        self.policy_hash.clone_from(&request.source_hash);
        self.requested_tier = Some(request.tier);
        self.projected_tier = None;
        self.status = "awaiting_canonical_projection";
    }

    fn reject_projection(&mut self, reason: &str) {
        self.status = "projection_failed";
        error!(%reason, "terrain LOD projection rejected");
    }

    fn accept_prepared(
        &mut self,
        result: Result<Vec<PreparedLods>, String>,
    ) -> Option<Vec<PreparedLods>> {
        match result {
            Ok(prepared) => Some(prepared),
            Err(error) => {
                self.reject_projection(&error);
                None
            }
        }
    }
}

type LodPayload = (TerrainLodMesh, Option<TerrainLodMesh>);
type PreparedLods = (ContentHash, Option<[LodPayload; 3]>);

fn quality_topology(settings: TerrainLodQualitySettings) -> [TerrainLodTopology; 3] {
    [1, settings.lod1_sample_stride, settings.lod2_sample_stride].map(|stride| {
        TerrainLodTopology::new(stride).expect("validated policy strides are supported")
    })
}

fn record_projection(
    tier: VisualQualityTier,
    started: bevy::platform::time::Instant,
    rebuilt: usize,
    preparation_ms: f64,
) {
    info!(target: "forge::quality", schema = "forge.runtime-event", schema_version = 1_u64,
        event_kind = "quality_projection_cpu", domain = "terrain", tier = ?tier,
        elapsed_ms = started.elapsed().as_secs_f64() * 1000.0, rebuilt_chunks = rebuilt,
        preparation_ms, elapsed_scope = "current_main_thread_call_excludes_background_wait",
        "terrain quality CPU projection completed");
}

/// Only borrowed geometry enters workers. No ECS/assets are mutated until every task succeeds.
/// Bevy preserves spawn order for tasks spawned directly by this scope (also on single-thread WASM).
fn prepare_changed_lods(
    chunks: &[TerrainChunkMesh],
    fingerprints: &[(ContentHash, bool)],
    topology: &[TerrainLodTopology; 3],
    protected: &BTreeSet<TerrainChunkId>,
) -> Result<Vec<PreparedLods>, String> {
    if chunks.len() != fingerprints.len() {
        return Err("terrain preparation inventory mismatch".into());
    }
    bevy::tasks::ComputeTaskPool::get_or_init(bevy::tasks::TaskPool::new)
        .scope(|scope| {
            for (chunk, &(fingerprint, changed)) in chunks.iter().zip(fingerprints) {
                scope.spawn(async move {
                    let payload = changed
                        .then(|| {
                            prepare_lod_payloads(chunk, topology, protected.contains(&chunk.id))
                        })
                        .transpose()?;
                    Ok((fingerprint, payload))
                });
            }
        })
        .into_iter()
        .collect()
}

fn prepare_lod_payloads(
    chunk: &TerrainChunkMesh,
    topology: &[TerrainLodTopology; 3],
    constrained: bool,
) -> Result<[LodPayload; 3], String> {
    if constrained {
        let full = topology[0]
            .project(chunk)
            .map_err(|error| error.to_string())?;
        return Ok([(full.clone(), None), (full.clone(), None), (full, None)]);
    }
    let first = topology[0]
        .project_transition(chunk, &topology[1])
        .map_err(|error| error.to_string())?;
    let second = topology[1]
        .project_transition(chunk, &topology[2])
        .map_err(|error| error.to_string())?;
    let last = topology[2]
        .project(chunk)
        .map_err(|error| error.to_string())?;
    Ok([
        (first.0, Some(first.1)),
        (second.0, Some(second.1)),
        (last, None),
    ])
}

impl ChunkCache {
    fn validate_bindings(
        &self,
        bindings: &Query<&Mesh3d>,
        meshes: &Assets<Mesh>,
    ) -> Result<(), String> {
        for variant in &self.variants {
            if !bindings
                .get(variant.entity)
                .is_ok_and(|binding| binding.0.id() == variant.mesh.id())
            {
                return Err("missing or changed derived terrain binding".to_owned());
            }
            if meshes.get(&variant.mesh).is_none() {
                return Err("missing derived terrain mesh".to_owned());
            }
        }
        Ok(())
    }
}

#[derive(Component)]
struct TerrainMorphBand {
    start: f32,
    end: f32,
}

#[allow(
    clippy::type_complexity,
    reason = "Bevy camera and morph component queries"
)]
fn update_terrain_morphs(
    cameras: Query<(&Camera, &GlobalTransform), With<Camera3d>>,
    mut meshes: Query<(&Transform, &TerrainMorphBand, &mut MeshMorphWeights)>,
) {
    let mut active = cameras.iter().filter(|(camera, _)| camera.is_active);
    let Some((_, camera)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    for (transform, band, mut morph) in &mut meshes {
        let distance = camera.translation().distance(transform.translation);
        let weight = if band.end > band.start {
            ((distance - band.start) / (band.end - band.start)).clamp(0.0, 1.0)
        } else {
            0.0
        };
        if let MeshMorphWeights::Value { weights } = &mut *morph {
            weights[0] = weight;
        }
    }
}
struct ChunkCache {
    source_hash: ContentHash,
    variants: [LodVariant; 3],
}
#[derive(Default, Resource)]
struct TerrainLodCache {
    #[cfg(any(target_arch = "wasm32", test))]
    incremental: Option<incremental::Preparation>,
    #[cfg(not(target_arch = "wasm32"))]
    background: bool,
    #[cfg(not(target_arch = "wasm32"))]
    pending: Option<background::Preparation>,
    source_hash: Option<ContentHash>,
    policy_hash: String,
    tier: Option<VisualQualityTier>,
    chunks: BTreeMap<TerrainChunkId, ChunkCache>,
    bank_chunks: BTreeSet<TerrainChunkId>,
    bank_bindings: Vec<(bevy::asset::AssetId<Mesh>, Transform)>,
}

impl TerrainLodCache {
    fn discard_unneeded_preparation(&mut self) {
        #[cfg(not(target_arch = "wasm32"))]
        background::discard_completed(self);
        #[cfg(target_arch = "wasm32")]
        {
            self.incremental = None;
        }
    }

    fn matches_policy(&self, request: &TerrainQualityRequest) -> bool {
        self.tier == Some(request.tier) && self.policy_hash == request.source_hash
    }
}

pub struct VisualValleyTerrainQualityPlugin {
    document: VisualValleyQualityProfileDocument,
    hash: String,
    tier: VisualQualityTier,
}
impl VisualValleyTerrainQualityPlugin {
    pub fn new(
        terrain: &TerrainDocument,
        document: VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
    ) -> Result<Self, VisualValleyQualityProfileError> {
        document.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
        let hash = document.content_hash()?.to_string();
        Ok(Self {
            document,
            hash,
            tier,
        })
    }
}
impl Plugin for VisualValleyTerrainQualityPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(TerrainQualityRequest {
            document: self.document.clone(),
            source_hash: self.hash.clone(),
            tier: self.tier,
        })
        .init_resource::<TerrainLodCache>()
        .init_resource::<TerrainLodProjectionStats>()
        .add_systems(
            Update,
            (refresh_terrain_lods, update_terrain_morphs).chain(),
        );
        #[cfg(not(target_arch = "wasm32"))]
        if app.get_sub_app(bevy::render::RenderApp).is_some() {
            app.world_mut().resource_mut::<TerrainLodCache>().background = true;
        }
    }
}

fn ranges(settings: TerrainLodQualitySettings) -> [VisibilityRange; 3] {
    // One surface at a time: geomorph completes before the abrupt topology handoff.
    // Render-only meshes are recentered at the same origin for CPU and shader distance tests.
    // LOD2 stays opaque after its incoming transition. Fade only near the outer cull boundary,
    // using at most the first transition's width, not the entire remaining terrain horizon.
    let fade_width = (settings.lod1_distance_m - settings.lod0_distance_m)
        .min(settings.cull_distance_m - settings.lod2_distance_m);
    [
        VisibilityRange {
            start_margin: 0.0..0.0,
            end_margin: settings.lod1_distance_m..settings.lod1_distance_m,
            use_aabb: false,
        },
        VisibilityRange {
            start_margin: settings.lod1_distance_m..settings.lod1_distance_m,
            end_margin: settings.lod2_distance_m..settings.lod2_distance_m,
            use_aabb: false,
        },
        VisibilityRange {
            start_margin: settings.lod2_distance_m..settings.lod2_distance_m,
            end_margin: (settings.cull_distance_m - fade_width)..settings.cull_distance_m,
            use_aabb: false,
        },
    ]
}

fn mesh(pair: &LodPayload, center: Vec3) -> Mesh {
    let (payload, target) = pair;
    let mut result = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    )
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_POSITION,
        payload
            .positions_m
            .iter()
            .map(|position| (Vec3::from_array(*position) - center).to_array())
            .collect::<Vec<_>>(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, payload.normals.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, payload.uvs.clone())
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_COLOR,
        payload
            .material_weights
            .iter()
            .map(|weights| [weights.grass, weights.earth, weights.rock, 1.0])
            .collect::<Vec<_>>(),
    )
    .with_inserted_indices(Indices::U32(payload.indices.clone()));
    if let Some(target) = target {
        result.set_morph_targets(
            payload
                .positions_m
                .iter()
                .zip(&target.positions_m)
                .zip(payload.normals.iter().zip(&target.normals))
                .map(|((start, end), (normal, target_normal))| {
                    MorphAttributes::new(
                        Vec3::from_array(*end) - Vec3::from_array(*start),
                        Vec3::from_array(*target_normal) - Vec3::from_array(*normal),
                        Vec3::ZERO,
                    )
                })
                .collect(),
        );
    }
    result
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::type_complexity,
    reason = "Bevy terrain cache projection parameters"
)]
fn refresh_terrain_lods(
    mut commands: Commands,
    request: Res<TerrainQualityRequest>,
    source: Res<TerrainRuntimeState>,
    mut cache: ResMut<TerrainLodCache>,
    mut stats: ResMut<TerrainLodProjectionStats>,
    mut meshes: ResMut<Assets<Mesh>>,
    originals: Query<(
        Entity,
        &VisualValleyTerrainChunk,
        &MeshMaterial3d<VisualValleyTerrainMaterial>,
        &Transform,
    )>,
    cameras: Query<Entity, (With<Camera3d>, Without<NormalPrepass>)>,
    render_bindings: Query<&Mesh3d>,
    banks: Query<(&Mesh3d, &Transform), With<crate::water::VisualValleyRiverBankSurface>>,
    mut cpu_trace: Option<ResMut<crate::QualityCpuFrame>>,
) {
    for camera in &cameras {
        commands.entity(camera).insert(NormalPrepass);
    }
    let terrain = source.cache();
    let bank_bindings = banks
        .iter()
        .map(|(mesh, transform)| (mesh.0.id(), *transform))
        .collect::<Vec<_>>();
    let current =
        cache.source_hash == Some(terrain.source_document_hash) && cache.matches_policy(&request);
    if current
        && stats.projected_tier == Some(request.tier)
        && cache.bank_bindings == bank_bindings
        && !meshes.is_changed()
    {
        cache.discard_unneeded_preparation();
        return;
    }
    let Some(protected) = protected_chunks(&terrain.chunks, &bank_bindings, &meshes, &mut stats)
    else {
        return;
    };
    if current && cache.bank_chunks == protected {
        cache.discard_unneeded_preparation();
        cache.bank_bindings = bank_bindings;
        update_stats(&mut stats, &cache, &request, terrain.chunks.len(), 0);
        return;
    }
    let projection_started = bevy::platform::time::Instant::now();
    let settings = request.document.preset(request.tier).terrain;
    let visibility = ranges(settings);
    let topology = quality_topology(settings);
    let bindings = originals
        .iter()
        .map(|(entity, marker, material, transform)| {
            (marker.id, (entity, material.0.clone(), *transform))
        })
        .collect::<BTreeMap<_, _>>();
    stats.begin_request(&request);
    if bindings.len() != terrain.chunks.len() {
        return;
    }
    // Prepare every changed chunk before mutating any renderer asset.
    if let Err(error) = validate_projection_bindings(
        &terrain.chunks,
        &cache,
        &bindings,
        &render_bindings,
        &meshes,
    ) {
        stats.reject_projection(&error);
        return;
    }
    let prepare_started = bevy::platform::time::Instant::now();
    let output = prepare_requested_lods(
        &mut cache,
        &request,
        terrain.source_document_hash,
        &terrain.chunks,
        &topology,
        &protected,
    );
    crate::quality_cpu_trace::record_elapsed(
        cpu_trace.as_deref_mut(),
        crate::QualityCpuPhase::TerrainPrepare,
        prepare_started,
    );
    let Some((prepared, preparation_ms)) = output else {
        stats.await_preparation();
        return;
    };
    let Some(prepared) = stats.accept_prepared(prepared) else {
        return;
    };
    let mut rebuilt = 0;
    let commit_started = bevy::platform::time::Instant::now();
    for (chunk, (fingerprint, payloads)) in terrain.chunks.iter().zip(prepared) {
        rebuilt += usize::from(project_chunk(
            &mut commands,
            &mut meshes,
            &mut cache,
            chunk,
            fingerprint,
            payloads,
            &bindings[&chunk.id],
            &visibility,
            settings,
        ));
    }
    cache.source_hash = Some(terrain.source_document_hash);
    cache.bank_chunks = protected;
    cache.bank_bindings = bank_bindings;
    cache.tier = Some(request.tier);
    cache.policy_hash.clone_from(&request.source_hash);
    update_stats(&mut stats, &cache, &request, bindings.len(), rebuilt);
    crate::quality_cpu_trace::record_elapsed(
        cpu_trace.as_deref_mut(),
        crate::QualityCpuPhase::TerrainCommit,
        commit_started,
    );
    record_projection(request.tier, projection_started, rebuilt, preparation_ms);
}

type PreparationOutput = (Result<Vec<PreparedLods>, String>, f64);
fn protected_chunks(
    chunks: &[TerrainChunkMesh],
    bindings: &[(bevy::asset::AssetId<Mesh>, Transform)],
    meshes: &Assets<Mesh>,
    stats: &mut TerrainLodProjectionStats,
) -> Option<BTreeSet<TerrainChunkId>> {
    match bank_constraints::affected_chunks(chunks, bindings, meshes) {
        Ok(chunks) => Some(chunks),
        Err(reason) => {
            stats.projected_tier = None;
            stats.status = "bank_constraints_failed";
            error!(%reason, "terrain bank constraints rejected");
            None
        }
    }
}
type CanonicalBinding = (Entity, Handle<VisualValleyTerrainMaterial>, Transform);

fn validate_projection_bindings(
    chunks: &[TerrainChunkMesh],
    cache: &TerrainLodCache,
    originals: &BTreeMap<TerrainChunkId, CanonicalBinding>,
    bindings: &Query<&Mesh3d>,
    meshes: &Assets<Mesh>,
) -> Result<(), String> {
    for chunk in chunks {
        if !originals.contains_key(&chunk.id) {
            return Err("missing canonical terrain binding".into());
        }
        if let Some(cached) = cache.chunks.get(&chunk.id) {
            cached.validate_bindings(bindings, meshes)?;
        }
    }
    Ok(())
}

fn prepare_requested_lods(
    cache: &mut TerrainLodCache,
    request: &TerrainQualityRequest,
    source: ContentHash,
    chunks: &[TerrainChunkMesh],
    topology: &[TerrainLodTopology; 3],
    protected: &BTreeSet<TerrainChunkId>,
) -> Option<PreparationOutput> {
    #[cfg(target_arch = "wasm32")]
    {
        incremental::prepare(cache, request, source, chunks, topology, protected)
    }
    #[cfg(not(target_arch = "wasm32"))]
    {
        if cache.background {
            return background::prepare(cache, request, source, chunks, topology, protected);
        }
        // Headless native execution remains synchronous.
        let _ = source;
        let started = bevy::platform::time::Instant::now();
        let result = prepare_from_snapshot(
            chunks,
            topology,
            protected,
            &cache.bank_chunks,
            !cache.matches_policy(request),
            &cache
                .chunks
                .iter()
                .map(|(id, c)| (*id, c.source_hash))
                .collect(),
        );
        Some((result, started.elapsed().as_secs_f64() * 1000.0))
    }
}

fn prepare_from_snapshot(
    chunks: &[TerrainChunkMesh],
    topology: &[TerrainLodTopology; 3],
    protected: &BTreeSet<TerrainChunkId>,
    previous_protected: &BTreeSet<TerrainChunkId>,
    changed_policy: bool,
    previous: &BTreeMap<TerrainChunkId, ContentHash>,
) -> Result<Vec<PreparedLods>, String> {
    let fingerprints = chunks
        .iter()
        .map(|chunk| {
            let hash = canonical_json_sha256(chunk).map_err(|e| e.to_string())?;
            // Protected banks use the canonical full topology at every tier, without
            // morph targets. Policy-only changes update ranges, not their geometry.
            let changed = (changed_policy && !protected.contains(&chunk.id))
                || protected.contains(&chunk.id) != previous_protected.contains(&chunk.id)
                || previous.get(&chunk.id) != Some(&hash);
            Ok((hash, changed))
        })
        .collect::<Result<Vec<_>, String>>()?;
    prepare_changed_lods(chunks, &fingerprints, topology, protected)
}

#[allow(
    clippy::too_many_arguments,
    reason = "explicit prevalidated projection inputs"
)]
fn project_chunk(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    cache: &mut TerrainLodCache,
    chunk: &TerrainChunkMesh,
    fingerprint: ContentHash,
    payloads: Option<[LodPayload; 3]>,
    binding: &(Entity, Handle<VisualValleyTerrainMaterial>, Transform),
    visibility: &[VisibilityRange; 3],
    settings: TerrainLodQualitySettings,
) -> bool {
    let (original, material, transform) = binding;
    let (mut minimum, mut maximum) = (Vec3::splat(f32::INFINITY), Vec3::splat(f32::NEG_INFINITY));
    for position in &chunk.positions_m {
        let position = Vec3::from_array(*position);
        minimum = minimum.min(position);
        maximum = maximum.max(position);
    }
    let bounds = Aabb::from_min_max(minimum, maximum);
    // Keep the canonical mesh and physics binding intact, but render separate local-space copies.
    commands.entity(*original).insert(Visibility::Hidden);
    let center = Vec3::from(bounds.center);
    let render_transform = Transform {
        translation: transform.transform_point(center),
        ..*transform
    };
    let render_bounds = Aabb {
        center: default(),
        half_extents: bounds.half_extents,
    };
    let rebuilt = payloads.is_some();
    let bands = [
        (settings.lod0_distance_m, settings.lod1_distance_m),
        (settings.lod1_distance_m, settings.lod2_distance_m),
        (0.0, 0.0),
    ];
    if let Some(cached) = cache.chunks.get_mut(&chunk.id) {
        for (level, variant) in cached.variants.iter_mut().enumerate() {
            if let Some(payloads) = &payloads {
                let payload = &payloads[level];
                *meshes
                    .get_mut(&variant.mesh)
                    .expect("derived meshes were prevalidated") = mesh(payload, center);
                variant.vertices = payload.0.positions_m.len();
                variant.triangles = payload.0.indices.len() / 3;
            }
            // Even a retained bank mesh must follow this tier's visibility distances.
            commands.entity(variant.entity).insert((
                render_bounds,
                render_transform,
                visibility[level].clone(),
                TerrainMorphBand {
                    start: bands[level].0,
                    end: bands[level].1,
                },
                MeshMorphWeights::Value { weights: vec![0.0] },
            ));
        }
        cached.source_hash = fingerprint;
    } else {
        let Some(payloads) = payloads else {
            return false;
        };
        let mut next_level = 0;
        let variants = payloads.map(|payload| {
            let level = next_level;
            next_level += 1;
            let handle = meshes.add(mesh(&payload, center));
            let entity = commands
                .spawn((
                    Name::new(format!("Terrain {} LOD {level}", chunk.id)),
                    Mesh3d(handle.clone()),
                    MeshMaterial3d(material.clone()),
                    render_transform,
                    render_bounds,
                    visibility[level].clone(),
                    TerrainMorphBand {
                        start: bands[level].0,
                        end: bands[level].1,
                    },
                    MeshMorphWeights::Value { weights: vec![0.0] },
                ))
                .id();
            LodVariant {
                entity,
                mesh: handle,
                vertices: payload.0.positions_m.len(),
                triangles: payload.0.indices.len() / 3,
            }
        });
        cache.chunks.insert(
            chunk.id,
            ChunkCache {
                source_hash: fingerprint,
                variants,
            },
        );
    }
    rebuilt
}

fn update_stats(
    stats: &mut TerrainLodProjectionStats,
    cache: &TerrainLodCache,
    request: &TerrainQualityRequest,
    canonical_chunks: usize,
    rebuilt: usize,
) {
    let settings = request.document.preset(request.tier).terrain;
    stats.terrain_hash = cache
        .source_hash
        .expect("projection just committed")
        .to_string();
    stats.canonical_chunks = canonical_chunks;
    stats.bank_constrained_chunks = cache.bank_chunks.len();
    stats.bank_constraints = (!cache.bank_chunks.is_empty())
        .then(|| {
            u32::try_from(canonical_chunks)
                .ok()
                .map(|canonical_chunks| {
                    forge_project::effective_quality::TerrainQualityConstraints {
                        terrain_hash: cache.source_hash.expect("projection just committed"),
                        canonical_chunks,
                        full_resolution_chunks: cache
                            .bank_chunks
                            .iter()
                            .map(|id| id.as_entity_id())
                            .collect(),
                    }
                })
        })
        .flatten();
    stats.derived_meshes = cache.chunks.len() * 3;
    stats.derived_vertices = cache
        .chunks
        .values()
        .flat_map(|chunk| &chunk.variants)
        .map(|variant| variant.vertices)
        .sum();
    stats.derived_triangles = cache
        .chunks
        .values()
        .flat_map(|chunk| &chunk.variants)
        .map(|variant| variant.triangles)
        .sum();
    stats.last_rebuilt_chunks = rebuilt;
    stats.projected_tier = Some(request.tier);
    stats.status = "projected_cpu_meshes_and_ranges_not_gpu_execution";
    info!(target: "forge::terrain", schema = "forge.runtime-event", schema_version = 1_u64,
        event_kind = "terrain_lod_projected", terrain_hash = %stats.terrain_hash,
        policy_hash = %stats.policy_hash, tier = ?request.tier, canonical_chunks = stats.canonical_chunks,
        derived_meshes = stats.derived_meshes, derived_vertices = stats.derived_vertices,
        derived_triangles = stats.derived_triangles, rebuilt_chunks = rebuilt,
        lod1_stride = settings.lod1_sample_stride, lod2_stride = settings.lod2_sample_stride,
        "terrain LOD caches and complementary visibility ranges projected; not GPU draw counts");
}
