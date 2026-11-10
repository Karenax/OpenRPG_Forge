use forge_core::{AreaId, ModuleId, bytes_sha256};
use forge_project::{module_package::ModuleVegetationBinding, workspace::ModuleOriginPin};
use forge_studio_frontend::{
    module_resources_acquisition::{Scope, acquire},
    module_resources_protocol::{Reply, Request, Snapshot},
};
use std::{
    cell::Cell,
    future::{Future, ready},
    task::{Context, Poll, Waker},
};

fn complete<T>(future: impl Future<Output = T>) -> T {
    let mut future = std::pin::pin!(future);
    match future
        .as_mut()
        .poll(&mut Context::from_waker(Waker::noop()))
    {
        Poll::Ready(value) => value,
        Poll::Pending => panic!("Immediate test transport unexpectedly waited"),
    }
}

fn scope() -> Scope {
    Scope {
        head: bytes_sha256(b"head"),
        module: ModuleId::deterministic("module"),
        area: AreaId::deterministic("area"),
        origin: Some(ModuleOriginPin {
            package_sha256: bytes_sha256(b"package"),
            receipt_sha256: bytes_sha256(b"receipt"),
            package_bytes: 10,
        }),
        vegetation: Some(bytes_sha256(b"vegetation")),
        terrain: None,
        imported: false,
        species: false,
    }
}

#[test]
fn module_acquisition_accepts_explicit_absence_and_refuses_missing_required_companions() {
    let requests = Cell::new(0);
    let read = |request| {
        assert!(matches!(request, Request::Describe { .. }));
        requests.set(requests.get() + 1);
        ready(Ok(Reply::Description { snapshot: None }))
    };
    assert!(
        complete(acquire(scope(), None, read, || true))
            .unwrap()
            .is_none()
    );
    let wanted = Scope {
        imported: true,
        ..scope()
    };
    assert!(complete(acquire(wanted, None, read, || true)).is_err());
    assert_eq!(requests.get(), 2);
}

#[test]
fn module_acquisition_refuses_foreign_scope_before_reading_any_payload() {
    let scope = scope();
    for invalid in 0..7 {
        let mut snapshot = Snapshot {
            head_hash: scope.head,
            package_sha256: scope.package().unwrap(),
            legacy_reference: false,
            module_id: scope.module,
            area_id: scope.area,
            vegetation: Some(ModuleVegetationBinding {
                area_id: scope.area,
                vegetation_sha256: scope.vegetation.unwrap(),
                sources: Vec::new(),
            }),
            objects: None,
            fish_profiles: Vec::new(),
            environment: None,
            terrain: None,
            ocean: None,
            local_lights: Vec::new(),
            placement_companions: Vec::new(),
            species: None,
            files: Vec::new(),
        };
        match invalid {
            0 => snapshot.head_hash = bytes_sha256(b"other head"),
            1 => snapshot.package_sha256 = bytes_sha256(b"other package"),
            2 => snapshot.module_id = ModuleId::deterministic("other module"),
            3 => snapshot.area_id = AreaId::deterministic("other area"),
            4 => {
                snapshot.vegetation.as_mut().unwrap().area_id =
                    AreaId::deterministic("other binding");
            }
            5 => {
                snapshot.vegetation.as_mut().unwrap().vegetation_sha256 =
                    bytes_sha256(b"other vegetation");
            }
            _ => snapshot.legacy_reference = true,
        }
        let requests = Cell::new(0);
        let read = |request| {
            assert!(matches!(request, Request::Describe { .. }));
            requests.set(requests.get() + 1);
            ready(Ok(Reply::Description {
                snapshot: Some(Box::new(snapshot.clone())),
            }))
        };
        assert!(complete(acquire(scope, None, read, || true)).is_err());
        assert_eq!(requests.get(), 1);
    }
}

#[test]
fn replaced_module_acquisition_stops_before_requests_and_after_description() {
    assert!(
        complete(acquire(
            scope(),
            None,
            |_| ready(Err("Replaced source should never request".into())),
            || false
        ))
        .is_err()
    );
    let active = Cell::new(true);
    let requests = Cell::new(0);
    let read = |_| {
        requests.set(requests.get() + 1);
        active.set(false);
        ready(Ok(Reply::Description { snapshot: None }))
    };
    assert!(complete(acquire(scope(), None, read, || active.get())).is_err());
    assert_eq!(requests.get(), 1);
}

#[cfg(not(target_arch = "wasm32"))]
#[test]
#[ignore = "Requires the original 019 authoring workspace and verified reference-assets files"]
fn installed_legacy_reference_restores_plants_surfaces_and_environment() {
    use forge_project::authoring::{
        AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest,
    };
    use forge_studio_frontend::module_resources_native::Resources;
    let root = std::path::PathBuf::from(std::env::var_os("FORGE_LEGACY_WORKSPACE").unwrap());
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    let area = "e620dd19-ead9-5913-a6d6-0f93109148c0".parse().unwrap();
    let state = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let scope = Scope::from_state(&state).unwrap().unwrap();
    assert!(scope.origin.is_none() && scope.imported);
    let mut reader = Resources::default();
    let data = complete(acquire(
        scope,
        None,
        |request| ready(reader.execute(&root, &mut host, request)),
        || true,
    ))
    .unwrap()
    .unwrap();
    assert!(data.snapshot.legacy_reference);
    let plants = data.imported.as_ref().unwrap();
    assert_eq!(plants.decode_geometry().unwrap().groups.len(), 6);
    let surfaces = data.objects.as_ref().unwrap();
    let catalog = surfaces.catalog();
    let current = state
        .zone_head
        .as_ref()
        .unwrap()
        .corrections
        .placements
        .as_deref()
        .unwrap();
    let referenced: std::collections::BTreeSet<_> = current
        .iter()
        .filter_map(|placement| {
            catalog
                .assets
                .iter()
                .find(|asset| {
                    asset.id == placement.asset_id && asset.content_hash == placement.asset_hash
                })
                .map(|asset| asset.id)
        })
        .collect();
    assert_eq!(referenced.len(), 6);
    let forge_project::module_package::ModuleObjectSource::SourceSurfaces { package, .. } =
        &data.snapshot.objects.as_ref().unwrap().source;
    let environment = data.environment.as_ref().unwrap();
    assert_eq!(environment.bootstrap_frames(), 120);
    assert_eq!(environment.file_count(), 266);
    #[cfg(feature = "scene-host-tests")]
    forge_engine::source_environment::package::prepare(environment).unwrap();
    assert_eq!(
        data.snapshot.files.len(),
        plants.files().len() + package.resources.len() + environment.file_count() + 3
    );
    let bytes: u64 = data.snapshot.files.iter().map(|file| file.bytes).sum();
    println!(
        "legacy_reference={} head={} index={} files={} bytes={} plant_groups=6 object_pins=6 environment_frames=120",
        data.snapshot.package_sha256,
        scope.head,
        data.imported_index.unwrap(),
        data.snapshot.files.len(),
        bytes
    );
}

#[cfg(not(target_arch = "wasm32"))]
#[test]
#[ignore = "Requires the explicitly selected retained module qualification workspace"]
fn private_acquisition_reuses_the_module_package_without_a_reference_library() {
    use forge_project::authoring::{
        AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest,
    };
    use forge_studio_frontend::module_resources_native::Resources;
    let root = std::path::PathBuf::from(std::env::var_os("FORGE_VIEWPORT_WORKSPACE").unwrap());
    assert!(root.is_absolute());
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    let area = "3d5bc933-f29e-53fe-8afa-5d312baa58b4".parse().unwrap();
    let state = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let scope = Scope::from_state(&state).unwrap().unwrap();
    let mut reader = Resources::default();
    let requests = Cell::new(0);
    let data = complete(acquire(
        scope,
        None,
        |request| {
            requests.set(requests.get() + 1);
            ready(reader.execute(&root, &mut host, request))
        },
        || true,
    ))
    .unwrap()
    .unwrap();
    assert!(requests.get() > 84);
    assert_eq!(
        data.snapshot.package_sha256.to_string(),
        "a6cd35f6c0c441565b62a9f3ad2aba7244cb9f9b20677610bdfbd5ee23be2e60"
    );
    let package = data.imported.as_ref().unwrap();
    assert_eq!(package.decode_geometry().unwrap().groups.len(), 6);
    let first_requests = requests.get();
    let previous = std::rc::Rc::new(data);
    requests.set(0);
    let second = complete(acquire(
        scope,
        Some(previous.clone()),
        |request| {
            requests.set(requests.get() + 1);
            ready(reader.execute(&root, &mut host, request))
        },
        || true,
    ))
    .unwrap()
    .unwrap();
    assert_eq!(
        requests.get(),
        1,
        "An unchanged index must not redownload its resources"
    );
    assert!(std::rc::Rc::ptr_eq(
        previous.imported.as_ref().unwrap(),
        second.imported.as_ref().unwrap()
    ));
    let proof = serde_json::json!({"schema":"forge.gate7.module-vegetation-acquisition-proof.v1", "head":scope.head,"package":scope.package().unwrap(),"imported_index":second.imported_index,"resources":second.snapshot.files.len(),"first_requests":first_requests,"cached_requests":requests.get(),"same_package_allocation_reused":true,"geometry_groups":6,"reference_library_requests":0,"webview_executed":false,"gpu_draw_executed":false,"gate_closed":false});
    let output =
        std::path::PathBuf::from(std::env::var_os("FORGE_VIEWPORT_ACQUISITION_PROOF").unwrap());
    assert!(output.is_absolute() && !output.exists());
    std::fs::write(output, serde_json::to_vec_pretty(&proof).unwrap()).unwrap();
}
