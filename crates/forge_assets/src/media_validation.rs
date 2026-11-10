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

/// The selected byte-validation route does not imply render or licence admission.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum MediaRoute {
    Standard,
    SourceGraph,
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ValidatedMedia {
    pub route: MediaRoute,
    pub info: MediaInfo,
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
    validate_input(bytes, format, limits)?;
    match format {
        MediaFormat::Png | MediaFormat::Jpeg => images::decode(bytes, format, limits),
        MediaFormat::Wav | MediaFormat::OggVorbis | MediaFormat::Flac => {
            audio::decode(bytes, format, limits)
        }
        MediaFormat::Glb => geometry::decode(bytes, limits),
    }
}

/// Validate media by its declared contents without treating an invalid source
/// graph as ordinary glTF. Consumers must still select a compatible renderer.
pub fn validate_declared_media(
    bytes: &[u8],
    format: MediaFormat,
    limits: MediaLimits,
    carrier_budget: crate::source_graph_glb::SourceGraphGlbReadBudget,
) -> Result<ValidatedMedia, MediaValidationError> {
    validate_input(bytes, format, limits)?;
    let has_source_graph = if format == MediaFormat::Glb {
        let (document, _) = crate::source_graph_glb::read_glb_document(bytes, carrier_budget)
            .map_err(|reason| error(format, reason))?;
        if crate::source_decal_glb::source_decal_declared(&document)
            .map_err(|reason| error(format, reason))?
        {
            return Err(error(format, "source decal requires its own scene renderer"));
        }
        crate::source_graph_glb::read_source_graph_glb_with_budget(bytes, carrier_budget)
            .map_err(|reason| error(format, reason))?
            .is_some()
    } else {
        false
    };
    if has_source_graph {
        Ok(ValidatedMedia {
            route: MediaRoute::SourceGraph,
            info: validate_source_graph_media(bytes, limits, carrier_budget)?,
        })
    } else {
        Ok(ValidatedMedia {
            route: MediaRoute::Standard,
            info: validate_media(bytes, format, limits)?,
        })
    }
}

/// Validate a declared source graph's pinned programs, geometry and images with
/// explicit carrier and decoded-media budgets. This is byte admission only: it
/// does not authorize ordinary glTF fallback, package distribution or rendering.
pub fn validate_source_graph_media(
    bytes: &[u8],
    limits: MediaLimits,
    carrier_budget: crate::source_graph_glb::SourceGraphGlbReadBudget,
) -> Result<MediaInfo, MediaValidationError> {
    validate_input(bytes, MediaFormat::Glb, limits)?;
    geometry::decode_source_graph(bytes, limits, carrier_budget)
}

fn validate_input(
    bytes: &[u8],
    format: MediaFormat,
    limits: MediaLimits,
) -> Result<(), MediaValidationError> {
    if bytes.is_empty() {
        return Err(error(format, "empty media input"));
    }
    if u64::try_from(bytes.len()).unwrap_or(u64::MAX) > limits.max_input_bytes {
        return Err(error(format, "input byte budget exceeded"));
    }
    Ok(())
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
