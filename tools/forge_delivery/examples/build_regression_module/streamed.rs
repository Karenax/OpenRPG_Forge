//! Original 2 km raster adventure, admitted by the ordinary module transport.
use forge_core::{CommandId, EntityId, Vec3, bytes_sha256};
use forge_project::{
    HeightSamplingAlgorithm, TerrainDocument, TerrainExtent,
    authoring::new_terrain,
    building::RoomDimensions,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    zone_authoring::{
        buildings::{AuthoredBuilding, BuildingEdit},
        placements::PlacementEdit,
        session::ZoneEditSession,
        start::ZoneStart,
    },
};

pub(super) fn terrain(key: &str, name: &str) -> Result<(TerrainDocument, Option<Vec<u8>>), String> {
    let mut document = new_terrain(key, "camp", name).map_err(|e| e.to_string())?;
    if key != "grand-plateau" {
        return Ok((document, None));
    }
    // Four MiB of original flat samples; subsequent cells are derived on demand.
    // This isolates the real game/room/prop integration from relief benchmarks.
    let raster = vec![0; 1024 * 1024 * 4];
    document.schema_version = forge_project::TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(2048.0, 2048.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 2.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 1024,
        height: 1024,
        origin_xz_m: [-1024.0; 2],
        sample_spacing_m: 2.0,
        outside_height_m: 0.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&raster),
    });
    document.validate().map_err(|e| e.to_string())?;
    Ok((document, Some(raster)))
}

pub(super) fn add_room(zone: &mut ZoneEditSession) -> Result<(), String> {
    let request = zone.head().prepare_building(
        CommandId::deterministic("grand-plateau/refuge"),
        BuildingEdit::Insert {
            building: AuthoredBuilding {
                id: EntityId::deterministic("grand-plateau/refuge"),
                name: "Refuge du plateau".into(),
                generator_version: 1,
                origin_m: Vec3::new(10.0, 0.0, -10.0),
                yaw_degrees: 0.0,
                dimensions: RoomDimensions {
                    width_m: 8.0,
                    depth_m: 6.0,
                    clear_height_m: 3.0,
                    wall_m: 0.25,
                    slab_m: 0.2,
                    door_width_m: 1.4,
                    door_height_m: 2.2,
                    door_offset_x_m: 0.0,
                },
                door_offset_override_m: None,
                locked: false,
            },
        },
    )?;
    zone.execute(&request)?;
    Ok(())
}

/// Keep NPC simulation islands at the origin. Only authored static objects and
/// the player start move, so a 160 m trip genuinely leaves their detailed cells.
pub(super) fn shift_static_scene(zone: &mut ZoneEditSession) -> Result<(), String> {
    let functional = zone
        .head()
        .corrections
        .gameplay
        .as_ref()
        .ok_or("Static HLOD fixture requires gameplay")?
        .functional_hash()
        .map_err(|error| error.to_string())?;
    for placement in zone
        .head()
        .corrections
        .placements
        .clone()
        .unwrap_or_default()
    {
        let mut transform = placement.transform;
        transform.translation_m.x += 256.0;
        let request = zone.head().prepare_placement(
            CommandId::deterministic(format!("static-hlod/placement/{}", placement.id)),
            PlacementEdit::Transform {
                id: placement.id,
                transform,
            },
        )?;
        zone.execute(&request)?;
    }
    for building in zone
        .head()
        .corrections
        .buildings
        .clone()
        .unwrap_or_default()
    {
        let mut origin_m = building.origin_m;
        origin_m.x += 256.0;
        let request = zone.head().prepare_building(
            CommandId::deterministic(format!("static-hlod/building/{}", building.id)),
            BuildingEdit::SetPose {
                id: building.id,
                origin_m,
                yaw_degrees: building.yaw_degrees,
            },
        )?;
        zone.execute(&request)?;
    }
    let request = zone.head().prepare_start(
        Some(ZoneStart {
            x_m: 256.0,
            z_m: 3.0,
            facing_degrees: Some(0.0),
        }),
        CommandId::deterministic("static-hlod/player-start"),
    )?;
    zone.execute(&request)?;
    if zone
        .head()
        .corrections
        .gameplay
        .as_ref()
        .ok_or("Gameplay was removed")?
        .functional_hash()
        .map_err(|error| error.to_string())?
        != functional
    {
        return Err("Static fixture preparation changed gameplay definitions".into());
    }
    Ok(())
}
