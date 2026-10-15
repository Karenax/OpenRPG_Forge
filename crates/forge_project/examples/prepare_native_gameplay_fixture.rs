//! Prepare test data through the same authoring commands, preserving the source.
use forge_core::{CommandId, ContentHash, EntityId, bytes_sha256};
use forge_gameplay::ObjectKind;
use forge_project::zone_authoring::session::ZoneEditSession;
use serde_json::json;
use std::{
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
};

fn write_new(path: &Path, bytes: &[u8]) -> Result<(), String> {
    OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(path)
        .and_then(|mut file| {
            file.write_all(bytes)?;
            file.sync_all()
        })
        .map_err(|error| error.to_string())
}

fn verified_input(
    root: &Path,
    name: &str,
    pin: ContentHash,
    expected_bytes: usize,
) -> Result<Vec<u8>, String> {
    if name.contains(['/', '\\', ':']) || name.starts_with('.') || expected_bytes > 64 * 1024 * 1024
    {
        return Err("Invalid or oversized fixture input".into());
    }
    let path = root.join(name);
    let metadata = fs::symlink_metadata(&path).map_err(|error| error.to_string())?;
    if !metadata.is_file()
        || metadata.file_type().is_symlink()
        || metadata.len() != u64::try_from(expected_bytes).map_err(|error| error.to_string())?
        || path
            .canonicalize()
            .map_err(|error| error.to_string())?
            .parent()
            != Some(root)
    {
        return Err(format!(
            "Fixture input is not the pinned local file: {name}"
        ));
    }
    let bytes = fs::read(&path).map_err(|error| error.to_string())?;
    if bytes_sha256(&bytes) != pin {
        return Err(format!("Fixture input hash mismatch: {name}"));
    }
    Ok(bytes)
}

#[allow(
    clippy::too_many_lines,
    reason = "Ordered preparation of one bounded data-only fixture"
)]
fn run() -> Result<(), String> {
    let arguments: Vec<_> = std::env::args_os().skip(1).collect();
    if !(3..=4).contains(&arguments.len()) {
        return Err("Usage: prepare_native_gameplay_fixture SNAPSHOT ARCHIVE_SHA256 NEW_OUTPUT [--compatible-retouch]".into());
    }
    let source = PathBuf::from(&arguments[0])
        .canonicalize()
        .map_err(|error| error.to_string())?;
    let expected = arguments[1]
        .to_str()
        .and_then(|value| ContentHash::parse(value).ok())
        .ok_or("Invalid source archive hash")?;
    let output = PathBuf::from(&arguments[2]);
    if !output.is_absolute() || output.exists() || output.starts_with(&source) {
        return Err(
            "Choose a fresh absolute fixture destination outside the source snapshot".into(),
        );
    }
    let retouch = arguments
        .get(3)
        .is_some_and(|value| value == "--compatible-retouch");
    if arguments.len() == 4 && !retouch {
        return Err("Unknown fixture option".into());
    }
    let archive = fs::read(source.join("zone.json")).map_err(|error| error.to_string())?;
    if archive.len() > 32 * 1024 * 1024 || bytes_sha256(&archive) != expected {
        return Err("Source archive is oversized or differs from its expected hash".into());
    }
    let mut zone = ZoneEditSession::from_archive_bytes(&archive)?;
    let source_head = zone.head().content_hash()?;
    let original_document = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .ok_or("The actual Studio snapshot has no authored RPG document")?
        .clone();
    let original_functional = original_document
        .functional_hash()
        .map_err(|error| error.to_string())?;
    // Exercise the authoring boundary on a proposed command, never persist the
    // malformed proposal or alter the Studio export used by the native game.
    let mut invalid = original_document.clone();
    let diagnostic_before = zone.archive_bytes()?;
    let missing =
        EntityId::deterministic(format!("native-gameplay-missing-dialogue/{source_head}"));
    let actor = invalid
        .actors
        .first_mut()
        .ok_or("The authored document contains no actor to diagnose")?;
    let actor_id = actor.id;
    actor.dialogue = Some(missing);
    let Err(reference_error) = zone.head().prepare_gameplay(
        Some(invalid),
        CommandId::deterministic(format!("native-gameplay-invalid-reference/{source_head}")),
    ) else {
        return Err("Authoring unexpectedly accepted a missing dialogue".into());
    };
    if !reference_error.contains(&missing.to_string())
        || zone.archive_bytes()? != diagnostic_before
        || zone.head().content_hash()? != source_head
    {
        return Err("Missing-reference diagnostic is not actionable or changed the source".into());
    }
    let reference_diagnostic = json!({
        "status":"REFERENCE_REJECTED_SOURCE_UNCHANGED", "actor":actor_id,
        "missing_dialogue":missing,"message":reference_error,"studio_ui_verified":false
    });
    let mut authoring_receipt = None;
    if retouch {
        let mut document = original_document.clone();
        let node = document
            .dialogues
            .iter_mut()
            .flat_map(|dialogue| dialogue.nodes.iter_mut())
            .next()
            .ok_or("A compatible dialogue retouch requires an authored dialogue")?;
        node.text
            .push_str(" [Retouche compatible de qualification]");
        let door = document
            .objects
            .iter_mut()
            .find(|object| matches!(object.kind, ObjectKind::Door { .. }))
            .ok_or("A compatible retouch requires an authored door")?;
        door.position.x += 0.05;
        if document
            .functional_hash()
            .map_err(|error| error.to_string())?
            != original_functional
        {
            return Err("The proposed retouch changes gameplay semantics".into());
        }
        let request = zone.head().prepare_gameplay(
            Some(document),
            CommandId::deterministic(format!("native-gameplay-retouch/{source_head}")),
        )?;
        let preview = zone.preview(&request)?;
        let applied = zone.execute(&request)?;
        if preview != applied {
            return Err("Authoring preview differs from applied fixture retouch".into());
        }
        authoring_receipt = Some(applied);
    }
    let (terrain, _) = zone.head().effective()?;
    let raster = terrain
        .sampled_source
        .as_ref()
        .ok_or("Imported terrain source absent")?;
    let mut inputs = vec![(
        "heights.bin".to_owned(),
        raster.samples_sha256,
        raster.byte_length().map_err(|error| error.to_string())?,
    )];
    let vegetation = zone
        .head()
        .base
        .vegetation
        .as_ref()
        .and_then(|definition| definition.imported_source.as_ref())
        .ok_or("Imported vegetation source absent")?;
    for block in &vegetation.blocks {
        inputs.push((
            format!("{}.bin", block.instances_sha256),
            block.instances_sha256,
            block.byte_len()?,
        ));
    }
    // All source files are authenticated before any destination is created.
    let mut copies = Vec::new();
    for (name, pin, length) in inputs {
        copies.push((
            name.clone(),
            pin,
            verified_input(&source, &name, pin, length)?,
        ));
    }
    let exported = if retouch {
        zone.archive_bytes()?
    } else {
        archive.clone()
    };
    let document = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .ok_or("Fixture lost its gameplay document")?;
    document.validate().map_err(|error| error.to_string())?;
    let output_head = zone.head().content_hash()?;
    fs::create_dir(&output).map_err(|error| error.to_string())?;
    let mut pins = Vec::new();
    write_new(&output.join("zone.json"), &exported)?;
    pins.push(json!({"path":"zone.json","sha256":bytes_sha256(&exported),"bytes":exported.len()}));
    for (name, pin, bytes) in &copies {
        write_new(&output.join(name), bytes)?;
        pins.push(json!({"path":name,"sha256":pin,"bytes":bytes.len()}));
    }
    // Recheck source identities after copying; the user's authoring data is read-only.
    if fs::read(source.join("zone.json")).map_err(|error| error.to_string())? != archive {
        return Err("Source archive changed during fixture preparation".into());
    }
    for (name, pin, bytes) in &copies {
        verified_input(&source, name, *pin, bytes.len())?;
    }
    let proof = json!({
        "schema":"forge.native-gameplay.fixture","version":1,"status":"DATA_ONLY_APPLICATION_FIXTURE",
        "source_snapshot":source,"source_archive_sha256":expected,"source_head_sha256":source_head,
        "snapshot":output,"archive_sha256":bytes_sha256(&exported),"head_sha256":output_head,
        "module_id":document.module_id,"area_id":document.area_id,"functional_hash":original_functional,
        "gameplay_document_sha256":forge_core::canonical_json_sha256(document).map_err(|error| error.to_string())?,
        "compatible_retouch":retouch,"authoring_receipt":authoring_receipt,"files":pins,
        "reference_diagnostic":reference_diagnostic,
        "source_unchanged":true,"application_commands_used":true,
        "copied_product_code":false,"studio_ui_verified":false,"runtime_verified":false,"gate_qualified":false
    });
    write_new(
        &output.join("fixture-provenance.json"),
        &serde_json::to_vec_pretty(&proof).map_err(|error| error.to_string())?,
    )?;
    println!("{proof}");
    Ok(())
}

fn main() {
    if let Err(error) = run() {
        eprintln!("Native gameplay fixture refused: {error}");
        std::process::exit(1);
    }
}
