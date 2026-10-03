//! Decode Bevy's RGBA8 texture readback, whose rows retain GPU copy padding.

const COPY_BYTES_PER_ROW_ALIGNMENT: usize = 256;

fn row_stride(width: u32) -> Result<usize, &'static str> {
    if width == 0 {
        return Err("Image de sélection GPU sans largeur");
    }
    let bytes = usize::try_from(width)
        .ok()
        .and_then(|width| width.checked_mul(4))
        .ok_or("Dimensions de sélection GPU trop grandes")?;
    bytes
        .checked_add(COPY_BYTES_PER_ROW_ALIGNMENT - 1)
        .map(|rounded| rounded / COPY_BYTES_PER_ROW_ALIGNMENT * COPY_BYTES_PER_ROW_ALIGNMENT)
        .ok_or("Dimensions de sélection GPU trop grandes")
}

pub(crate) fn rgba8_pixel(
    data: &[u8],
    size: [u32; 2],
    position: [usize; 2],
) -> Result<[u8; 4], &'static str> {
    let [width, height] = size;
    let [x, y] = position;
    let stride = row_stride(width)?;
    let width = usize::try_from(width).map_err(|_| "Largeur de sélection GPU trop grande")?;
    let height = usize::try_from(height).map_err(|_| "Hauteur de sélection GPU trop grande")?;
    if height == 0 || x >= width || y >= height {
        return Err("Pixel de sélection hors de l'image");
    }
    let expected = stride
        .checked_mul(height)
        .ok_or("Dimensions de sélection GPU trop grandes")?;
    if data.len() != expected {
        return Err("Image de sélection GPU incomplète ou de taille inattendue");
    }
    // The verified total size bounds both products and the four-byte slice.
    let offset = y * stride + x * 4;
    data[offset..offset + 4]
        .try_into()
        .map_err(|_| "Pixel de sélection absent")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rows_align_to_256_bytes_at_the_64_pixel_boundary() {
        assert_eq!(row_stride(1), Ok(256));
        assert_eq!(row_stride(63), Ok(256));
        assert_eq!(row_stride(64), Ok(256));
        assert_eq!(row_stride(65), Ok(512));
        assert_eq!(row_stride(994), Ok(4096));
    }

    #[test]
    fn reads_ids_on_multiple_padded_rows_without_reading_padding() {
        let mut data = vec![0xad; 512 * 3];
        data[64 * 4..65 * 4].copy_from_slice(&[1, 2, 3, 255]);
        data[512..516].copy_from_slice(&[4, 5, 6, 255]);
        data[2 * 512 + 64 * 4..2 * 512 + 65 * 4].copy_from_slice(&[7, 8, 9, 255]);
        assert_eq!(rgba8_pixel(&data, [65, 3], [64, 0]), Ok([1, 2, 3, 255]));
        assert_eq!(rgba8_pixel(&data, [65, 3], [0, 1]), Ok([4, 5, 6, 255]));
        assert_eq!(rgba8_pixel(&data, [65, 3], [64, 2]), Ok([7, 8, 9, 255]));
    }

    #[test]
    fn reads_an_aligned_row_and_preserves_background_and_alpha() {
        let mut data = vec![0; 256 * 2];
        data[256 + 63 * 4..256 + 64 * 4].copy_from_slice(&[0, 0, 0, 255]);
        assert_eq!(rgba8_pixel(&data, [64, 2], [63, 1]), Ok([0, 0, 0, 255]));
        assert_eq!(rgba8_pixel(&data, [64, 2], [0, 0]), Ok([0, 0, 0, 0]));
    }

    #[test]
    fn rejects_tight_truncated_and_oversized_buffers() {
        for data in [vec![0; 65 * 4 * 2], vec![0; 1023], vec![0; 1025]] {
            assert!(rgba8_pixel(&data, [65, 2], [0, 1]).is_err());
        }
    }

    #[test]
    fn rejects_empty_dimensions_and_out_of_bounds_pixels() {
        assert!(rgba8_pixel(&[], [0, 1], [0, 0]).is_err());
        assert!(rgba8_pixel(&[], [1, 0], [0, 0]).is_err());
        let data = vec![0; 512 * 2];
        assert!(rgba8_pixel(&data, [65, 2], [65, 0]).is_err());
        assert!(rgba8_pixel(&data, [65, 2], [0, 2]).is_err());
    }
}
