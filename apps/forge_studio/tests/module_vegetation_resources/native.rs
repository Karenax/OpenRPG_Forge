use forge_core::{CommandId, ContentHash, bytes_sha256};
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};
use forge_studio_frontend::{
    module_resources_native::Resources,
    module_resources_protocol::{Assembly, Reply, Request, Snapshot},
    module_workspace_bootstrap::{BootstrapRequest, prepare_module_workspace},
};
use std::{fs, path::PathBuf, sync::Arc};

fn input(name: &str) -> PathBuf {
    let path = PathBuf::from(std::env::var_os(name).expect(name));
    assert!(path.is_absolute());
    path
}

fn describe(
    reader: &mut Resources,
    root: &std::path::Path,
    host: &mut AuthoringHost,
    head: ContentHash,
) -> Snapshot {
    let request = Request::Describe {
        expected_head: head,
    };
    let wire = forge_studio_frontend::encode_tauri_wire(&request).unwrap();
    let request = forge_studio_frontend::decode_tauri_wire(&wire).unwrap();
    let reply = reader.execute(root, host, request).unwrap();
    let wire = forge_studio_frontend::encode_tauri_wire(&reply).unwrap();
    let reply: Reply = forge_studio_frontend::decode_tauri_wire(&wire).unwrap();
    let Reply::Description {
        snapshot: Some(snapshot),
    } = reply
    else {
        panic!("No restored vegetation")
    };
    *snapshot
}

#[test]
#[ignore = "Requires an explicitly pinned private full module and a fresh persistent qualification directory"]
#[allow(
    clippy::too_many_lines,
    reason = "One ordered private-package qualification keeps setup, stale reads and retouch evidence in the same lifecycle."
)]
fn private_module_restores_its_own_vegetation_resources_through_the_native_authority() {
    let package = input("FORGE_RESTORE_MODULE_PACKAGE");
    let output = input("FORGE_RESTORE_OUTPUT");
    assert!(!output.exists());
    fs::create_dir(&output).unwrap();
    let expected: ContentHash = std::env::var("FORGE_RESTORE_MODULE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let bytes = fs::read(&package).unwrap();
    assert_eq!(bytes_sha256(&bytes), expected);
    let origin = forge_delivery::orpmod::read_origin_documents(Arc::from(bytes)).unwrap();
    let area = origin
        .module
        .vegetation
        .first()
        .expect("Full vegetation module required")
        .area_id;
    let report = prepare_module_workspace(&BootstrapRequest {
        package_path: package.clone(),
        expected_package_sha256: expected,
        area_id: area,
        destination: output.join("workspace"),
    })
    .unwrap();
    assert!(report.restored_vegetation_areas.contains(&area));
    let mut host = AuthoringHost::new(&report.workspace_root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    host.execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let mut reader = Resources::default();
    let snapshot = describe(
        &mut reader,
        &report.workspace_root,
        &mut host,
        report.zone_head_sha256,
    );
    assert_eq!(snapshot.package_sha256, expected);
    assert_eq!(snapshot.module_id, origin.module.module_id);
    assert!(
        origin
            .read_payload("module.json", 16 * 1024 * 1024)
            .is_err()
    );
    assert!(
        origin
            .read_payload("../module.json", 16 * 1024 * 1024)
            .is_err()
    );
    let request = |head, package, index, offset| Request::Read {
        expected_head: head,
        expected_package: package,
        resource_index: index,
        offset,
    };
    for invalid in [
        request(snapshot.head_hash, bytes_sha256(b"other package"), 0, 0),
        request(snapshot.head_hash, expected, snapshot.files.len(), 0),
        request(snapshot.head_hash, expected, 0, 1),
        request(bytes_sha256(b"stale head"), expected, 0, 0),
    ] {
        assert!(
            reader
                .execute(&report.workspace_root, &mut host, invalid)
                .is_err()
        );
    }
    let mut restored_bytes = 0;
    for (index, file) in snapshot.files.iter().enumerate() {
        let mut assembly = Assembly::new(&snapshot, index).unwrap();
        while let Some(offset) = assembly.next_offset() {
            let reply = reader
                .execute(
                    &report.workspace_root,
                    &mut host,
                    request(snapshot.head_hash, expected, index, offset),
                )
                .unwrap();
            let wire = forge_studio_frontend::encode_tauri_wire(&reply).unwrap();
            let Reply::Resource { chunk } =
                forge_studio_frontend::decode_tauri_wire(&wire).unwrap()
            else {
                panic!("No resource chunk")
            };
            assembly.accept(chunk).unwrap();
        }
        let bytes = assembly.finish().unwrap();
        assert_eq!(bytes, origin.read_payload(&file.path, bytes.len()).unwrap());
        restored_bytes += bytes.len();
    }
    let archive = host.game_archive(snapshot.head_hash).unwrap();
    let session = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let mut corrections = session.head().corrections.clone();
    let placement = corrections
        .placements
        .as_mut()
        .unwrap()
        .first_mut()
        .unwrap();
    placement.transform.translation_m.x += 0.25;
    let edit = ZoneRequest {
        command_id: CommandId::deterministic("restore-companions/edit-carrier"),
        expected_hash: snapshot.head_hash,
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
    let edited_head = edited.zone_head.unwrap().content_hash().unwrap();
    assert_ne!(edited_head, snapshot.head_hash);
    assert!(
        reader
            .execute(
                &report.workspace_root,
                &mut host,
                request(snapshot.head_hash, expected, 0, 0)
            )
            .is_err()
    );
    assert!(
        reader
            .execute(
                &report.workspace_root,
                &mut host,
                request(edited_head, expected, 0, 0)
            )
            .is_err()
    );
    let retouched = describe(&mut reader, &report.workspace_root, &mut host, edited_head);
    let current =
        ZoneEditSession::from_archive_bytes(&host.game_archive(edited_head).unwrap()).unwrap();
    assert!(
        forge_delivery::module_vegetation::resolve_imported(
            &origin.module,
            current.head(),
            |path, maximum| origin.read_payload(path, maximum)
        )
        .is_err(),
        "Installed admission must retain its exact original head requirement"
    );
    assert_eq!(retouched.files, snapshot.files);
    assert_eq!(
        retouched.vegetation.as_ref().unwrap().sources,
        snapshot.vegetation.as_ref().unwrap().sources
    );
    assert_ne!(
        retouched.vegetation.as_ref().unwrap().vegetation_sha256,
        snapshot.vegetation.as_ref().unwrap().vegetation_sha256
    );
    let mut reopened = Resources::default();
    assert_eq!(
        describe(
            &mut reopened,
            &report.workspace_root,
            &mut host,
            edited_head
        ),
        retouched
    );
    assert_eq!(bytes_sha256(&fs::read(&package).unwrap()), expected);
    let proof = serde_json::json!({"schema":"forge.gate7.module-vegetation-restoration-proof.v1",
        "package_sha256": expected, "bootstrap":report,"original_head":snapshot.head_hash,
        "retouched_head":edited_head,"resources":snapshot.files.len(),"restored_bytes":restored_bytes,
        "exact_payloads_verified":true,"cross_package_and_stale_reads_refused":true,
        "metadata_and_traversal_reads_refused":true,"canonical_retouch_and_reader_reopen_verified":true,
        "package_unchanged":true,"tauri_json_roundtrip_verified":true,
        "webview_executed":false,"gpu_projection_executed":false,"gate_closed":false});
    fs::write(
        output.join("proof.json"),
        serde_json::to_vec_pretty(&proof).unwrap(),
    )
    .unwrap();
}
