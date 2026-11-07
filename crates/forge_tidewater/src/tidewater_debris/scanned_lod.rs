//! Acquired distances for the shared three-level engine detail policy.

pub use forge_engine::distance_lod::fixed_bands::Draw;
use forge_engine::distance_lod::fixed_bands::Policy;

const POLICY: Policy = match Policy::new(22.0, 70.0, 260.0, 0.12, 6.0, Some(0)) {
    Ok(policy) => policy,
    Err(_) => panic!("Invalid acquired scanned LOD policy"),
};

/// Select source LOD buckets using source double-precision thresholds.
///
/// `frustum_intersects` must test the instance sphere expanded by six metres,
/// matching the source's inclusion of casters just outside the camera view.
///
/// # Errors
/// Rejects nonfinite or negative world distances and sphere radii.
pub fn select(
    distance_m: f64,
    radius_m: f64,
    frustum_intersects: bool,
) -> Result<Vec<Draw>, String> {
    POLICY.select(distance_m, radius_m, frustum_intersects)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the acquired source CPU LOD oracle"]
    fn matches_acquired_source_bands_and_shadow_population() {
        let path = std::env::var("FORGE_DEBRIS_SCANNED_LOD_REFERENCE").unwrap();
        let raw = std::fs::read(path).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&raw).to_string(),
            "ed31bc51b6592a8917b8dfcd6744881fdbc013549831e3c4ac07ab110def7454"
        );
        let reference: serde_json::Value = serde_json::from_slice(&raw).unwrap();
        let cases = reference["cases"].as_array().unwrap();
        assert_eq!(cases.len(), 7332);
        for case in cases {
            let radius = case["radius_m"].as_f64().unwrap();
            let distance = case["effective_distance"].as_f64().unwrap() + radius * 6.0;
            let depth = if case["away"].as_bool().unwrap() {
                -distance
            } else {
                distance
            };
            let visible = depth + radius + 6.0 >= 0.01 && depth - (radius + 6.0) <= 10000.0;
            let actual = select(distance, radius, visible).unwrap();
            let expected = case["draws"].as_array().unwrap();
            assert_eq!(actual.len(), expected.len(), "{}", case["source_id"]);
            for (draw, source) in actual.iter().zip(expected) {
                assert_eq!(u64::from(draw.lod), source["lod"].as_u64().unwrap());
                assert!(
                    (f64::from(draw.fade) - source["iData_f32"][2].as_f64().unwrap()).abs() < 1e-7
                );
                assert_eq!(
                    draw.outgoing,
                    source["iData_f32"][3].as_f64().unwrap() > 0.5
                );
                assert_eq!(
                    draw.casts_shadow(),
                    source["casts_shadow"].as_bool().unwrap()
                );
            }
        }
        assert!(select(f64::NAN, 1.0, true).is_err());
        assert!(select(1.0, -1.0, true).is_err());
    }
}
