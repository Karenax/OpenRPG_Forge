//! Bounded presentation adapter for deterministic authored-road meshes.

use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::*, render::render_resource::PrimitiveTopology,
};
use forge_project::{AuthoredRoad, MAX_ROADS_PER_DOCUMENT, RoadSurface};
use forge_world::{
    GeneratedPaths, GeneratedRoad, GeneratedRoads, GeneratedTerrain, MAX_GENERATED_ROAD_INDICES,
    MAX_GENERATED_ROAD_VERTICES, ROAD_CROSS_SECTION_VERTEX_COUNT, ROAD_GENERATION_VERSION,
    TERRAIN_HALF_EXTENT_M,
};

const ROAD_POSITION_Y_LIMIT_M: f32 = 10_001.0;
const ROAD_INDICES_PER_LONGITUDINAL_INTERVAL: usize = 12;
const EARTH_MATERIAL_WEIGHTS: [f32; 4] = [0.0, 1.0, 0.0, 1.0];
const MIN_TRIANGLE_NORMAL_SQUARED: f64 = 1.0e-18;
const NORMAL_COMPONENT_TOLERANCE: f64 = 1.0e-4;

pub(crate) fn validate_generated_roads(
    terrain: &GeneratedTerrain,
    paths: &GeneratedPaths,
    generated: &GeneratedRoads,
) -> Result<(), String> {
    if generated.generation_version != ROAD_GENERATION_VERSION
        || generated.terrain_document_hash != terrain.source_document_hash
        || generated.path_document_hash != paths.path_document_hash
        || generated.roads.len() > MAX_ROADS_PER_DOCUMENT
    {
        return Err("Sources, version ou nombre de routes incohérents.".into());
    }

    let (total_vertices, total_indices) = generated
        .roads
        .iter()
        .try_fold((0_usize, 0_usize), |(vertices, indices), road| {
            Some((
                vertices.checked_add(road.positions_m.len())?,
                indices.checked_add(road.indices.len())?,
            ))
        })
        .ok_or_else(|| "Budget de géométrie des routes dépassé.".to_owned())?;
    if total_vertices > MAX_GENERATED_ROAD_VERTICES || total_indices > MAX_GENERATED_ROAD_INDICES {
        return Err("Budget de géométrie des routes dépassé.".into());
    }

    let mut previous_id = None;
    for road in &generated.roads {
        if previous_id.is_some_and(|previous| previous >= road.path_id) {
            return Err("Routes générées non canoniques ou dupliquées.".into());
        }
        previous_id = Some(road.path_id);
        let path = paths
            .paths
            .binary_search_by_key(&road.path_id, |path| path.id)
            .ok()
            .map(|index| &paths.paths[index])
            .ok_or_else(|| "Route générée sans chemin source.".to_owned())?;
        if path.closed {
            return Err("Une route générée doit référencer un chemin ouvert.".into());
        }
        AuthoredRoad {
            path_id: road.path_id,
            profile: road.profile,
            surface: road.surface,
        }
        .validate()
        .map_err(|error| error.to_string())?;
        validate_road_mesh(road)?;
    }
    Ok(())
}

fn validate_road_mesh(road: &GeneratedRoad) -> Result<(), String> {
    let vertex_count = road.positions_m.len();
    if vertex_count < ROAD_CROSS_SECTION_VERTEX_COUNT * 2
        || !vertex_count.is_multiple_of(ROAD_CROSS_SECTION_VERTEX_COUNT)
        || road.normals.len() != vertex_count
        || road.uvs.len() != vertex_count
    {
        return Err("Attributs de route incomplets ou incohérents.".into());
    }
    let row_count = vertex_count / ROAD_CROSS_SECTION_VERTEX_COUNT;
    let expected_indices = row_count
        .checked_sub(1)
        .and_then(|intervals| intervals.checked_mul(ROAD_INDICES_PER_LONGITUDINAL_INTERVAL))
        .ok_or_else(|| "Topologie de route invalide.".to_owned())?;
    if road.indices.len() != expected_indices || !road.indices.len().is_multiple_of(3) {
        return Err("Topologie de route invalide.".into());
    }

    if road.positions_m.iter().any(|position| {
        !position.iter().all(|value| value.is_finite())
            || !(-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position[0])
            || !(-ROAD_POSITION_Y_LIMIT_M..=ROAD_POSITION_Y_LIMIT_M).contains(&position[1])
            || !(-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position[2])
    }) {
        return Err("Sommet de route non fini ou hors zone.".into());
    }
    if road.normals.iter().any(|normal| {
        let squared_length = normal.iter().map(|value| value * value).sum::<f32>();
        !normal.iter().all(|value| value.is_finite())
            || normal[1] <= 0.0
            || (squared_length - 1.0).abs() > 0.01
    }) {
        return Err("Normale de route invalide.".into());
    }

    let mut previous_distance = None;
    for row in road.uvs.as_chunks::<ROAD_CROSS_SECTION_VERTEX_COUNT>().0 {
        let distance = row[0][1];
        if row[0][0].to_bits() != 0.0_f32.to_bits()
            || row[1][0].to_bits() != 0.5_f32.to_bits()
            || row[2][0].to_bits() != 1.0_f32.to_bits()
            || row.iter().any(|uv| {
                !uv.iter().all(|value| value.is_finite()) || uv[1].to_bits() != distance.to_bits()
            })
            || distance < 0.0
            || previous_distance.is_some_and(|previous| distance <= previous)
        {
            return Err("UV ou ordre longitudinal de route invalide.".into());
        }
        previous_distance = Some(distance);
    }
    if road.uvs[0][1].to_bits() != 0.0_f32.to_bits() {
        return Err("La distance longitudinale de route doit commencer à zéro.".into());
    }

    for (interval, actual) in road
        .indices
        .as_chunks::<ROAD_INDICES_PER_LONGITUDINAL_INTERVAL>()
        .0
        .iter()
        .enumerate()
    {
        let current = interval
            .checked_mul(ROAD_CROSS_SECTION_VERTEX_COUNT)
            .and_then(|value| u32::try_from(value).ok())
            .ok_or_else(|| "Index de route hors limites.".to_owned())?;
        let next = current
            .checked_add(u32::try_from(ROAD_CROSS_SECTION_VERTEX_COUNT).expect("constant fits u32"))
            .ok_or_else(|| "Index de route hors limites.".to_owned())?;
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
            return Err("Indices ou winding de route invalides.".into());
        }
    }
    validate_road_geometry(road)?;
    Ok(())
}

fn validate_road_geometry(road: &GeneratedRoad) -> Result<(), String> {
    let mut accumulated = vec![[0.0_f64; 3]; road.positions_m.len()];
    for triangle in road.indices.as_chunks::<3>().0 {
        let indices = triangle.map(|index| usize::try_from(index).ok());
        let [Some(first_index), Some(second_index), Some(third_index)] = indices else {
            return Err("Index de route hors limites.".into());
        };
        let points = [first_index, second_index, third_index]
            .map(|index| road.positions_m.get(index).copied());
        let [Some(first), Some(second), Some(third)] = points else {
            return Err("Index de route hors limites.".into());
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
            return Err("Triangle ou winding géométrique de route invalide.".into());
        }
        for index in [first_index, second_index, third_index] {
            for axis in 0..3 {
                accumulated[index][axis] += cross[axis];
            }
        }
    }

    for (actual, expected) in road.normals.iter().zip(accumulated) {
        let squared_length = expected.iter().map(|value| value * value).sum::<f64>();
        if !squared_length.is_finite() || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED {
            return Err("Normale géométrique de route invalide.".into());
        }
        let inverse_length = squared_length.sqrt().recip();
        if actual.iter().zip(expected).any(|(actual, expected)| {
            (f64::from(*actual) - expected * inverse_length).abs() > NORMAL_COMPONENT_TOLERANCE
        }) {
            return Err("Normale de route incohérente avec ses triangles.".into());
        }
    }
    Ok(())
}

pub(crate) fn build_editor_road_mesh(road: &GeneratedRoad) -> Mesh {
    let colors = vec![EARTH_MATERIAL_WEIGHTS; road.positions_m.len()];
    let surface_weights = match road.surface {
        RoadSurface::CompactedEarthV1 => colors,
    };
    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, road.positions_m.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, road.normals.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, road.uvs.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, surface_weights)
    .with_inserted_indices(Indices::U32(road.indices.clone()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;
    use forge_core::{Revision, Vec3};
    use forge_project::{
        AuthoredPath, PathControlPoint, PathDocument, PathId, PathInterpolation, RoadDocument,
        RoadProfile, RoadProfileAlgorithm,
    };
    use forge_world::{PathGenerationPlan, RoadGenerationPlan, TerrainGenerationPlan};

    fn generated_fixture(count: usize) -> (GeneratedTerrain, GeneratedPaths, GeneratedRoads) {
        let terrain_document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let mut paths = (0..count)
            .map(|index| {
                #[allow(clippy::cast_precision_loss)]
                let x_m = -120.0 + index as f32 * 3.5;
                AuthoredPath {
                    id: PathId::deterministic(format!("editor-road/path-{index:02}")),
                    name: format!("Chemin {index}"),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(x_m, 0.0, -30.0)),
                        PathControlPoint::new(Vec3::new(x_m, 0.0, 30.0)),
                    ],
                }
            })
            .collect::<Vec<_>>();
        paths.sort_by_key(|path| path.id);
        let path_document = PathDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Chemins",
            paths,
        );
        let road_document = RoadDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Routes",
            path_document
                .paths
                .iter()
                .map(|path| AuthoredRoad {
                    path_id: path.id,
                    profile: RoadProfile {
                        algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                        width_m: 4.0,
                        crown_height_m: 0.08,
                    },
                    surface: RoadSurface::CompactedEarthV1,
                })
                .collect(),
        );
        let terrain = TerrainGenerationPlan::from_document(&terrain_document)
            .unwrap()
            .generate()
            .unwrap();
        let paths = PathGenerationPlan::from_documents(&terrain_document, &path_document)
            .unwrap()
            .generate()
            .unwrap();
        let roads =
            RoadGenerationPlan::from_documents(&terrain_document, &path_document, &road_document)
                .unwrap()
                .generate()
                .unwrap();
        (terrain, paths, roads)
    }

    #[test]
    fn valid_roads_build_the_existing_terrain_material_vertex_contract() {
        let (terrain, paths, roads) = generated_fixture(2);
        validate_generated_roads(&terrain, &paths, &roads).unwrap();
        let road = &roads.roads[0];
        let mesh = build_editor_road_mesh(road);
        assert_eq!(mesh.count_vertices(), road.positions_m.len());
        assert_eq!(mesh.indices().unwrap().len(), road.indices.len());
        assert!(matches!(
            mesh.attribute(Mesh::ATTRIBUTE_COLOR),
            Some(VertexAttributeValues::Float32x4(colors))
                if colors.len() == road.positions_m.len()
                    && colors.iter().all(|color| *color == EARTH_MATERIAL_WEIGHTS)
        ));
    }

    #[test]
    fn validation_rejects_wrong_sources_order_profile_and_mesh_shape() {
        let (terrain, paths, roads) = generated_fixture(2);
        let mut invalid = roads.clone();
        invalid.generation_version += 1;
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());

        let mut invalid = roads.clone();
        invalid.terrain_document_hash = forge_core::ContentHash::from_bytes([0x11; 32]);
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
        let mut invalid = roads.clone();
        invalid.path_document_hash = forge_core::ContentHash::from_bytes([0x22; 32]);
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());

        let mut invalid = roads.clone();
        invalid.roads.reverse();
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
        let mut invalid = roads.clone();
        invalid.roads[0].profile.width_m = 0.5;
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());

        let mut invalid = roads.clone();
        invalid.roads[0].positions_m.pop();
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
        let mut invalid = roads.clone();
        invalid.roads[0].indices[0] = u32::MAX;
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
        let mut invalid = roads.clone();
        invalid.roads[0].normals[0] = [0.0; 3];
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
        let mut invalid = roads.clone();
        invalid.roads[0].uvs[0][0] = f32::NAN;
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
        let mut invalid = roads;
        invalid.roads[0].positions_m[0][0] = TERRAIN_HALF_EXTENT_M;
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
    }

    #[test]
    fn aggregate_vertex_and_index_budgets_fail_before_mesh_adaptation() {
        let (terrain, paths, roads) = generated_fixture(1);
        let mut invalid = roads.clone();
        invalid.roads[0]
            .positions_m
            .resize(MAX_GENERATED_ROAD_VERTICES + 1, [0.0; 3]);
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());

        let mut invalid = roads;
        invalid.roads[0]
            .indices
            .resize(MAX_GENERATED_ROAD_INDICES + 1, 0);
        assert!(validate_generated_roads(&terrain, &paths, &invalid).is_err());
    }

    #[test]
    fn validation_rejects_reversed_cross_sections_and_degenerate_triangles() {
        let (terrain, paths, roads) = generated_fixture(1);
        let mut reversed = roads.clone();
        for row in reversed.roads[0].positions_m.as_chunks_mut::<3>().0 {
            row.swap(0, 2);
        }
        assert!(validate_generated_roads(&terrain, &paths, &reversed).is_err());

        let mut degenerate = roads;
        degenerate.roads[0].positions_m[3] = degenerate.roads[0].positions_m[0];
        assert!(validate_generated_roads(&terrain, &paths, &degenerate).is_err());
    }
}
