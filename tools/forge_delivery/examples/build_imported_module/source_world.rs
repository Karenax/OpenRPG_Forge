//! An explicit new qualification fork, not a migration of the flat regression.
use super::bounded;
use forge_core::{CommandId, Revision, Vec3, bytes_sha256};
use forge_project::{
    TerrainDocument, VegetationCollisionProfile, VegetationCollisionShape, VegetationDocument,
    zone_authoring::{
        GeneratorStamp,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::path::Path;

const CAMP_X: f32 = 8.0;
const CAMP_Z: f32 = -88.0;

pub struct Prepared {
    pub zone: ZoneEditSession,
    pub blobs: Vec<Vec<u8>>,
    pub report: serde_json::Value,
}

pub fn prepare(
    loaded: &forge_delivery::module_runtime::LoadedModuleZone,
    root: &Path,
    grass_profile: Option<forge_core::ContentHash>,
) -> Result<Prepared, String> {
    if !root.is_absolute() {
        return Err("Source-world directory must be absolute".into());
    }
    let terrain_bytes = bounded(&root.join("terrain.json"), 256 * 1024)?;
    let vegetation_bytes = bounded(&root.join("canonical-vegetation.json"), 256 * 1024)?;
    let archive_bytes = bounded(&root.join("zone.json"), 4 * 1024 * 1024)?;
    let mut terrain: TerrainDocument =
        serde_json::from_slice(&terrain_bytes).map_err(|e| e.to_string())?;
    let mut vegetation: VegetationDocument =
        serde_json::from_slice(&vegetation_bytes).map_err(|e| e.to_string())?;
    terrain.validate().map_err(|e| e.to_string())?;
    vegetation.validate().map_err(|e| e.to_string())?;
    let source = ZoneEditSession::from_archive_bytes(&archive_bytes)?;
    // Source files describe the generated base. `effective` stamps the later
    // authoring revision and is not byte-identical to that immutable checkpoint.
    if source.head().base.terrain != terrain
        || source.head().base.vegetation.as_ref() != Some(&vegetation)
    {
        return Err("Restored documents differ from their verified source archive".into());
    }
    let original = ZoneEditSession::from_archive_bytes(&loaded.archive)?;
    let corrections = &original.head().corrections;
    // Source-dependent edits require an explicit rebase, outside this composition.
    if corrections.base_elevation_m.is_some()
        || corrections.height_deltas.is_some()
        || corrections.material_overrides.is_some()
        || !corrections.density.is_empty()
        || corrections.species.is_some()
        || corrections.promotions.is_some()
        || corrections.source_object_edits.is_some()
        || corrections.imported_plant_offsets.is_some()
        || corrections.imported_plant_placements.is_some()
        || corrections.vegetation_exclusions.is_some()
        || corrections.paths.is_some()
        || corrections.rivers.is_some()
    {
        return Err("RPG template contains source-dependent edits requiring review".into());
    }
    let mut blobs = vec![bounded(&root.join("heights.bin"), 64 * 1024 * 1024)?];
    let source_population = vegetation
        .imported_source
        .as_ref()
        .ok_or("Source population missing")?
        .clone();
    for block in &source_population.blocks {
        let blob = bounded(
            &root.join(format!("canonical-{}.bin", block.instances_sha256)),
            block.byte_len()?,
        )?;
        if bytes_sha256(&blob) != block.instances_sha256 || blob.len() != block.byte_len()? {
            return Err("Source population hash or byte length differs".into());
        }
        blobs.push(blob);
    }
    if let Some(profile_manifest_sha256) = grass_profile {
        let imported = vegetation
            .imported_source
            .as_mut()
            .ok_or("Grass requires its explicit imported field source")?;
        imported.version = 2;
        imported.grass = Some(forge_project::imported_vegetation::ImportedGrassField {
            profile_manifest_sha256,
        });
        vegetation.validate().map_err(|e| e.to_string())?;
    }
    // New seed with template identities. Source coordinates/bytes stay exact;
    // neither historical input archive is rewritten or imported as a timeline.
    terrain.project_id = original.head().base.terrain.project_id;
    terrain.area_id = original.head().base.terrain.area_id;
    terrain.revision = Revision::INITIAL;
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    vegetation.revision = terrain.revision;
    let mut collision_layers = Vec::new();
    for layer in &mut vegetation.layers {
        let radius = match layer.id.as_str() {
            "tidewater_trees" => Some(0.35),
            "tidewater_palms" => Some(0.20),
            "tidewater_bananas" | "tidewater_young_palms" => Some(0.15),
            _ => None,
        };
        if let Some(radius_m) = radius {
            layer.collision = Some(VegetationCollisionProfile {
                shape: VegetationCollisionShape::Cylinder,
                radius_m,
                height_m: 3.0,
                activation_distance_m: 48.0,
            });
            collision_layers.push(layer.id.to_string());
        }
    }
    let plan = SampledTerrainPlan::from_document(&terrain, &blobs[0]).map_err(|e| e.to_string())?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let mut base = zone.head().base.clone();
    base.vegetation = Some(vegetation);
    base.generator = GeneratorStamp {
        name: "gate7-source-world-composition".into(),
        version: if grass_profile.is_some() { "2" } else { "1" }.into(),
        inputs_hash: bytes_sha256(
            &[
                terrain_bytes.as_slice(),
                vegetation_bytes.as_slice(),
                archive_bytes.as_slice(),
                loaded.archive.as_slice(),
                grass_profile
                    .as_ref()
                    .map_or(&[][..], |hash| hash.as_bytes().as_slice()),
            ]
            .concat(),
        ),
    };
    zone.execute(
        &zone
            .head()
            .prepare_regeneration(base, CommandId::deterministic("gate7/source-world/base-v1"))?,
    )?;
    let mut relocated = corrections.clone();
    let mut poses = Vec::new();
    for placement in relocated.placements.as_deref_mut().unwrap_or_default() {
        if placement.locked {
            return Err("Unlock template placement before relocation".into());
        }
        let before = placement.transform.translation_m;
        placement.transform.translation_m = relocate(before, &plan)?;
        poses.push(serde_json::json!({"kind":"placement", "id":placement.id,
            "before":before,"after":placement.transform.translation_m}));
    }
    for building in relocated.buildings.as_deref_mut().unwrap_or_default() {
        if building.locked {
            return Err("Unlock template building before relocation".into());
        }
        let before = building.origin_m;
        building.origin_m = relocate(before, &plan)?;
        // Sample the complete footprint at <=0.5 m to avoid burying the slab.
        // This is an explicit authored foundation pose, not a terrain mutation.
        let d = building.effective_dimensions()?;
        let (s, c) = building.yaw_degrees.to_radians().sin_cos();
        let mut highest = f32::NEG_INFINITY;
        let mut lowest = f32::INFINITY;
        let mut x = -d.width_m * 0.5;
        while x <= d.width_m * 0.5 {
            let mut z = -d.depth_m * 0.5;
            while z <= d.depth_m * 0.5 {
                let height = height(
                    &plan,
                    building.origin_m.x + x * c + z * s,
                    building.origin_m.z - x * s + z * c,
                )?;
                highest = highest.max(height);
                lowest = lowest.min(height);
                z += 0.5;
            }
            x += 0.5;
        }
        if highest - lowest > 0.25 {
            return Err("Camp foundation needs a flatter site or an authored terrain edit".into());
        }
        building.origin_m.y = before.y + highest + 0.02;
        poses.push(serde_json::json!({"kind":"building","id":building.id,
            "before":before,"after":building.origin_m,
            "footprint_min_height_m":lowest,"footprint_max_height_m":highest}));
    }
    if let Some(gameplay) = relocated.gameplay.as_deref_mut() {
        for actor in &mut gameplay.actors {
            let before = actor.position;
            actor.position = relocate(before, &plan)?;
            for point in &mut actor.patrol {
                *point = relocate(*point, &plan)?;
            }
            poses.push(serde_json::json!({"kind":"actor","id":actor.id,
                "before":before,"after":actor.position}));
        }
        for object in &mut gameplay.objects {
            object.position = relocate(object.position, &plan)?;
        }
        for place in &mut gameplay.places {
            place.position = relocate(place.position, &plan)?;
        }
    }
    if let Some(start) = relocated.start.as_deref_mut() {
        start.x_m += CAMP_X;
        start.z_m += CAMP_Z;
    }
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic("gate7/source-world/relocated-rpg-v1"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash()?,
        action: ZoneAction::edit(relocated),
    })?;
    let report = serde_json::json!({
        "schema":"forge.gate7.world-composition.v1",
        "source_terrain_sha256":bytes_sha256(&terrain_bytes),
        "source_vegetation_sha256":bytes_sha256(&vegetation_bytes),
        "source_archive_sha256":bytes_sha256(&archive_bytes),
        "rpg_template_archive_sha256":bytes_sha256(&loaded.archive),
        "new_seed":true,"historical_timelines_imported":false,
        "source_population":source_population,
        "immutable_population_bytes_preserved":true,
        "authored_grass_profile_manifest_sha256":grass_profile,
        "camp_offset_xz_m":[CAMP_X,CAMP_Z],
        "collision_proxy_layers":collision_layers,
        "collision_policy":"Authored cylinder proxies for tree/palm/banana trunks, scaled by source transforms; soft undergrowth has no collider. Not exact mesh collisions.",
        "poses":poses,"native_render_qualified":false,"gate_closed":false,
    });
    Ok(Prepared {
        zone,
        blobs,
        report,
    })
}

fn relocate(before: Vec3, plan: &SampledTerrainPlan) -> Result<Vec3, String> {
    let (x, z) = (before.x + CAMP_X, before.z + CAMP_Z);
    Ok(Vec3::new(x, before.y + height(plan, x, z)?, z))
}

fn height(plan: &SampledTerrainPlan, x: f32, z: f32) -> Result<f32, String> {
    let value = plan
        .height_at(f64::from(x), f64::from(z))
        .map_err(|e| e.to_string())?;
    if !value.is_finite() || value.abs() > 4096.0 {
        return Err("Camp terrain height is not representable".into());
    }
    #[allow(clippy::cast_possible_truncation)] // Finite terrain height bounded above.
    Ok(value as f32)
}
