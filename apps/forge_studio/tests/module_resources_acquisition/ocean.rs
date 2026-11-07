use super::*;
use forge_project::module_package::{ModuleOceanBinding, ModuleOceanFiles};

#[test]
fn foreign_or_incomplete_ocean_never_reads_payloads() {
    for foreign in [true, false] {
        let scope = scope();
        let mut snapshot = snapshot(scope);
        let package = ModuleOceanFiles {
            manifest: ModuleFilePin {
                path: "water/index.json".into(),
                sha256: bytes_sha256(b"index"),
            },
            resources: vec![],
        };
        snapshot.ocean = Some(ModuleOceanBinding {
            area_id: if foreign {
                AreaId::deterministic("foreign ocean")
            } else {
                scope.area
            },
            package: package.clone(),
            surface: Some(package),
            shore_sim: None,
        });
        let calls = Cell::new(0);
        let result = complete(acquire(
            scope,
            None,
            |request| {
                assert!(matches!(request, Request::Describe { .. }));
                calls.set(calls.get() + 1);
                ready(Ok(Reply::Description {
                    snapshot: Some(Box::new(snapshot.clone())),
                }))
            },
            || true,
        ));
        assert!(result.is_err());
        assert_eq!(calls.get(), 1);
    }
}

#[test]
fn source_index_closure_requires_exact_roles_hashes_sizes_and_licence() {
    use forge_project::module_package::ocean::close_resources;
    let hash = bytes_sha256(b"ocean input");
    let original = vec![
        ModuleFile {
            path: "water/input.bin".into(),
            bytes: 7,
            sha256: hash,
            role: ModuleFileRole::OceanSimulation,
        },
        ModuleFile {
            path: "water/LICENSE.txt".into(),
            bytes: 3,
            sha256: hash,
            role: ModuleFileRole::License,
        },
    ];
    let original_package = ModuleOceanFiles {
        manifest: ModuleFilePin {
            path: "water/index.json".into(),
            sha256: hash,
        },
        resources: original
            .iter()
            .map(|file| ModuleFilePin {
                path: file.path.clone(),
                sha256: file.sha256,
            })
            .collect(),
    };
    for fault in 0..7 {
        let mut files = original.clone();
        let mut package = original_package.clone();
        match fault {
            0 => {}
            1 => files[0].role = ModuleFileRole::License,
            2 => files[1].role = ModuleFileRole::OceanSimulation,
            3 => files[0].bytes += 1,
            4 => files[0].sha256 = bytes_sha256(b"foreign input"),
            5 => package.resources.push(package.resources[0].clone()),
            _ => {
                package.resources.pop();
            }
        }
        let files = files
            .iter()
            .map(|file| (file.path.as_str(), file))
            .collect();
        let result = close_resources(
            &files,
            &package,
            [
                ("input.bin".into(), hash, 7),
                ("LICENSE.txt".into(), hash, 3),
            ]
            .into_iter(),
            ModuleFileRole::OceanSimulation,
            "LICENSE.txt",
        );
        assert_eq!(result.is_ok(), fault == 0, "fault {fault}");
    }
}

#[test]
#[ignore = "Requires the explicitly pinned independent dense module, read sequentially without creating a workspace"]
#[allow(
    clippy::too_many_lines,
    reason = "One closed real module exercises native restoration, async codecs and cache revocation together"
)]
fn private_ocean_uses_native_closure_codecs_and_invalidates_changed_inventory() {
    use forge_studio_frontend::module_resources_acquisition::Acquired;
    use std::{path::PathBuf, rc::Rc, sync::Arc};
    let path = PathBuf::from(std::env::var_os("FORGE_OCEAN_RESOURCES_MODULE").unwrap());
    assert!(path.is_absolute());
    let bytes: Arc<[u8]> = std::fs::read(path).unwrap().into();
    let expected = std::env::var("FORGE_OCEAN_RESOURCES_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    assert_eq!(bytes_sha256(&bytes), expected);
    let area: AreaId = std::env::var("FORGE_OCEAN_RESOURCES_AREA")
        .unwrap()
        .parse()
        .unwrap();
    let origin = forge_delivery::orpmod::read_origin_documents(Arc::clone(&bytes)).unwrap();
    let archive = origin
        .module
        .zones
        .iter()
        .find(|zone| zone.area_id == area)
        .unwrap();
    let archive = origin
        .read_payload(
            &archive.archive_path,
            forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES,
        )
        .unwrap();
    let zone =
        forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(&archive)
            .unwrap();
    let snapshot =
        forge_studio_frontend::module_resources_native::restore(&origin, expected, &zone)
            .unwrap()
            .unwrap();
    assert!(snapshot.ocean.is_some());
    let scope = Scope {
        head: snapshot.head_hash,
        area,
        module: snapshot.module_id,
        origin: Some(ModuleOriginPin {
            package_sha256: expected,
            package_bytes: bytes.len() as u64,
            receipt_sha256: bytes_sha256(b"qualification read-only origin"),
        }),
        terrain: snapshot.terrain.as_ref().map(|binding| {
            let ModuleTerrainSource::SourceRaster {
                source_descriptor_sha256,
                ..
            } = binding.source;
            source_descriptor_sha256
        }),
        vegetation: snapshot
            .vegetation
            .as_ref()
            .map(|binding| binding.vegetation_sha256),
        imported: true,
        species: snapshot.species.is_some(),
    };
    let payloads = RefCell::new(BTreeMap::<usize, Vec<u8>>::new());
    let calls = Cell::new(0_usize);
    let load = |snapshot: &Snapshot, previous: Option<Rc<Acquired>>| {
        complete(acquire(
            scope,
            previous,
            |request| {
                calls.set(calls.get() + 1);
                ready(match request {
                    Request::Describe { .. } => Ok(Reply::Description {
                        snapshot: Some(Box::new(snapshot.clone())),
                    }),
                    Request::Read {
                        resource_index,
                        offset,
                        ..
                    } => {
                        let file = &snapshot.files[resource_index];
                        let mut payloads = payloads.borrow_mut();
                        let payload = payloads.entry(resource_index).or_insert_with(|| {
                            origin
                                .read_payload(&file.path, file.bytes.try_into().unwrap())
                                .unwrap()
                        });
                        let end = (offset
                            + forge_studio_frontend::module_resources_protocol::CHUNK_BYTES)
                            .min(payload.len());
                        Ok(Reply::Resource {
                            chunk: Chunk {
                                head_hash: scope.head,
                                package_sha256: expected,
                                resource_index,
                                resource_sha256: file.sha256,
                                total_bytes: payload.len(),
                                offset,
                                bytes: payload[offset..end].to_vec(),
                            },
                        })
                    }
                })
            },
            || true,
        ))
    };
    let first = Rc::new(load(&snapshot, None).unwrap().unwrap());
    let ocean = first.ocean.as_ref().unwrap();
    assert!(ocean.surface.is_some() && ocean.shore.is_some());
    let native = forge_delivery::module_ocean::resolve(&origin.module, area, |path, maximum| {
        origin.read_payload(path, maximum)
    })
    .unwrap()
    .unwrap();
    assert_eq!(
        ocean.ocean.file("row.wgsl").unwrap(),
        native.package.file("row.wgsl").unwrap()
    );
    assert_eq!(
        ocean.surface.as_ref().unwrap().file("water-frame.bin"),
        native.surface.as_ref().unwrap().file("water-frame.bin")
    );
    calls.set(0);
    let cached = load(&snapshot, Some(Rc::clone(&first))).unwrap().unwrap();
    assert_eq!(calls.get(), 1);
    assert!(Rc::ptr_eq(ocean, cached.ocean.as_ref().unwrap()));
    let mut changed = snapshot.clone();
    let index = &changed.ocean.as_ref().unwrap().package.manifest.path;
    changed
        .files
        .iter_mut()
        .find(|file| &file.path == index)
        .unwrap()
        .role = ModuleFileRole::License;
    assert!(load(&changed, Some(first)).is_err());
    eprintln!(
        "native water resource closure decoded and cached; changed manifest role refused; {} pinned files",
        snapshot.files.len()
    );
}
