use super::{MediaFormat, MediaInfo, MediaLimits, MediaValidationError, error};
use image::ImageDecoder;
use std::io::Cursor;

pub(super) fn decode(
    bytes: &[u8],
    format: MediaFormat,
    limits: MediaLimits,
) -> Result<MediaInfo, MediaValidationError> {
    let image_format = match format {
        MediaFormat::Png => image::ImageFormat::Png,
        MediaFormat::Jpeg => image::ImageFormat::Jpeg,
        _ => return Err(error(format, "not an image format")),
    };
    let mut reader = image::ImageReader::with_format(Cursor::new(bytes), image_format);
    let mut allocation = image::Limits::default();
    allocation.max_alloc = Some(limits.max_decoded_bytes);
    allocation.max_image_width = Some(u32::try_from(limits.max_image_pixels).unwrap_or(u32::MAX));
    allocation.max_image_height = allocation.max_image_width;
    reader.limits(allocation);
    let decoder = reader.into_decoder().map_err(|e| image_error(format, &e))?;
    let (width, height) = decoder.dimensions();
    let pixels = u64::from(width) * u64::from(height);
    if width == 0 || height == 0 {
        return Err(error(format, "empty image dimensions"));
    }
    if pixels > limits.max_image_pixels || decoder.total_bytes() > limits.max_decoded_bytes {
        return Err(error(format, "image pixel/allocation budget exceeded"));
    }
    let decoded_bytes = decoder.total_bytes();
    let length = usize::try_from(decoded_bytes)
        .map_err(|_| error(format, "image allocation budget overflow"))?;
    let mut output = Vec::new();
    output
        .try_reserve_exact(length)
        .map_err(|_| error(format, "image allocation budget unavailable"))?;
    output.resize(length, 0);
    decoder
        .read_image(&mut output)
        .map_err(|e| image_error(format, &e))?;
    Ok(MediaInfo::Image {
        width,
        height,
        decoded_bytes,
    })
}
fn image_error(format: MediaFormat, err: &image::ImageError) -> MediaValidationError {
    if matches!(err, image::ImageError::Limits(_)) {
        error(format, format!("image decoder budget: {err}"))
    } else {
        error(format, format!("image decode failed: {err}"))
    }
}
