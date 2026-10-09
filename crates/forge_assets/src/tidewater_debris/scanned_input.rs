//! Atomic scanned acquisition for native and browser transports, independent of ECS.
use super::{
    collisions::{Category, CollisionFrames},
    scanned::{ScannedPackage, atlases, geometry},
};
use forge_core::bytes_sha256;

const PROGRAM_PIN: &str = "3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3";
const PROGRAM_BYTES: usize = 23_573;

/// Verified source inputs; edited placements remain in the common zone document.
pub struct ScannedInput {
    pub package: ScannedPackage,
    pub geometry: geometry::Geometry,
    pub collisions: CollisionFrames,
    pub instance_program: String,
}

impl ScannedInput {
    /// Verify the final instance program and complete pinned package before publication.
    ///
    /// # Errors
    /// Refuses altered shaders, transport failures, malformed geometry/atlases and
    /// collider owners whose source frame differs from the immutable placement.
    pub fn load(
        manifest: &[u8],
        program: &[u8],
        read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let instance_program = verify_program(program)?;
        Self::finish(ScannedPackage::load(manifest, read)?, instance_program)
    }

    /// Apply the same atomic validation to a browser or asynchronous native transport.
    ///
    /// # Errors
    /// Rejects the same inputs as `Self::load`; a failed read publishes no input.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        program: &[u8],
        read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let instance_program = verify_program(program)?;
        Self::finish(
            ScannedPackage::load_async(manifest, read).await?,
            instance_program,
        )
    }

    #[allow(
        clippy::float_cmp,
        reason = "Immutable acquired frames must match exactly; this is not a measured transform tolerance."
    )]
    fn finish(package: ScannedPackage, instance_program: String) -> Result<Self, String> {
        let geometry = geometry::prepare(&package)?;
        // Validate all acquired levels and formats, without producing replacement mips.
        atlases::prepare(&package)?;
        let collisions = CollisionFrames::load(
            package
                .file("collisions/collision-frames.json")
                .ok_or("Scanned collision ownership absent")?,
        )?;
        for instance in &geometry.instances {
            let source = &instance.source;
            let owner = collisions
                .owner(&instance.source_id)
                .ok_or("Scanned collision owner absent")?;
            if owner.category != Category::Scanned
                || owner.source_frame.translation_m != [source.x, source.y, source.z]
                || owner.source_frame.rotation_xyzw != instance.quaternion_xyzw
                || owner.source_frame.scale != [source.sx, source.sy, source.sz]
                || owner.source_matrix_column_major != instance.matrix_column_major
            {
                return Err(format!(
                    "Scanned collision frame differs: {}",
                    instance.source_id
                ));
            }
        }
        Ok(Self {
            package,
            geometry,
            collisions,
            instance_program,
        })
    }
}

fn verify_program(bytes: &[u8]) -> Result<String, String> {
    if bytes.len() != PROGRAM_BYTES || bytes_sha256(bytes).to_string() != PROGRAM_PIN {
        return Err("Scanned final instance program size or revision differs".into());
    }
    String::from_utf8(bytes.to_vec()).map_err(|error| error.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn wrong_instance_program_is_refused_before_transport() {
        let result = ScannedInput::load(b"{}", b"{}", |_, _| {
            panic!("Rejected instance programs must not acquire payloads")
        });
        assert!(result.err().unwrap().contains("instance program"));
    }

    #[test]
    #[ignore = "Requires the pinned scanned package and final instance shader"]
    fn acquired_scans_match_in_both_transports_and_refuse_tampering() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let program =
            std::fs::read(std::env::var("FORGE_DEBRIS_SCANNED_PROGRAM_FIXTURE").unwrap()).unwrap();
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let read = |name: &str, _: usize| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        };
        let sync = ScannedInput::load(&manifest, &program, read).unwrap();
        let future = ScannedInput::load_async(&manifest, &program, |name, limit| {
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
        assert_eq!(sync.geometry.instances.len(), 141);
        assert_eq!(sync.geometry.prototypes.len(), 12);
        assert_eq!(sync.collisions.owners().len(), 946);
        assert_eq!(sync.instance_program, asynchronous.instance_program);
        assert_eq!(
            sync.package.preparation(),
            asynchronous.package.preparation()
        );
        let frames = |input: &ScannedInput| {
            input
                .geometry
                .instances
                .iter()
                .map(|instance| (instance.source_id.clone(), instance.matrix_column_major))
                .collect::<Vec<_>>()
        };
        assert_eq!(frames(&sync), frames(&asynchronous));
        let mut altered = program.clone();
        altered[0] ^= 1;
        assert!(
            ScannedInput::load(&manifest, &altered, |_, _| panic!(
                "Altered program fetched payloads"
            ))
            .is_err()
        );
        let error = ScannedInput::load(&manifest, &program, |name, limit| {
            let mut bytes = read(name, limit)?;
            if name == "collisions/collision-frames.json" {
                bytes[0] ^= 1;
            }
            Ok(bytes)
        })
        .err()
        .unwrap();
        assert!(error.contains("pin") || error.contains("size"), "{error}");
    }
}
