//! Exercise the existing workspace adoption route with a complete imported zone.
use forge_core::{CommandId, ModuleId, bytes_sha256, document_store::DocumentStore};
use forge_project::workspace::{
    file_session::FileWorkspaceSession,
    objects::{ReviewedZone, ZoneObjectStore},
    session::{WorkspaceAction, WorkspaceRequest},
    source_blobs::SourceBlobStore,
};
use std::{error::Error, fs, io::Write, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 4 {
        return Err("usage: verify_imported_adoption SOURCE_AUTHORITY ARCHIVE_FILE NEW_WORKSPACE NEW_REPORT".into());
    }
    let source_authority = PathBuf::from(&args[0]);
    let archive = fs::read(&args[1])?;
    let reviewed = ReviewedZone::from_bytes(&archive, bytes_sha256(&archive))?;
    let root = PathBuf::from(&args[2]);
    fs::create_dir(&root)?;
    let zone_directory = root.join(reviewed.reference().area_id.to_string());
    fs::create_dir(&zone_directory)?;
    let document = DocumentStore::open(&zone_directory)?;
    document.save(None, &archive)?;
    drop(document);
    let source_blobs = SourceBlobStore::open_existing(&source_authority)?;
    let staged = SourceBlobStore::open(&zone_directory)?;
    for (&hash, &length) in reviewed.required_source_blobs() {
        staged.publish(hash, &source_blobs.read(hash, length)?)?;
    }
    let mut workspace = FileWorkspaceSession::create(&root)?;
    let module = ModuleId::deterministic("tidewater-imported-module");
    for (key, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: reviewed.project_id(),
                name: "Tidewater".into(),
            },
        ),
        (
            "module",
            WorkspaceAction::CreateModule {
                project_id: reviewed.project_id(),
                id: module,
                name: "Tidewater island".into(),
            },
        ),
    ] {
        let head = workspace.session().head();
        let request = WorkspaceRequest {
            command_id: CommandId::deterministic(format!("tidewater-adoption/{key}")),
            base_revision: head.revision,
            base_hash: head.content_hash()?,
            action,
        };
        workspace.execute(&request)?;
    }
    let review = workspace.review_adoption(
        reviewed.reference().area_id,
        reviewed.project_id(),
        module,
        reviewed.reference().archive_hash,
    )?;
    workspace.adopt(&review.request)?;
    workspace.save()?;
    drop(workspace);
    let reopened = FileWorkspaceSession::open(&root)?;
    let reference = &reopened.session().head().projects[0].modules[0].zones[0];
    if reference != reviewed.reference() {
        return Err("adopted reference changed".into());
    }
    let authority = root.join("workspace-v1");
    if ZoneObjectStore::open(&authority)?.read(reviewed.project_id(), reference)? != archive {
        return Err("adopted archive changed".into());
    }
    let destination = SourceBlobStore::open_existing(&authority)?;
    for (&hash, &length) in reviewed.required_source_blobs() {
        destination.read(hash, length)?;
    }
    let report = serde_json::json!({"status":"pass","source_blobs":reviewed.required_source_blobs().len(),
        "source_bytes":reviewed.required_source_blobs().values().sum::<usize>(),"reference":reference,
        "workspace_head_hash":reopened.session().head().content_hash()?,
        "scope":"workspace adoption/save/reopen; Studio UI and renderer pending"});
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[3])?;
    writeln!(output, "{}", serde_json::to_string_pretty(&report)?)?;
    println!(
        "Adopted {} immutable source blobs",
        reviewed.required_source_blobs().len()
    );
    Ok(())
}
