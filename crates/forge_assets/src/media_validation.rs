//! Native byte-only media decoding with explicit resource budgets.
//! Vorbis admission supports Floor1 only; Floor0 is refused before codec probing.
//! Successful decoding is not artistic, placement or runtime qualification.
use thiserror::Error;
mod audio;
mod geometry;
mod images;
mod vorbis_setup;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum MediaFormat {
    Glb,
    Png,
    Jpeg,
    Wav,
    OggVorbis,
    Flac,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct MediaLimits {
    pub max_input_bytes: u64,
    pub max_image_pixels: u64,
    pub max_decoded_bytes: u64,
    pub max_audio_frames: u64,
    pub max_audio_channels: u32,
    pub max_audio_sample_rate: u32,
    pub max_audio_packets: u64,
    pub max_mesh_vertices: u64,
    pub max_mesh_indices: u64,
    pub max_mesh_primitives: u64,
}
impl Default for MediaLimits {
    fn default() -> Self {
        Self {
            max_input_bytes: 256 * 1024 * 1024,
            max_image_pixels: 64 * 1024 * 1024,
            max_decoded_bytes: 256 * 1024 * 1024,
            max_audio_frames: 28_800_000,
            max_audio_channels: 8,
            max_audio_sample_rate: 192_000,
            max_audio_packets: 100_000,
            max_mesh_vertices: 8_000_000,
            max_mesh_indices: 24_000_000,
            max_mesh_primitives: 65_536,
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub enum MediaInfo {
    Image {
        width: u32,
        height: u32,
        decoded_bytes: u64,
    },
    Audio {
        channels: u32,
        sample_rate: u32,
        frames: u64,
        samples: u64,
        decoded_bytes: u64,
    },
    Glb {
        meshes: u64,
        primitives: u64,
        vertices: u64,
        indices: u64,
        has_geometry: bool,
    },
}

#[derive(Debug, Error)]
#[error("{format:?}: {reason}")]
pub struct MediaValidationError {
    pub format: MediaFormat,
    pub reason: String,
}

pub fn validate_media(
    bytes: &[u8],
    format: MediaFormat,
    limits: MediaLimits,
) -> Result<MediaInfo, MediaValidationError> {
    if bytes.is_empty() {
        return Err(error(format, "empty media input"));
    }
    if u64::try_from(bytes.len()).unwrap_or(u64::MAX) > limits.max_input_bytes {
        return Err(error(format, "input byte budget exceeded"));
    }
    match format {
        MediaFormat::Png | MediaFormat::Jpeg => images::decode(bytes, format, limits),
        MediaFormat::Wav | MediaFormat::OggVorbis | MediaFormat::Flac => {
            audio::decode(bytes, format, limits)
        }
        MediaFormat::Glb => geometry::decode(bytes, limits),
    }
}

fn error(format: MediaFormat, reason: impl Into<String>) -> MediaValidationError {
    MediaValidationError {
        format,
        reason: reason.into(),
    }
}
fn number(value: usize, format: MediaFormat) -> Result<u64, MediaValidationError> {
    u64::try_from(value).map_err(|_| error(format, "numeric budget overflow"))
}
fn bounded_add(
    total: u64,
    value: u64,
    limit: u64,
    format: MediaFormat,
    label: &str,
) -> Result<u64, MediaValidationError> {
    let total = total
        .checked_add(value)
        .ok_or_else(|| error(format, format!("{label} budget overflow")))?;
    if total > limit {
        return Err(error(format, format!("{label} budget exceeded")));
    }
    Ok(total)
}
