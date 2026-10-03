use sha2::{Digest, Sha256};
use std::{
    fs,
    path::{Path, PathBuf},
};

fn files(path: &Path, result: &mut Vec<PathBuf>) -> std::io::Result<()> {
    for entry in fs::read_dir(path)? {
        let entry = entry?;
        let path = entry.path();
        if entry.file_type()?.is_dir() {
            files(&path, result)?;
        } else if path.extension().is_some_and(|extension| extension == "rs") {
            result.push(path);
        }
    }
    Ok(())
}
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let crate_root = PathBuf::from(std::env::var("CARGO_MANIFEST_DIR")?);
    let repository = crate_root
        .parent()
        .and_then(Path::parent)
        .ok_or("missing workspace root")?;
    let fork = repository.join("third_party/rerecast-0.4.0");
    let mut inputs = vec![
        repository.join("Cargo.toml"),
        repository.join("Cargo.lock"),
        crate_root.join("Cargo.toml"),
        crate_root.join("build.rs"),
        fork.join("Cargo.toml"),
    ];
    files(&crate_root.join("src"), &mut inputs)?;
    files(&fork.join("src"), &mut inputs)?;
    inputs.sort();
    let mut hash = Sha256::new();
    hash.update(b"forge-navigation/rerecast-0.4.0/landmass-0.9.2/v1\0");
    for path in inputs {
        println!("cargo:rerun-if-changed={}", path.display());
        let relative = path
            .strip_prefix(repository)?
            .to_string_lossy()
            .replace('\\', "/");
        hash.update(relative.as_bytes());
        hash.update([0]);
        hash.update(fs::read(path)?);
        hash.update([0]);
    }
    let hex: String = hash
        .finalize()
        .iter()
        .flat_map(|byte| {
            const HEX: &[u8; 16] = b"0123456789abcdef";
            [
                char::from(HEX[usize::from(*byte >> 4)]),
                char::from(HEX[usize::from(*byte & 15)]),
            ]
        })
        .collect();
    println!("cargo:rustc-env=FORGE_NAV_PROVIDER_SOURCE_SHA256={hex}");
    Ok(())
}
