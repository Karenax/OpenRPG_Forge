//! Bounded presentation adapter for deterministic procedural wall meshes.

use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::*, render::render_resource::PrimitiveTopology,
};
use forge_project::{
    AuthoredWall, MAX_WALLS_PER_DOCUMENT, PathDocument, TerrainDocument, WallDocument, WallSurface,
};
use forge_world::{
    GeneratedPath, GeneratedPaths, GeneratedTerrain, GeneratedWall, GeneratedWalls,
    MAX_GENERATED_WALL_INDICES, MAX_GENERATED_WALL_VERTICES, TERRAIN_HALF_EXTENT_M,
    WALL_CAP_INDEX_COUNT, WALL_CAP_VERTEX_COUNT, WALL_GENERATION_VERSION,
    WALL_INDICES_PER_LONGITUDINAL_INTERVAL, WALL_VERTICES_PER_ROW,
    validate_generated_walls as validate_generated_walls_against_sources,
};

const WALL_POSITION_Y_LIMIT_M: f32 = 10_009.0;
const MIN_TRIANGLE_NORMAL_SQUARED: f64 = 1.0e-18;
const NORMAL_COMPONENT_TOLERANCE: f64 = 1.0e-4;
const PROFILE_GEOMETRY_TOLERANCE_M: f64 = 1.0e-4;

pub(crate) fn validate_generated_walls(
    effective_terrain: &GeneratedTerrain,
    paths: &GeneratedPaths,
    generated: &GeneratedWalls,
    source_terrain: &TerrainDocument,
    source_paths: &PathDocument,
    source_walls: &WallDocument,
) -> Result<(), String> {
    source_walls
        .validate()
        .map_err(|error| format!("Document canonique de murs invalide : {error}"))?;
    let source_wall_hash = source_walls
        .content_hash()
        .map_err(|error| format!("Hash canonique des murs indisponible : {error}"))?;
    let exact_walls = source_walls.walls.len() == generated.walls.len()
        && source_walls
            .walls
            .iter()
            .zip(&generated.walls)
            .all(|(source, derived)| {
                source.path_id == derived.path_id
                    && source.profile == derived.profile
                    && source.surface == derived.surface
            });
    if generated.generation_version != WALL_GENERATION_VERSION
        || generated.terrain_document_hash != effective_terrain.source_document_hash
        || generated.path_document_hash != paths.path_document_hash
        || generated.wall_document_hash != source_wall_hash
        || generated.walls.len() > MAX_WALLS_PER_DOCUMENT
        || !exact_walls
    {
        return Err("Sources, version ou nombre de murs incohérents.".into());
    }
    validate_generated_walls_against_sources(source_terrain, source_paths, source_walls, generated)
        .map_err(|error| {
            format!("Murs générés différents de leurs sources canoniques : {error}")
        })?;

    let (total_vertices, total_indices) = generated
        .walls
        .iter()
        .try_fold((0_usize, 0_usize), |(vertices, indices), wall| {
            Some((
                vertices.checked_add(wall.positions_m.len())?,
                indices.checked_add(wall.indices.len())?,
            ))
        })
        .ok_or_else(|| "Budget de géométrie des murs dépassé.".to_owned())?;
    if total_vertices > MAX_GENERATED_WALL_VERTICES || total_indices > MAX_GENERATED_WALL_INDICES {
        return Err("Budget de géométrie des murs dépassé.".into());
    }

    let mut previous_id = None;
    for wall in &generated.walls {
        if previous_id.is_some_and(|previous| previous >= wall.path_id) {
            return Err("Murs générés non canoniques ou dupliqués.".into());
        }
        previous_id = Some(wall.path_id);
        let path = paths
            .paths
            .binary_search_by_key(&wall.path_id, |path| path.id)
            .ok()
            .map(|index| &paths.paths[index])
            .ok_or_else(|| "Mur généré sans chemin source.".to_owned())?;
        if path.closed {
            return Err("Un mur généré doit référencer un chemin ouvert.".into());
        }
        AuthoredWall {
            path_id: wall.path_id,
            profile: wall.profile,
            surface: wall.surface,
        }
        .validate()
        .map_err(|error| error.to_string())?;
        validate_wall_mesh(wall, path)?;
    }
    Ok(())
}

fn validate_wall_mesh(wall: &GeneratedWall, path: &GeneratedPath) -> Result<(), String> {
    let vertex_count = wall.positions_m.len();
    let body_vertex_count = vertex_count
        .checked_sub(WALL_CAP_VERTEX_COUNT)
        .ok_or_else(|| "Attributs de mur incomplets ou incohérents.".to_owned())?;
    if body_vertex_count < WALL_VERTICES_PER_ROW * 2
        || !body_vertex_count.is_multiple_of(WALL_VERTICES_PER_ROW)
        || wall.normals.len() != vertex_count
    {
        return Err("Attributs de mur incomplets ou incohérents.".into());
    }
    let row_count = body_vertex_count / WALL_VERTICES_PER_ROW;
    if row_count != path.samples.len() {
        return Err("Nombre de sections de mur incohérent avec son chemin.".into());
    }
    let expected_indices = row_count
        .checked_sub(1)
        .and_then(|intervals| intervals.checked_mul(WALL_INDICES_PER_LONGITUDINAL_INTERVAL))
        .and_then(|body| body.checked_add(WALL_CAP_INDEX_COUNT))
        .ok_or_else(|| "Topologie de mur invalide.".to_owned())?;
    if wall.indices.len() != expected_indices || !wall.indices.len().is_multiple_of(3) {
        return Err("Topologie de mur invalide.".into());
    }

    if wall.positions_m.iter().any(|position| {
        !position.iter().all(|value| value.is_finite())
            || !(-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position[0])
            || !(-WALL_POSITION_Y_LIMIT_M..=WALL_POSITION_Y_LIMIT_M).contains(&position[1])
            || !(-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&position[2])
    }) {
        return Err("Sommet de mur non fini ou hors zone.".into());
    }
    if wall.normals.iter().any(|normal| {
        let squared_length = normal.iter().map(|value| value * value).sum::<f32>();
        !normal.iter().all(|value| value.is_finite()) || (squared_length - 1.0).abs() > 0.01
    }) {
        return Err("Normale de mur invalide.".into());
    }

    let rows = wall.positions_m[..body_vertex_count]
        .as_chunks::<WALL_VERTICES_PER_ROW>()
        .0;
    for (row, sample) in rows.iter().zip(&path.samples) {
        if !same_vec3_bits(row[0], row[7])
            || !same_vec3_bits(row[1], row[2])
            || !same_vec3_bits(row[3], row[4])
            || !same_vec3_bits(row[5], row[6])
            || row[0][0].to_bits() != row[1][0].to_bits()
            || row[0][2].to_bits() != row[1][2].to_bits()
            || row[5][0].to_bits() != row[4][0].to_bits()
            || row[5][2].to_bits() != row[4][2].to_bits()
        {
            return Err("Duplicata ou verticale de section de mur incohérent.".into());
        }
        let left_height = f64::from(row[1][1]) - f64::from(row[0][1]);
        let right_height = f64::from(row[4][1]) - f64::from(row[5][1]);
        let base_thickness = horizontal_distance(row[0], row[5]);
        let top_thickness = horizontal_distance(row[1], row[4]);
        let base_midpoint_x = f64::midpoint(f64::from(row[0][0]), f64::from(row[5][0]));
        let base_midpoint_z = f64::midpoint(f64::from(row[0][2]), f64::from(row[5][2]));
        if (left_height - f64::from(wall.profile.height_m)).abs() > PROFILE_GEOMETRY_TOLERANCE_M
            || (right_height - f64::from(wall.profile.height_m)).abs()
                > PROFILE_GEOMETRY_TOLERANCE_M
            || (base_thickness - f64::from(wall.profile.thickness_m)).abs()
                > PROFILE_GEOMETRY_TOLERANCE_M
            || (top_thickness - f64::from(wall.profile.thickness_m)).abs()
                > PROFILE_GEOMETRY_TOLERANCE_M
            || (base_midpoint_x - f64::from(sample.position_m.x)).abs()
                > PROFILE_GEOMETRY_TOLERANCE_M
            || (base_midpoint_z - f64::from(sample.position_m.z)).abs()
                > PROFILE_GEOMETRY_TOLERANCE_M
        {
            return Err("Section générée incohérente avec le profil de mur.".into());
        }
    }

    let caps = &wall.positions_m[body_vertex_count..];
    let first = rows[0];
    let last = rows[row_count - 1];
    let expected_caps = [
        first[0], first[5], first[4], first[1], last[0], last[5], last[4], last[1],
    ];
    if caps
        .iter()
        .zip(expected_caps)
        .any(|(actual, expected)| !same_vec3_bits(*actual, expected))
    {
        return Err("Embouts de mur incohérents avec ses sections.".into());
    }

    validate_wall_indices(row_count, body_vertex_count, &wall.indices)?;
    validate_wall_normals(wall)
}

fn validate_wall_indices(
    row_count: usize,
    cap_vertex_start: usize,
    actual: &[u32],
) -> Result<(), String> {
    let body_index_count = row_count
        .checked_sub(1)
        .and_then(|intervals| intervals.checked_mul(WALL_INDICES_PER_LONGITUDINAL_INTERVAL))
        .ok_or_else(|| "Topologie de mur invalide.".to_owned())?;
    for (interval, actual) in actual[..body_index_count]
        .as_chunks::<WALL_INDICES_PER_LONGITUDINAL_INTERVAL>()
        .0
        .iter()
        .enumerate()
    {
        let current = interval
            .checked_mul(WALL_VERTICES_PER_ROW)
            .and_then(|value| u32::try_from(value).ok())
            .ok_or_else(|| "Index de mur hors limites.".to_owned())?;
        let next = current
            .checked_add(u32::try_from(WALL_VERTICES_PER_ROW).expect("constant fits u32"))
            .ok_or_else(|| "Index de mur hors limites.".to_owned())?;
        let expected = [
            current,
            next,
            current + 1,
            current + 1,
            next,
            next + 1,
            current + 2,
            next + 2,
            current + 3,
            current + 3,
            next + 2,
            next + 3,
            current + 4,
            next + 4,
            current + 5,
            current + 5,
            next + 4,
            next + 5,
            current + 6,
            next + 6,
            current + 7,
            current + 7,
            next + 6,
            next + 7,
        ];
        if *actual != expected {
            return Err("Indices ou winding longitudinal de mur invalides.".into());
        }
    }

    let start = u32::try_from(cap_vertex_start)
        .map_err(|_| "Index d’embout de mur hors limites.".to_owned())?;
    let expected_caps = [
        start,
        start + 3,
        start + 2,
        start,
        start + 2,
        start + 1,
        start + 4,
        start + 5,
        start + 6,
        start + 4,
        start + 6,
        start + 7,
    ];
    if actual[body_index_count..] != expected_caps {
        return Err("Indices ou winding des embouts de mur invalides.".into());
    }
    Ok(())
}

fn validate_wall_normals(wall: &GeneratedWall) -> Result<(), String> {
    let mut accumulated = vec![[0.0_f64; 3]; wall.positions_m.len()];
    for triangle in wall.indices.as_chunks::<3>().0 {
        let indices = triangle.map(|index| usize::try_from(index).ok());
        let [Some(first_index), Some(second_index), Some(third_index)] = indices else {
            return Err("Index de mur hors limites.".into());
        };
        let points = [first_index, second_index, third_index]
            .map(|index| wall.positions_m.get(index).copied());
        let [Some(first), Some(second), Some(third)] = points else {
            return Err("Index de mur hors limites.".into());
        };
        let first_edge = subtract(second, first);
        let second_edge = subtract(third, first);
        let cross = [
            first_edge[1] * second_edge[2] - first_edge[2] * second_edge[1],
            first_edge[2] * second_edge[0] - first_edge[0] * second_edge[2],
            first_edge[0] * second_edge[1] - first_edge[1] * second_edge[0],
        ];
        let squared_length = cross.iter().map(|value| value * value).sum::<f64>();
        if cross.iter().any(|value| !value.is_finite())
            || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
        {
            return Err("Triangle ou winding géométrique de mur invalide.".into());
        }
        for index in [first_index, second_index, third_index] {
            for axis in 0..3 {
                accumulated[index][axis] += cross[axis];
            }
        }
    }

    for (actual, expected) in wall.normals.iter().zip(accumulated) {
        let squared_length = expected.iter().map(|value| value * value).sum::<f64>();
        if !squared_length.is_finite() || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED {
            return Err("Normale géométrique de mur invalide.".into());
        }
        let inverse_length = squared_length.sqrt().recip();
        if actual.iter().zip(expected).any(|(actual, expected)| {
            (f64::from(*actual) - expected * inverse_length).abs() > NORMAL_COMPONENT_TOLERANCE
        }) {
            return Err("Normale de mur incohérente avec ses triangles.".into());
        }
    }
    Ok(())
}

fn subtract(second: [f32; 3], first: [f32; 3]) -> [f64; 3] {
    [
        f64::from(second[0]) - f64::from(first[0]),
        f64::from(second[1]) - f64::from(first[1]),
        f64::from(second[2]) - f64::from(first[2]),
    ]
}

fn horizontal_distance(first: [f32; 3], second: [f32; 3]) -> f64 {
    let dx = f64::from(second[0]) - f64::from(first[0]);
    let dz = f64::from(second[2]) - f64::from(first[2]);
    dx.hypot(dz)
}

fn same_vec3_bits(first: [f32; 3], second: [f32; 3]) -> bool {
    first
        .iter()
        .zip(second)
        .all(|(first, second)| first.to_bits() == second.to_bits())
}

pub(crate) fn build_editor_wall_mesh(wall: &GeneratedWall) -> Mesh {
    match wall.surface {
        WallSurface::ForgeStoneV1 => Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, wall.positions_m.clone())
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, wall.normals.clone())
        .with_inserted_indices(Indices::U32(wall.indices.clone())),
    }
}

pub(crate) fn procedural_editor_wall_material() -> StandardMaterial {
    StandardMaterial {
        base_color: Color::srgb(0.34, 0.32, 0.29),
        metallic: 0.0,
        perceptual_roughness: 0.94,
        reflectance: 0.22,
        ..default()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{Revision, Vec3};
    use forge_project::{
        AuthoredPath, PathControlPoint, PathDocument, PathId, PathInterpolation, WallProfile,
        WallProfileAlgorithm,
    };
    use forge_world::{PathGenerationPlan, TerrainGenerationPlan, WallGenerationPlan};

    fn generated_fixture(
        count: usize,
    ) -> (
        forge_world::GeneratedTerrain,
        GeneratedPaths,
        GeneratedWalls,
        TerrainDocument,
        PathDocument,
        WallDocument,
    ) {
        let terrain_document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let mut paths = (0..count)
            .map(|index| {
                #[allow(clippy::cast_precision_loss)]
                let x_m = -96.0 + index as f32 * 12.0;
                AuthoredPath {
                    id: PathId::deterministic(format!("editor-wall/path-{index:02}")),
                    name: format!("Mur {index}"),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(x_m, 0.0, -18.0)),
                        PathControlPoint::new(Vec3::new(x_m + 4.0, 0.0, 18.0)),
                    ],
                }
            })
            .collect::<Vec<_>>();
        paths.sort_by_key(|path| path.id);
        let path_document = PathDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Chemins de murs",
            paths,
        );
        let wall_document = WallDocument::new(
            terrain_document.project_id,
            terrain_document.area_id,
            Revision::INITIAL,
            "Murs",
            path_document
                .paths
                .iter()
                .map(|path| AuthoredWall {
                    path_id: path.id,
                    profile: WallProfile {
                        algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                        height_m: 1.5,
                        thickness_m: 0.3,
                    },
                    surface: WallSurface::ForgeStoneV1,
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
        let walls =
            WallGenerationPlan::from_documents(&terrain_document, &path_document, &wall_document)
                .unwrap()
                .generate()
                .unwrap();
        (
            terrain,
            paths,
            walls,
            terrain_document,
            path_document,
            wall_document,
        )
    }

    fn rotate_wall_sections(wall: &mut GeneratedWall, path: &GeneratedPath) {
        let body_vertex_count = wall.positions_m.len() - WALL_CAP_VERTEX_COUNT;
        let (body, caps) = wall.positions_m.split_at_mut(body_vertex_count);
        let rows = body.as_chunks_mut::<WALL_VERTICES_PER_ROW>().0;
        let (sin, cos) = 0.35_f32.sin_cos();
        for (row, sample) in rows.iter_mut().zip(&path.samples) {
            for position in row {
                let delta_x = position[0] - sample.position_m.x;
                let delta_z = position[2] - sample.position_m.z;
                position[0] = sample.position_m.x + cos * delta_x - sin * delta_z;
                position[2] = sample.position_m.z + sin * delta_x + cos * delta_z;
            }
        }
        let first = rows[0];
        let last = rows[rows.len() - 1];
        caps.copy_from_slice(&[
            first[0], first[5], first[4], first[1], last[0], last[5], last[4], last[1],
        ]);
        rebuild_test_normals(wall);
    }

    #[allow(
        clippy::cast_possible_truncation,
        reason = "test-only normal reconstruction starts from bounded f32 wall positions"
    )]
    fn rebuild_test_normals(wall: &mut GeneratedWall) {
        let mut accumulated = vec![[0.0_f64; 3]; wall.positions_m.len()];
        for triangle in wall.indices.as_chunks::<3>().0 {
            let [first_index, second_index, third_index] =
                triangle.map(|index| usize::try_from(index).unwrap());
            let first_edge = subtract(
                wall.positions_m[second_index],
                wall.positions_m[first_index],
            );
            let second_edge =
                subtract(wall.positions_m[third_index], wall.positions_m[first_index]);
            let normal = [
                first_edge[1] * second_edge[2] - first_edge[2] * second_edge[1],
                first_edge[2] * second_edge[0] - first_edge[0] * second_edge[2],
                first_edge[0] * second_edge[1] - first_edge[1] * second_edge[0],
            ];
            for index in [first_index, second_index, third_index] {
                for (component, value) in accumulated[index].iter_mut().zip(normal) {
                    *component += value;
                }
            }
        }
        wall.normals = accumulated
            .into_iter()
            .map(|normal| {
                let inverse_length = normal
                    .iter()
                    .map(|value| value * value)
                    .sum::<f64>()
                    .sqrt()
                    .recip();
                normal.map(|component| (component * inverse_length) as f32)
            })
            .collect();
    }

    #[test]
    fn valid_walls_build_an_opaque_procedural_mesh_without_uvs() {
        let (terrain, paths, walls, source_terrain, source_paths, source_walls) =
            generated_fixture(2);
        validate_generated_walls(
            &terrain,
            &paths,
            &walls,
            &source_terrain,
            &source_paths,
            &source_walls,
        )
        .unwrap();
        let wall = &walls.walls[0];
        let mesh = build_editor_wall_mesh(wall);
        assert_eq!(mesh.count_vertices(), wall.positions_m.len());
        assert_eq!(mesh.indices().unwrap().len(), wall.indices.len());
        assert!(mesh.attribute(Mesh::ATTRIBUTE_POSITION).is_some());
        assert!(mesh.attribute(Mesh::ATTRIBUTE_NORMAL).is_some());
        assert!(mesh.attribute(Mesh::ATTRIBUTE_UV_0).is_none());
        let material = procedural_editor_wall_material();
        assert_eq!(material.alpha_mode, AlphaMode::Opaque);
        assert!(material.perceptual_roughness > 0.9);
    }

    #[test]
    fn validation_rejects_wrong_sources_order_profile_and_mesh_shape() {
        let (terrain, paths, walls, source_terrain, source_paths, source_walls) =
            generated_fixture(2);
        let rejects = |invalid: &GeneratedWalls| {
            validate_generated_walls(
                &terrain,
                &paths,
                invalid,
                &source_terrain,
                &source_paths,
                &source_walls,
            )
            .is_err()
        };
        let mut invalid = walls.clone();
        invalid.generation_version += 1;
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.terrain_document_hash = forge_core::ContentHash::from_bytes([0x11; 32]);
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.path_document_hash = forge_core::ContentHash::from_bytes([0x22; 32]);
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.walls.reverse();
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.walls[0].profile.height_m = 0.1;
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.walls[0].positions_m.pop();
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.walls[0].indices[0] = u32::MAX;
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.walls[0].normals[0] = [0.0; 3];
        assert!(rejects(&invalid));
        let mut invalid = walls.clone();
        invalid.walls[0].positions_m[0][0] = TERRAIN_HALF_EXTENT_M;
        assert!(rejects(&invalid));
        let mut invalid = walls;
        let last = invalid.walls[0].positions_m.len() - 1;
        invalid.walls[0].positions_m[last][1] += 0.5;
        assert!(rejects(&invalid));
    }

    #[test]
    fn exact_source_validation_rejects_coherent_translation_and_orientation_forgery() {
        let (terrain, paths, walls, source_terrain, source_paths, source_walls) =
            generated_fixture(1);
        let rejects = |invalid: &GeneratedWalls| {
            validate_generated_walls(
                &terrain,
                &paths,
                invalid,
                &source_terrain,
                &source_paths,
                &source_walls,
            )
            .is_err()
        };

        let mut translated = walls.clone();
        for position in &mut translated.walls[0].positions_m {
            position[1] += 0.25;
        }
        validate_wall_mesh(&translated.walls[0], &paths.paths[0]).unwrap();
        assert!(rejects(&translated));

        let mut reoriented = walls;
        rotate_wall_sections(&mut reoriented.walls[0], &paths.paths[0]);
        validate_wall_mesh(&reoriented.walls[0], &paths.paths[0]).unwrap();
        assert!(rejects(&reoriented));
    }

    #[test]
    fn aggregate_wall_geometry_budgets_fail_before_mesh_adaptation() {
        let (terrain, paths, walls, source_terrain, source_paths, source_walls) =
            generated_fixture(1);
        let rejects = |invalid: &GeneratedWalls| {
            validate_generated_walls(
                &terrain,
                &paths,
                invalid,
                &source_terrain,
                &source_paths,
                &source_walls,
            )
            .is_err()
        };
        let mut invalid = walls.clone();
        invalid.walls[0]
            .positions_m
            .resize(MAX_GENERATED_WALL_VERTICES + 1, [0.0; 3]);
        assert!(rejects(&invalid));
        let mut invalid = walls;
        invalid.walls[0]
            .indices
            .resize(MAX_GENERATED_WALL_INDICES + 1, 0);
        assert!(rejects(&invalid));
    }
}
