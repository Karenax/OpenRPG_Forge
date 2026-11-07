use super::*;
use forge_core::{CommandId, ContentHash};
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    zone_authoring::session::ZoneEditSession,
};
use forge_studio_frontend::{
    module_resources_acquisition::Acquired,
    module_resources_native::Resources,
    module_workspace_bootstrap::{BootstrapRequest, prepare_module_workspace},
};
use std::{
    io::Write,
    path::{Path, PathBuf},
    rc::Rc,
};

fn wire_read(
    root: &Path,
    host: &mut AuthoringHost,
    reader: &mut Resources,
    scope: Scope,
    previous: Option<Rc<Acquired>>,
    calls: &Cell<usize>,
) -> Acquired {
    complete(acquire(
        scope,
        previous,
        |request| {
            calls.set(calls.get() + 1);
            let wire = forge_studio_frontend::encode_tauri_wire(&request).unwrap();
            let reply = reader
                .execute(
                    root,
                    host,
                    forge_studio_frontend::decode_tauri_wire(&wire).unwrap(),
                )
                .unwrap();
            let wire = forge_studio_frontend::encode_tauri_wire(&reply).unwrap();
            ready(forge_studio_frontend::decode_tauri_wire(&wire))
        },
        || true,
    ))
    .unwrap()
    .unwrap()
}

#[test]
#[ignore = "Requires an explicitly pinned API 8 terrain module, its admitted source directory and a fresh qualification output"]
#[allow(
    clippy::too_many_lines,
    reason = "One retained authority lifecycle qualifies transport, cache, cancellation and retouch"
)]
fn private_terrain_uses_native_authority_and_the_common_codec_through_wire_chunks() {
    let package = PathBuf::from(std::env::var_os("FORGE_RESOURCES_MODULE").unwrap());
    let source = PathBuf::from(std::env::var_os("FORGE_RESOURCES_DIRECTORY").unwrap());
    let output = PathBuf::from(std::env::var_os("FORGE_RESOURCES_OUTPUT").unwrap());
    assert!(
        package.is_absolute() && source.is_absolute() && output.is_absolute() && !output.exists()
    );
    std::fs::create_dir(&output).unwrap();
    let expected: ContentHash = std::env::var("FORGE_RESOURCES_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let bytes = std::fs::read(&package).unwrap();
    assert_eq!(bytes_sha256(&bytes), expected);
    let module = forge_delivery::orpmod::inspect(&package, false).unwrap();
    assert_eq!(module.compatibility.module_api_version, 8);
    let area = module.terrain_surfaces[0].area_id;
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
    let first = Rc::new(wire_read(root, &mut host, &mut reader, scope, None, &calls));
    assert!(first.terrain.is_some() && first.terrain_index.is_some());
    assert_eq!(
        first.snapshot.terrain.as_ref(),
        Some(&module.terrain_surfaces[0])
    );
    let initial_calls = calls.replace(0);
    assert!(initial_calls > 3);
    let cached = wire_read(
        root,
        &mut host,
        &mut reader,
        scope,
        Some(first.clone()),
        &calls,
    );
    assert_eq!(calls.get(), 1);
    assert!(Rc::ptr_eq(
        first.terrain.as_ref().unwrap(),
        cached.terrain.as_ref().unwrap()
    ));
    let installed = forge_delivery::module_runtime::load(&source, area, None).unwrap();
    let native = installed.terrain().unwrap().unwrap();
    assert_eq!(Some(native.package_sha256), first.terrain_index);
    assert_eq!(
        native.package.file("terrain.wgsl"),
        first.terrain.as_ref().unwrap().file("terrain.wgsl")
    );
    calls.set(0);
    let cancelled = complete(acquire(
        scope,
        None,
        |request| {
            calls.set(calls.get() + 1);
            ready(reader.execute(root, &mut host, request))
        },
        || calls.get() < 3,
    ));
    assert!(cancelled.is_err());
    assert_eq!(calls.get(), 3);
    let session =
        ZoneEditSession::from_archive_bytes(&host.game_archive(scope.head).unwrap()).unwrap();
    let request = session
        .head()
        .prepare_terrain_action(
            forge_project::terrain_edit::TerrainAction::Edit(
                forge_project::terrain_edit::TerrainEdit::Rename {
                    name: "Terrain transporté et retouché".into(),
                },
            ),
            CommandId::deterministic("terrain-wire/retouch"),
        )
        .unwrap();
    host.execute(AuthoringRequest::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let edited = host
        .execute(AuthoringRequest::ApplyZone { request })
        .unwrap();
    let updated_scope = Scope::from_state(&edited).unwrap().unwrap();
    assert_ne!(scope.head, updated_scope.head);
    assert_eq!(scope.terrain, updated_scope.terrain);
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
    let updated = wire_read(
        root,
        &mut host,
        &mut reader,
        updated_scope,
        Some(first.clone()),
        &calls,
    );
    assert_eq!(calls.get(), 1);
    assert!(Rc::ptr_eq(
        first.terrain.as_ref().unwrap(),
        updated.terrain.as_ref().unwrap()
    ));
    let empty = complete(acquire(
        updated_scope,
        Some(first.clone()),
        |request| {
            assert!(matches!(request, Request::Describe { .. }));
            ready(Ok(Reply::Description { snapshot: None }))
        },
        || true,
    ))
    .unwrap();
    assert!(empty.is_none());
    assert_eq!(bytes_sha256(&std::fs::read(&package).unwrap()), expected);
    let proof = serde_json::json!({
        "schema":"forge.gate7.module-terrain-transport.v1", "passed":true,
        "module_package":package,"package_sha256":expected,"bootstrap":bootstrap,
        "terrain_index":first.terrain_index,"original_head":scope.head,"retouched_head":updated_scope.head,
        "resource_count":first.snapshot.files.len(),"initial_wire_requests":initial_calls,"cached_requests":1,"retouch_requests":1,
        "same_immutable_package_reused":true,"cancelled_after_three_requests":true,"stale_head_refused":true,
        "empty_description_clears_terrain":true,"installed_reader_matches":true,"package_unchanged":true,
        "webview_executed":false,"renderer_adopted":false,"gpu_qualified":false,"gate_closed":false,
    });
    std::fs::OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(output.join("proof.json"))
        .unwrap()
        .write_all(&serde_json::to_vec_pretty(&proof).unwrap())
        .unwrap();
}
