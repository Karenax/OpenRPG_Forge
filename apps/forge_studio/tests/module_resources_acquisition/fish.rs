use super::*;
use forge_assets::{
    AssetCatalog, AssetKind, AssetRecord, AssetSource,
    source_fish_payload::{SourceFishPayload, SourceFishProfile},
};
use forge_core::AssetId;
use forge_project::module_package::{ModuleFishBinding, ModuleObjectAsset};
use std::rc::Rc;

fn fixture(scope: Scope) -> (Snapshot, BTreeMap<String, Vec<u8>>) {
    let mut snapshot = snapshot(scope);
    let description = br#"{"prototypes":[{}],"instances":[{"source_id":"other/fish"}]}"#.to_vec();
    let resources = [
        ("geometry/fish.json", description),
        ("geometry/fish.bin", b"fish geometry".to_vec()),
        ("LICENSE.txt", b"test license".to_vec()),
        ("source/original.js", b"historical source only".to_vec()),
    ];
    let indexed: BTreeMap<_, _> = resources
        .iter()
        .map(|(name, bytes)| {
            (
                *name,
                serde_json::json!({"bytes":bytes.len(),"sha256":bytes_sha256(bytes)}),
            )
        })
        .collect();
    let license = &resources[2].1;
    let index = serde_json::to_vec(&serde_json::json!({
        "files":indexed,
        "license":{"file":"LICENSE.txt","bytes":license.len(),"sha256":bytes_sha256(license)}
    }))
    .unwrap();
    let hash = bytes_sha256(&index);
    let id = AssetId::deterministic("other/fish");
    let catalog = serde_json::to_vec(&AssetCatalog {
        schema_version: 1,
        assets: vec![AssetRecord {
            id,
            logical_name: "other/fish".into(),
            kind: AssetKind::Mesh,
            source: AssetSource::External {
                uri: "https://example.invalid/fish".into(),
                declared_license: Some("MIT".into()),
            },
            content_hash: hash,
        }],
    })
    .unwrap();
    let mut payloads = BTreeMap::new();
    let mut add = |name: &str, bytes: Vec<u8>, role: ModuleFileRole| {
        let path = format!("fish/profile/{name}");
        let pin = ModuleFilePin {
            path: path.clone(),
            sha256: bytes_sha256(&bytes),
        };
        snapshot.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256: pin.sha256,
            role,
        });
        payloads.insert(path, bytes);
        pin
    };
    let manifest = add("package.json", index, ModuleFileRole::ObjectSource);
    let catalog = add("catalog.json", catalog, ModuleFileRole::ObjectSource);
    let pins = resources
        .into_iter()
        .filter(|(name, _)| *name != "source/original.js")
        .map(|(name, bytes)| {
            let role = if name == "LICENSE.txt" {
                ModuleFileRole::License
            } else {
                ModuleFileRole::ObjectSource
            };
            add(name, bytes, role)
        })
        .collect();
    snapshot.fish_profiles.push(ModuleFishBinding {
        area_id: scope.area,
        package: ModuleObjectFiles {
            manifest,
            resources: pins,
        },
        catalog,
        excluded_source_scripts: vec!["source/original.js".into()],
        description: "geometry/fish.json".into(),
        prototypes: 1,
        instances: 1,
        assets: vec![ModuleObjectAsset {
            asset_id: id,
            sha256: hash,
        }],
    });
    (snapshot, payloads)
}

fn reply(
    snapshot: &Snapshot,
    payloads: &BTreeMap<String, Vec<u8>>,
    request: Request,
) -> Result<Reply, String> {
    match request {
        Request::Describe { expected_head } if expected_head == snapshot.head_hash => {
            Ok(Reply::Description {
                snapshot: Some(Box::new(snapshot.clone())),
            })
        }
        Request::Read {
            expected_head,
            expected_package,
            resource_index,
            offset,
        } if expected_head == snapshot.head_hash && expected_package == snapshot.package_sha256 => {
            let file = snapshot.files.get(resource_index).ok_or("missing file")?;
            let bytes = payloads.get(&file.path).ok_or("missing payload")?;
            let end = (offset + forge_studio_frontend::module_resources_protocol::CHUNK_BYTES)
                .min(bytes.len());
            if offset >= end {
                return Err("invalid offset".into());
            }
            Ok(Reply::Resource {
                chunk: Chunk {
                    head_hash: snapshot.head_hash,
                    package_sha256: snapshot.package_sha256,
                    resource_index,
                    resource_sha256: file.sha256,
                    total_bytes: bytes.len(),
                    offset,
                    bytes: bytes[offset..end].to_vec(),
                },
            })
        }
        _ => Err("unexpected request".into()),
    }
}

#[test]
fn profiles_are_checked_once_and_reused_for_a_new_head_of_the_same_module() {
    let initial = scope();
    let (snapshot, payloads) = fixture(initial);
    let reads = Cell::new(0);
    let acquired = Rc::new(
        complete(acquire(
            initial,
            None,
            |request| {
                if matches!(request, Request::Read { .. }) {
                    reads.set(reads.get() + 1);
                }
                ready(reply(&snapshot, &payloads, request))
            },
            || true,
        ))
        .unwrap()
        .unwrap(),
    );
    assert!(reads.get() > 0);
    assert_eq!(acquired.fish_profiles.len(), 1);
    assert!(
        acquired.fish_profiles[0]
            .payload
            .file("source/original.js")
            .is_none()
    );
    assert_eq!(
        acquired.fish_profiles[0]
            .profile()
            .unwrap()
            .identify(0, "other/fish")
            .unwrap(),
        Some(snapshot.fish_profiles[0].assets[0].asset_id)
    );

    let next = Scope {
        head: bytes_sha256(b"edited head"),
        ..initial
    };
    let mut next_snapshot = snapshot;
    next_snapshot.head_hash = next.head;
    let next_data = complete(acquire(
        next,
        Some(acquired.clone()),
        |request| {
            assert!(matches!(request, Request::Describe { .. }));
            ready(reply(&next_snapshot, &payloads, request))
        },
        || true,
    ))
    .unwrap()
    .unwrap();
    assert!(Rc::ptr_eq(
        &acquired.fish_profiles[0],
        &next_data.fish_profiles[0]
    ));

    let mut drifted = next_snapshot.clone();
    drifted
        .files
        .iter_mut()
        .find(|file| file.path == "fish/profile/geometry/fish.bin")
        .unwrap()
        .bytes += 1;
    let reads = Cell::new(0);
    assert!(
        complete(acquire(
            next,
            Some(acquired.clone()),
            |request| {
                if matches!(request, Request::Read { .. }) {
                    reads.set(reads.get() + 1);
                }
                ready(reply(&drifted, &payloads, request))
            },
            || true
        ))
        .is_err()
    );
    assert!(reads.get() > 0);

    assert_foreign_module_reloads(next, next_snapshot, &payloads, &acquired);
}

fn assert_foreign_module_reloads(
    next: Scope,
    next_snapshot: Snapshot,
    payloads: &BTreeMap<String, Vec<u8>>,
    acquired: &Rc<forge_studio_frontend::module_resources_acquisition::Acquired>,
) {
    let foreign = Scope {
        module: ModuleId::deterministic("other module"),
        ..next
    };
    let mut foreign_snapshot = next_snapshot;
    foreign_snapshot.module_id = foreign.module;
    let reads = Cell::new(0);
    let foreign_data = complete(acquire(
        foreign,
        Some(Rc::clone(acquired)),
        |request| {
            if matches!(request, Request::Read { .. }) {
                reads.set(reads.get() + 1);
            }
            ready(reply(&foreign_snapshot, payloads, request))
        },
        || true,
    ))
    .unwrap()
    .unwrap();
    assert!(reads.get() > 0);
    assert!(!Rc::ptr_eq(
        &acquired.fish_profiles[0],
        &foreign_data.fish_profiles[0]
    ));
}

#[test]
fn altered_or_foreign_fish_resources_are_refused() {
    let scope = scope();
    let (snapshot, mut payloads) = fixture(scope);
    payloads.get_mut("fish/profile/geometry/fish.bin").unwrap()[0] ^= 1;
    assert!(
        complete(acquire(
            scope,
            None,
            |request| ready(reply(&snapshot, &payloads, request)),
            || true
        ))
        .is_err()
    );

    let mut foreign = snapshot;
    foreign.fish_profiles[0].area_id = AreaId::deterministic("foreign fish area");
    let calls = Cell::new(0);
    assert!(
        complete(acquire(
            scope,
            None,
            |request| {
                calls.set(calls.get() + 1);
                assert!(matches!(request, Request::Describe { .. }));
                ready(reply(&foreign, &payloads, request))
            },
            || true
        ))
        .is_err()
    );
    assert_eq!(calls.get(), 1);
}

#[test]
fn incomplete_or_ambiguous_fish_inventory_fails_before_resource_reads() {
    let scope = scope();
    let (snapshot, payloads) = fixture(scope);
    for mode in 0..3 {
        let mut invalid = snapshot.clone();
        match mode {
            0 => invalid
                .files
                .retain(|file| file.path != "fish/profile/geometry/fish.bin"),
            1 => invalid.files.push(invalid.files.last().unwrap().clone()),
            _ => {
                invalid
                    .files
                    .iter_mut()
                    .find(|file| file.path == "fish/profile/geometry/fish.bin")
                    .unwrap()
                    .role = ModuleFileRole::AssetAuxiliary;
            }
        }
        let calls = Cell::new(0);
        assert!(
            complete(acquire(
                scope,
                None,
                |request| {
                    calls.set(calls.get() + 1);
                    assert!(matches!(request, Request::Describe { .. }));
                    ready(reply(&invalid, &payloads, request))
                },
                || true
            ))
            .is_err()
        );
        assert_eq!(calls.get(), 1);
    }
    let mut invalid_license = snapshot;
    invalid_license
        .files
        .iter_mut()
        .find(|file| file.path == "fish/profile/LICENSE.txt")
        .unwrap()
        .role = ModuleFileRole::ObjectSource;
    assert!(
        complete(acquire(
            scope,
            None,
            |request| ready(reply(&invalid_license, &payloads, request)),
            || true
        ))
        .is_err()
    );
}

#[test]
fn empty_fish_profiles_preserve_the_existing_wire_shape() {
    let snapshot = snapshot(scope());
    let value = serde_json::to_value(&snapshot).unwrap();
    assert!(value.get("fish_profiles").is_none());
    let restored: Snapshot = serde_json::from_value(value).unwrap();
    assert!(restored.fish_profiles.is_empty());
}

fn source_catalog(
    instances: &[serde_json::Value],
    first: usize,
    hash: forge_core::ContentHash,
) -> AssetCatalog {
    AssetCatalog {
        schema_version: 1,
        assets: instances[first..]
            .iter()
            .map(|instance| {
                let source_id = instance["source_id"].as_str().unwrap();
                AssetRecord {
                    id: AssetId::deterministic(source_id),
                    logical_name: source_id.into(),
                    kind: AssetKind::Mesh,
                    source: AssetSource::External {
                        uri: "https://example.invalid/source".into(),
                        declared_license: Some("MIT".into()),
                    },
                    content_hash: hash,
                }
            })
            .collect(),
    }
}

fn collect_fish_resources(
    root: &std::path::Path,
    index: &forge_assets::source_fish_package::ResourceIndex,
    snapshot: &mut Snapshot,
    payloads: &mut BTreeMap<String, Vec<u8>>,
) -> (Vec<String>, Vec<ModuleFilePin>) {
    let mut resources = Vec::new();
    let excluded_source_scripts: Vec<_> = index
        .files
        .keys()
        .filter(|name| {
            name.rsplit_once('.')
                .is_some_and(|(_, extension)| extension == "js")
        })
        .cloned()
        .collect();
    assert!(!excluded_source_scripts.is_empty());
    for (name, pin) in &index.files {
        let bytes = std::fs::read(root.join(name)).unwrap();
        assert_eq!((bytes.len(), bytes_sha256(&bytes)), (pin.bytes, pin.sha256));
        if excluded_source_scripts.binary_search(name).is_ok() {
            continue;
        }
        let path = format!("fish/profile/{name}");
        snapshot.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256: pin.sha256,
            role: if *name == index.license {
                ModuleFileRole::License
            } else {
                ModuleFileRole::ObjectSource
            },
        });
        resources.push(ModuleFilePin {
            path: path.clone(),
            sha256: pin.sha256,
        });
        payloads.insert(path, bytes);
    }
    (excluded_source_scripts, resources)
}

fn real_profile(variable: &str, expected: &str, description: &str, first: usize, total: usize) {
    let root =
        std::path::PathBuf::from(std::env::var(variable).expect("Private fish package required"));
    let manifest = std::fs::read(root.join("package.json")).unwrap();
    let hash: forge_core::ContentHash = expected.parse().unwrap();
    let index = forge_assets::source_fish_package::resource_index(&manifest, hash).unwrap();
    let current = scope();
    let mut snapshot = snapshot(current);
    let mut payloads = BTreeMap::new();
    let (excluded_source_scripts, resources) =
        collect_fish_resources(&root, &index, &mut snapshot, &mut payloads);
    let document: serde_json::Value =
        serde_json::from_slice(&payloads[&format!("fish/profile/{description}")]).unwrap();
    let instances = document["instances"].as_array().unwrap();
    assert_eq!(instances.len(), total);
    let catalog = source_catalog(instances, first, hash);
    let mut assets: Vec<_> = catalog
        .assets
        .iter()
        .map(|asset| ModuleObjectAsset {
            asset_id: asset.id,
            sha256: asset.content_hash,
        })
        .collect();
    assets.sort_by_key(|asset| asset.asset_id);
    let catalog = serde_json::to_vec(&catalog).unwrap();
    let catalog_path = "fish/profile/catalog.json".to_owned();
    let catalog_pin = ModuleFilePin {
        path: catalog_path.clone(),
        sha256: bytes_sha256(&catalog),
    };
    snapshot.files.push(ModuleFile {
        path: catalog_path.clone(),
        bytes: catalog.len() as u64,
        sha256: catalog_pin.sha256,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(catalog_path, catalog);
    let manifest_path = "fish/profile/package.json".to_owned();
    snapshot.files.push(ModuleFile {
        path: manifest_path.clone(),
        bytes: manifest.len() as u64,
        sha256: hash,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(manifest_path.clone(), manifest);
    snapshot.fish_profiles.push(ModuleFishBinding {
        area_id: current.area,
        package: ModuleObjectFiles {
            manifest: ModuleFilePin {
                path: manifest_path,
                sha256: hash,
            },
            resources,
        },
        catalog: catalog_pin,
        excluded_source_scripts,
        description: description.into(),
        prototypes: 18,
        instances: u32::try_from(total).unwrap(),
        assets,
    });
    let acquired = complete(acquire(
        current,
        None,
        |request| ready(reply(&snapshot, &payloads, request)),
        || true,
    ))
    .unwrap()
    .unwrap();
    let profile = acquired.fish_profiles[0].profile().unwrap();
    for (ordinal, instance) in instances.iter().enumerate() {
        let source_id = instance["source_id"].as_str().unwrap();
        assert_eq!(
            profile.identify(ordinal, source_id).unwrap(),
            (ordinal >= first).then(|| AssetId::deterministic(source_id))
        );
    }
    // Only standalone profiles declare draw/shadow distances. Attachments such
    // as game fish require a separate host and must not receive invented LODs.
    #[cfg(feature = "scene-host-tests")]
    if document.get("draw_range_m").is_some() {
        assert_standalone_preparation(&acquired);
    }
}

#[cfg(feature = "scene-host-tests")]
fn assert_standalone_preparation(
    acquired: &forge_studio_frontend::module_resources_acquisition::Acquired,
) {
    use forge_engine::source_fish::{host::scene, surface::SpecularMode};
    use forge_project::zone_authoring::placements::ManualPlacement;

    let profile = acquired.fish_profiles[0].profile().unwrap();
    assert_eq!(
        scene::profile_policy(&profile).unwrap(),
        forge_engine::distance_lod::Policy::new(90.0, 81.0, 22.0, 28.0, 0.88).unwrap()
    );
    let record = &acquired.fish_profiles[0].catalog.assets[0];
    let placement = ManualPlacement {
        id: forge_core::EntityId::deterministic("fish-acquisition/placement"),
        name: "fish".into(),
        asset_id: record.id,
        asset_hash: record.content_hash,
        transform: forge_core::Transform::IDENTITY,
        locked: false,
        particles: None,
        physics: None,
        local_lights: None,
    };
    let mut unrelated = placement.clone();
    unrelated.id = forge_core::EntityId::deterministic("fish-acquisition/other-placement");
    unrelated.asset_id = AssetId::deterministic("fish-acquisition/other-asset");
    let selected = scene::select_placements(
        acquired.fish_profiles.iter().map(AsRef::as_ref),
        &[unrelated, placement.clone()],
    )
    .unwrap();
    assert_eq!(selected, vec![placement.clone()]);
    let prepared = scene::from_catalogs(
        acquired.fish_profiles.iter().map(AsRef::as_ref),
        &selected,
        forge_engine::source_surface::PROFILE_PROGRAM,
        SpecularMode::ThreeReference,
    )
    .unwrap()
    .unwrap();
    assert_eq!(prepared.placements(), std::slice::from_ref(&placement));
    let mut changed = placement;
    changed.asset_hash = bytes_sha256(b"different profile");
    assert!(
        scene::select_placements(
            acquired.fish_profiles.iter().map(AsRef::as_ref),
            std::slice::from_ref(&changed),
        )
        .is_err()
    );
    assert!(
        scene::from_catalogs(
            acquired.fish_profiles.iter().map(AsRef::as_ref),
            &[changed],
            forge_engine::source_surface::PROFILE_PROGRAM,
            SpecularMode::ThreeReference,
        )
        .is_err()
    );
    assert_different_policy_profiles(acquired);
}

#[cfg(feature = "scene-host-tests")]
fn assert_different_policy_profiles(
    acquired: &forge_studio_frontend::module_resources_acquisition::Acquired,
) {
    use forge_assets::{
        source_fish_package::PinnedFishPayload,
        source_fish_payload::{OwnedFishProfile, SourceFishPayload},
    };
    use forge_engine::source_fish::{host::scene, surface::SpecularMode};
    use forge_project::zone_authoring::placements::ManualPlacement;

    struct Variant<'a> {
        source: &'a PinnedFishPayload,
        description: Vec<u8>,
    }
    impl SourceFishPayload for Variant<'_> {
        fn file(&self, name: &str) -> Option<&[u8]> {
            if name == "geometry/fish-props.json" {
                Some(&self.description)
            } else {
                self.source.file(name)
            }
        }
    }

    // The source bytes are real, while the two in-memory descriptions and pins
    // are test variants. This does not claim a second package was admitted.
    let source = &acquired.fish_profiles[0];
    let description = source.profile().unwrap().description().to_vec();
    let mut variant: serde_json::Value = serde_json::from_slice(&description).unwrap();
    variant["draw_range_m"] = serde_json::json!(30.0);
    variant["shadow_range_m"] = serde_json::json!(6.0);
    let descriptions = [description, serde_json::to_vec(&variant).unwrap()];
    let mut profiles = Vec::new();
    let mut placements = Vec::new();
    for (ordinal, description) in descriptions.into_iter().enumerate() {
        let pin = bytes_sha256(format!("independent test profile {ordinal}").as_bytes());
        let mut asset = source.catalog.assets[ordinal].clone();
        asset.content_hash = pin;
        placements.push(ManualPlacement {
            id: forge_core::EntityId::deterministic(format!("independent test fish {ordinal}")),
            name: format!("fish {ordinal}"),
            asset_id: asset.id,
            asset_hash: pin,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
            particles: None,
            physics: None,
            local_lights: None,
        });
        profiles.push(
            OwnedFishProfile::new(
                pin,
                Variant {
                    source: &source.payload,
                    description,
                },
                AssetCatalog {
                    schema_version: source.catalog.schema_version,
                    assets: vec![asset],
                },
                "geometry/fish-props.json".into(),
                (18, 78),
            )
            .unwrap(),
        );
    }
    placements.sort_by_key(|placement| placement.id);
    let prepared = scene::from_catalogs(
        profiles.iter(),
        &placements,
        forge_engine::source_surface::PROFILE_PROGRAM,
        SpecularMode::ThreeReference,
    )
    .unwrap()
    .unwrap();
    assert_eq!(prepared.placements(), placements);
}

#[test]
#[ignore = "requires the byte-pinned private village fish package"]
fn real_village_profile_passes_studio_resource_transport() {
    real_profile(
        "FORGE_TIDEWATER_FISH_PROP_PACKAGE",
        "e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a",
        "geometry/fish-props.json",
        0,
        78,
    );
}

#[test]
#[ignore = "requires the byte-pinned private game fish package"]
fn real_game_profile_passes_studio_resource_transport() {
    real_profile(
        "FORGE_TIDEWATER_GAME_FISH_PACKAGE",
        "f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d",
        "geometry/game-fish.json",
        18,
        23,
    );
}
