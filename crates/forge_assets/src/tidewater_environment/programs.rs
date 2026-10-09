//! Transport-neutral source kernels. GPU layout validation belongs to the engine.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Deserialize)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    files: BTreeMap<String, ContentHash>,
}

/// Fully acquired, hash-verified program bytes; contains no GPU objects.
pub struct ProgramsPackage {
    manifest: Vec<u8>,
    pin: ContentHash,
    files: BTreeMap<String, Vec<u8>>,
}

impl ProgramsPackage {
    /// Sequential bounded transport, with no requests before manifest validation.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        pin: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let meta = parse(manifest, pin)?;
        let mut files = BTreeMap::new();
        for (name, expected) in meta.files {
            let bytes = read(name.clone(), 1_000_000).await?;
            if bytes.is_empty() || bytes.len() > 1_000_000 || bytes_sha256(&bytes) != expected {
                return Err(format!("Environment program payload differs: {name}"));
            }
            files.insert(name, bytes);
        }
        Ok(Self {
            manifest: manifest.to_vec(),
            pin,
            files,
        })
    }

    #[must_use]
    pub fn manifest(&self) -> &[u8] {
        &self.manifest
    }
    #[must_use]
    pub fn pin(&self) -> ContentHash {
        self.pin
    }
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
}

fn required() -> BTreeSet<String> {
    let mut names: BTreeSet<_> = ["LICENSE", "mips-a.wgsl", "mips-b.wgsl"]
        .into_iter()
        .map(str::to_owned)
        .collect();
    names.extend((0..6).map(|i| format!("face-{i}.wgsl")));
    names.extend((0..4).map(|i| format!("uniform-{i}.bin")));
    for target in 0..2 {
        names.insert(format!("sh-{target}.wgsl"));
        names.extend((0..6).map(|i| format!("filter-{target}-{i}.wgsl")));
    }
    names
}

fn parse(bytes: &[u8], pin: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 2_000_000 || bytes_sha256(bytes) != pin {
        return Err("Environment program manifest hash/size differs".into());
    }
    let meta: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if meta.schema != "forge.tidewater.environment-programs"
        || meta.version != 1
        || meta.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta.files.keys().cloned().collect::<BTreeSet<_>>() != required()
    {
        return Err("Unsupported or incomplete environment programs".into());
    }
    Ok(meta)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::task::{Context, Poll, Waker};

    fn manifest() -> Vec<u8> {
        let files: BTreeMap<_, _> = required()
            .into_iter()
            .map(|name| (name, bytes_sha256(b"payload")))
            .collect();
        serde_json::to_vec(&serde_json::json!({
            "schema": "forge.tidewater.environment-programs", "version": 1,
            "source_commit": "1438b1abfcaee3267092b75573014f4d9b4a983c",
            "files": files
        }))
        .unwrap()
    }

    fn complete<F: std::future::Future>(future: F) -> F::Output {
        let mut future = std::pin::pin!(future);
        match future
            .as_mut()
            .poll(&mut Context::from_waker(Waker::noop()))
        {
            Poll::Ready(value) => value,
            Poll::Pending => panic!("test transport must be immediately ready"),
        }
    }

    #[test]
    fn rejects_invalid_manifest_before_transport() {
        let bytes = manifest();
        let bad_pin = bytes_sha256(b"other");
        assert!(
            complete(ProgramsPackage::load_async(&bytes, bad_pin, |_, _| {
                panic!("invalid pin must not request a payload");
                #[allow(unreachable_code)]
                std::future::ready(Ok(Vec::new()))
            }))
            .is_err()
        );
        let mut meta: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        let files = meta["files"].as_object_mut().unwrap();
        let pin = files.remove("LICENSE").unwrap();
        files.insert("../LICENSE".into(), pin);
        let bytes = serde_json::to_vec(&meta).unwrap();
        assert!(parse(&bytes, bytes_sha256(&bytes)).is_err());
    }

    #[test]
    fn acquires_all_files_and_stops_at_corruption() {
        let bytes = manifest();
        let pin = bytes_sha256(&bytes);
        let mut calls = 0;
        let package = complete(ProgramsPackage::load_async(&bytes, pin, |_, limit| {
            assert_eq!(limit, 1_000_000);
            calls += 1;
            std::future::ready(Ok(b"payload".to_vec()))
        }))
        .unwrap();
        assert_eq!(calls, 27);
        assert_eq!(package.manifest(), bytes);
        assert_eq!(package.pin(), pin);
        assert_eq!(package.file("LICENSE"), Some(b"payload".as_slice()));
        calls = 0;
        assert!(
            complete(ProgramsPackage::load_async(&bytes, pin, |_, _| {
                calls += 1;
                std::future::ready(Ok(b"corrupt".to_vec()))
            }))
            .is_err()
        );
        assert_eq!(calls, 1);
    }
}
