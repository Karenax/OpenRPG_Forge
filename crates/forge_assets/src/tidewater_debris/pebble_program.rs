//! Immutable acquired Pebble program shared by filesystem and Studio transports.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;

const PIN: &str = "0f6ea25a3fdcc20371241db9729e2b58b39f7e8e29500f064da5ffc3a92b3201";
const SHADER_PIN: &str = "6c9e1f3659f384fe58d33405713ae185ae334db1d1ca7d3e620f8d690def557b";

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ShaderEntry {
    file: String,
    bytes: usize,
    sha256: ContentHash,
}

// The exact manifest hash also covers the source module, stage and preparer pins.
#[derive(Deserialize)]
struct Descriptor {
    schema: String,
    version: u32,
    source_commit: String,
    debris_package_sha256: ContentHash,
    building_package_sha256: ContentHash,
    shader: ShaderEntry,
}

/// Renderer-independent verified source; cell state stays runtime-owned.
#[derive(Clone, Debug)]
pub struct Program {
    source: String,
}

impl Program {
    /// Acquire the exact program before publishing any renderer resource.
    ///
    /// # Errors
    /// Refuses changed provenance, source packages, paths, byte counts or shader bytes.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let descriptor = parse(manifest, expected)?;
        let raw = read(&descriptor.shader.file, descriptor.shader.bytes)?;
        finish(&descriptor.shader, raw)
    }

    /// Use the same immutable acquisition through a browser transport.
    ///
    /// # Errors
    /// Propagates transport errors and the same refusals as `Self::load`.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let descriptor = parse(manifest, expected)?;
        let raw = read(descriptor.shader.file.clone(), descriptor.shader.bytes).await?;
        finish(&descriptor.shader, raw)
    }

    #[must_use]
    pub fn source(&self) -> &str {
        &self.source
    }
}

#[must_use]
pub fn program_hash() -> ContentHash {
    PIN.parse().expect("constant Pebble program hash")
}

fn parse(raw: &[u8], expected: ContentHash) -> Result<Descriptor, String> {
    if raw.len() > 65_536 || expected != program_hash() || bytes_sha256(raw) != expected {
        return Err("Pebble program manifest revision differs".into());
    }
    let descriptor: Descriptor = serde_json::from_slice(raw).map_err(|error| error.to_string())?;
    if descriptor.schema != "forge.tidewater.debris-pebble-program"
        || descriptor.version != 1
        || descriptor.source_commit != super::COMMIT
        || descriptor.debris_package_sha256 != super::package_hash()
        || descriptor.building_package_sha256.to_string()
            != "a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6"
        || descriptor.shader.file != "pebbles.wgsl"
        || descriptor.shader.bytes != 39_318
        || descriptor.shader.sha256.to_string() != SHADER_PIN
    {
        return Err("Unsupported acquired Pebble program".into());
    }
    Ok(descriptor)
}

fn finish(file: &ShaderEntry, raw: Vec<u8>) -> Result<Program, String> {
    if raw.len() != file.bytes || bytes_sha256(&raw) != file.sha256 {
        return Err("Pebble shader bytes or revision differs".into());
    }
    let source = String::from_utf8(raw).map_err(|error| error.to_string())?;
    Ok(Program { source })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn refused_manifest_never_fetches_shader() {
        assert!(
            Program::load(b"{}", program_hash(), |_, _| {
                panic!("Refused provenance must not perform transport")
            })
            .is_err()
        );
    }

    #[test]
    #[ignore = "Requires the acquired Pebble program directory; no renderer or GPU"]
    fn acquired_transports_preserve_source_and_refuse_tampering() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_PEBBLE_PROGRAM").unwrap());
        let manifest = std::fs::read(root.join("program.json")).unwrap();
        let read = |name: &str, _: usize| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        };
        let sync = Program::load(&manifest, program_hash(), read).unwrap();
        let future = Program::load_async(&manifest, program_hash(), |name, limit| {
            std::future::ready(read(&name, limit))
        });
        let mut future = std::pin::pin!(future);
        let mut context = std::task::Context::from_waker(std::task::Waker::noop());
        let std::task::Poll::Ready(asynchronous) =
            std::future::Future::poll(future.as_mut(), &mut context)
        else {
            panic!("Immediate transport unexpectedly suspended");
        };
        assert_eq!(sync.source(), asynchronous.unwrap().source());
        assert_eq!(
            sync.source().as_bytes(),
            read("pebbles.wgsl", 39_318).unwrap()
        );
        assert!(
            Program::load(&manifest, program_hash(), |name, limit| {
                let mut bytes = read(name, limit)?;
                bytes[0] ^= 1;
                Ok(bytes)
            })
            .is_err()
        );
        assert!(
            Program::load(&manifest, program_hash(), |name, limit| {
                let mut bytes = read(name, limit)?;
                bytes.pop();
                Ok(bytes)
            })
            .is_err()
        );
    }
}
