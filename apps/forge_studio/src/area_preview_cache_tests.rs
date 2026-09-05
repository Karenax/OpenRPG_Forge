use super::*;
use forge_core::{AreaId, CommandId, ModuleId, ProjectId, Revision};
use forge_project::{
    AuthoredPath, AuthoredRiver, AuthoredRoad, AuthoredWall, NewProjectCommand, PathControlPoint,
    PathId, PathInterpolation, RiverChannelProfile, RiverChannelProfileAlgorithm, RiverSurface,
    RoadProfile, RoadProfileAlgorithm, RoadSurface, TerrainHeightDelta, TerrainMaterialLayerId,
    VegetationDensityOverride, VegetationExclusion, VegetationExclusionId, VegetationKind,
    VegetationLayer, VegetationSpeciesPreset, WallProfile, WallProfileAlgorithm, WallSurface,
    WorkspaceArea, WorkspaceChange, WorkspaceDocument, WorkspaceEdit,
};
use forge_world::VegetationGenerationPlan;

fn workspace() -> WorkspaceDocument {
    WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("area-preview/create"),
        project_id: ProjectId::deterministic("area-preview/project"),
        module_id: module_id(),
        area_id: AreaId::deterministic("area-preview/area"),
        project_name: "Preview".into(),
        module_name: "Module".into(),
        area_name: "Zone".into(),
        seed: 42,
        spawn: Vec3::new(0.0, 2.0, 0.0),
    })
    .unwrap()
    .document
}

fn area() -> WorkspaceArea {
    workspace().project.modules.remove(0).areas.remove(0)
}

fn paint_grass_cells(
    document: &WorkspaceDocument,
    command_key: &str,
    cells: &[(u16, u16)],
) -> WorkspaceDocument {
    let module = &document.project.modules[0];
    let area = &module.areas[0];
    let layer_id = area
        .vegetation
        .layers
        .iter()
        .find(|layer| layer.id.as_str() == "forge_grass")
        .unwrap()
        .id
        .clone();
    document
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic(command_key),
            base_revision: document.revision,
            change: WorkspaceChange::SetVegetationDensityOverrides {
                module_id: module.id,
                area_id: area.id,
                layer_id,
                expected_density_hash: area.vegetation_density.content_hash().unwrap(),
                density_overrides: cells
                    .iter()
                    .map(|&(x, z)| VegetationDensityOverride::new(x, z, u8::MAX))
                    .collect(),
            },
        })
        .unwrap()
        .document
}

fn set_catalog(
    document: &WorkspaceDocument,
    command_key: &str,
    layers: Vec<VegetationLayer>,
) -> WorkspaceDocument {
    let module = &document.project.modules[0];
    let area = &module.areas[0];
    document
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic(command_key),
            base_revision: document.revision,
            change: WorkspaceChange::SetVegetationCatalog {
                module_id: module.id,
                area_id: area.id,
                expected_vegetation_hash: area.vegetation.content_hash().unwrap(),
                expected_density_hash: area.vegetation_density.content_hash().unwrap(),
                layers,
            },
        })
        .unwrap()
        .document
}

fn set_exclusions(
    document: &WorkspaceDocument,
    command_key: &str,
    exclusions: Vec<VegetationExclusion>,
) -> WorkspaceDocument {
    let module = &document.project.modules[0];
    let area = &module.areas[0];
    document
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic(command_key),
            base_revision: document.revision,
            change: WorkspaceChange::SetVegetationExclusions {
                module_id: module.id,
                area_id: area.id,
                expected_vegetation_hash: area.vegetation.content_hash().unwrap(),
                exclusions,
            },
        })
        .unwrap()
        .document
}

fn cached_vegetation_pointers(
    cache: &AreaPreviewCache,
) -> Vec<*const forge_world::VegetationChunk> {
    cache
        .cached
        .as_ref()
        .unwrap()
        .vegetation
        .chunks()
        .iter()
        .map(std::sync::Arc::as_ptr)
        .collect()
}

fn module_id() -> ModuleId {
    ModuleId::deterministic("area-preview/module")
}

fn request(area: &WorkspaceArea) -> AreaPreviewRequest {
    let base_revision = [
        area.terrain.revision.get(),
        area.vegetation.revision.get(),
        area.vegetation_density.revision.get(),
        area.paths.revision.get(),
        area.roads.revision.get(),
        area.rivers.revision.get(),
        area.walls.revision.get(),
    ]
    .into_iter()
    .max()
    .unwrap();
    AreaPreviewRequest {
        key: AreaKey {
            project_id: area.terrain.project_id,
            module_id: module_id(),
            area_id: area.id,
        },
        base_revision,
    }
}

fn paint_one_grass_chunk(area: &mut WorkspaceArea) {
    area.vegetation_density.revision = Revision::new(2);
    let grass = area
        .vegetation_density
        .layers
        .iter_mut()
        .find(|layer| layer.layer_id.as_str() == "forge_grass")
        .unwrap();
    grass.overrides = (16_u16..24)
        .flat_map(|cell_z| {
            (16_u16..24).map(move |cell_x| VegetationDensityOverride::new(cell_x, cell_z, u8::MAX))
        })
        .collect();
    area.vegetation_density.validate().unwrap();
}

fn grass_chunk_cells(chunk_x: u16, chunk_z: u16) -> Vec<(u16, u16)> {
    let first_x = chunk_x * 8;
    let first_z = chunk_z * 8;
    (first_z..first_z + 8)
        .flat_map(|cell_z| (first_x..first_x + 8).map(move |cell_x| (cell_x, cell_z)))
        .collect()
}

fn assert_baseline_handle_reuse(
    cache: &AreaPreviewCache,
    baseline: &[std::sync::Arc<forge_world::VegetationChunk>],
    replaced_indices: &[usize],
) {
    let current = cache.cached.as_ref().unwrap().vegetation.chunks();
    assert_eq!(current.len(), baseline.len());
    for (index, (before, after)) in baseline.iter().zip(current).enumerate() {
        assert_eq!(
            std::sync::Arc::ptr_eq(before, after),
            !replaced_indices.contains(&index),
            "unexpected CPU cache handle identity at chunk index {index}"
        );
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "one parity helper keeps every effective-terrain dependent source binding together"
)]
fn assert_full_vegetation_parity(area: &WorkspaceArea, preview: &AreaPreview) {
    let effective_terrain = forge_world::RiverGenerationPlan::from_documents(
        &area.terrain,
        &area.paths,
        &area.roads,
        &area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap()
    .effective_terrain;
    let effective_terrain_hash = effective_terrain.content_hash().unwrap();
    assert_eq!(preview.effective_terrain, effective_terrain);
    let full = VegetationGenerationPlan::from_documents_with_density(
        &effective_terrain,
        &area.vegetation,
        &area.vegetation_density,
    )
    .unwrap()
    .generate()
    .unwrap();
    assert_eq!(
        preview.vegetation.terrain_document_hash,
        full.terrain_document_hash
    );
    assert_eq!(
        preview.vegetation.vegetation_document_hash,
        full.vegetation_document_hash
    );
    assert!(
        preview
            .vegetation
            .origin_xz_m
            .into_iter()
            .zip(full.origin_xz_m)
            .all(|(actual, expected)| actual.to_bits() == expected.to_bits())
    );
    assert_eq!(
        preview.vegetation.cell_size_m.to_bits(),
        full.cell_size_m.to_bits()
    );
    assert_eq!(preview.vegetation.cells_per_axis, full.cells_per_axis);
    assert_eq!(preview.vegetation.kind_counts, full.kind_counts);
    assert_eq!(preview.vegetation.chunks.len(), full.chunks.len());
    for (actual_chunk, expected_chunk) in preview.vegetation.chunks.iter().zip(&full.chunks) {
        assert_eq!(actual_chunk.id, expected_chunk.id);
        assert_eq!(actual_chunk.coordinate, expected_chunk.coordinate);
        assert_eq!(actual_chunk.kind_counts, expected_chunk.kind_counts);
        assert_eq!(actual_chunk.instances.len(), expected_chunk.instances.len());
        for (actual, expected) in actual_chunk.instances.iter().zip(&expected_chunk.instances) {
            let tolerance = expected.slope_gradient_squared.abs().max(1.0) * f64::EPSILON * 2.0;
            assert!(
                (actual.slope_gradient_squared - expected.slope_gradient_squared).abs()
                    <= tolerance
            );
            let mut normalized = actual.clone();
            normalized.slope_gradient_squared = expected.slope_gradient_squared;
            assert_eq!(&normalized, expected);
        }
    }
    assert_eq!(
        preview.vegetation_density_source_hash,
        area.vegetation_density.content_hash().unwrap()
    );
    assert_eq!(preview.paths.terrain_document_hash, effective_terrain_hash);
    assert_eq!(
        preview.paths.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(preview.roads.terrain_document_hash, effective_terrain_hash);
    assert_eq!(
        preview.roads.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(
        preview.roads.road_document_hash,
        area.roads.content_hash().unwrap()
    );
    assert_eq!(
        preview.rivers.terrain_document_hash,
        area.terrain.content_hash().unwrap()
    );
    assert_eq!(
        preview.rivers.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(
        preview.rivers.road_document_hash,
        area.roads.content_hash().unwrap()
    );
    assert_eq!(
        preview.rivers.river_document_hash,
        area.rivers.content_hash().unwrap()
    );
    assert_eq!(
        preview.rivers.effective_terrain_document_hash,
        effective_terrain_hash
    );
    assert_eq!(preview.walls.terrain_document_hash, effective_terrain_hash);
    assert_eq!(
        preview.walls.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(
        preview.walls.wall_document_hash,
        area.walls.content_hash().unwrap()
    );
}

fn assert_cached_vegetation_is_bit_exact(area: &WorkspaceArea, cache: &AreaPreviewCache) {
    let effective_terrain = forge_world::RiverGenerationPlan::from_documents(
        &area.terrain,
        &area.paths,
        &area.roads,
        &area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap()
    .effective_terrain;
    let full = VegetationGenerationPlan::from_documents_with_density(
        &effective_terrain,
        &area.vegetation,
        &area.vegetation_density,
    )
    .unwrap()
    .generate()
    .unwrap();
    assert_eq!(cache.cached.as_ref().unwrap().vegetation.snapshot(), *full);
}

fn apply_partial_terrain_payload(cache: &mut GeneratedTerrain, payload: &GeneratedTerrain) {
    for chunk in &payload.chunks {
        let index = usize::from(chunk.coordinate.z) * 8 + usize::from(chunk.coordinate.x);
        cache.chunks[index] = chunk.clone();
    }
    cache.source_document_hash = payload.source_document_hash;
}

#[test]
fn initial_zero_density_preview_is_complete_reconstructible_and_bounded() {
    let area = area();
    assert!(
        area.vegetation_density
            .layers
            .iter()
            .all(|layer| layer.default_density == 0 && layer.overrides.is_empty())
    );
    let before = area.clone();
    let json = prepare_area_preview(request(&area), &area).unwrap();
    let preview = read_area_preview(&json).unwrap();

    assert_eq!(preview.terrain.chunks.len(), 64);
    assert_eq!(preview.vegetation.chunks.len(), 64);
    assert!(
        preview
            .vegetation
            .chunks
            .iter()
            .all(|chunk| chunk.instances.is_empty())
    );
    assert!(preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 64);
    assert_eq!(preview.vegetation_retained_chunks, 0);
    assert_eq!(preview.base_source_hash, None);
    assert_full_vegetation_parity(&area, &preview);
    assert!(json.len() < MAX_PREVIEW_JSON_BYTES);
    assert_eq!(area, before);
}

#[test]
fn catalogue_rename_and_zero_density_addition_retain_the_complete_preview_cache() {
    let base = workspace();
    let source = paint_grass_cells(
        &base,
        "area-preview/catalogue-source",
        &[(16, 16), (17, 16), (16, 17)],
    );
    let source_area = &source.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(source_area), source_area, None)
            .unwrap(),
    )
    .unwrap();
    let terrain_hash = initial.terrain.source_document_hash;
    let initial_pointers = cached_vegetation_pointers(&cache);

    let mut renamed_layers = source_area.vegetation.layers.clone();
    renamed_layers[3].display_name = "Prairies renommées".into();
    let renamed = set_catalog(&source, "area-preview/catalogue-renamed", renamed_layers);
    let renamed_area = &renamed.project.modules[0].areas[0];
    let renamed_preview = read_area_preview(
        &cache
            .prepare(request(renamed_area), renamed_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert_ne!(
        initial.vegetation.vegetation_document_hash,
        renamed_preview.vegetation.vegetation_document_hash
    );
    assert!(renamed_preview.vegetation_regenerated);
    assert_eq!(renamed_preview.vegetation_changed_chunks, 0);
    assert_eq!(renamed_preview.vegetation_retained_chunks, 64);
    assert_eq!(cached_vegetation_pointers(&cache), initial_pointers);
    assert_full_vegetation_parity(renamed_area, &renamed_preview);

    let mut expanded_layers = renamed_area.vegetation.layers.clone();
    expanded_layers.push(VegetationSpeciesPreset::Shrub.build_layer(
        "forge_second_shrub",
        "Second arbuste Forge",
        TerrainMaterialLayerId::new("meadow_grass"),
    ));
    let expanded = set_catalog(&renamed, "area-preview/catalogue-expanded", expanded_layers);
    let expanded_area = &expanded.project.modules[0].areas[0];
    let added_density = expanded_area
        .vegetation_density
        .layers
        .iter()
        .find(|layer| layer.layer_id.as_str() == "forge_second_shrub")
        .unwrap();
    assert_eq!(added_density.default_density, 0);
    assert!(added_density.overrides.is_empty());
    let expanded_preview = read_area_preview(
        &cache
            .prepare(request(expanded_area), expanded_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert!(expanded_preview.vegetation_regenerated);
    assert_eq!(expanded_preview.vegetation_changed_chunks, 0);
    assert_eq!(expanded_preview.vegetation_retained_chunks, 64);
    assert_eq!(cached_vegetation_pointers(&cache), initial_pointers);
    assert!(expanded_preview.vegetation.chunks.iter().all(|chunk| {
        chunk
            .instances
            .iter()
            .all(|instance| instance.layer_id.as_str() != "forge_second_shrub")
    }));
    assert_full_vegetation_parity(expanded_area, &expanded_preview);
    assert_cached_vegetation_is_bit_exact(expanded_area, &cache);

    let restored = WorkspaceDocument::from_json(&expanded.to_pretty_json().unwrap()).unwrap();
    let restored_area = &restored.project.modules[0].areas[0];
    let replay = read_area_preview(
        &cache
            .prepare(request(restored_area), restored_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();
    assert!(!replay.vegetation_regenerated);
    assert_eq!(replay.vegetation_changed_chunks, 0);
    assert_eq!(replay.vegetation_retained_chunks, 64);
    assert_full_vegetation_parity(restored_area, &replay);
}

#[test]
fn local_circle_exclusion_updates_only_the_touched_preview_chunks() {
    let base = workspace();
    let cells = (24_u16..32)
        .flat_map(|z| (24_u16..32).map(move |x| (x, z)))
        .collect::<Vec<_>>();
    let source = paint_grass_cells(&base, "area-preview/exclusion-source", &cells);
    let source_area = &source.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(source_area), source_area, None)
            .unwrap(),
    )
    .unwrap();
    let terrain_hash = initial.terrain.source_document_hash;
    let before_pointers = cached_vegetation_pointers(&cache);
    let anchor = initial.vegetation.chunks[3 * 8 + 3]
        .instances
        .first()
        .unwrap();
    let target = set_exclusions(
        &source,
        "area-preview/exclusion-target",
        vec![VegetationExclusion::Circle {
            id: VegetationExclusionId::new("local_circle"),
            center_x_m: anchor.position_m.x,
            center_z_m: anchor.position_m.z,
            radius_m: 0.25,
        }],
    );
    let target_area = &target.project.modules[0].areas[0];
    let preview = read_area_preview(
        &cache
            .prepare(request(target_area), target_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();
    let changed = initial
        .vegetation
        .chunks
        .iter()
        .zip(&preview.vegetation.chunks)
        .filter_map(|(before, after)| (before != after).then_some(after.coordinate))
        .collect::<Vec<_>>();

    assert_eq!(
        changed,
        vec![forge_world::TerrainChunkCoordinate { x: 3, z: 3 }]
    );
    assert!(preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, changed.len());
    assert_eq!(preview.vegetation_retained_chunks, 64 - changed.len());
    let after_pointers = cached_vegetation_pointers(&cache);
    for (index, (before, after)) in before_pointers.iter().zip(after_pointers).enumerate() {
        assert_eq!(
            before == &after,
            index != 3 * 8 + 3,
            "only the touched preview chunk may receive a new cache handle"
        );
    }
    assert_full_vegetation_parity(target_area, &preview);
    assert_cached_vegetation_is_bit_exact(target_area, &cache);
}

#[test]
fn local_density_paint_changes_only_its_vegetation_chunk_and_keeps_terrain_partial() {
    let mut area = area();
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(&cache.prepare(request(&area), &area, None).unwrap()).unwrap();
    let terrain_hash = initial.terrain.source_document_hash;
    paint_one_grass_chunk(&mut area);

    let json = cache
        .prepare(request(&area), &area, Some(terrain_hash))
        .unwrap();
    let preview = read_area_preview(&json).unwrap();
    assert_eq!(preview.base_source_hash, Some(terrain_hash));
    assert!(preview.terrain.chunks.is_empty());
    assert!(preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 1);
    assert_eq!(preview.vegetation_retained_chunks, 63);
    assert!(preview.vegetation.kind_counts.grass_patches > 0);
    assert_full_vegetation_parity(&area, &preview);
    assert_cached_vegetation_is_bit_exact(&area, &cache);
    assert!(json.len() < MAX_PREVIEW_JSON_BYTES);
}

#[test]
fn disposable_density_candidates_from_one_base_return_to_the_canonical_cache() {
    let canonical = workspace();
    let western_candidate = paint_grass_cells(
        &canonical,
        "area-preview/candidate-a",
        &grass_chunk_cells(0, 0),
    );
    let eastern_candidate = paint_grass_cells(
        &canonical,
        "area-preview/candidate-b",
        &grass_chunk_cells(1, 0),
    );
    let canonical_area = &canonical.project.modules[0].areas[0];
    let western_area = &western_candidate.project.modules[0].areas[0];
    let eastern_area = &eastern_candidate.project.modules[0].areas[0];
    let canonical_request = request(canonical_area);
    let mut cache = AreaPreviewCache::default();

    let canonical_preview = read_area_preview(
        &cache
            .prepare(canonical_request, canonical_area, None)
            .unwrap(),
    )
    .unwrap();
    assert_full_vegetation_parity(canonical_area, &canonical_preview);
    let terrain_hash = canonical_preview.terrain.source_document_hash;
    let canonical_handles = cache.cached.as_ref().unwrap().vegetation.chunks().to_vec();

    let preview_a = read_area_preview(
        &cache
            .prepare_vegetation_density_candidate(
                canonical_request,
                western_area,
                Some(terrain_hash),
            )
            .unwrap(),
    )
    .unwrap();
    assert_full_vegetation_parity(western_area, &preview_a);
    assert_ne!(
        preview_a.vegetation_density_source_hash,
        canonical_preview.vegetation_density_source_hash
    );
    assert_eq!(preview_a.vegetation_changed_chunks, 1);
    assert_eq!(preview_a.vegetation_retained_chunks, 63);
    assert_baseline_handle_reuse(&cache, &canonical_handles, &[0]);

    let preview_b = read_area_preview(
        &cache
            .prepare_vegetation_density_candidate(
                canonical_request,
                eastern_area,
                Some(terrain_hash),
            )
            .unwrap(),
    )
    .unwrap();
    assert_full_vegetation_parity(eastern_area, &preview_b);
    assert_ne!(
        preview_b.vegetation_density_source_hash,
        preview_a.vegetation_density_source_hash
    );
    assert_eq!(preview_b.vegetation_changed_chunks, 2);
    assert_eq!(preview_b.vegetation_retained_chunks, 62);
    assert_baseline_handle_reuse(&cache, &canonical_handles, &[0, 1]);

    let candidate_b_handles = cache.cached.as_ref().unwrap().vegetation.chunks().to_vec();
    let no_op = read_area_preview(
        &cache
            .prepare_vegetation_density_candidate(
                canonical_request,
                eastern_area,
                Some(terrain_hash),
            )
            .unwrap(),
    )
    .unwrap();
    assert_full_vegetation_parity(eastern_area, &no_op);
    assert_eq!(no_op.vegetation, preview_b.vegetation);
    assert_eq!(
        no_op.vegetation_density_source_hash,
        preview_b.vegetation_density_source_hash
    );
    assert!(!no_op.vegetation_regenerated);
    assert_eq!(no_op.vegetation_changed_chunks, 0);
    assert_eq!(no_op.vegetation_retained_chunks, 64);
    assert_baseline_handle_reuse(&cache, &candidate_b_handles, &[]);

    let restored = read_area_preview(
        &cache
            .prepare(canonical_request, canonical_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();
    assert_full_vegetation_parity(canonical_area, &restored);
    assert_eq!(restored.vegetation, canonical_preview.vegetation);
    assert_eq!(
        restored.vegetation_density_source_hash,
        canonical_preview.vegetation_density_source_hash
    );
    assert_eq!(restored.vegetation_changed_chunks, 1);
    assert_eq!(restored.vegetation_retained_chunks, 63);
    assert_baseline_handle_reuse(&cache, &canonical_handles, &[0, 1]);
    assert_cached_vegetation_is_bit_exact(canonical_area, &cache);
}

#[test]
fn terrain_change_invalidates_vegetation_key_and_both_projections_match_full_generation() {
    let mut area = area();
    paint_one_grass_chunk(&mut area);
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(&cache.prepare(request(&area), &area, None).unwrap()).unwrap();
    let mut assembled_terrain = initial.terrain;
    let base_terrain_hash = assembled_terrain.source_document_hash;

    area.terrain.revision = Revision::new(3);
    area.terrain.height_deltas = (68_u16..=92)
        .flat_map(|sample_z| {
            (68_u16..=92).map(move |sample_x| TerrainHeightDelta::new(sample_x, sample_z, 5.0))
        })
        .collect();
    area.terrain.validate().unwrap();
    let preview = read_area_preview(
        &cache
            .prepare(request(&area), &area, Some(base_terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert_eq!(preview.base_source_hash, Some(base_terrain_hash));
    assert!(!preview.terrain.chunks.is_empty());
    assert!(preview.terrain.chunks.len() <= 3);
    apply_partial_terrain_payload(&mut assembled_terrain, &preview.terrain);
    let full_terrain = TerrainGenerationPlan::from_document(&area.terrain)
        .unwrap()
        .generate()
        .unwrap();
    assert_eq!(assembled_terrain, full_terrain);
    assert_eq!(cache.cached.as_ref().unwrap().terrain, full_terrain);
    assert!(preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 1);
    assert_eq!(preview.vegetation_retained_chunks, 63);
    assert_eq!(
        preview.vegetation.terrain_document_hash,
        area.terrain.content_hash().unwrap()
    );
    assert_full_vegetation_parity(&area, &preview);
    assert_cached_vegetation_is_bit_exact(&area, &cache);
}

#[test]
fn unchanged_sources_keep_every_cached_chunk_but_still_send_complete_vegetation() {
    let mut area = area();
    paint_one_grass_chunk(&mut area);
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(&cache.prepare(request(&area), &area, None).unwrap()).unwrap();
    let pointers = cache
        .cached
        .as_ref()
        .unwrap()
        .vegetation
        .chunks()
        .iter()
        .map(std::sync::Arc::as_ptr)
        .collect::<Vec<_>>();
    let terrain_hash = initial.terrain.source_document_hash;
    let json = cache
        .prepare(request(&area), &area, Some(terrain_hash))
        .unwrap();
    let preview = read_area_preview(&json).unwrap();

    assert_eq!(preview.base_source_hash, Some(terrain_hash));
    assert!(preview.terrain.chunks.is_empty());
    assert!(!preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 0);
    assert_eq!(preview.vegetation_retained_chunks, 64);
    assert_eq!(
        preview.vegetation.terrain_document_hash,
        initial.vegetation.terrain_document_hash
    );
    assert_eq!(
        preview.vegetation.vegetation_document_hash,
        initial.vegetation.vegetation_document_hash
    );
    assert_eq!(
        preview.vegetation.kind_counts,
        initial.vegetation.kind_counts
    );
    assert_eq!(preview.vegetation.chunks.len(), 64);
    assert_eq!(
        pointers,
        cache
            .cached
            .as_ref()
            .unwrap()
            .vegetation
            .chunks()
            .iter()
            .map(std::sync::Arc::as_ptr)
            .collect::<Vec<_>>()
    );
    assert_full_vegetation_parity(&area, &preview);
    assert_cached_vegetation_is_bit_exact(&area, &cache);
    assert!(json.len() < MAX_PREVIEW_JSON_BYTES);
}

#[test]
fn path_only_edit_reuses_all_terrain_and_vegetation_chunks_and_replaces_paths() {
    let source = workspace();
    let source_area = &source.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(source_area), source_area, None)
            .unwrap(),
    )
    .unwrap();
    let terrain_hash = initial.terrain.source_document_hash;
    let vegetation_pointers = cached_vegetation_pointers(&cache);
    assert!(initial.paths.paths.is_empty());

    let changed = source
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/path-only"),
            base_revision: source.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: source.project.modules[0].id,
                area_id: source_area.id,
                expected_path_document_hash: source_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: PathId::deterministic("area-preview/path"),
                    name: "Chemin de cache".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(-16.0, 0.0, -8.0)),
                        PathControlPoint::new(Vec3::new(16.0, 0.0, 8.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let changed_area = &changed.project.modules[0].areas[0];
    let preview = read_area_preview(
        &cache
            .prepare(request(changed_area), changed_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert_eq!(preview.base_source_hash, Some(terrain_hash));
    assert!(preview.terrain.chunks.is_empty());
    assert!(!preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 0);
    assert_eq!(preview.vegetation_retained_chunks, 64);
    assert_eq!(preview.paths.paths.len(), 1);
    assert_eq!(preview.paths.paths[0].name, "Chemin de cache");
    assert_eq!(
        preview.paths.path_document_hash,
        changed_area.paths.content_hash().unwrap()
    );
    assert_eq!(cached_vegetation_pointers(&cache), vegetation_pointers);
}

#[test]
fn road_only_edit_reuses_terrain_vegetation_and_path_projection() {
    let source = workspace();
    let source_area = &source.project.modules[0].areas[0];
    let path_id = PathId::deterministic("area-preview/road-path");
    let with_path = source
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/road-path-source"),
            base_revision: source.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: source.project.modules[0].id,
                area_id: source_area.id,
                expected_path_document_hash: source_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: path_id,
                    name: "Route du cache".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(-24.0, 0.0, -8.0)),
                        PathControlPoint::new(Vec3::new(0.0, 0.0, 8.0)),
                        PathControlPoint::new(Vec3::new(24.0, 0.0, -8.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let with_path_area = &with_path.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(with_path_area), with_path_area, None)
            .unwrap(),
    )
    .unwrap();
    let terrain_hash = initial.terrain.source_document_hash;
    let vegetation_pointers = cached_vegetation_pointers(&cache);
    assert!(initial.roads.roads.is_empty());

    let changed = with_path
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/road-only"),
            base_revision: with_path.revision,
            change: WorkspaceChange::UpsertRoad {
                module_id: with_path.project.modules[0].id,
                area_id: with_path_area.id,
                expected_path_document_hash: with_path_area.paths.content_hash().unwrap(),
                expected_road_document_hash: with_path_area.roads.content_hash().unwrap(),
                road: AuthoredRoad {
                    path_id,
                    profile: RoadProfile {
                        algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                        width_m: 5.0,
                        crown_height_m: 0.1,
                    },
                    surface: RoadSurface::CompactedEarthV1,
                },
            },
        })
        .unwrap()
        .document;
    let changed_area = &changed.project.modules[0].areas[0];
    let preview = read_area_preview(
        &cache
            .prepare(request(changed_area), changed_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert_eq!(preview.base_source_hash, Some(terrain_hash));
    assert!(preview.terrain.chunks.is_empty());
    assert!(!preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 0);
    assert_eq!(preview.vegetation_retained_chunks, 64);
    assert_eq!(preview.paths, initial.paths);
    assert_eq!(preview.roads.roads.len(), 1);
    assert_eq!(preview.roads.roads[0].path_id, path_id);
    assert_eq!(
        preview.roads.road_document_hash,
        changed_area.roads.content_hash().unwrap()
    );
    assert_eq!(cached_vegetation_pointers(&cache), vegetation_pointers);
}

#[test]
fn wall_only_edit_reuses_every_non_wall_projection_and_zero_terrain_chunks() {
    let source = workspace();
    let source_area = &source.project.modules[0].areas[0];
    let path_id = PathId::deterministic("area-preview/wall-path");
    let with_path = source
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/wall-path-source"),
            base_revision: source.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: source.project.modules[0].id,
                area_id: source_area.id,
                expected_path_document_hash: source_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: path_id,
                    name: "Mur du cache".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(-24.0, 0.0, -8.0)),
                        PathControlPoint::new(Vec3::new(0.0, 0.0, 8.0)),
                        PathControlPoint::new(Vec3::new(24.0, 0.0, -8.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let with_path_area = &with_path.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(with_path_area), with_path_area, None)
            .unwrap(),
    )
    .unwrap();
    let terrain_hash = initial.terrain.source_document_hash;
    let vegetation_pointers = cached_vegetation_pointers(&cache);
    assert!(initial.walls.walls.is_empty());

    let changed = with_path
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/wall-only"),
            base_revision: with_path.revision,
            change: WorkspaceChange::UpsertWall {
                module_id: with_path.project.modules[0].id,
                area_id: with_path_area.id,
                expected_terrain_document_hash: with_path_area.terrain.content_hash().unwrap(),
                expected_path_document_hash: with_path_area.paths.content_hash().unwrap(),
                expected_road_document_hash: with_path_area.roads.content_hash().unwrap(),
                expected_river_document_hash: with_path_area.rivers.content_hash().unwrap(),
                expected_wall_document_hash: with_path_area.walls.content_hash().unwrap(),
                wall: AuthoredWall {
                    path_id,
                    profile: WallProfile {
                        algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                        height_m: 1.5,
                        thickness_m: 0.3,
                    },
                    surface: WallSurface::ForgeStoneV1,
                },
            },
        })
        .unwrap()
        .document;
    let changed_area = &changed.project.modules[0].areas[0];
    let preview = read_area_preview(
        &cache
            .prepare(request(changed_area), changed_area, Some(terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert_eq!(preview.base_source_hash, Some(terrain_hash));
    assert!(preview.terrain.chunks.is_empty());
    assert_eq!(preview.effective_terrain, initial.effective_terrain);
    assert!(!preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 0);
    assert_eq!(preview.vegetation_retained_chunks, 64);
    assert_eq!(preview.vegetation, initial.vegetation);
    assert_eq!(preview.paths, initial.paths);
    assert_eq!(preview.roads, initial.roads);
    assert_eq!(preview.rivers, initial.rivers);
    assert_ne!(
        preview.walls.wall_document_hash,
        initial.walls.wall_document_hash
    );
    assert_eq!(preview.walls.walls.len(), 1);
    assert_eq!(preview.walls.walls[0].path_id, path_id);
    assert!(!preview.walls.walls[0].positions_m.is_empty());
    assert_eq!(preview.walls.terrain_document_hash, terrain_hash);
    assert_eq!(
        preview.walls.wall_document_hash,
        changed_area.walls.content_hash().unwrap()
    );
    assert_eq!(cached_vegetation_pointers(&cache), vegetation_pointers);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one cache scenario proves ford creation, retouch and every effective-terrain correlation"
)]
fn road_only_ford_creation_and_retouch_reuse_effective_terrain_vegetation_paths_and_water() {
    let source = workspace();
    let source_area = &source.project.modules[0].areas[0];
    let river_path_id = PathId::deterministic("area-preview/ford-river");
    let road_path_id = PathId::deterministic("area-preview/ford-road");
    let with_river_path = source
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/ford-river-path"),
            base_revision: source.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: source.project.modules[0].id,
                area_id: source_area.id,
                expected_path_document_hash: source_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: river_path_id,
                    name: "Rivière du gué".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(0.0, 0.0, -80.0)),
                        PathControlPoint::new(Vec3::new(0.0, 0.0, 80.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let river_path_area = &with_river_path.project.modules[0].areas[0];
    let with_paths = with_river_path
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/ford-road-path"),
            base_revision: with_river_path.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: with_river_path.project.modules[0].id,
                area_id: river_path_area.id,
                expected_path_document_hash: river_path_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: road_path_id,
                    name: "Route du gué".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(-80.0, 0.0, 0.0)),
                        PathControlPoint::new(Vec3::new(80.0, 0.0, 0.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let paths_area = &with_paths.project.modules[0].areas[0];
    let with_river = with_paths
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/ford-river-profile"),
            base_revision: with_paths.revision,
            change: WorkspaceChange::UpsertRiver {
                module_id: with_paths.project.modules[0].id,
                area_id: paths_area.id,
                expected_terrain_document_hash: paths_area.terrain.content_hash().unwrap(),
                expected_path_document_hash: paths_area.paths.content_hash().unwrap(),
                expected_road_document_hash: paths_area.roads.content_hash().unwrap(),
                expected_river_document_hash: paths_area.rivers.content_hash().unwrap(),
                river: AuthoredRiver {
                    path_id: river_path_id,
                    channel: RiverChannelProfile {
                        algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                        surface_width_m: 8.0,
                        bed_depth_m: 1.5,
                        bank_blend_width_m: 4.0,
                    },
                    surface: RiverSurface::ForgeWaterV1,
                },
            },
        })
        .unwrap()
        .document;
    let river_area = &with_river.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(river_area), river_area, None)
            .unwrap(),
    )
    .unwrap();
    assert_eq!(initial.rivers.rivers.len(), 1);
    assert!(initial.rivers.road_river_intersections.is_empty());
    let effective_terrain_hash = initial.terrain.source_document_hash;
    let vegetation_pointers = cached_vegetation_pointers(&cache);
    let water_mesh = initial.rivers.rivers[0].clone();

    let with_road = with_river
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/ford-road-profile"),
            base_revision: with_river.revision,
            change: WorkspaceChange::UpsertRoad {
                module_id: with_river.project.modules[0].id,
                area_id: river_area.id,
                expected_path_document_hash: river_area.paths.content_hash().unwrap(),
                expected_road_document_hash: river_area.roads.content_hash().unwrap(),
                road: AuthoredRoad {
                    path_id: road_path_id,
                    profile: RoadProfile {
                        algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                        width_m: 4.0,
                        crown_height_m: 0.05,
                    },
                    surface: RoadSurface::CompactedEarthV1,
                },
            },
        })
        .unwrap()
        .document;
    let road_area = &with_road.project.modules[0].areas[0];
    let ford_created = read_area_preview(
        &cache
            .prepare(request(road_area), road_area, Some(effective_terrain_hash))
            .unwrap(),
    )
    .unwrap();

    assert_eq!(ford_created.base_source_hash, Some(effective_terrain_hash));
    assert!(ford_created.terrain.chunks.is_empty());
    assert_eq!(ford_created.effective_terrain, initial.effective_terrain);
    assert_eq!(
        ford_created.effective_terrain.content_hash().unwrap(),
        effective_terrain_hash
    );
    assert!(!ford_created.vegetation_regenerated);
    assert_eq!(ford_created.vegetation_changed_chunks, 0);
    assert_eq!(ford_created.vegetation_retained_chunks, 64);
    assert_eq!(ford_created.paths, initial.paths);
    assert_eq!(ford_created.rivers.rivers, vec![water_mesh.clone()]);
    let [created_road] = ford_created.roads.roads.as_slice() else {
        panic!("adding the road profile must rebuild exactly one road");
    };
    assert_eq!(created_road.path_id, road_path_id);
    let road_mesh = created_road.clone();
    let [first_ford] = ford_created.rivers.road_river_intersections.as_slice() else {
        panic!("adding the orthogonal road must derive exactly one ford");
    };
    assert_eq!(first_ford.river_path_id, river_path_id);
    assert_eq!(first_ford.road_path_id, road_path_id);
    assert_eq!(
        first_ford.kind,
        forge_world::RoadRiverIntersectionKind::TerrainFordV1
    );
    assert_eq!(cached_vegetation_pointers(&cache), vegetation_pointers);
    assert_eq!(
        ford_created.rivers.effective_terrain_document_hash,
        effective_terrain_hash
    );
    assert_eq!(
        ford_created.rivers.path_document_hash,
        ford_created.paths.path_document_hash
    );
    assert_eq!(
        ford_created.rivers.road_document_hash,
        ford_created.roads.road_document_hash
    );
    assert_eq!(
        ford_created.rivers.river_document_hash,
        road_area.rivers.content_hash().unwrap()
    );

    let retouched = with_road
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/ford-road-retouch"),
            base_revision: with_road.revision,
            change: WorkspaceChange::UpsertRoad {
                module_id: with_road.project.modules[0].id,
                area_id: road_area.id,
                expected_path_document_hash: road_area.paths.content_hash().unwrap(),
                expected_road_document_hash: road_area.roads.content_hash().unwrap(),
                road: AuthoredRoad {
                    path_id: road_path_id,
                    profile: RoadProfile {
                        algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                        width_m: 6.0,
                        crown_height_m: 0.15,
                    },
                    surface: RoadSurface::CompactedEarthV1,
                },
            },
        })
        .unwrap()
        .document;
    let retouched_area = &retouched.project.modules[0].areas[0];
    let ford_retouched = read_area_preview(
        &cache
            .prepare(
                request(retouched_area),
                retouched_area,
                Some(effective_terrain_hash),
            )
            .unwrap(),
    )
    .unwrap();

    assert_eq!(
        ford_retouched.base_source_hash,
        Some(effective_terrain_hash)
    );
    assert!(ford_retouched.terrain.chunks.is_empty());
    assert_eq!(ford_retouched.effective_terrain, initial.effective_terrain);
    assert_eq!(
        ford_retouched.effective_terrain.content_hash().unwrap(),
        effective_terrain_hash
    );
    assert!(!ford_retouched.vegetation_regenerated);
    assert_eq!(ford_retouched.vegetation_changed_chunks, 0);
    assert_eq!(ford_retouched.vegetation_retained_chunks, 64);
    assert_eq!(ford_retouched.paths, initial.paths);
    assert_eq!(ford_retouched.rivers.rivers, vec![water_mesh]);
    let [retouched_road] = ford_retouched.roads.roads.as_slice() else {
        panic!("retouching the road profile must rebuild exactly one road");
    };
    assert_eq!(retouched_road.path_id, road_path_id);
    assert_ne!(retouched_road, &road_mesh);
    assert_eq!(cached_vegetation_pointers(&cache), vegetation_pointers);
    let [updated_ford] = ford_retouched.rivers.road_river_intersections.as_slice() else {
        panic!("retouching the crossing road must preserve exactly one ford");
    };
    assert_eq!(updated_ford.river_path_id, first_ford.river_path_id);
    assert_eq!(updated_ford.road_path_id, first_ford.road_path_id);
    assert_eq!(updated_ford.kind, first_ford.kind);
    assert_eq!(
        updated_ford.position_xz_m.map(f32::to_bits),
        first_ford.position_xz_m.map(f32::to_bits)
    );
    assert_eq!(
        updated_ford.crossing_angle_degrees.to_bits(),
        first_ford.crossing_angle_degrees.to_bits()
    );
    assert_eq!(
        updated_ford.water_surface_height_m.to_bits(),
        first_ford.water_surface_height_m.to_bits()
    );
    assert_ne!(
        updated_ford.road_surface_height_m.to_bits(),
        first_ford.road_surface_height_m.to_bits()
    );
    assert_eq!(
        ford_retouched.rivers.effective_terrain_document_hash,
        effective_terrain_hash
    );
    assert_eq!(
        ford_retouched.rivers.path_document_hash,
        ford_retouched.paths.path_document_hash
    );
    assert_eq!(
        ford_retouched.rivers.road_document_hash,
        ford_retouched.roads.road_document_hash
    );
    assert_eq!(
        ford_retouched.rivers.river_document_hash,
        retouched_area.rivers.content_hash().unwrap()
    );
    assert_full_vegetation_parity(retouched_area, &ford_retouched);
    assert_cached_vegetation_is_bit_exact(retouched_area, &cache);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one cache transition proves all effective-terrain dependents remain hash-correlated"
)]
fn river_only_edit_partially_refreshes_the_effective_terrain_and_dependents() {
    let source = workspace();
    let source_area = &source.project.modules[0].areas[0];
    let path_id = PathId::deterministic("area-preview/river-path");
    let with_path = source
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/river-path-source"),
            base_revision: source.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: source.project.modules[0].id,
                area_id: source_area.id,
                expected_path_document_hash: source_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: path_id,
                    name: "Rivière du cache".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(-24.0, 0.0, -8.0)),
                        PathControlPoint::new(Vec3::new(0.0, 0.0, 8.0)),
                        PathControlPoint::new(Vec3::new(24.0, 0.0, -8.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let with_path_area = &with_path.project.modules[0].areas[0];
    let canonical_terrain_hash = with_path_area.terrain.content_hash().unwrap();
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(with_path_area), with_path_area, None)
            .unwrap(),
    )
    .unwrap();
    let base_effective_terrain_hash = initial.terrain.source_document_hash;
    assert_eq!(base_effective_terrain_hash, canonical_terrain_hash);
    assert!(initial.rivers.rivers.is_empty());

    let changed = with_path
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/river-only"),
            base_revision: with_path.revision,
            change: WorkspaceChange::UpsertRiver {
                module_id: with_path.project.modules[0].id,
                area_id: with_path_area.id,
                expected_terrain_document_hash: canonical_terrain_hash,
                expected_path_document_hash: with_path_area.paths.content_hash().unwrap(),
                expected_road_document_hash: with_path_area.roads.content_hash().unwrap(),
                expected_river_document_hash: with_path_area.rivers.content_hash().unwrap(),
                river: AuthoredRiver {
                    path_id,
                    channel: RiverChannelProfile {
                        algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                        surface_width_m: 8.0,
                        bed_depth_m: 1.5,
                        bank_blend_width_m: 4.0,
                    },
                    surface: RiverSurface::ForgeWaterV1,
                },
            },
        })
        .unwrap()
        .document;
    let changed_area = &changed.project.modules[0].areas[0];
    let preview = read_area_preview(
        &cache
            .prepare(
                request(changed_area),
                changed_area,
                Some(base_effective_terrain_hash),
            )
            .unwrap(),
    )
    .unwrap();

    assert_eq!(preview.base_source_hash, Some(base_effective_terrain_hash));
    assert!(!preview.terrain.chunks.is_empty());
    assert!(preview.terrain.chunks.len() < 64);
    assert!(preview.vegetation_regenerated);
    assert_eq!(
        preview.vegetation_changed_chunks + preview.vegetation_retained_chunks,
        64
    );
    assert_eq!(preview.rivers.rivers.len(), 1);
    assert_eq!(preview.rivers.rivers[0].path_id, path_id);
    assert_eq!(preview.rivers.terrain_document_hash, canonical_terrain_hash);
    assert_eq!(
        preview.rivers.river_document_hash,
        changed_area.rivers.content_hash().unwrap()
    );
    assert_ne!(
        preview.rivers.effective_terrain_document_hash,
        canonical_terrain_hash
    );
    assert_eq!(
        preview.terrain.source_document_hash,
        preview.rivers.effective_terrain_document_hash
    );
    assert_eq!(
        preview.effective_terrain.content_hash().unwrap(),
        preview.rivers.effective_terrain_document_hash
    );
    assert_eq!(
        preview.paths.terrain_document_hash,
        preview.rivers.effective_terrain_document_hash
    );
    assert_eq!(
        preview.roads.terrain_document_hash,
        preview.rivers.effective_terrain_document_hash
    );
    assert_full_vegetation_parity(changed_area, &preview);
    assert_cached_vegetation_is_bit_exact(changed_area, &cache);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one cache transition proves the river terrain change rebuilds wall bases atomically"
)]
fn river_edit_rebuilds_wall_bases_against_the_new_effective_terrain() {
    let source = workspace();
    let source_area = &source.project.modules[0].areas[0];
    let wall_path_id = PathId::deterministic("area-preview/wall-over-river");
    let river_path_id = PathId::deterministic("area-preview/river-under-wall");
    let with_wall_path = source
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/wall-crossing-path"),
            base_revision: source.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: source.project.modules[0].id,
                area_id: source_area.id,
                expected_path_document_hash: source_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: wall_path_id,
                    name: "Mur traversant".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(-40.0, 0.0, 0.0)),
                        PathControlPoint::new(Vec3::new(40.0, 0.0, 0.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let wall_path_area = &with_wall_path.project.modules[0].areas[0];
    let with_paths = with_wall_path
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/river-crossing-path"),
            base_revision: with_wall_path.revision,
            change: WorkspaceChange::UpsertPath {
                module_id: with_wall_path.project.modules[0].id,
                area_id: wall_path_area.id,
                expected_path_document_hash: wall_path_area.paths.content_hash().unwrap(),
                path: AuthoredPath {
                    id: river_path_id,
                    name: "Rivière sous le mur".into(),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(0.0, 0.0, -40.0)),
                        PathControlPoint::new(Vec3::new(0.0, 0.0, 40.0)),
                    ],
                },
            },
        })
        .unwrap()
        .document;
    let paths_area = &with_paths.project.modules[0].areas[0];
    let with_wall = with_paths
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/wall-before-river"),
            base_revision: with_paths.revision,
            change: WorkspaceChange::UpsertWall {
                module_id: with_paths.project.modules[0].id,
                area_id: paths_area.id,
                expected_terrain_document_hash: paths_area.terrain.content_hash().unwrap(),
                expected_path_document_hash: paths_area.paths.content_hash().unwrap(),
                expected_road_document_hash: paths_area.roads.content_hash().unwrap(),
                expected_river_document_hash: paths_area.rivers.content_hash().unwrap(),
                expected_wall_document_hash: paths_area.walls.content_hash().unwrap(),
                wall: AuthoredWall {
                    path_id: wall_path_id,
                    profile: WallProfile {
                        algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                        height_m: 1.5,
                        thickness_m: 0.3,
                    },
                    surface: WallSurface::ForgeStoneV1,
                },
            },
        })
        .unwrap()
        .document;
    let with_wall_area = &with_wall.project.modules[0].areas[0];
    let mut cache = AreaPreviewCache::default();
    let initial = read_area_preview(
        &cache
            .prepare(request(with_wall_area), with_wall_area, None)
            .unwrap(),
    )
    .unwrap();
    let initial_effective_hash = initial.terrain.source_document_hash;
    let initial_wall_hash = initial.walls.wall_document_hash;
    let initial_wall = initial.walls.walls[0].clone();

    let changed = with_wall
        .preview_edit(WorkspaceEdit {
            command_id: CommandId::deterministic("area-preview/river-changes-wall-base"),
            base_revision: with_wall.revision,
            change: WorkspaceChange::UpsertRiver {
                module_id: with_wall.project.modules[0].id,
                area_id: with_wall_area.id,
                expected_terrain_document_hash: with_wall_area.terrain.content_hash().unwrap(),
                expected_path_document_hash: with_wall_area.paths.content_hash().unwrap(),
                expected_road_document_hash: with_wall_area.roads.content_hash().unwrap(),
                expected_river_document_hash: with_wall_area.rivers.content_hash().unwrap(),
                river: AuthoredRiver {
                    path_id: river_path_id,
                    channel: RiverChannelProfile {
                        algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                        surface_width_m: 8.0,
                        bed_depth_m: 1.5,
                        bank_blend_width_m: 4.0,
                    },
                    surface: RiverSurface::ForgeWaterV1,
                },
            },
        })
        .unwrap()
        .document;
    let changed_area = &changed.project.modules[0].areas[0];
    let preview = read_area_preview(
        &cache
            .prepare(
                request(changed_area),
                changed_area,
                Some(initial_effective_hash),
            )
            .unwrap(),
    )
    .unwrap();

    assert_eq!(preview.base_source_hash, Some(initial_effective_hash));
    assert!(!preview.terrain.chunks.is_empty());
    assert!(preview.terrain.chunks.len() < 64);
    assert_ne!(
        preview.rivers.effective_terrain_document_hash,
        initial_effective_hash
    );
    assert_eq!(
        preview.walls.terrain_document_hash,
        preview.rivers.effective_terrain_document_hash
    );
    assert_eq!(preview.walls.wall_document_hash, initial_wall_hash);
    assert_eq!(
        preview.walls.path_document_hash,
        initial.walls.path_document_hash
    );
    assert_eq!(preview.walls.walls.len(), 1);
    assert_ne!(preview.walls.walls[0].positions_m, initial_wall.positions_m);
    assert_eq!(
        preview.walls.wall_document_hash,
        changed_area.walls.content_hash().unwrap()
    );
    assert_full_vegetation_parity(changed_area, &preview);
    assert_cached_vegetation_is_bit_exact(changed_area, &cache);
}

#[test]
fn wrong_terrain_base_and_invalid_requests_never_publish_or_commit_a_stale_cache() {
    let area = area();
    let mut cache = AreaPreviewCache::default();
    cache.prepare(request(&area), &area, None).unwrap();
    let key_before = cache.cached.as_ref().unwrap().vegetation.key();
    let wrong_hash = ContentHash::from_bytes([0xA5; 32]);
    let preview = read_area_preview(
        &cache
            .prepare(request(&area), &area, Some(wrong_hash))
            .unwrap(),
    )
    .unwrap();
    assert_eq!(preview.base_source_hash, None);
    assert_eq!(preview.terrain.chunks.len(), 64);
    assert!(!preview.vegetation_regenerated);
    assert_eq!(preview.vegetation_changed_chunks, 0);
    assert_eq!(preview.vegetation_retained_chunks, 64);
    assert_full_vegetation_parity(&area, &preview);
    assert_cached_vegetation_is_bit_exact(&area, &cache);

    let mut future = area.clone();
    future.vegetation_density.revision = Revision::new(2);
    let mut stale_request = request(&future);
    stale_request.base_revision = 1;
    assert!(
        cache
            .prepare(
                stale_request,
                &future,
                Some(area.terrain.content_hash().unwrap())
            )
            .is_err()
    );
    assert_eq!(cache.cached.as_ref().unwrap().vegetation.key(), key_before);

    let oversized = " ".repeat(MAX_PREVIEW_JSON_BYTES + 1);
    assert_eq!(
        read_area_preview(&oversized).unwrap_err(),
        "Aperçu trop volumineux."
    );
}

#[test]
fn serialization_budget_rejects_an_oversized_projection_before_cache_commit() {
    let mut area = area();
    area.vegetation.revision = Revision::new(2);
    for layer in &mut area.vegetation.layers {
        layer.acceptance_probability = 1.0;
        layer.attempts_per_cell = if layer.kind == VegetationKind::Grass {
            4
        } else {
            8
        };
        layer.minimum_spacing_m = if layer.kind == VegetationKind::Grass {
            0.0
        } else {
            0.01
        };
        layer.maximum_slope_gradient = 16.0;
        layer.minimum_altitude_m = -10_000.0;
        layer.maximum_altitude_m = 10_000.0;
        layer.material_filter = None;
    }
    area.vegetation.validate().unwrap();
    area.vegetation_density.revision = Revision::new(2);
    for layer in &mut area.vegetation_density.layers {
        layer.default_density = u8::MAX;
    }
    area.vegetation_density.validate().unwrap();

    let mut cache = AreaPreviewCache::default();
    assert_eq!(
        cache.prepare(request(&area), &area, None).unwrap_err(),
        "L’aperçu dépasse le budget de transfert de 24 Mio."
    );
    assert!(cache.cached.is_none());
}
