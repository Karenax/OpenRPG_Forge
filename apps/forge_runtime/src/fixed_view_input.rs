//! Separate diagnostic cameras from the pinned world/kit identity.
use forge_world::WorldSketchDocument;
use std::{io::Read, path::Path};
const LIMIT: u64 = 512 * 1024;

pub fn load(path: &Path, source: &WorldSketchDocument) -> Result<WorldSketchDocument, String> {
    let mut bytes = Vec::new();
    std::fs::File::open(path)
        .map_err(|e| e.to_string())?
        .take(LIMIT + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    validate(&bytes, source)
}

fn validate(bytes: &[u8], source: &WorldSketchDocument) -> Result<WorldSketchDocument, String> {
    if bytes.len() as u64 > LIMIT {
        return Err("proof views exceed 512 KiB".into());
    }
    let text = std::str::from_utf8(bytes).map_err(|e| e.to_string())?;
    let candidate = WorldSketchDocument::from_json(text).map_err(|e| e.to_string())?;
    let mut comparison = candidate.clone();
    comparison
        .validation_cameras
        .clone_from(&source.validation_cameras);
    if comparison != *source {
        return Err("proof views may change cameras only".into());
    }
    Ok(candidate)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn accepts_camera_only_and_preserves_source() {
        let source = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let mut candidate = source.clone();
        candidate.validation_cameras[0].vertical_fov_degrees = 3.0;
        assert_eq!(
            validate(&serde_json::to_vec(&candidate).unwrap(), &source).unwrap(),
            candidate
        );
        candidate.terrain_seed += 1;
        assert!(validate(&serde_json::to_vec(&candidate).unwrap(), &source).is_err());
        assert!(validate(&vec![b' '; usize::try_from(LIMIT).unwrap() + 1], &source).is_err());
        assert!(validate(b"{}", &source).is_err());
        assert_eq!(
            source,
            forge_world::embedded_visual_valley_world_sketch().unwrap()
        );
    }
}
