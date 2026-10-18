//! Complete procedural import input, usable by native and Studio transports.
//! Geometry, collision ownership and the source Nature program stay pinned together.
use super::{DebrisPackage, collisions::CollisionFrames};
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::BTreeMap;

const PIN: &str = "63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216";

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    files: BTreeMap<String, Entry>,
    limits: String,
}

/// One immutable acquisition; placements and their overrides remain zone-owned.
pub struct NativeInput {
    pub geometry: DebrisPackage,
    pub collisions: CollisionFrames,
    pub nature_program: String,
}

impl NativeInput {
    /// Verify all input files before returning anything for scene publication.
    ///
    /// # Errors
    /// Rejects altered manifests, payloads, frames or a missing source program.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let manifest = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &manifest.files {
            let bytes = read(name, entry.bytes)?;
            verify(name, entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(files)
    }

    /// Use the same atomic verification with Studio's asynchronous transport.
    ///
    /// # Errors
    /// Rejects the same inputs as `Self::load` and propagates transport failures.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let manifest = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &manifest.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(name, entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(files)
    }

    fn finish(mut files: BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        let geometry_manifest = files
            .remove("geometry/package.json")
            .ok_or("Debris geometry manifest missing")?;
        let geometry =
            DebrisPackage::load(&geometry_manifest, super::package_hash(), |name, _| {
                files
                    .remove(&format!("geometry/{name}"))
                    .ok_or_else(|| format!("Debris geometry payload missing: {name}"))
            })?;
        let collisions = CollisionFrames::load(
            &files
                .remove("collision-frames.json")
                .ok_or("Debris collision ownership missing")?,
        )?;
        let nature_program = String::from_utf8(
            files
                .remove("nature.wgsl")
                .ok_or("Debris Nature program missing")?,
        )
        .map_err(|error| error.to_string())?;
        // These two hash-verified provenance files are retained in the import package.
        // They are not mutable authoring data or renderer inputs.
        files
            .remove("CREDITS-scanned.md")
            .ok_or("Debris credits missing")?;
        files
            .remove("program.json")
            .ok_or("Debris program provenance missing")?;
        if !files.is_empty() {
            return Err("Debris native input contains unconsumed files".into());
        }
        Ok(Self {
            geometry,
            collisions,
            nature_program,
        })
    }
}

fn parse(raw: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if raw.len() > 1_048_576 || expected.to_string() != PIN || bytes_sha256(raw) != expected {
        return Err("Debris native manifest size or revision differs".into());
    }
    let manifest: Manifest = serde_json::from_slice(raw).map_err(|error| error.to_string())?;
    let total = manifest
        .files
        .values()
        .try_fold(0_usize, |sum, entry| sum.checked_add(entry.bytes));
    if manifest.schema != "forge.tidewater.debris-native-input"
        || manifest.version != 1
        || manifest.source_commit != super::COMMIT
        || manifest.files.len() != 4909
        || manifest.limits.is_empty()
        || total.is_none_or(|bytes| bytes > 32 * 1024 * 1024)
        || manifest.files.iter().any(|(name, entry)| {
            entry.bytes > 8 * 1024 * 1024
                || name.split('/').any(|part| {
                    part.is_empty() || part == "." || part == ".." || part.contains(['\\', ':'])
                })
        })
    {
        return Err("Unsupported Debris native input".into());
    }
    Ok(manifest)
}

fn verify(name: &str, entry: &Entry, raw: &[u8]) -> Result<(), String> {
    if raw.len() != entry.bytes || bytes_sha256(raw) != entry.sha256 {
        return Err(format!(
            "Debris native payload size or revision differs: {name}"
        ));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn manifest_refusal_precedes_transport() {
        let pin = PIN.parse().unwrap();
        let result = NativeInput::load(b"{}", pin, |_, _| {
            panic!("A refused manifest must not fetch any payload")
        });
        assert!(result.is_err());
    }

    #[test]
    #[ignore = "Requires the pinned procedural native import fixture"]
    fn acquired_native_input_survives_both_transports_and_refuses_shader_tampering() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_NATIVE_FIXTURE").unwrap());
        let raw = std::fs::read(root.join("debris-native.json")).unwrap();
        let pin = PIN.parse().unwrap();
        let read = |name: &str, _: usize| std::fs::read(root.join(name)).map_err(|e| e.to_string());
        let sync = NativeInput::load(&raw, pin, read).unwrap();
        let future = NativeInput::load_async(&raw, pin, |name, limit| {
            std::future::ready(read(&name, limit))
        });
        let mut future = std::pin::pin!(future);
        let mut context = std::task::Context::from_waker(std::task::Waker::noop());
        let std::task::Poll::Ready(result) =
            std::future::Future::poll(future.as_mut(), &mut context)
        else {
            panic!("Immediate fixture transport unexpectedly suspended")
        };
        let asynchronous = result.unwrap();
        assert_eq!(sync.geometry.frames(), asynchronous.geometry.frames());
        assert_eq!(sync.nature_program, asynchronous.nature_program);
        assert_eq!(sync.collisions.owners().len(), 946);
        assert_eq!(asynchronous.collisions.owners().len(), 946);
        let error = NativeInput::load(&raw, pin, |name, limit| {
            let mut bytes = read(name, limit)?;
            if name == "nature.wgsl" {
                bytes[0] ^= 1;
            }
            Ok(bytes)
        })
        .err()
        .unwrap();
        assert!(error.contains("nature.wgsl"), "{error}");
    }
}
