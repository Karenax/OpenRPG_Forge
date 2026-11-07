use super::*;
use forge_core::{CommandId, ContentHash};
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};
use forge_studio_frontend::{
    module_resources_acquisition::Acquired,
    module_resources_native::Resources,
    module_workspace_bootstrap::{BootstrapRequest, prepare_module_workspace},
};
use std::{path::Path, rc::Rc};

fn read(
    root: &Path,
    host: &mut AuthoringHost,
    reader: &mut Resources,
    scope: Scope,
    previous: Option<Rc<Acquired>>,
    calls: &Cell<usize>,
) -> Option<Acquired> {
    complete(acquire(
        scope,
        previous,
        |request| {
            calls.set(calls.get() + 1);
            ready(reader.execute(root, host, request))
        },
        || true,
    ))
    .unwrap()
}

fn reused(first: &Acquired, second: &Acquired) {
    assert_eq!(first.local_lights.len(), second.local_lights.len());
    for (pin, package) in &first.local_lights {
        assert!(std::sync::Arc::ptr_eq(package, &second.local_lights[pin]));
    }
    assert!(Rc::ptr_eq(
        first.imported.as_ref().unwrap(),
        second.imported.as_ref().unwrap()
    ));
    assert!(Rc::ptr_eq(
        first.objects.as_ref().unwrap(),
        second.objects.as_ref().unwrap()
    ));
    assert!(Rc::ptr_eq(
        first.environment.as_ref().unwrap(),
        second.environment.as_ref().unwrap()
    ));
    assert!(Rc::ptr_eq(
        first.programs.as_ref().unwrap(),
        second.programs.as_ref().unwrap()
    ));
}

#[test]
#[ignore = "Requires an explicitly pinned retained qualification workspace; writes only the designated proof"]
fn retained_module_supplies_its_own_light_packages_without_another_workspace_copy() {
    let root =
        std::path::PathBuf::from(std::env::var_os("FORGE_COMMON_VIEWPORT_WORKSPACE").unwrap());
    let output = std::path::PathBuf::from(std::env::var_os("FORGE_COMMON_VIEWPORT_PROOF").unwrap());
    assert!(root.is_absolute() && output.is_absolute() && !output.exists());
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    let state = host
        .execute(AuthoringRequest::OpenManaged {
            area_id: "3d5bc933-f29e-53fe-8afa-5d312baa58b4".parse().unwrap(),
        })
        .unwrap();
    let scope = Scope::from_state(&state).unwrap().unwrap();
    assert_eq!(
        scope.package().unwrap().to_string(),
        "40dad5614f63652f4e5f1e6d373a2c4c9898fe50b28432fc73dae0fd8803b59b"
    );
    let mut reader = Resources::default();
    let calls = Cell::new(0);
    let first = Rc::new(read(&root, &mut host, &mut reader, scope, None, &calls).unwrap());
    assert!(!first.local_lights.is_empty());
    assert_eq!(first.local_lights.len(), first.snapshot.local_lights.len());
    let initial_calls = calls.replace(0);
    let second = read(
        &root,
        &mut host,
        &mut reader,
        scope,
        Some(first.clone()),
        &calls,
    )
    .unwrap();
    reused(&first, &second);
    assert_eq!(calls.get(), 1);
    let plain = host
        .execute(AuthoringRequest::OpenManaged {
            area_id: "3ade9df9-dcb2-5637-890a-ebc983f15392".parse().unwrap(),
        })
        .unwrap();
    calls.set(0);
    assert!(
        read(
            &root,
            &mut host,
            &mut reader,
            Scope::from_state(&plain).unwrap().unwrap(),
            Some(first.clone()),
            &calls
        )
        .is_none()
    );
    assert_eq!(calls.get(), 1);
    let proof = serde_json::json!({"schema":"forge.gate7.common-runtime-resource-proof.v1","workspace":root,
        "head":scope.head,"package":scope.package().unwrap(),"resources":first.snapshot.files.len(),
        "local_light_packages":first.local_lights.len(),"initial_requests":initial_calls,"cached_requests":1,
        "same_immutable_allocations":true,"other_area_has_no_companions":true,"additional_workspace_copies":0,
        "webview_observed":false,"gate_closed":false});
    std::fs::write(output, serde_json::to_vec_pretty(&proof).unwrap()).unwrap();
}

#[test]
#[ignore = "Requires an explicitly pinned API 7 private module and a fresh qualification directory"]
#[allow(
    clippy::too_many_lines,
    reason = "One authority lifecycle retains the admission, cache, edit and area-isolation evidence"
)]
fn private_module_resources_share_one_authority_and_keep_other_areas_empty() {
    let package = std::path::PathBuf::from(std::env::var_os("FORGE_RESOURCES_MODULE").unwrap());
    let output = std::path::PathBuf::from(std::env::var_os("FORGE_RESOURCES_OUTPUT").unwrap());
    assert!(package.is_absolute() && output.is_absolute() && !output.exists());
    std::fs::create_dir(&output).unwrap();
    let expected: ContentHash = std::env::var("FORGE_RESOURCES_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let bytes = std::fs::read(&package).unwrap();
    assert_eq!(bytes_sha256(&bytes), expected);
    let area = "3d5bc933-f29e-53fe-8afa-5d312baa58b4".parse().unwrap();
    let plain = "3ade9df9-dcb2-5637-890a-ebc983f15392".parse().unwrap();
    let bootstrap = prepare_module_workspace(&BootstrapRequest {
        package_path: package.clone(),
        expected_package_sha256: expected,
        area_id: area,
        destination: output.join("workspace"),
    })
    .unwrap();
    let root = &bootstrap.workspace_root;
    let mut host = AuthoringHost::new(root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    let state = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let scope = Scope::from_state(&state).unwrap().unwrap();
    let mut reader = Resources::default();
    let calls = Cell::new(0);
    let first = Rc::new(read(root, &mut host, &mut reader, scope, None, &calls).unwrap());
    assert_eq!(first.snapshot.package_sha256, expected);
    assert_eq!(first.objects.as_ref().unwrap().catalog().assets.len(), 23);
    assert_eq!(
        first
            .imported
            .as_ref()
            .unwrap()
            .decode_geometry()
            .unwrap()
            .groups
            .len(),
        6
    );
    assert!(first.environment.is_some() && first.programs.is_some());
    assert!(!first.local_lights.is_empty());
    let initial_calls = calls.replace(0);
    let cached = read(
        root,
        &mut host,
        &mut reader,
        scope,
        Some(first.clone()),
        &calls,
    )
    .unwrap();
    assert_eq!(calls.get(), 1);
    reused(&first, &cached);
    let archive = host.game_archive(scope.head).unwrap();
    let session = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let mut corrections = session.head().corrections.clone();
    corrections.placements.as_mut().unwrap()[0]
        .transform
        .translation_m
        .x += 0.25;
    let edit = ZoneRequest {
        command_id: CommandId::deterministic("module-resources/retouch"),
        expected_hash: scope.head,
        expected_revision: session.head().revision,
        action: ZoneAction::edit(corrections),
    };
    host.execute(AuthoringRequest::PreviewZone {
        request: edit.clone(),
    })
    .unwrap();
    let edited = host
        .execute(AuthoringRequest::ApplyZone { request: edit })
        .unwrap();
    let updated_scope = Scope::from_state(&edited).unwrap().unwrap();
    assert_ne!(scope.head, updated_scope.head);
    assert!(
        reader
            .execute(
                root,
                &mut host,
                Request::Describe {
                    expected_head: scope.head
                }
            )
            .is_err()
    );
    calls.set(0);
    let updated = read(
        root,
        &mut host,
        &mut reader,
        updated_scope,
        Some(first.clone()),
        &calls,
    )
    .unwrap();
    assert_eq!(calls.get(), 1);
    reused(&first, &updated);
    let state = host
        .execute(AuthoringRequest::OpenManaged { area_id: plain })
        .unwrap();
    calls.set(0);
    assert!(
        read(
            root,
            &mut host,
            &mut reader,
            Scope::from_state(&state).unwrap().unwrap(),
            Some(first.clone()),
            &calls
        )
        .is_none()
    );
    assert_eq!(calls.get(), 1);
    let state = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let reopened_scope = Scope::from_state(&state).unwrap().unwrap();
    calls.set(0);
    let reopened = read(
        root,
        &mut host,
        &mut reader,
        reopened_scope,
        Some(first.clone()),
        &calls,
    )
    .unwrap();
    assert_eq!(calls.get(), 1);
    reused(&first, &reopened);
    assert_eq!(bytes_sha256(&std::fs::read(&package).unwrap()), expected);
    let proof = serde_json::json!({
        "schema":"forge.gate7.studio-module-resources-proof.v1", "bootstrap":bootstrap,
        "original_head":scope.head,"retouched_head":updated_scope.head,
        "package_sha256":expected,"resources":first.snapshot.files.len(),
        "initial_requests":initial_calls,"cached_requests":1,"retouch_requests":1,
        "vegetation_geometry_groups":6,"source_object_assets":23,
        "local_light_packages":first.local_lights.len(),
        "same_immutable_packages_reused":true,"plain_area_resources_absent":true,
        "stale_head_refused":true,"package_unchanged":true,
        "webview_executed":false,"gpu_projection_executed":false,"gate_closed":false,
    });
    std::fs::write(
        output.join("proof.json"),
        serde_json::to_vec_pretty(&proof).unwrap(),
    )
    .unwrap();
}
