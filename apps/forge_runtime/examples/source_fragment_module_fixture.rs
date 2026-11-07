//! Reproducible, original module fixture for the installed source-material path.
//! Usage: `source_fragment_module_fixture ABSOLUTE_OUTPUT_ROOT valid|vertex-color|source-graph|source-graph-physics|source-graph-companions|source-graph-particles`
//! No Vault, user workspace, purchased assets, or external source files are read.
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, DurableAssetSource, GOVERNED_IMPORT_SCHEMA,
    GOVERNED_IMPORT_VERSION, GovernedAssetFile, GovernedAssetKind, GovernedImportAsset,
    GovernedImportCatalog, RedistributionRights,
};
use forge_core::{
    AssetId, CommandId, ContentHash, EntityId, ModuleId, Transform, Vec3, bytes_sha256,
};
use forge_delivery::module_builder::{ExportMetadata, ExportedModule, build_zone};
use forge_project::{
    authoring::new_terrain,
    module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin},
    module_package::ModuleRights,
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::ZoneEditSession,
    },
};
use serde_json::{Value, json};
use std::{
    collections::BTreeMap,
    path::{Path, PathBuf},
};

#[path = "support/source_graph_companions.rs"]
mod source_graph_companions;
#[path = "../../../crates/forge_assets/tests/support/source_graph_fixture.rs"]
pub(crate) mod source_graph_fixture;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut args = std::env::args_os().skip(1);
    let root = PathBuf::from(args.next().ok_or("output root required")?);
    let mode = args
        .next()
        .and_then(|value| value.into_string().ok())
        .ok_or("mode required")?;
    if !matches!(
        mode.as_str(),
        "valid"
            | "vertex-color"
            | "source-graph"
            | "source-graph-physics"
            | "source-graph-companions"
            | "source-graph-particles"
    ) {
        return Err(
            "mode must be valid, vertex-color, source-graph, source-graph-physics, source-graph-companions or source-graph-particles".into(),
        );
    }
    if args.next().is_some() || !root.is_absolute() || root.exists() {
        return Err("output root must be a new absolute path".into());
    }
    if mode == "source-graph-particles" {
        create_particle_only_fixture(&root).map(|_| ())
    } else {
        create_fixture(&root, &mode).map(|_| ())
    }
}

pub(crate) fn create_fixture(
    root: &Path,
    mode: &str,
) -> Result<ExportedModule, Box<dyn std::error::Error>> {
    let invalid = mode == "vertex-color";
    let particles = mode == "source-graph-companions";
    let companions = mode == "source-graph-physics" || particles;
    let graph = mode == "source-graph" || companions;
    let terrain = new_terrain("source-fragment-fixture", "camp", "Fixture source")?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let npc = populate_gameplay(&mut zone)?;
    let asset_id = if companions {
        forge_assets::governed_import::vault_asset_id(source_graph_companions::LIBRARY_ID)
    } else {
        AssetId::deterministic("fragment-fixture/model")
    };
    let mut companion_fixture = if companions {
        Some(source_graph_companions::fixture(asset_id, particles)?)
    } else {
        None
    };
    let model = if let Some(fixture) = &mut companion_fixture {
        std::mem::take(&mut fixture.model)
    } else if graph {
        source_graph_fixture::fixture()?
    } else {
        fixture_glb(invalid)?
    };
    let pin = bytes_sha256(&model);
    if !graph {
        let visual = VisualAssetPin {
            asset_id,
            sha256: pin,
        };
        let request = zone.head().prepare_gameplay_visuals(
            Some(vec![GameplayVisualBinding {
                area_id: zone.head().base.terrain.area_id,
                entity_id: npc,
                asset: visual,
                local_transform: Transform::IDENTITY,
            }]),
            &[visual],
            CommandId::deterministic("fragment-fixture/visual"),
        )?;
        zone.execute(&request)?;
    }
    let mut placement = fixture_placement(asset_id, pin, graph);
    let mut companion_pins =
        configure_companions(&mut zone, &mut placement, companion_fixture.as_ref())?;
    let request = zone.head().prepare_placement(
        CommandId::deterministic("fragment-fixture/placement"),
        PlacementEdit::Insert { placement },
    )?;
    zone.execute(&request)?;
    let particle = companion_fixture
        .as_mut()
        .and_then(|fixture| fixture.particles.take());
    if let Some(effect) = &particle {
        add_particle_placement(&mut zone, effect)?;
        if let Some(pins) = &mut companion_pins {
            pins["particles"] = json!(effect.reference);
        }
    }
    let recipe = companions.then_some(source_graph_companions::PHYSICS_RECIPE);
    let (mut catalog, mut payloads) = fixture_assets(asset_id, model, graph, recipe);
    if let Some(effect) = particle {
        append_particle_assets(&mut catalog, &mut payloads, effect)?;
    }
    if let Some(fixture) = companion_fixture {
        for (path, bytes) in fixture.payloads {
            if payloads.insert(path, bytes).is_some() {
                return Err("Synthetic companion payload path conflicts".into());
            }
        }
    }
    let archive = zone.archive_bytes()?;
    let exported = export_zone(root, &zone, &archive, catalog, payloads)?;
    print_summary(mode, &exported, &archive, pin, companion_pins)?;
    Ok(exported)
}

fn export_zone(
    root: &Path,
    zone: &ZoneEditSession,
    archive: &[u8],
    catalog: GovernedImportCatalog,
    payloads: BTreeMap<String, Vec<u8>>,
) -> Result<ExportedModule, String> {
    build_zone(
        root,
        archive,
        zone.head().content_hash()?,
        ExportMetadata {
            name: "Module de qualification matériau source".into(),
            version: "1.0.0".into(),
            publisher: "OpenRPG Forge tests".into(),
            license_text: "Original synthetic module fixture for local qualification.".into(),
            rights: ModuleRights::PrivateLocalOnly,
        },
        catalog,
        payloads,
    )
}

/// Construct the same original particle-only input for preflight and native replay.
#[allow(
    dead_code,
    reason = "Used by installed-runtime preflight tests when this fixture is shared"
)]
pub(crate) fn create_particle_only_fixture(
    root: &Path,
) -> Result<ExportedModule, Box<dyn std::error::Error>> {
    let physics_asset =
        forge_assets::governed_import::vault_asset_id(source_graph_companions::LIBRARY_ID);
    let mut fixture = source_graph_companions::fixture(physics_asset, true)?;
    let effect = fixture
        .particles
        .take()
        .ok_or("Synthetic particle asset absent")?;
    let terrain = new_terrain("source-fragment-fixture", "camp", "Fixture source")?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    populate_gameplay(&mut zone)?;
    add_particle_placement(&mut zone, &effect)?;
    let (catalog, mut payloads) = fixture_assets(
        effect.asset_id,
        effect.model,
        true,
        Some(source_graph_companions::PARTICLE_RECIPE),
    );
    for (path, bytes) in fixture.payloads {
        if path.starts_with("_forge-export/placement-companions/particles/")
            && payloads.insert(path, bytes).is_some()
        {
            return Err("Synthetic particle payload path conflicts".into());
        }
    }
    let archive = zone.archive_bytes()?;
    let exported = export_zone(root, &zone, &archive, catalog, payloads)?;
    print_summary(
        "source-graph-particles",
        &exported,
        &archive,
        effect.reference.carrier_sha256,
        Some(json!({"particles": effect.reference})),
    )?;
    Ok(exported)
}

fn configure_companions(
    zone: &mut ZoneEditSession,
    placement: &mut ManualPlacement,
    fixture: Option<&source_graph_companions::Fixture>,
) -> Result<Option<Value>, String> {
    let Some(fixture) = fixture else {
        return Ok(None);
    };
    let request = zone.head().prepare_physics(
        Some(fixture.physics_settings.clone()),
        CommandId::deterministic("fragment-fixture/physics"),
    )?;
    zone.execute(&request)?;
    placement.physics = Some(Box::new(fixture.physics.clone()));
    Ok(Some(json!({"physics":fixture.physics})))
}

fn add_particle_placement(
    zone: &mut ZoneEditSession,
    effect: &source_graph_companions::ParticleFixture,
) -> Result<(), String> {
    let mut placement = fixture_placement(effect.asset_id, effect.reference.carrier_sha256, true);
    placement.id = EntityId::deterministic("fragment-fixture/particle-placement");
    placement.name = "Synthetic particle source graph".into();
    placement.particles = Some(Box::new(effect.reference.clone()));
    placement.transform = Transform::from_translation(Vec3::new(2.0, 1.4, -4.0));
    let request = zone.head().prepare_placement(
        CommandId::deterministic("fragment-fixture/particle-placement"),
        PlacementEdit::Insert { placement },
    )?;
    zone.execute(&request)?;
    Ok(())
}

fn populate_gameplay(zone: &mut ZoneEditSession) -> Result<EntityId, Box<dyn std::error::Error>> {
    let player = EntityId::deterministic("fragment-fixture/player");
    let npc = EntityId::deterministic("fragment-fixture/npc");
    let gameplay = serde_json::from_value(json!({
        "schema":"forge.gameplay", "version":1,
        "module_id":ModuleId::deterministic("fragment-fixture/module"),
        "area_id":zone.head().base.terrain.area_id, "player":player,
        "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,
            "attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,
            "max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "actors":[
            {"id":player,"name":"Player","role":"player","position":{"x":0.,"y":4.,"z":0.},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false},
            {"id":npc,"name":"Keeper","role":"friendly","position":{"x":2.,"y":0.9,"z":-4.},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}
        ],
        "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
    }))?;
    let request = zone.head().prepare_gameplay(
        Some(gameplay),
        CommandId::deterministic("fragment-fixture/gameplay"),
    )?;
    zone.execute(&request)?;
    Ok(npc)
}

fn fixture_placement(asset_id: AssetId, pin: ContentHash, graph: bool) -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic("fragment-fixture/placement"),
        name: if graph {
            "Source graph triangle"
        } else {
            "Source material triangle"
        }
        .into(),
        asset_id,
        asset_hash: pin,
        physics: None,
        particles: None,
        local_lights: None,
        transform: Transform::from_translation(Vec3::new(0., if graph { 1.4 } else { 0.05 }, -4.)),
        locked: false,
    }
}

fn print_summary(
    mode: &str,
    exported: &ExportedModule,
    archive: &[u8],
    pin: ContentHash,
    companions: Option<Value>,
) -> Result<(), serde_json::Error> {
    let mut summary = json!({
        "mode": mode,
        "package":exported.package_path,
        "source_root":exported.source_root,
        "area_id":exported.area_id,
        "head_sha256":exported.head_hash,
        "archive_sha256":bytes_sha256(archive),
        "package_sha256":exported.package_sha256,
        "transport_sha256":exported.transport_manifest_sha256,
        "asset_sha256":pin,
    });
    if let Some(companions) = companions {
        summary["module_id"] = json!(exported.module_id);
        summary["synthetic_placement_companions"] = companions;
    }
    println!("{}", serde_json::to_string(&summary)?);
    Ok(())
}

fn fixture_assets(
    asset_id: AssetId,
    model: Vec<u8>,
    graph: bool,
    recipe: Option<&[u8]>,
) -> (GovernedImportCatalog, BTreeMap<String, Vec<u8>>) {
    let recipe = if let Some(recipe) = recipe {
        recipe.to_vec()
    } else if graph {
        br#"{"schema":"forge.qualification.source-model","version":1,"mode":"source-graph","generator":"source_fragment_module_fixture"}"#.to_vec()
    } else {
        br#"{"schema":"forge.qualification.source-model","version":1,"mode":"source-fragment","generator":"source_fragment_module_fixture"}"#.to_vec()
    };
    let path = if graph {
        "assets/source-graph.glb"
    } else {
        "assets/fragment.glb"
    };
    let asset = GovernedImportAsset {
        id: asset_id,
        name: if graph {
            "Synthetic source graph"
        } else {
            "Synthetic source fragment"
        }
        .into(),
        kind: GovernedAssetKind::Mesh,
        payload: file(path, &model),
        provenance: AssetProvenance {
            source: "Original generated qualification fixture".into(),
            author: "OpenRPG Forge tests".into(),
            license: "LicenseRef-local-qualification-fixture".into(),
            evidence: "licenses/fixture.txt".into(),
            acquired_on: "2026-11-02".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Unknown,
                redistribution_evidence: None,
            },
        },
        derived_from: None,
        transformations: vec![],
        authored_changes: true,
        durable_source: Some(DurableAssetSource::Recipe {
            file: file("recipes/fixture.json", &recipe),
        }),
        thumbnail: None,
    };
    let catalog = GovernedImportCatalog {
        schema: GOVERNED_IMPORT_SCHEMA.into(),
        schema_version: GOVERNED_IMPORT_VERSION,
        assets: vec![asset],
    };
    let payloads = BTreeMap::from([
        (path.into(), model),
        ("recipes/fixture.json".into(), recipe),
        (
            "licenses/fixture.txt".into(),
            b"Original synthetic geometry and texture for this local qualification only.".to_vec(),
        ),
    ]);
    (catalog, payloads)
}

fn append_particle_assets(
    catalog: &mut GovernedImportCatalog,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    effect: source_graph_companions::ParticleFixture,
) -> Result<(), String> {
    let (mut additional, mut files) = fixture_assets(
        effect.asset_id,
        effect.model,
        true,
        Some(source_graph_companions::PARTICLE_RECIPE),
    );
    let mut asset = additional
        .assets
        .pop()
        .ok_or("Synthetic particle asset absent")?;
    asset.name = "Synthetic source graph particles".into();
    asset.payload.path = "assets/source-graph-particles.glb".into();
    if let Some(DurableAssetSource::Recipe { file }) = &mut asset.durable_source {
        file.path = "recipes/particle-fixture.json".into();
    } else {
        return Err("Synthetic particle recipe absent".into());
    }
    let model = files
        .remove("assets/source-graph.glb")
        .ok_or("Synthetic particle model absent")?;
    let recipe = files
        .remove("recipes/fixture.json")
        .ok_or("Synthetic particle recipe absent")?;
    if payloads.insert(asset.payload.path.clone(), model).is_some()
        || payloads
            .insert("recipes/particle-fixture.json".into(), recipe)
            .is_some()
    {
        return Err("Synthetic particle asset path conflicts".into());
    }
    for (path, bytes) in files {
        if let Some(existing) = payloads.get(&path) {
            if existing != &bytes {
                return Err("Synthetic shared asset payload differs".into());
            }
        } else {
            payloads.insert(path, bytes);
        }
    }
    catalog.assets.push(asset);
    Ok(())
}

fn file(path: &str, bytes: &[u8]) -> GovernedAssetFile {
    GovernedAssetFile {
        path: path.into(),
        sha256: bytes_sha256(bytes),
        bytes: bytes.len() as u64,
    }
}

fn fixture_glb(invalid_color: bool) -> Result<Vec<u8>, String> {
    // Original 1x1 RGBA PNG: the same bytes are embedded for base, ORM and normal.
    const PNG: &[u8] = &[
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6,
        0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 104, 104, 248, 255,
        31, 0, 6, 130, 2, 255, 179, 190, 81, 51, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    ];
    let mut bin = Vec::new();
    let mut views = Vec::<Value>::new();
    let mut add = |bytes: &[u8], target: Option<u32>| {
        while !bin.len().is_multiple_of(4) {
            bin.push(0);
        }
        let offset = bin.len();
        bin.extend_from_slice(bytes);
        let mut view = json!({"buffer":0,"byteOffset":offset,"byteLength":bytes.len()});
        if let Some(target) = target {
            view["target"] = json!(target);
        }
        views.push(view);
    };
    let floats = |values: &[f32]| {
        values
            .iter()
            .flat_map(|v| v.to_le_bytes())
            .collect::<Vec<_>>()
    };
    add(&floats(&[-1., 0., 0., 1., 0., 0., 0., 2., 0.]), Some(34962));
    add(&floats(&[0., 0., 1., 0., 0., 1., 0., 0., 1.]), Some(34962));
    add(
        &floats(&[1., 0., 0., 1., 1., 0., 0., 1., 1., 0., 0., 1.]),
        Some(34962),
    );
    add(&floats(&[0., 0., 1., 0., 0.5, 1.]), Some(34962));
    add(&[0, 0, 1, 0, 2, 0], Some(34963));
    add(PNG, None);
    if invalid_color {
        add(
            &floats(&[1., 1., 1., 1., 1., 1., 1., 1., 1., 1., 1., 1.]),
            Some(34962),
        );
    }
    while !bin.len().is_multiple_of(4) {
        bin.push(0);
    }
    let mut accessors = vec![
        json!({"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[-1,0,0],"max":[1,2,0]}),
        json!({"bufferView":1,"componentType":5126,"count":3,"type":"VEC3"}),
        json!({"bufferView":2,"componentType":5126,"count":3,"type":"VEC4"}),
        json!({"bufferView":3,"componentType":5126,"count":3,"type":"VEC2"}),
        json!({"bufferView":4,"componentType":5123,"count":3,"type":"SCALAR"}),
    ];
    let mut attributes = json!({"POSITION":0,"NORMAL":1,"TANGENT":2,"TEXCOORD_0":3});
    if invalid_color {
        accessors.push(json!({"bufferView":6,"componentType":5126,"count":3,"type":"VEC4"}));
        attributes["COLOR_0"] = json!(5);
    }
    let contract = json!({
        "schema_version":1,"source_program_sha256":bytes_sha256(b"synthetic source fragment").to_string(),
        "linear_rgb_rows":[[1.,0.,0.,0.],[0.,1.,0.,0.],[0.,0.,1.,0.]],
        "metallic":[0.,0.,0.,0.,0.],"occlusion":[1.,0.,0.,0.,0.],
        "perceptual_roughness":[0.,1.,0.,0.,0.],"normal_strength":1.,
        "target_tangent_space_y_sign":1.,"double_sided_normal_mode":"single_sided"
    });
    let document = json!({
        "asset":{"version":"2.0"},"extensionsUsed":["OPENRPG_source_fragment"],
        "extensionsRequired":["OPENRPG_source_fragment"],
        "buffers":[{"byteLength":bin.len()}],"bufferViews":views,"accessors":accessors,
        "images":[{"bufferView":5,"mimeType":"image/png"},{"bufferView":5,"mimeType":"image/png"},{"bufferView":5,"mimeType":"image/png"}],
        "textures":[{"source":0},{"source":1},{"source":2}],
        "materials":[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0},"metallicRoughnessTexture":{"index":1}},
            "normalTexture":{"index":2},"occlusionTexture":{"index":1},"extensions":{"OPENRPG_source_fragment":contract}}],
        "meshes":[{"primitives":[{"attributes":attributes,"indices":4,"material":0}]}],
        "nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0
    });
    let mut json = serde_json::to_vec(&document).map_err(|error| error.to_string())?;
    json.resize(json.len().next_multiple_of(4), b' ');
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(
        u32::try_from(28 + json.len() + bin.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    bytes.extend(
        u32::try_from(json.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes.extend(
        u32::try_from(bin.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    bytes.extend(b"BIN\0");
    bytes.extend(bin);
    Ok(bytes)
}

#[cfg(test)]
mod companion_mode_tests {
    use super::*;
    use forge_project::zone_authoring::session::SourceCompanionRequirements;

    fn selected(
        fixture: &source_graph_companions::Fixture,
    ) -> (SourceCompanionRequirements, Vec<ManualPlacement>) {
        let terrain = new_terrain("companion-mode-test", "camp", "Fixture").unwrap();
        let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        let asset =
            forge_assets::governed_import::vault_asset_id(source_graph_companions::LIBRARY_ID);
        let mut placement = fixture_placement(asset, fixture.physics.carrier_sha256, true);
        configure_companions(&mut zone, &mut placement, Some(fixture)).unwrap();
        let request = zone
            .head()
            .prepare_placement(
                CommandId::deterministic("fixture-test/physical"),
                PlacementEdit::Insert { placement },
            )
            .unwrap();
        zone.execute(&request).unwrap();
        if let Some(effect) = &fixture.particles {
            add_particle_placement(&mut zone, effect).unwrap();
        }
        (
            zone.required_source_companions().unwrap(),
            zone.head().corrections.placements.clone().unwrap(),
        )
    }

    #[test]
    fn physics_mode_has_no_particle_placement_or_dependency() {
        let asset =
            forge_assets::governed_import::vault_asset_id(source_graph_companions::LIBRARY_ID);
        let physical = source_graph_companions::fixture(asset, false).unwrap();
        let combined = source_graph_companions::fixture(asset, true).unwrap();
        let (physical_requirements, physical_placements) = selected(&physical);
        let (combined_requirements, combined_placements) = selected(&combined);
        assert_eq!(physical_placements.len(), 1);
        assert!(physical_placements[0].physics.is_some());
        assert!(physical_placements[0].particles.is_none());
        assert_eq!(physical_requirements.physics.len(), 1);
        assert!(physical_requirements.particles.is_empty());
        assert_eq!(combined_placements.len(), 2);
        assert_eq!(combined_requirements.physics, physical_requirements.physics);
        assert_eq!(combined_requirements.particles.len(), 1);
        assert!(
            combined_placements
                .iter()
                .any(|item| item.particles.is_some() && item.physics.is_none())
        );
    }
}
