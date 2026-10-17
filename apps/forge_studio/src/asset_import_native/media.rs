use super::{GovernedAssetKind, ImportSource, ImportSourceKind, ImportUpload, MAX_UPLOAD_BYTES};
use forge_assets::media_validation::{MediaFormat, MediaLimits, validate_media};

pub(super) fn extension(upload: &ImportUpload) -> Result<String, String> {
    if upload.name.is_empty()
        || upload.name.len() > 256
        || upload.name.contains(['/', '\\', ':'])
        || upload.name.chars().any(char::is_control)
    {
        return Err("Choose a filename, not a filesystem path".into());
    }
    let extension = upload
        .name
        .rsplit_once('.')
        .map(|(_, ext)| ext.to_ascii_lowercase())
        .ok_or("Imported file extension is absent")?;
    if !extension.is_ascii() || extension.len() > 8 {
        return Err("Unsupported imported file extension".into());
    }
    Ok(extension)
}
pub(super) fn check_upload(upload: &ImportUpload, limit: usize) -> Result<(), String> {
    if upload.bytes.is_empty()
        || upload.bytes.len() > limit
        || u64::try_from(upload.bytes.len()).ok() != Some(upload.byte_length)
    {
        return Err("Empty, oversized or inconsistent import upload".into());
    }
    extension(upload)?;
    Ok(())
}
pub(super) fn media(upload: &ImportUpload, kind: GovernedAssetKind) -> Result<String, String> {
    check_upload(upload, MAX_UPLOAD_BYTES)?;
    let ext = extension(upload)?;
    let format = match (kind, ext.as_str()) {
        (GovernedAssetKind::Mesh, "glb") => MediaFormat::Glb,
        (GovernedAssetKind::Texture, "png") => MediaFormat::Png,
        (GovernedAssetKind::Texture, "jpg" | "jpeg") => MediaFormat::Jpeg,
        (GovernedAssetKind::Audio, "wav") => MediaFormat::Wav,
        (GovernedAssetKind::Audio, "ogg") => MediaFormat::OggVorbis,
        (GovernedAssetKind::Audio, "flac") => MediaFormat::Flac,
        _ => return Err("File format differs from the selected media type".into()),
    };
    validate_media(&upload.bytes, format, MediaLimits::default()).map_err(|e| e.to_string())?;
    Ok(ext)
}
pub(super) fn source(upload: &ImportSource) -> Result<String, String> {
    check_upload(&upload.upload, MAX_UPLOAD_BYTES)?;
    let ext = extension(&upload.upload)?;
    match upload.kind {
        ImportSourceKind::Recipe => {
            if ext != "json" || upload.upload.bytes.len() > 1024 * 1024 {
                return Err("A durable recipe must be a bounded JSON document".into());
            }
            let _: serde_json::Value = serde_json::from_slice(&upload.upload.bytes)
                .map_err(|e| format!("Invalid durable recipe: {e}"))?;
        }
        ImportSourceKind::AuthoredFile => match ext.as_str() {
            "blend" => {
                if !upload.upload.bytes.starts_with(b"BLENDER") {
                    return Err("Invalid authored Blender file".into());
                }
            }
            "glb" => {
                media(&upload.upload, GovernedAssetKind::Mesh)?;
            }
            "png" | "jpg" | "jpeg" => {
                media(&upload.upload, GovernedAssetKind::Texture)?;
            }
            "wav" | "ogg" | "flac" => {
                media(&upload.upload, GovernedAssetKind::Audio)?;
            }
            _ => return Err("Unsupported durable source format".into()),
        },
    }
    Ok(ext)
}
pub(super) fn mime(path: &str) -> &'static str {
    match path.rsplit('.').next().unwrap_or_default() {
        "glb" => "model/gltf-binary",
        "png" => "image/png",
        "jpg" | "jpeg" => "image/jpeg",
        "wav" => "audio/wav",
        "ogg" => "audio/ogg",
        "flac" => "audio/flac",
        "json" => "application/json",
        "txt" => "text/plain",
        _ => "application/octet-stream",
    }
}
