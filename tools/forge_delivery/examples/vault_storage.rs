//! Storage interoperability verifier and explicit export of a real, autonomous file.
use forge_delivery::vault_storage;
use std::{fs, io::Write, path::PathBuf};

fn run() -> Result<(), String> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args.len() < 3 || !matches!(args[0].as_str(), "verify" | "export") {
        return Err("usage: vault_storage verify|export ROOT FILE [OUTPUT] [LIMIT_BYTES]".into());
    }
    let root = PathBuf::from(&args[1]);
    let file = PathBuf::from(&args[2]);
    let limit_index = if args[0] == "export" { 4 } else { 3 };
    let limit = args
        .get(limit_index)
        .map_or(Ok(1024_u64 * 1024 * 1024), |v| {
            v.parse::<u64>().map_err(|e| e.to_string())
        })?;
    let size = vault_storage::logical_size(&root, &file, limit)?;
    let hash = if args[0] == "export" {
        let output = PathBuf::from(args.get(3).ok_or("missing OUTPUT")?);
        let bytes = vault_storage::read_file(&root, &file, limit)?;
        let mut target = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&output)
            .map_err(|e| e.to_string())?;
        target.write_all(&bytes).map_err(|e| e.to_string())?;
        target.sync_all().map_err(|e| e.to_string())?;
        forge_delivery::hash(&bytes)
    } else {
        vault_storage::file_hash(&root, &file, limit)?
    };
    println!(
        "{}",
        serde_json::json!({"state":"verified","bytes":size,"sha256":hash})
    );
    Ok(())
}

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
