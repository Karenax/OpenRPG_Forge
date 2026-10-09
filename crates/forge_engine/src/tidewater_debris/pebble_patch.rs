//! Acquired pebble patch and linear RGBA8 density mask, before GPU publication.

/// Source vertex inputs; instance cells and placement documents remain separate.
pub struct Patch {
    pub positions: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub slots: Vec<[f32; 4]>,
    pub indices: Vec<u16>,
    pub density_rgba8: Vec<u8>,
}

impl Patch {
    /// Decode little-endian buffers obtained from the verified Debris package.
    /// Retains source slot seeds and triangle order without regenerating geometry.
    ///
    /// # Errors
    /// Refuses mismatched source dimensions, nonfinite attributes, invalid slots,
    /// zero normals and out-of-bounds indices. The caller verifies payload hashes.
    pub fn decode(
        positions: &[u8],
        normals: &[u8],
        slots: &[u8],
        indices: &[u8],
        mask: &[u8],
    ) -> Result<Self, String> {
        if positions.len() != 2864 * 12
            || normals.len() != 2864 * 12
            || slots.len() != 2864 * 16
            || indices.len() != 4704 * 3 * 2
            || mask.len() != 1024 * 1024 * 4
        {
            return Err("Acquired pebble buffer dimensions differ".into());
        }
        let positions = vectors::<3>(positions)?;
        let normals = vectors::<3>(normals)?;
        let slots = vectors::<4>(slots)?;
        let indices: Vec<_> = indices
            .as_chunks::<2>()
            .0
            .iter()
            .map(|v| u16::from_le_bytes(*v))
            .collect();
        if indices.iter().any(|i| usize::from(*i) >= positions.len())
            || normals.iter().any(|n| {
                let squared = n.iter().map(|v| v * v).sum::<f32>();
                !squared.is_finite() || squared <= 0.0
            })
            || slots.iter().any(|slot| {
                !(0.0..4.0).contains(&slot[0])
                    || !(0.0..4.0).contains(&slot[1])
                    || !(0.0..1.0).contains(&slot[2])
                    || ![0.0, 1.0, 2.0].contains(&slot[3])
            })
        {
            return Err("Invalid acquired pebble topology or slots".into());
        }
        Ok(Self {
            positions,
            normals,
            slots,
            indices,
            density_rgba8: mask.to_vec(),
        })
    }
}

fn vectors<const N: usize>(raw: &[u8]) -> Result<Vec<[f32; N]>, String> {
    raw.as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|row| {
            let vector = std::array::from_fn(|i| f32::from_le_bytes(row[i]));
            if vector.iter().any(|v| !v.is_finite()) {
                Err("Nonfinite pebble vertex attribute".into())
            } else {
                Ok(vector)
            }
        })
        .collect()
}
