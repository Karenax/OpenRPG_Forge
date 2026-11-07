//! The same module authority supplies vegetation, object and environment resources.
use forge_core::{AreaId, ModuleId, bytes_sha256};
use forge_project::{
    module_package::{
        ModuleEnvironmentBinding, ModuleEnvironmentFiles, ModuleEnvironmentSource, ModuleFile,
        ModuleFilePin, ModuleFileRole, ModuleObjectBinding, ModuleObjectFiles, ModuleObjectSource,
        ModuleTerrainBinding, ModuleTerrainFiles, ModuleTerrainSource,
    },
    workspace::ModuleOriginPin,
};
use forge_studio_frontend::{
    module_resources_acquisition::{Scope, acquire},
    module_resources_protocol::{Chunk, Reply, Request, Snapshot},
};
use std::{
    cell::{Cell, RefCell},
    collections::BTreeMap,
    future::{Future, ready},
    task::{Context, Poll, Waker},
};

#[path = "module_resources_acquisition/companion.rs"]
mod companion;
#[path = "module_resources_acquisition/fish.rs"]
mod fish;
#[path = "module_resources_acquisition/ocean.rs"]
mod ocean;

fn complete<T>(future: impl Future<Output = T>) -> T {
    let mut future = std::pin::pin!(future);
    match future
        .as_mut()
        .poll(&mut Context::from_waker(Waker::noop()))
    {
        Poll::Ready(value) => value,
        Poll::Pending => panic!("Immediate transport unexpectedly waited"),
    }
}

fn scope() -> Scope {
    Scope {
        head: bytes_sha256(b"head"),
        module: ModuleId::deterministic("resources/module"),
        area: AreaId::deterministic("resources/area"),
        origin: Some(ModuleOriginPin {
            package_sha256: bytes_sha256(b"package"),
            receipt_sha256: bytes_sha256(b"receipt"),
            package_bytes: 10,
        }),
        vegetation: None,
        terrain: None,
        imported: false,
        species: false,
    }
}

fn snapshot(scope: Scope) -> Snapshot {
    Snapshot {
        head_hash: scope.head,
        package_sha256: scope.package().unwrap(),
        legacy_reference: false,
        module_id: scope.module,
        area_id: scope.area,
        vegetation: None,
        objects: None,
        fish_profiles: Vec::new(),
        environment: None,
        terrain: None,
        ocean: None,
        local_lights: Vec::new(),
        placement_companions: Vec::new(),
        species: None,
        files: Vec::new(),
    }
}

fn local_managed_state() -> forge_project::authoring::AuthoringState {
    let document =
        forge_project::authoring::new_terrain("resources", "local", "Local terrain").unwrap();
    let session = forge_project::zone_authoring::session::ZoneEditSession::from_initial_terrain(
        document.clone(),
    )
    .unwrap();
    serde_json::from_value(serde_json::json!({
        "module_creator_context": {
            "module_id":scope().module,"name":"Local module",
            "workspace_hash":bytes_sha256(b"local workspace"),"module_hash":bytes_sha256(b"local module"),
            "definition":null,"can_undo":false,"can_redo":false
        },
        "gameplay_module_id":scope().module,"zone_head":session.head(),"document":document,
        "dirty":false,"receipt":null,"playtest_hash":null
    })).unwrap()
}

#[test]
fn a_local_managed_module_owns_its_empty_resources_without_inventing_a_package() {
    let mut state = local_managed_state();
    assert!(Scope::owns_source(&state));
    let local = Scope::from_state(&state).unwrap().unwrap();
    assert!(local.origin.is_none() && local.package().is_none());
    assert_eq!(local.module, state.gameplay_module_id.unwrap());
    assert_eq!(
        local.area,
        state.zone_head.as_ref().unwrap().base.terrain.area_id
    );
    let calls = Cell::new(0);
    assert!(complete(acquire(local, None, |request| {
        assert!(matches!(request, Request::Describe { expected_head } if expected_head == local.head));
        calls.set(calls.get()+1);
        ready(Ok(Reply::Description { snapshot: None }))
    }, || true)).unwrap().is_none());
    assert_eq!(calls.get(), 1);
    state.zone_head = None;
    assert!(Scope::owns_source(&state));
    assert!(Scope::from_state(&state).is_err());
    state.module_creator_context = None;
    assert!(!Scope::owns_source(&state));
    assert!(Scope::from_state(&state).unwrap().is_none());
}

#[test]
fn a_local_module_refuses_a_received_package_before_any_payload_read() {
    let received = scope();
    let local = Scope {
        origin: None,
        ..received
    };
    let calls = Cell::new(0);
    assert!(
        complete(acquire(
            local,
            None,
            |request| {
                assert!(matches!(request, Request::Describe { .. }));
                calls.set(calls.get() + 1);
                ready(Ok(Reply::Description {
                    snapshot: Some(Box::new(snapshot(received))),
                }))
            },
            || true
        ))
        .is_err()
    );
    assert_eq!(calls.get(), 1);
    assert_ne!(local.scene(), received.scene());
    assert_ne!(
        local.scene(),
        Scope {
            module: ModuleId::deterministic("other local module"),
            ..local
        }
        .scene()
    );
    assert_ne!(
        local.scene(),
        Scope {
            area: AreaId::deterministic("other local area"),
            ..local
        }
        .scene()
    );
}

#[test]
fn a_managed_context_cannot_claim_another_module_identity() {
    let mut state = local_managed_state();
    state.gameplay_module_id = Some(ModuleId::deterministic("foreign module"));
    assert!(Scope::owns_source(&state));
    assert!(Scope::from_state(&state).is_err());
}

#[test]
fn absent_render_families_do_not_read_a_reference_library() {
    let calls = Cell::new(0);
    let data = complete(acquire(
        scope(),
        None,
        |request| {
            assert!(matches!(request, Request::Describe { .. }));
            calls.set(calls.get() + 1);
            ready(Ok(Reply::Description {
                snapshot: Some(Box::new(snapshot(scope()))),
            }))
        },
        || true,
    ))
    .unwrap()
    .unwrap();
    assert!(data.imported.is_none() && data.objects.is_none());
    assert!(data.environment.is_none() && data.programs.is_none());
    assert!(data.terrain.is_none() && data.terrain_index.is_none());
    assert!(data.local_lights.is_empty());
    assert_eq!(calls.get(), 1);
}

#[test]
fn foreign_object_and_environment_bindings_are_refused_before_any_payload() {
    let scope = scope();
    let foreign = AreaId::deterministic("resources/foreign");
    let index = ModuleFilePin {
        path: "source/manifest.json".into(),
        sha256: bytes_sha256(b"index"),
    };
    for family in 0..5 {
        let mut description = snapshot(scope);
        if family == 0 {
            description.objects = Some(ModuleObjectBinding {
                area_id: foreign,
                source: ModuleObjectSource::SourceSurfaces {
                    package: ModuleObjectFiles {
                        manifest: index.clone(),
                        resources: Vec::new(),
                    },
                    assets: Vec::new(),
                },
            });
        } else if family == 1 {
            description.environment = Some(ModuleEnvironmentBinding {
                area_id: foreign,
                source: ModuleEnvironmentSource::SourceProfile {
                    environment: ModuleEnvironmentFiles {
                        manifest: index.clone(),
                        resources: Vec::new(),
                    },
                    programs: None,
                },
                solar_clock: None,
            });
        } else if family == 2 {
            description
                .local_lights
                .push(forge_project::module_package::ModuleLocalLightPackage {
                    areas: vec![foreign],
                    manifest: index.clone(),
                    descriptor_sha256: bytes_sha256(b"descriptor"),
                    resources: Vec::new(),
                });
        } else {
            description.terrain = Some(ModuleTerrainBinding {
                area_id: if family == 3 { foreign } else { scope.area },
                source: ModuleTerrainSource::SourceRaster {
                    source_descriptor_sha256: bytes_sha256(b"foreign terrain"),
                    package: ModuleTerrainFiles {
                        manifest: index.clone(),
                        resources: Vec::new(),
                    },
                },
            });
        }
        let calls = Cell::new(0);
        let result = complete(acquire(
            scope,
            None,
            |request| {
                assert!(matches!(request, Request::Describe { .. }));
                calls.set(calls.get() + 1);
                ready(Ok(Reply::Description {
                    snapshot: Some(Box::new(description.clone())),
                }))
            },
            || true,
        ));
        assert!(result.err().unwrap().contains("canonical scope"));
        assert_eq!(calls.get(), 1);
    }
}

#[test]
fn terrain_inventory_refuses_duplicates_roles_and_budget_before_payload_transport() {
    let mut scope = scope();
    scope.terrain = Some(bytes_sha256(b"terrain descriptor"));
    let index = ModuleFilePin {
        path: "terrain/package.json".into(),
        sha256: bytes_sha256(b"index"),
    };
    let resource = ModuleFilePin {
        path: "terrain/terrain-splat.bin".into(),
        sha256: bytes_sha256(b"splat"),
    };
    for mode in 0..4 {
        let mut description = snapshot(scope);
        let mut resources = vec![resource.clone()];
        description.files = vec![
            ModuleFile {
                path: index.path.clone(),
                sha256: index.sha256,
                bytes: 10,
                role: ModuleFileRole::TerrainSurface,
            },
            ModuleFile {
                path: resource.path.clone(),
                sha256: resource.sha256,
                bytes: 10,
                role: ModuleFileRole::TerrainSurface,
            },
        ];
        match mode {
            0 => resources.push(resource.clone()),
            1 => description.files[1].role = ModuleFileRole::Asset,
            2 => description.files[1].bytes = 513 * 1024 * 1024,
            _ => description.files.push(description.files[1].clone()),
        }
        description.terrain = Some(ModuleTerrainBinding {
            area_id: scope.area,
            source: ModuleTerrainSource::SourceRaster {
                source_descriptor_sha256: scope.terrain.unwrap(),
                package: ModuleTerrainFiles {
                    manifest: index.clone(),
                    resources,
                },
            },
        });
        let calls = Cell::new(0);
        assert!(
            complete(acquire(
                scope,
                None,
                |request| {
                    assert!(matches!(request, Request::Describe { .. }));
                    calls.set(calls.get() + 1);
                    ready(Ok(Reply::Description {
                        snapshot: Some(Box::new(description.clone())),
                    }))
                },
                || true
            ))
            .is_err()
        );
        assert_eq!(calls.get(), 1);
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "One complete wire exchange checks both licence admission and refusal before texture reads"
)]
fn data_only_terrain_requires_its_own_licence_role_before_payload_reads() {
    let mut scope = scope();
    scope.terrain = Some(bytes_sha256(b"surface descriptor"));
    let payloads = BTreeMap::from([
        ("terrain-normal.bin", vec![1_u8; 20]),
        ("terrain-splat.bin", vec![2_u8; 4]),
        ("terrain-detail.bin", vec![3_u8; 4]),
        ("LICENSE.txt", b"test licence".to_vec()),
    ]);
    let pins: serde_json::Map<_, _> = payloads
        .iter()
        .map(|(name, bytes)| {
            (
                (*name).to_owned(),
                serde_json::json!({
                    "bytes": bytes.len(), "sha256": bytes_sha256(bytes)
                }),
            )
        })
        .collect();
    let index = serde_json::to_vec(&serde_json::json!({
        "schema":"forge.terrain-surface-package", "version":1,
        "raster":{"width":2,"height":2,"origin_xz_m":[-1.0,-1.0],
            "sample_spacing_m":1.0,"outside_height_m":-10.0,
            "encoding":"float32_le","extent_m":[2.0,2.0]},
        "textures":{"normal":[2,2],"splat":[1,1],"detail":[1,1]},
        "files":pins
    }))
    .unwrap();
    for licence_role in [ModuleFileRole::License, ModuleFileRole::TerrainSurface] {
        let mut description = snapshot(scope);
        let mut bytes = BTreeMap::from([("terrain/package.json".to_owned(), index.clone())]);
        let mut resources = Vec::new();
        for (name, payload) in &payloads {
            let path = format!("terrain/{name}");
            let hash = bytes_sha256(payload);
            resources.push(ModuleFilePin {
                path: path.clone(),
                sha256: hash,
            });
            description.files.push(ModuleFile {
                path: path.clone(),
                sha256: hash,
                bytes: payload.len() as u64,
                role: if *name == "LICENSE.txt" {
                    licence_role
                } else {
                    ModuleFileRole::TerrainSurface
                },
            });
            bytes.insert(path, payload.clone());
        }
        let manifest = ModuleFilePin {
            path: "terrain/package.json".into(),
            sha256: bytes_sha256(&index),
        };
        description.files.push(ModuleFile {
            path: manifest.path.clone(),
            sha256: manifest.sha256,
            bytes: index.len() as u64,
            role: ModuleFileRole::TerrainSurface,
        });
        description.terrain = Some(ModuleTerrainBinding {
            area_id: scope.area,
            source: ModuleTerrainSource::SourceRaster {
                source_descriptor_sha256: scope.terrain.unwrap(),
                package: ModuleTerrainFiles {
                    manifest,
                    resources,
                },
            },
        });
        let reads = RefCell::new(Vec::new());
        let result = complete(acquire(
            scope,
            None,
            |request| {
                let reply = match request {
                    Request::Describe { .. } => Reply::Description {
                        snapshot: Some(Box::new(description.clone())),
                    },
                    Request::Read {
                        resource_index,
                        offset,
                        ..
                    } => {
                        let file = &description.files[resource_index];
                        let data = &bytes[&file.path];
                        assert_eq!(offset, 0);
                        reads.borrow_mut().push(file.path.clone());
                        Reply::Resource {
                            chunk: Chunk {
                                head_hash: scope.head,
                                package_sha256: scope.package().unwrap(),
                                resource_index,
                                resource_sha256: file.sha256,
                                total_bytes: data.len(),
                                offset,
                                bytes: data.clone(),
                            },
                        }
                    }
                };
                ready(Ok(reply))
            },
            || true,
        ));
        if licence_role == ModuleFileRole::License {
            let acquired = result.unwrap().unwrap();
            assert_eq!(acquired.terrain.unwrap().file_count(), 4);
            assert_eq!(reads.borrow().len(), 5);
        } else {
            assert!(result.is_err());
            assert_eq!(&*reads.borrow(), &["terrain/package.json"]);
        }
    }
}

#[cfg(not(target_arch = "wasm32"))]
#[path = "module_resources_acquisition/private.rs"]
mod private;

#[cfg(not(target_arch = "wasm32"))]
#[path = "module_resources_acquisition/terrain_private.rs"]
mod terrain_private;
