//! Bridge from the projected NWN walkmesh to the qualified Forge navigation provider.

use crate::projection::ProjectedZone;
use forge_navigation::{AgentProfile, TileInput, TileKey, generate_tile, source_hash};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct NavigationValidation {
    pub tile_x: i32,
    pub tile_z: i32,
    pub source_triangles: usize,
    pub polygons: usize,
    pub source_hash: String,
}

/// Converts the projected walkmesh into the canonical triangle form expected by
/// `forge_navigation` and runs the same bounded Recast/Landmass provider as the Studio.
/// A radius of one metre keeps the whole synthetic zone inside one navigation tile.
pub fn validate_forge_navigation(zone: &ProjectedZone) -> Result<NavigationValidation, String> {
    if zone.walkmesh.triangles.is_empty() {
        return Err("projected zone has no walkmesh triangles".to_owned());
    }
    let profile = AgentProfile {
        radius_m: 1.0,
        height_m: 2.5,
        ..AgentProfile::default()
    };
    let mut vertices = Vec::with_capacity(zone.walkmesh.triangles.len() * 3);
    let mut triangles = Vec::with_capacity(zone.walkmesh.triangles.len());
    for triangle in &zone.walkmesh.triangles {
        let base = vertices.len() as u32;
        vertices.push([triangle.a.x, triangle.a.y, triangle.a.z]);
        vertices.push([triangle.b.x, triangle.b.y, triangle.b.z]);
        vertices.push([triangle.c.x, triangle.c.y, triangle.c.z]);
        triangles.push([base, base + 1, base + 2]);
    }
    let key = TileKey::at([0.0, 0.0, 0.0], profile);
    let mut input = TileInput {
        key,
        profile,
        revision: 1,
        source_hash: String::new(),
        token: 0,
        vertices,
        triangles,
        areas: Vec::new(),
        policy_source_hash: None,
        curved_obstacles: Vec::new(),
    };
    input.source_hash = source_hash(&input)?;
    let built = generate_tile(input)?;
    Ok(NavigationValidation {
        tile_x: key.x,
        tile_z: key.z,
        source_triangles: built.source_triangles,
        polygons: built.polygon.polygon_count(),
        source_hash: built.source_hash,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_an_empty_walkmesh() {
        // The full provider test is exercised by the projection integration test with
        // the committed synthetic zone; this unit test only guards the explicit refusal.
        let zone = crate::projection::ProjectedZone {
            resref: "empty".to_owned(),
            name: "Empty".to_owned(),
            area_id: forge_core::AreaId::deterministic("empty"),
            terrain: forge_project::TerrainDocument::new(
                forge_core::ProjectId::deterministic("project"),
                forge_core::AreaId::deterministic("empty"),
                forge_core::Revision::INITIAL,
                "Empty",
                1,
                forge_project::TerrainExtent::new(10.0, 10.0),
                forge_project::HeightSamplingParameters {
                    algorithm: forge_project::HeightSamplingAlgorithm::FractalValueNoiseV1,
                    sample_spacing_m: 1.0,
                    base_elevation_m: 0.0,
                    amplitude_m: 0.0,
                    base_frequency_cycles_per_m: 0.01,
                    octaves: 1,
                    lacunarity: 2.0,
                    persistence: 0.5,
                },
                vec![forge_project::TerrainMaterialLayer::new("ground", "Ground")],
            ),
            gameplay: forge_gameplay::GameplayDocument {
                schema: forge_gameplay::GAMEPLAY_SCHEMA.to_owned(),
                version: forge_gameplay::GAMEPLAY_VERSION,
                module_id: forge_core::ModuleId::deterministic("module"),
                area_id: forge_core::AreaId::deterministic("empty"),
                profile: forge_gameplay::RulesProfile {
                    id: "test".to_owned(),
                    version: 1,
                    interaction_range_m: 2.0,
                    attack_range_m: 1.5,
                    attack_damage: 1,
                    attack_stamina_cost: 1,
                    max_stamina: 100,
                    max_action_receipts: 128,
                    max_events_per_tick: 64,
                },
                creator: None,
                narrative: None,
                factions: None,
                simulation: None,
                scripting: None,
                cinematics: None,
                player: forge_core::EntityId::deterministic("player"),
                actors: Vec::new(),
                items: Vec::new(),
                objects: Vec::new(),
                places: Vec::new(),
                dialogues: Vec::new(),
                quests: Vec::new(),
            },
            walkmesh: crate::walkmesh::WalkSurface {
                triangles: Vec::new(),
                min: forge_core::Vec3::ZERO,
                max: forge_core::Vec3::ZERO,
            },
            doors: Vec::new(),
            instances: Vec::new(),
            spawn: forge_core::Vec3::ZERO,
            spawn_yaw: 0.0,
            archive_path: String::new(),
            archive_bytes: Vec::new(),
            archive_sha256: forge_core::bytes_sha256(b"archive"),
            head_hash: forge_core::bytes_sha256(b"head"),
            gameplay_sha256: forge_core::bytes_sha256(b"gameplay"),
            tile_count: 0,
            navigation: crate::projection::NavigationStats {
                wok_files: 0,
                dwk_files: 0,
                pwk_files: 0,
                triangles: 0,
                unsupported: 0,
            },
            diagnostics: Vec::new(),
            area_root: std::path::PathBuf::new(),
        };
        assert!(validate_forge_navigation(&zone).is_err());
    }
}
