use super::*;
use forge_core::{AreaId, CommandId, ModuleId, ProjectId, Revision};
use forge_project::{
    NewProjectCommand, TerrainHeightDelta, TerrainMaterialLayerId, VegetationDensityOverride,
    VegetationExclusion, VegetationExclusionId, VegetationKind, VegetationLayer,
    VegetationSpeciesPreset, WorkspaceArea, WorkspaceChange, WorkspaceDocument, WorkspaceEdit,
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

fn assert_full_vegetation_parity(area: &WorkspaceArea, preview: &AreaPreview) {
    let full = VegetationGenerationPlan::from_documents_with_density(
        &area.terrain,
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
}

fn assert_cached_vegetation_is_bit_exact(area: &WorkspaceArea, cache: &AreaPreviewCache) {
    let full = VegetationGenerationPlan::from_documents_with_density(
        &area.terrain,
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
