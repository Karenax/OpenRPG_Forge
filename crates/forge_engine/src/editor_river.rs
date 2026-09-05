//! Bounded presentation adapter for deterministic authored-river meshes.

use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::*, render::render_resource::PrimitiveTopology,
};
use forge_project::{
    AuthoredRiver, MAX_RIVERS_PER_DOCUMENT, RiverDocument, RiverSurface, TerrainDocument,
};
use forge_world::{
    GeneratedPaths, GeneratedRiver, GeneratedRivers, GeneratedRoads, GeneratedTerrain,
    MAX_GENERATED_RIVER_INDICES, MAX_GENERATED_RIVER_VERTICES, RIVER_CROSS_SECTION_VERTEX_COUNT,
    RIVER_GENERATION_VERSION, TERRAIN_HALF_EXTENT_M, validate_generated_road_river_intersections,
};

const RIVER_POSITION_Y_LIMIT_M: f32 = 10_001.0;
const RIVER_INDICES_PER_LONGITUDINAL_INTERVAL: usize = 12;
const MIN_TRIANGLE_NORMAL_SQUARED: f64 = 1.0e-18;
const NORMAL_COMPONENT_TOLERANCE: f64 = 1.0e-4;
const CROSS_SECTION_TOLERANCE_M: f64 = 1.0e-3;

pub(crate) fn validate_generated_rivers(
    effective_terrain: &GeneratedTerrain,
    paths: &GeneratedPaths,
    roads: &GeneratedRoads,
    generated: &GeneratedRivers,
    source_terrain: &TerrainDocument,
    source_rivers: &RiverDocument,
) -> Result<(), String> {
    validate_generated_river_sources(
        effective_terrain,
        paths,
        roads,
        generated,
        source_terrain,
        source_rivers,
    )?;

    let (total_vertices, total_indices) = generated
        .rivers
        .iter()
        .try_fold((0_usize, 0_usize), |(vertices, indices), river| {
            Some((
                vertices.checked_add(river.positions_m.len())?,
                indices.checked_add(river.indices.len())?,
            ))
        })
        .ok_or_else(|| "Budget de géométrie des rivières dépassé.".to_owned())?;
    if total_vertices > MAX_GENERATED_RIVER_VERTICES || total_indices > MAX_GENERATED_RIVER_INDICES
    {
        return Err("Budget de géométrie des rivières dépassé.".into());
    }

    let mut previous_id = None;
    for river in &generated.rivers {
        if previous_id.is_some_and(|previous| previous >= river.path_id) {
            return Err("Rivières générées non canoniques ou dupliquées.".into());
        }
        previous_id = Some(river.path_id);
        let path = paths
            .paths
            .binary_search_by_key(&river.path_id, |path| path.id)
            .ok()
            .map(|index| &paths.paths[index])
            .ok_or_else(|| "Rivière générée sans chemin source.".to_owned())?;
        if path.closed {
            return Err("Une rivière générée doit référencer un chemin ouvert.".into());
        }
        if roads
            .roads
            .binary_search_by_key(&river.path_id, |road| road.path_id)
            .is_ok()
        {
            return Err("Un chemin ne peut pas porter une route et une rivière.".into());
        }
        AuthoredRiver {
            path_id: river.path_id,
            channel: river.channel,
            surface: river.surface,
        }
        .validate()
        .map_err(|error| error.to_string())?;
        validate_river_mesh(river)?;
    }
    validate_generated_road_river_intersections(paths, roads, generated)
        .map_err(|error| format!("Métadonnées de gué invalides : {error}"))
}

fn validate_generated_river_sources(
    effective_terrain: &GeneratedTerrain,
    paths: &GeneratedPaths,
    roads: &GeneratedRoads,
    generated: &GeneratedRivers,
    source_terrain: &TerrainDocument,
    source_rivers: &RiverDocument,
) -> Result<(), String> {
    source_terrain
        .validate()
        .map_err(|error| format!("Document canonique de terrain invalide : {error}"))?;
    source_rivers
        .validate()
        .map_err(|error| format!("Document canonique de rivières invalide : {error}"))?;
    let source_terrain_hash = source_terrain
        .content_hash()
        .map_err(|error| format!("Hash canonique du terrain indisponible : {error}"))?;
    let source_river_hash = source_rivers
        .content_hash()
        .map_err(|error| format!("Hash canonique des rivières indisponible : {error}"))?;
    if generated.generation_version != RIVER_GENERATION_VERSION
        || generated.terrain_document_hash != source_terrain_hash
        || generated.effective_terrain_document_hash != effective_terrain.source_document_hash
        || paths.terrain_document_hash != effective_terrain.source_document_hash
        || roads.terrain_document_hash != effective_terrain.source_document_hash
        || roads.path_document_hash != paths.path_document_hash
        || generated.path_document_hash != paths.path_document_hash
        || generated.road_document_hash != roads.road_document_hash
        || source_river_hash != generated.river_document_hash
        || source_terrain.project_id != source_rivers.project_id
        || source_terrain.area_id != source_rivers.area_id
        || generated.rivers.len() > MAX_RIVERS_PER_DOCUMENT
    {
        return Err("Sources, version ou nombre de rivières incohérents.".into());
    }

    let exact_rivers = source_rivers.rivers.len() == generated.rivers.len()
        && source_rivers
            .rivers
            .iter()
            .zip(&generated.rivers)
            .all(|(source, derived)| {
                source.path_id == derived.path_id
                    && source.channel == derived.channel
                    && source.surface == derived.surface
            });
    if !exact_rivers {
        return Err("Projection de rivières incomplète ou différente de sa source.".into());
    }
    Ok(())
}

fn validate_river_mesh(river: &GeneratedRiver) -> Result<(), String> {
    let vertex_count = river.positions_m.len();
    if vertex_count < RIVER_CROSS_SECTION_VERTEX_COUNT * 2
        || !vertex_count.is_multiple_of(RIVER_CROSS_SECTION_VERTEX_COUNT)
        || river.normals.len() != vertex_count
        || river.uvs.len() != vertex_count
    {
        return Err("Attributs de rivière incomplets ou incohérents.".into());
    }
    let row_count = vertex_count / RIVER_CROSS_SECTION_VERTEX_COUNT;
    let expected_indices = row_count
        .checked_sub(1)
        .and_then(|intervals| intervals.checked_mul(RIVER_INDICES_PER_LONGITUDINAL_INTERVAL))
        .ok_or_else(|| "Topologie de rivière invalide.".to_owned())?;
    if river.indices.len() != expected_indices || !river.indices.len().is_multiple_of(3) {
        return Err("Topologie de rivière invalide.".into());
    }

    if river.positions_m.iter().any(|position| {
        !position.iter().all(|value| value.is_finite())
            || !(-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position[0])
            || !(-RIVER_POSITION_Y_LIMIT_M..=RIVER_POSITION_Y_LIMIT_M).contains(&position[1])
            || !(-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position[2])
    }) {
        return Err("Sommet de rivière non fini ou hors zone.".into());
    }
    if river.normals.iter().any(|normal| {
        let squared_length = normal.iter().map(|value| value * value).sum::<f32>();
        !normal.iter().all(|value| value.is_finite())
            || normal[1] <= 0.0
            || (squared_length - 1.0).abs() > 0.01
    }) {
        return Err("Normale de rivière invalide.".into());
    }

    let mut previous_distance = None;
    let mut previous_surface = None;
    for (positions, uvs) in river
        .positions_m
        .as_chunks::<RIVER_CROSS_SECTION_VERTEX_COUNT>()
        .0
        .iter()
        .zip(river.uvs.as_chunks::<RIVER_CROSS_SECTION_VERTEX_COUNT>().0)
    {
        let distance = uvs[0][1];
        let surface = positions[1][1];
        if uvs[0][0].to_bits() != 0.0_f32.to_bits()
            || uvs[1][0].to_bits() != 0.5_f32.to_bits()
            || uvs[2][0].to_bits() != 1.0_f32.to_bits()
            || uvs.iter().any(|uv| {
                !uv.iter().all(|value| value.is_finite()) || uv[1].to_bits() != distance.to_bits()
            })
            || distance < 0.0
            || previous_distance.is_some_and(|previous| distance <= previous)
            || positions
                .iter()
                .any(|position| position[1].to_bits() != surface.to_bits())
            || previous_surface.is_some_and(|previous| surface > previous)
        {
            return Err("UV, surface ou ordre longitudinal de rivière invalide.".into());
        }
        validate_cross_section(river, positions)?;
        previous_distance = Some(distance);
        previous_surface = Some(surface);
    }
    if river.uvs[0][1].to_bits() != 0.0_f32.to_bits() {
        return Err("La distance longitudinale de rivière doit commencer à zéro.".into());
    }

    for (interval, actual) in river
        .indices
        .as_chunks::<RIVER_INDICES_PER_LONGITUDINAL_INTERVAL>()
        .0
        .iter()
        .enumerate()
    {
        let current = interval
            .checked_mul(RIVER_CROSS_SECTION_VERTEX_COUNT)
            .and_then(|value| u32::try_from(value).ok())
            .ok_or_else(|| "Index de rivière hors limites.".to_owned())?;
        let next = current
            .checked_add(
                u32::try_from(RIVER_CROSS_SECTION_VERTEX_COUNT).expect("constant fits u32"),
            )
            .ok_or_else(|| "Index de rivière hors limites.".to_owned())?;
        let expected = [
            current,
            next,
            current + 1,
            current + 1,
            next,
            next + 1,
            current + 1,
            next + 1,
            current + 2,
            current + 2,
            next + 1,
            next + 2,
        ];
        if *actual != expected {
            return Err("Indices ou winding de rivière invalides.".into());
        }
    }
    validate_river_geometry(river)
}

fn validate_cross_section(
    river: &GeneratedRiver,
    positions: &[[f32; 3]; RIVER_CROSS_SECTION_VERTEX_COUNT],
) -> Result<(), String> {
    let endpoint_distance = horizontal_distance(positions[0], positions[2]);
    let expected_width = f64::from(river.channel.surface_width_m);
    let midpoint = [
        f64::midpoint(f64::from(positions[0][0]), f64::from(positions[2][0])),
        f64::midpoint(f64::from(positions[0][2]), f64::from(positions[2][2])),
    ];
    if !endpoint_distance.is_finite()
        || (endpoint_distance - expected_width).abs() > CROSS_SECTION_TOLERANCE_M
        || (midpoint[0] - f64::from(positions[1][0])).abs() > CROSS_SECTION_TOLERANCE_M
        || (midpoint[1] - f64::from(positions[1][2])).abs() > CROSS_SECTION_TOLERANCE_M
    {
        return Err("Largeur ou centre de rivière incohérent avec son profil.".into());
    }
    Ok(())
}

fn horizontal_distance(first: [f32; 3], second: [f32; 3]) -> f64 {
    let dx = f64::from(second[0]) - f64::from(first[0]);
    let dz = f64::from(second[2]) - f64::from(first[2]);
    (dx * dx + dz * dz).sqrt()
}

fn validate_river_geometry(river: &GeneratedRiver) -> Result<(), String> {
    let mut accumulated = vec![[0.0_f64; 3]; river.positions_m.len()];
    for triangle in river.indices.as_chunks::<3>().0 {
        let indices = triangle.map(|index| usize::try_from(index).ok());
        let [Some(first_index), Some(second_index), Some(third_index)] = indices else {
            return Err("Index de rivière hors limites.".into());
        };
        let points = [first_index, second_index, third_index]
            .map(|index| river.positions_m.get(index).copied());
        let [Some(first), Some(second), Some(third)] = points else {
            return Err("Index de rivière hors limites.".into());
        };
        let first_edge = [
            f64::from(second[0]) - f64::from(first[0]),
            f64::from(second[1]) - f64::from(first[1]),
            f64::from(second[2]) - f64::from(first[2]),
        ];
        let second_edge = [
            f64::from(third[0]) - f64::from(first[0]),
            f64::from(third[1]) - f64::from(first[1]),
            f64::from(third[2]) - f64::from(first[2]),
        ];
        let cross = [
            first_edge[1] * second_edge[2] - first_edge[2] * second_edge[1],
            first_edge[2] * second_edge[0] - first_edge[0] * second_edge[2],
            first_edge[0] * second_edge[1] - first_edge[1] * second_edge[0],
        ];
        let squared_length = cross.iter().map(|value| value * value).sum::<f64>();
        if cross.iter().any(|value| !value.is_finite())
            || cross[1] <= 0.0
            || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
        {
            return Err("Triangle ou winding géométrique de rivière invalide.".into());
        }
        for index in [first_index, second_index, third_index] {
            for axis in 0..3 {
                accumulated[index][axis] += cross[axis];
            }
        }
    }

    for (actual, expected) in river.normals.iter().zip(accumulated) {
        let squared_length = expected.iter().map(|value| value * value).sum::<f64>();
        if !squared_length.is_finite() || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED {
            return Err("Normale géométrique de rivière invalide.".into());
        }
        let inverse_length = squared_length.sqrt().recip();
        if actual.iter().zip(expected).any(|(actual, expected)| {
            (f64::from(*actual) - expected * inverse_length).abs() > NORMAL_COMPONENT_TOLERANCE
        }) {
            return Err("Normale de rivière incohérente avec ses triangles.".into());
        }
    }
    Ok(())
}

pub(crate) fn build_editor_river_mesh(river: &GeneratedRiver) -> Mesh {
    match river.surface {
        RiverSurface::ForgeWaterV1 => Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, river.positions_m.clone())
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, river.normals.clone())
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, river.uvs.clone())
        .with_inserted_indices(Indices::U32(river.indices.clone())),
    }
}

pub(crate) fn procedural_editor_river_material() -> StandardMaterial {
    StandardMaterial {
        base_color: Color::srgba(0.055, 0.24, 0.32, 0.82),
        alpha_mode: AlphaMode::Blend,
        metallic: 0.0,
        perceptual_roughness: 0.18,
        reflectance: 0.5,
        double_sided: true,
        cull_mode: None,
        ..default()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;
    use forge_core::{ContentHash, Revision, Vec3};
    use forge_project::{
        AuthoredPath, AuthoredRoad, PathControlPoint, PathDocument, PathId, PathInterpolation,
        RiverChannelProfile, RiverChannelProfileAlgorithm, RiverDocument, RoadDocument,
        RoadProfile, RoadProfileAlgorithm, RoadSurface, TerrainDocument,
    };
    use forge_world::{
        MAX_TERRAIN_FORD_CROSSING_ANGLE_DEGREES, MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES,
        PathGenerationPlan, RiverGenerationPlan, RoadGenerationPlan,
    };

    fn generated_fixture() -> (
        GeneratedTerrain,
        GeneratedPaths,
        GeneratedRoads,
        GeneratedRivers,
        TerrainDocument,
        RiverDocument,
    ) {
        let terrain_document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let path_id = PathId::deterministic("editor-river-adapter/path");
        let road_path_id = PathId::deterministic("editor-river-adapter/road-path");
        let mut authored_paths = vec![
            AuthoredPath {
                id: path_id,
                name: "Cours d’eau".into(),
                closed: false,
                interpolation: PathInterpolation::CentripetalCatmullRomV1,
                control_points: vec![
                    PathControlPoint::new(Vec3::new(-24.0, 0.0, -12.0)),
                    PathControlPoint::new(Vec3::new(24.0, 0.0, 12.0)),
                ],
            },
            AuthoredPath {
                id: road_path_id,
                name: "Route du gué".into(),
                closed: false,
                interpolation: PathInterpolation::CentripetalCatmullRomV1,
                control_points: vec![
                    PathControlPoint::new(Vec3::new(-24.0, 0.0, 12.0)),
                    PathControlPoint::new(Vec3::new(24.0, 0.0, -12.0)),
                ],
            },
        ];
        authored_paths.sort_by_key(|path| path.id);
        let path_document = PathDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Chemins de rivière",
            authored_paths,
        );
        let road_document = RoadDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Routes",
            vec![AuthoredRoad {
                path_id: road_path_id,
                profile: RoadProfile {
                    algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                    width_m: 4.0,
                    crown_height_m: 0.1,
                },
                surface: RoadSurface::CompactedEarthV1,
            }],
        );
        let river_document = RiverDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Rivières",
            vec![AuthoredRiver {
                path_id,
                channel: RiverChannelProfile {
                    algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                    surface_width_m: 8.0,
                    bed_depth_m: 1.5,
                    bank_blend_width_m: 4.0,
                },
                surface: RiverSurface::ForgeWaterV1,
            }],
        );
        let result = RiverGenerationPlan::from_documents(
            &terrain_document,
            &path_document,
            &road_document,
            &river_document,
        )
        .unwrap()
        .generate()
        .unwrap();
        let paths = PathGenerationPlan::from_documents(&result.effective_terrain, &path_document)
            .unwrap()
            .generate()
            .unwrap();
        let roads = RoadGenerationPlan::from_documents(
            &result.effective_terrain,
            &path_document,
            &road_document,
        )
        .unwrap()
        .generate()
        .unwrap();
        let effective_terrain =
            forge_world::TerrainGenerationPlan::from_document(&result.effective_terrain)
                .unwrap()
                .generate()
                .unwrap();
        (
            effective_terrain,
            paths,
            roads,
            result.generated,
            terrain_document,
            river_document,
        )
    }

    #[test]
    fn valid_river_builds_a_bounded_external_asset_free_water_mesh() {
        let (effective, paths, roads, rivers, source_terrain, source_rivers) = generated_fixture();
        validate_generated_rivers(
            &effective,
            &paths,
            &roads,
            &rivers,
            &source_terrain,
            &source_rivers,
        )
        .unwrap();
        let generated = &rivers.rivers[0];
        let mesh = build_editor_river_mesh(generated);
        assert_eq!(mesh.count_vertices(), generated.positions_m.len());
        assert_eq!(mesh.indices().unwrap().len(), generated.indices.len());
        assert!(matches!(
            mesh.attribute(Mesh::ATTRIBUTE_POSITION),
            Some(VertexAttributeValues::Float32x3(values)) if values == &generated.positions_m
        ));

        let material = procedural_editor_river_material();
        assert_eq!(material.alpha_mode, AlphaMode::Blend);
        assert!(material.base_color_texture.is_none());
        assert!(material.normal_map_texture.is_none());
        assert!(material.metallic_roughness_texture.is_none());
        assert_eq!(rivers.road_river_intersections.len(), 1);
    }

    #[test]
    fn validation_rejects_forged_or_noncanonical_ford_metadata() {
        let (effective, paths, roads, rivers, source_terrain, source_rivers) = generated_fixture();
        let ford = rivers.road_river_intersections[0];
        let rejects = |invalid: &GeneratedRivers| {
            validate_generated_rivers(
                &effective,
                &paths,
                &roads,
                invalid,
                &source_terrain,
                &source_rivers,
            )
            .is_err()
        };

        let mut invalid = rivers.clone();
        invalid.road_river_intersections.clear();
        assert!(rejects(&invalid));

        let mut invalid = rivers.clone();
        invalid.road_river_intersections.push(ford);
        assert!(rejects(&invalid));

        let mut invalid = rivers.clone();
        invalid.road_river_intersections[0].road_path_id = ford.river_path_id;
        assert!(rejects(&invalid));

        let mut invalid = rivers.clone();
        invalid.road_river_intersections[0].road_path_id =
            PathId::deterministic("editor-river-adapter/missing-road");
        assert!(rejects(&invalid));

        for position_xz_m in [
            [f32::NAN, 0.0],
            [200.0, 200.0],
            [TERRAIN_HALF_EXTENT_M, 0.0],
        ] {
            let mut invalid = rivers.clone();
            invalid.road_river_intersections[0].position_xz_m = position_xz_m;
            assert!(rejects(&invalid));
        }
        for crossing_angle_degrees in [
            f32::NAN,
            MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES - 0.01,
            MAX_TERRAIN_FORD_CROSSING_ANGLE_DEGREES + 0.01,
            ford.crossing_angle_degrees + 1.0,
        ] {
            let mut invalid = rivers.clone();
            invalid.road_river_intersections[0].crossing_angle_degrees = crossing_angle_degrees;
            assert!(rejects(&invalid));
        }

        let mut invalid = rivers.clone();
        invalid.road_river_intersections[0].road_surface_height_m = f32::INFINITY;
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.road_river_intersections[0].water_surface_height_m =
            invalid.road_river_intersections[0].road_surface_height_m;
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.road_river_intersections[0].road_surface_height_m += 0.01;
        assert!(rejects(&invalid));
        let mut invalid = rivers;
        invalid.road_river_intersections[0].water_surface_height_m += 0.25;
        assert!(rejects(&invalid));
    }

    #[test]
    fn validation_rejects_wrong_sources_order_and_malformed_geometry() {
        let (effective, paths, roads, rivers, source_terrain, source_rivers) = generated_fixture();
        let rejects = |invalid: &GeneratedRivers| {
            validate_generated_rivers(
                &effective,
                &paths,
                &roads,
                invalid,
                &source_terrain,
                &source_rivers,
            )
            .is_err()
        };

        let mut invalid = rivers.clone();
        invalid.generation_version += 1;
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.effective_terrain_document_hash = ContentHash::from_bytes([0x22; 32]);
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.path_document_hash = ContentHash::from_bytes([0x33; 32]);
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.road_document_hash = ContentHash::from_bytes([0x44; 32]);
        assert!(rejects(&invalid));

        let mut invalid = rivers.clone();
        invalid.rivers.push(invalid.rivers[0].clone());
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.rivers[0].positions_m.pop();
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.rivers[0].indices[0] = u32::MAX;
        assert!(rejects(&invalid));
        let mut invalid = rivers.clone();
        invalid.rivers[0].uvs[0][0] = f32::NAN;
        assert!(rejects(&invalid));
        let mut invalid = rivers;
        let final_row = invalid.rivers[0].positions_m.len() - RIVER_CROSS_SECTION_VERTEX_COUNT;
        let raised = invalid.rivers[0].positions_m[0][1] + 1.0;
        for position in &mut invalid.rivers[0].positions_m[final_row..] {
            position[1] = raised;
        }
        assert!(rejects(&invalid));
    }

    #[test]
    fn aggregate_budgets_fail_before_mesh_adaptation() {
        let (effective, paths, roads, rivers, source_terrain, source_rivers) = generated_fixture();
        let rejects = |invalid: &GeneratedRivers| {
            validate_generated_rivers(
                &effective,
                &paths,
                &roads,
                invalid,
                &source_terrain,
                &source_rivers,
            )
            .is_err()
        };
        let mut invalid = rivers.clone();
        invalid.rivers[0]
            .positions_m
            .resize(MAX_GENERATED_RIVER_VERTICES + 1, [0.0; 3]);
        assert!(rejects(&invalid));

        let mut invalid = rivers;
        invalid.rivers[0]
            .indices
            .resize(MAX_GENERATED_RIVER_INDICES + 1, 0);
        assert!(rejects(&invalid));
    }
}
