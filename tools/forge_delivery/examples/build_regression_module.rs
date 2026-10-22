//! Original, editable regression adventures. No Vault or historical script is executed.
use forge_assets::governed_import::*;
use forge_core::{
    AssetId, CommandId, ContentHash, EntityId, ModuleId, Transform, Vec3, bytes_sha256,
};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    orpmod,
    selected_legacy_dialogue::{self, SelectedLegacyDialogue},
};
use forge_project::{
    authoring::new_terrain,
    module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin},
    module_package::*,
    zone_authoring::{
        gameplay::GameplayDocument,
        placements::{ManualPlacement, PlacementEdit},
        session::ZoneEditSession,
    },
};
use serde_json::{Value, json};
use std::{fs, path::Path};

struct HistoricalImport {
    dialogue: SelectedLegacyDialogue,
    license: Vec<u8>,
    pins: Value,
}

fn historical_import(path: &Path) -> Result<HistoricalImport, Box<dyn std::error::Error>> {
    let directory = path
        .parent()
        .ok_or("historical source directory required")?;
    let bytes = historical_file(
        path,
        214334,
        "4f3b74205b596aa28028f8794c49296053a93b739fbfb330a63efd63752c7dfd",
    )?;
    let dialogue = selected_legacy_dialogue::extract(
        &bytes,
        ContentHash::parse("4f3b74205b596aa28028f8794c49296053a93b739fbfb330a63efd63752c7dfd")?,
    )?;
    let mut pins = vec![
        json!({"source_revision":selected_legacy_dialogue::SOURCE_COMMIT,
        "source_path":selected_legacy_dialogue::SOURCE_PATH,"sha256":dialogue.source_sha256,
        "bytes":bytes.len(),"selection":"two text nodes and one side-effect-free edge only"}),
    ];
    let mut license = None;
    for (local, source, sha256, length) in [
        (
            "README.md",
            "apps/Forge.Runtime/content/solaren/README.md",
            "b3c8ade7021224667da3094ceee024773b5f09f7ccdf07573001d2de54ccee96",
            2915,
        ),
        (
            "voice-assets.json",
            "apps/Forge.Runtime/content/solaren/audio/dialogues/voice-assets.json",
            "1bd3f7f2f23e1537495f56c585619479d031d5b3d54fb6aa48f8e5374256d4fc",
            3249,
        ),
        (
            "LICENSE.txt",
            "LICENSE.txt",
            "e222243fc97d4af8a5ecb2022c8e0e660c607d4b1de5b00a0620b9d39ede76b4",
            4635,
        ),
    ] {
        let bytes = historical_file(&directory.join(local), length, sha256)?;
        pins.push(
            json!({"source_revision":selected_legacy_dialogue::SOURCE_COMMIT,
            "source_path":source,"sha256":sha256,"bytes":length,
            "included_payload":local=="LICENSE.txt"}),
        );
        if local == "LICENSE.txt" {
            license = Some(bytes);
        }
    }
    Ok(HistoricalImport {
        dialogue,
        license: license.ok_or("Historical root licence absent")?,
        pins: json!(pins),
    })
}

fn historical_file(
    path: &Path,
    length: usize,
    sha256: &str,
) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
    use std::io::Read;
    forge_delivery::manifest::safe_chain(path)?;
    let file = fs::File::open(path)?;
    let metadata = file.metadata()?;
    if !metadata.is_file() || usize::try_from(metadata.len()).ok() != Some(length) {
        return Err(format!(
            "Historical evidence size differs from its independent pin: {}",
            path.display()
        )
        .into());
    }
    let mut bytes = Vec::with_capacity(length);
    file.take(u64::try_from(length)? + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() != length || bytes_sha256(&bytes) != ContentHash::parse(sha256)? {
        return Err(format!(
            "Historical evidence differs from the independently pinned source: {}",
            path.display()
        )
        .into());
    }
    Ok(bytes)
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut args = std::env::args_os().skip(1);
    let output = std::path::PathBuf::from(args.next().ok_or("absolute output directory required")?);
    if !output.is_absolute() || output.exists() {
        return Err("A new absolute output directory is required".into());
    }
    let imported = args
        .next()
        .map(|path| historical_import(Path::new(&path)))
        .transpose()?;
    if args.next().is_some() {
        return Err("unexpected additional argument".into());
    }
    forge_delivery::manifest::safe_chain(&output)?;
    fs::create_dir(&output)?;
    let first = build(&output, "trois-cairns", "Les Trois Cairns", None)?;
    let second = build(
        &output,
        "relais-des-lanternes",
        "Le Relais des Lanternes",
        imported.as_ref(),
    )?;
    println!(
        "{}",
        serde_json::to_string_pretty(
            &json!({"schema":"forge.gate4-regression-modules","version":1,"modules":[first,second]})
        )?
    );
    Ok(())
}

fn build(
    output: &Path,
    key: &str,
    name: &str,
    imported: Option<&HistoricalImport>,
) -> Result<Value, Box<dyn std::error::Error>> {
    let root = output.join(key);
    for folder in ["assets", "zones", "recipes", "provenance", "licenses"] {
        fs::create_dir_all(root.join(folder))?;
    }
    let terrain = new_terrain(key, "camp", name)?;
    let (project_id, area_id) = (terrain.project_id, terrain.area_id);
    let module_id = ModuleId::deterministic(format!("gate4/{key}"));
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let start = zone.head().prepare_start(
        Some(forge_project::zone_authoring::start::ZoneStart {
            x_m: authored_position(key, Vec3::new(0.0, 0.0, 3.0)).x,
            z_m: authored_position(key, Vec3::new(0.0, 0.0, 3.0)).z,
            facing_degrees: Some(if key == "relais-des-lanternes" {
                90.0
            } else {
                0.0
            }),
        }),
        CommandId::deterministic(format!("{key}/start")),
    )?;
    zone.execute(&start)?;
    let mut files = Vec::new();
    write(&root, &mut files, "licenses/generated.txt", b"Original geometric models and recipes: CC0-1.0. Generated by OpenRPG Forge's regression adventure factory. This does not license any imported historical text.", ModuleFileRole::License)?;
    let catalog = assets(&root, &mut files)?;
    let document = gameplay(key, module_id, area_id, imported.map(|i| &i.dialogue))?;
    let request = zone.head().prepare_gameplay(
        Some(document.clone()),
        CommandId::deterministic(format!("{key}/gameplay")),
    )?;
    zone.execute(&request)?;
    add_landmarks(&mut zone, key, &catalog)?;
    let gameplay_visuals = visual_bindings(&document, &catalog);
    let pins = catalog
        .assets
        .iter()
        .map(|asset| VisualAssetPin {
            asset_id: asset.id,
            sha256: asset.payload.sha256,
        })
        .collect::<Vec<_>>();
    let request = zone.head().prepare_gameplay_visuals(
        Some(gameplay_visuals.clone()),
        &pins,
        CommandId::deterministic(format!("{key}/gameplay-visuals")),
    )?;
    zone.execute(&request)?;
    let archive = zone.archive_bytes()?;
    write(
        &root,
        &mut files,
        "zones/camp.json",
        &archive,
        ModuleFileRole::ZoneArchive,
    )?;
    write(
        &root,
        &mut files,
        "assets/catalog.json",
        &serde_json::to_vec(&catalog)?,
        ModuleFileRole::AssetCatalog,
    )?;
    let (license_paths, proof_paths) = provenance(&root, &mut files, imported)?;
    let manifest = ModulePackageManifest {
        creator: None,
        schema:MODULE_PACKAGE_SCHEMA.into(),version:MODULE_PACKAGE_VERSION,project_id,module_id,name:name.into(),module_version:"1.0.0".into(),
        compatibility:ModuleCompatibility { engine_version:orpmod::ENGINE_VERSION.into(),module_api_version:orpmod::MODULE_API_VERSION },
        provenance:ModuleProvenance { publisher:"OpenRPG Forge".into(),source:"Original Gate 4 regression adventure; selected historical text when explicitly supplied".into(),source_revision:"gate4-regression-v1".into(),rights:ModuleRights::PrivateLocalOnly,license_paths:license_paths.clone(),proof_paths,credits_path:"CREDITS.txt".into() },
        asset_catalog_path:"assets/catalog.json".into(),files,zones:vec![ModuleZone { area_id,archive_path:"zones/camp.json".into(),archive_sha256:bytes_sha256(&archive),head_hash:zone.head().content_hash()?,gameplay_sha256:Some(forge_core::canonical_json_sha256(&document)?),source_blobs:vec![] }],
        assets:catalog.assets.iter().enumerate().map(|(index,asset)| ModuleAsset { asset_id:asset.id,file_path:asset.payload.path.clone(),sha256:asset.payload.sha256,dependencies:if index==0 {vec![]} else {vec![catalog.assets[0].id]} }).collect(),gameplay_visuals,
    };
    fs::write(root.join("module.json"), manifest.to_bytes()?)?;
    let archive_path = output.join(format!("{key}.orpmod"));
    let (_, transport) = orpmod::pack(
        &root,
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module_id.to_string(),
            label: name.into(),
            version: manifest.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: manifest.provenance.source_revision.clone(),
            rights: Rights::PrivateLocalOnly,
            license_paths,
            files: vec![],
            product: None,
        },
        &archive_path,
        false,
    )?;
    Ok(
        json!({"name":name,"module_id":module_id,"area_id":area_id,"source":root,"package":archive_path,"transport_manifest_sha256":transport,"package_sha256":forge_delivery::file_hash(&archive_path)?,"historical_import":imported.is_some(),"gameplay":document,"variant_asset":catalog.assets[1].id}),
    )
}

fn provenance(
    root: &Path,
    files: &mut Vec<ModuleFile>,
    imported: Option<&HistoricalImport>,
) -> Result<(Vec<String>, Vec<String>), Box<dyn std::error::Error>> {
    let source = json!({"schema":"forge.regression-adventure-source","version":1,
        "author":"OpenRPG Forge","source":"Original procedural geometry and authored RPG documents",
        "generator":"build_regression_module","historical_subset":imported.map(|i|&i.dialogue),
        "excluded":["all other historical scenes and dialogue nodes","all historical scripts, effects and conditions","all historical audio and assets"]});
    write(
        root,
        files,
        "provenance/source.json",
        &serde_json::to_vec_pretty(&source)?,
        ModuleFileRole::Provenance,
    )?;
    let mut licenses = vec!["licenses/generated.txt".into()];
    let mut proofs = vec!["provenance/source.json".into()];
    if let Some(imported) = imported {
        write(
            root,
            files,
            "licenses/historical-root.txt",
            &imported.license,
            ModuleFileRole::License,
        )?;
        let metadata = format!(
            "Selected module metadata, preserved verbatim: author = {}; license = {}. The distinct historical repository-root LICENSE.txt is preserved byte-for-byte as historical-root.txt. No redistribution grant is inferred from either label. Private/local only. Original generated geometry is governed separately.",
            imported.dialogue.author, imported.dialogue.license
        );
        write(
            root,
            files,
            "licenses/historical-dialogue.txt",
            metadata.as_bytes(),
            ModuleFileRole::License,
        )?;
        write(
            root,
            files,
            "provenance/historical-source-pins.json",
            &serde_json::to_vec_pretty(&imported.pins)?,
            ModuleFileRole::Provenance,
        )?;
        licenses.extend([
            "licenses/historical-root.txt".into(),
            "licenses/historical-dialogue.txt".into(),
        ]);
        proofs.push("provenance/historical-source-pins.json".into());
    }
    write(root,files,"CREDITS.txt",b"Original adventure and procedural sample models: OpenRPG Forge. Where present, selected Solaren text preserves its exact historical author, license, source pins and root licence separately. No historical audio or unselected scene is included.",ModuleFileRole::Credits)?;
    Ok((licenses, proofs))
}

fn add_landmarks(
    zone: &mut ZoneEditSession,
    key: &str,
    catalog: &GovernedImportCatalog,
) -> Result<(), String> {
    let placements = [-3.0_f32, 3.0]
        .into_iter()
        .enumerate()
        .map(|(i, x)| ManualPlacement {
            id: EntityId::deterministic(format!("{key}/cairn/{i}")),
            name: format!("Cairn {}", i + 1),
            asset_id: catalog.assets[0].id,
            asset_hash: catalog.assets[0].payload.sha256,
            physics: None,
            particles: None,
            transform: Transform {
                translation_m: authored_position(key, Vec3::new(x, 0.6, -1.0)),
                scale: Vec3::new(0.6, 1.2, 0.6),
                ..Transform::IDENTITY
            },
            locked: false,
        })
        .collect();
    let request = zone.head().prepare_placement(
        CommandId::deterministic(format!("{key}/landmarks")),
        PlacementEdit::InsertMany { placements },
    )?;
    zone.execute(&request)?;
    Ok(())
}

fn visual_bindings(
    document: &GameplayDocument,
    catalog: &GovernedImportCatalog,
) -> Vec<GameplayVisualBinding> {
    [
        (&document.actors[1].id, 0, [0.5, 1.8, 0.5]),
        (&document.actors[2].id, 1, [0.5, 1.8, 0.5]),
        (&document.objects[0].id, 0, [0.6, 0.6, 0.6]),
    ]
    .into_iter()
    .map(|(entity, index, scale)| GameplayVisualBinding {
        area_id: document.area_id,
        entity_id: *entity,
        asset: VisualAssetPin {
            asset_id: catalog.assets[index].id,
            sha256: catalog.assets[index].payload.sha256,
        },
        local_transform: Transform {
            scale: Vec3::new(scale[0], scale[1], scale[2]),
            ..Transform::IDENTITY
        },
    })
    .collect()
}

fn write(
    root: &Path,
    files: &mut Vec<ModuleFile>,
    path: &str,
    bytes: &[u8],
    role: ModuleFileRole,
) -> Result<(), std::io::Error> {
    fs::write(root.join(path), bytes)?;
    files.push(ModuleFile {
        path: path.into(),
        bytes: bytes.len() as u64,
        sha256: bytes_sha256(bytes),
        role,
    });
    Ok(())
}

fn assets(
    root: &Path,
    files: &mut Vec<ModuleFile>,
) -> Result<GovernedImportCatalog, Box<dyn std::error::Error>> {
    let mut assets = Vec::new();
    for (index, (label, color)) in [
        ("blue", [0.08, 0.35, 0.72, 1.0]),
        ("amber", [0.9, 0.38, 0.05, 1.0]),
    ]
    .into_iter()
    .enumerate()
    {
        let recipe = serde_json::to_vec_pretty(
            &json!({"schema":"forge.authored-box-recipe","version":1,"dimensions_m":[1.0,1.0,1.0],"base_color":color,"metallic":0.0,"roughness":0.65,"generator":"build_regression_module/box_glb","generator_version":1}),
        )?;
        let recipe_path = format!("recipes/{label}.json");
        write(
            root,
            files,
            &recipe_path,
            &recipe,
            ModuleFileRole::AssetAuxiliary,
        )?;
        let mesh = box_glb(color)?;
        let path = format!("assets/{label}.glb");
        write(root, files, &path, &mesh, ModuleFileRole::Asset)?;
        assets.push(GovernedImportAsset {
            id: AssetId::deterministic(format!("forge/gate4/original-box/{label}")),
            name: format!("Original {label} box"),
            kind: GovernedAssetKind::Mesh,
            payload: GovernedAssetFile {
                path,
                bytes: mesh.len() as u64,
                sha256: bytes_sha256(&mesh),
            },
            provenance: AssetProvenance {
                source: "Original procedural geometry from this preserved recipe".into(),
                author: "OpenRPG Forge".into(),
                license: "CC0-1.0".into(),
                evidence: "licenses/generated.txt".into(),
                acquired_on: "2026-10-15".into(),
                rights: AssetRights {
                    local_use: true,
                    redistribution: RedistributionRights::Allowed,
                    redistribution_evidence: Some("licenses/generated.txt".into()),
                },
            },
            derived_from: if index == 0 {
                None
            } else {
                Some(AssetId::deterministic("forge/gate4/original-box/blue"))
            },
            transformations: vec![AssetTransformation {
                tool: "build_regression_module/box_glb".into(),
                version: "1".into(),
                parameters_sha256: bytes_sha256(&recipe),
            }],
            authored_changes: true,
            durable_source: Some(DurableAssetSource::Recipe {
                file: GovernedAssetFile {
                    path: recipe_path,
                    bytes: recipe.len() as u64,
                    sha256: bytes_sha256(&recipe),
                },
            }),
            thumbnail: None,
        });
    }
    Ok(GovernedImportCatalog {
        schema: GOVERNED_IMPORT_SCHEMA.into(),
        schema_version: GOVERNED_IMPORT_VERSION,
        assets,
    })
}

fn gameplay(
    key: &str,
    module: ModuleId,
    area: forge_core::AreaId,
    imported: Option<&SelectedLegacyDialogue>,
) -> Result<GameplayDocument, serde_json::Error> {
    let id = |name: &str| EntityId::deterministic(format!("{key}/{name}"));
    let (player, npc, enemy, token, reward, pickup, quest, dialogue, root_node, active_node) = (
        id("player"),
        id("keeper"),
        id("sentinel"),
        id("beacon-token"),
        id("reward"),
        id("pickup"),
        id("quest"),
        id("dialogue"),
        id("root-node"),
        id("active-node"),
    );
    let accept = json!({"id":id("accept"),"text":"Je vais récupérer la pierre et affronter la sentinelle.","condition":{"kind":"quest","quest":quest,"status":"available"},"effects":[{"kind":"accept_quest","quest":quest}],"next":active_node});
    let finish = json!({"id":id("finish"),"text":"La sentinelle est vaincue. Voici la pierre.","condition":{"kind":"actor_defeated","actor":enemy},"effects":[{"kind":"complete_quest","quest":quest}],"next":null});
    let mut nodes = vec![
        json!({"id":root_node,"text":"Une pierre de signal est perdue près du cairn. La sentinelle en garde l'accès.","choices":[accept]}),
        json!({"id":active_node,"text":"Rapportez la pierre après avoir vaincu la sentinelle.","choices":[finish]}),
    ];
    let start = if let Some(source) = imported {
        nodes.push(json!({"id":source.nodes[0].id,"text":source.nodes[0].text,"choices":[{"id":source.choice.id,"text":source.choice.text,"condition":{"kind":"always"},"effects":[],"next":source.nodes[1].id}]}));
        nodes.push(json!({"id":source.nodes[1].id,"text":source.nodes[1].text,"choices":[{"id":id("original-transition"),"text":"Parlons maintenant du relais.","condition":{"kind":"always"},"effects":[],"next":root_node}]}));
        source.nodes[0].id
    } else {
        root_node
    };
    let mut document: GameplayDocument = serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":module,"area_id":area,
        "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},"player":player,
        "actors":[actor(player,"Voyageur","player",[0.0,0.95,3.0],100,None),actor(npc,"Vigie du relais","friendly",[0.0,0.9,0.0],100,Some(dialogue)),actor(enemy,"Sentinelle","enemy",[2.5,0.9,-3.0],40,None)],
        "items":[{"id":token,"name":"Pierre de signal","max_stack":1},{"id":reward,"name":"Insigne du relais","max_stack":1}],
        "objects":[{"id":pickup,"name":"Pierre de signal","position":{"x":0.0,"y":0.3,"z":-3.0},"kind":{"kind":"pickup","item":token,"quantity":1},"physics":{"body_half_extents_m":{"x":0.3,"y":0.3,"z":0.3},"mass_kg":0.0,"pushable":false,"throwable":false,"break_energy_j":null,"supports":[],"flammable":false,"heat_damage_per_tick":0}}],
        "places":[{"id":id("camp"),"name":"Le relais","position":{"x":0.0,"y":0.0,"z":3.0},"links":[id("cairns")]},{"id":id("cairns"),"name":"Les cairns","position":{"x":0.0,"y":0.0,"z":-3.0},"links":[id("camp")]}],
        "dialogues":[{"id":dialogue,"start":start,"nodes":nodes}],
        "quests":[{"id":quest,"name":"Rallumer le signal","giver":npc,"required_item":token,"required_quantity":1,"reward_item":reward,"reward_quantity":1}]
    }))?;
    for actor in &mut document.actors {
        actor.position = authored_position(key, actor.position);
    }
    for object in &mut document.objects {
        object.position = authored_position(key, object.position);
    }
    for place in &mut document.places {
        place.position = authored_position(key, place.position);
    }
    if key == "trois-cairns" {
        document.actors[1].name = "Gardienne des cairns".into();
        document.actors[2].name = "Pillard des cairns".into();
        document.quests[0].name = "Le sceau des trois cairns".into();
        document.items[0].name = "Sceau des cairns".into();
        document.items[1].name = "Marque de la gardienne".into();
        document.objects[0].name = "Sceau des cairns".into();
        document.dialogues[0].nodes[0].text = "Un pillard a dérobé le sceau des trois cairns. Retrouvez-le et rapportez le sceau pour restaurer ce passage.".into();
        document.dialogues[0].nodes[0].choices[0].text =
            "Je retrouverai le sceau et repousserai le pillard.".into();
        document.dialogues[0].nodes[1].text =
            "Le sceau doit revenir aux cairns. Le pillard vous barre encore le passage.".into();
        document.dialogues[0].nodes[1].choices[0].text =
            "Le pillard est vaincu. Voici le sceau des cairns.".into();
    }
    Ok(document)
}

/// Authored sample layouts share governed models while keeping distinct approaches.
fn authored_position(key: &str, position: Vec3) -> Vec3 {
    if key == "relais-des-lanternes" {
        Vec3::new(position.z, position.y, -position.x)
    } else {
        position
    }
}

fn actor(
    id: EntityId,
    name: &str,
    role: &str,
    position: [f32; 3],
    vitality: u32,
    dialogue: Option<EntityId>,
) -> Value {
    json!({"id":id,"name":name,"role":role,"position":{"x":position[0],"y":position[1],"z":position[2]},"max_vitality":vitality,"protection":0,"inventory_capacity":12,"dialogue":dialogue,"patrol":[],"flees_danger":false})
}

fn box_glb(color: [f32; 4]) -> Result<Vec<u8>, serde_json::Error> {
    let faces = [
        (
            [
                [0.5, -0.5, -0.5],
                [0.5, 0.5, -0.5],
                [0.5, 0.5, 0.5],
                [0.5, -0.5, 0.5],
            ],
            [1.0, 0.0, 0.0],
        ),
        (
            [
                [-0.5, -0.5, 0.5],
                [-0.5, 0.5, 0.5],
                [-0.5, 0.5, -0.5],
                [-0.5, -0.5, -0.5],
            ],
            [-1.0, 0.0, 0.0],
        ),
        (
            [
                [-0.5, 0.5, -0.5],
                [-0.5, 0.5, 0.5],
                [0.5, 0.5, 0.5],
                [0.5, 0.5, -0.5],
            ],
            [0.0, 1.0, 0.0],
        ),
        (
            [
                [-0.5, -0.5, 0.5],
                [-0.5, -0.5, -0.5],
                [0.5, -0.5, -0.5],
                [0.5, -0.5, 0.5],
            ],
            [0.0, -1.0, 0.0],
        ),
        (
            [
                [0.5, -0.5, 0.5],
                [0.5, 0.5, 0.5],
                [-0.5, 0.5, 0.5],
                [-0.5, -0.5, 0.5],
            ],
            [0.0, 0.0, 1.0],
        ),
        (
            [
                [-0.5, -0.5, -0.5],
                [-0.5, 0.5, -0.5],
                [0.5, 0.5, -0.5],
                [0.5, -0.5, -0.5],
            ],
            [0.0, 0.0, -1.0],
        ),
    ];
    let mut binary = Vec::new();
    for (corners, _) in faces {
        for corner in corners {
            for value in corner {
                binary.extend(f32::to_le_bytes(value));
            }
        }
    }
    for (_, normal) in faces {
        for _ in 0..4 {
            for value in normal {
                binary.extend(f32::to_le_bytes(value));
            }
        }
    }
    for face in 0_u16..6 {
        for local in [0_u16, 1, 2, 0, 2, 3] {
            binary.extend((face * 4 + local).to_le_bytes());
        }
    }
    let mut document = serde_json::to_vec(
        &json!({"asset":{"version":"2.0","generator":"OpenRPG Forge original box recipe v1"},"buffers":[{"byteLength":binary.len()}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":288,"target":34962},{"buffer":0,"byteOffset":288,"byteLength":288,"target":34962},{"buffer":0,"byteOffset":576,"byteLength":72,"target":34963}],"accessors":[{"bufferView":0,"componentType":5126,"count":24,"type":"VEC3","min":[-0.5,-0.5,-0.5],"max":[0.5,0.5,0.5]},{"bufferView":1,"componentType":5126,"count":24,"type":"VEC3"},{"bufferView":2,"componentType":5123,"count":36,"type":"SCALAR"}],"materials":[{"pbrMetallicRoughness":{"baseColorFactor":color,"metallicFactor":0.0,"roughnessFactor":0.65}}],"meshes":[{"primitives":[{"attributes":{"POSITION":0,"NORMAL":1},"indices":2,"material":0}]}],"nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0}),
    )?;
    while !document.len().is_multiple_of(4) {
        document.push(b' ');
    }
    let mut glb = b"glTF".to_vec();
    glb.extend(2_u32.to_le_bytes());
    glb.extend(((28 + document.len() + binary.len()) as u32).to_le_bytes());
    glb.extend((document.len() as u32).to_le_bytes());
    glb.extend(b"JSON");
    glb.extend(document);
    glb.extend((binary.len() as u32).to_le_bytes());
    glb.extend(b"BIN\0");
    glb.extend(binary);
    Ok(glb)
}
