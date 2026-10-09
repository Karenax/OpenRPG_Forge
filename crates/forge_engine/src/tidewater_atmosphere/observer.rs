//! Validate acquired observer changes without altering static scattering inputs.
pub(crate) fn validate(params: &[u8], baseline: &[u8]) -> bool {
    if params.len() != 64
        || baseline.len() != 64
        || params[..28] != baseline[..28]
        || params[32..] != baseline[32..]
    {
        return false;
    }
    let radius = f32::from_le_bytes(params[28..32].try_into().expect("four validated bytes"));
    radius.is_finite() && radius > 0.0
}

#[cfg(test)]
mod tests {
    use super::*;
    fn baseline() -> Vec<u8> {
        [
            0.1_f32, 0.2, 0.3, 6360.0, 0.4, 0.5, 0.6, 6360.014, 1.0, 2.0, 3.0, 4.0, 0.0, 1.0, 0.0,
            5.0,
        ]
        .into_iter()
        .flat_map(f32::to_le_bytes)
        .collect()
    }
    #[test]
    fn accepts_source_coast_then_village_with_identical_scattering() {
        let base = baseline();
        let mut coast = base.clone();
        coast[28..32].copy_from_slice(&6360.004_f32.to_le_bytes());
        assert!(validate(&coast, &base));
        assert!(validate(&base, &base));
    }
    #[test]
    fn rejects_nonfinite_nonpositive_and_truncated_observers() {
        let base = baseline();
        for radius in [f32::NAN, f32::INFINITY, 0.0, -1.0] {
            let mut value = base.clone();
            value[28..32].copy_from_slice(&radius.to_le_bytes());
            assert!(!validate(&value, &base));
        }
        assert!(!validate(&base[..63], &base));
        assert!(!validate(&base, &base[..63]));
    }
    #[test]
    fn rejects_changes_to_each_other_parameter_lane() {
        let base = baseline();
        for lane in (0..16).filter(|lane| *lane != 7) {
            let mut value = base.clone();
            value[lane * 4..lane * 4 + 4].copy_from_slice(&99.0_f32.to_le_bytes());
            assert!(!validate(&value, &base));
        }
    }
}
