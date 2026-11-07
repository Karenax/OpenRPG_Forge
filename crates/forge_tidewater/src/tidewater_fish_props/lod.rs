//! Data-specific distances for the shared engine detail policy.

pub const POLICY: forge_engine::distance_lod::Policy =
    match forge_engine::distance_lod::Policy::new(90.0, 81.0, 22.0, 28.0, 0.88) {
        Ok(policy) => policy,
        Err(_) => panic!("Invalid acquired fish LOD policy"),
    };

#[cfg(test)]
mod tests {
    use super::*;
    use serde::Deserialize;

    #[test]
    fn dormant_groups_retain_near_shadows_and_wake_at_draw_boundary() {
        assert!(POLICY.group_may_contribute(100.0, 10.0, true));
        assert!(!POLICY.group_may_contribute(100.1, 10.0, true));
        assert!(POLICY.group_may_contribute(31.9, 10.0, false));
        assert!(!POLICY.group_may_contribute(32.0, 10.0, false));
        assert!(POLICY.group_may_contribute(32.0, 10.0, true));
        assert!(POLICY.group_may_contribute(f32::NAN, 10.0, false));
    }

    #[derive(Deserialize)]
    struct Reference {
        cases: Vec<Case>,
        intact_source_cull_executed: bool,
    }
    #[derive(Deserialize)]
    struct Case {
        distance_bits: String,
        length_bits: String,
        frustum_intersects: bool,
        shadow: bool,
        draws: Vec<Expected>,
    }
    #[derive(Deserialize)]
    struct Expected {
        lod: usize,
        fade: f64,
        outgoing: bool,
    }

    #[test]
    #[allow(
        clippy::cast_possible_truncation,
        reason = "The source uploads fade as float32."
    )]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn matches_all_intact_source_culling_cases() {
        let path = std::env::var("FORGE_TIDEWATER_FISH_LOD_REFERENCE").unwrap();
        let bytes = std::fs::read(path).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&bytes).to_string(),
            "6ba2f67453d1a302fab9dac055a90e17ada1c03153382d0d585b2e1c951ff1ae"
        );
        let reference: Reference = serde_json::from_slice(&bytes).unwrap();
        assert!(reference.intact_source_cull_executed);
        assert_eq!(reference.cases.len(), 3432);
        let mut off_camera_shadows = 0;
        let mut fades = 0;
        for case in reference.cases {
            let distance = f64::from_bits(u64::from_str_radix(&case.distance_bits, 16).unwrap());
            let length = f64::from_bits(u64::from_str_radix(&case.length_bits, 16).unwrap());
            let actual = POLICY
                .select(distance, length, case.frustum_intersects)
                .unwrap();
            assert_eq!(actual.shadow, case.shadow);
            assert_eq!(actual.draws.len(), case.draws.len());
            for (draw, expected) in actual.draws.iter().zip(&case.draws) {
                assert_eq!(draw.lod, expected.lod);
                assert_eq!(draw.outgoing, expected.outgoing);
                assert!((draw.fade - expected.fade as f32).abs() <= 1e-6);
            }
            if !case.frustum_intersects && actual.shadow {
                off_camera_shadows += 1;
            }
            if actual.draws.len() == 2 {
                fades += 1;
            }
        }
        assert!(off_camera_shadows > 0 && fades > 0);
    }

    #[test]
    fn invalid_culling_inputs_are_refused() {
        assert!(POLICY.select(f64::NAN, 1.0, true).is_err());
        assert!(POLICY.select(-1.0, 1.0, true).is_err());
        assert!(POLICY.select(1.0, 0.0, true).is_err());
        assert!(POLICY.select(1.0, f64::INFINITY, true).is_err());
    }
}
