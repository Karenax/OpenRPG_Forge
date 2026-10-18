//! Source distance policy, evaluated in f64 like the pinned JavaScript culling.
#[derive(Clone, Copy, Debug)]
pub struct Draw {
    pub lod: usize,
    pub fade: f32,
    pub outgoing: bool,
}

#[derive(Debug)]
pub struct Selection {
    pub draws: Vec<Draw>,
    /// Shadow selection precedes the camera frustum test in the source.
    pub shadow: bool,
}

/// Conservative broad phase for a group enclosing every member's draw sphere.
/// Off-screen groups can still cast source shadows within 22 metres.
#[must_use]
pub fn group_may_contribute(distance: f32, radius: f32, frustum_intersects: bool) -> bool {
    if !distance.is_finite() || !radius.is_finite() || distance < 0.0 || radius < 0.0 {
        return true;
    }
    distance <= 90.0 + radius && (frustum_intersects || distance < 22.0 + radius)
}

/// Retain source thresholds, inclusive draw range and complementary LOD bands.
///
/// # Errors
/// Refuses non-finite or negative distances and non-positive lengths.
#[allow(
    clippy::cast_possible_truncation,
    reason = "GPU fade uniforms use float32; source distance arithmetic remains f64."
)]
pub fn select(distance: f64, length: f64, frustum_intersects: bool) -> Result<Selection, String> {
    if !distance.is_finite() || distance < 0.0 || !length.is_finite() || length <= 0.0 {
        return Err("Invalid fish culling inputs".into());
    }
    let mut selection = Selection {
        draws: Vec::new(),
        shadow: false,
    };
    if distance > 90.0 {
        return Ok(selection);
    }
    selection.shadow = distance < 22.0;
    if !frustum_intersects {
        return Ok(selection);
    }
    let threshold = length * 28.0;
    if distance > 81.0 {
        selection.draws.push(Draw {
            lod: usize::from(distance >= threshold),
            fade: (1.0 - band(distance, 81.0, 90.0)) as f32,
            outgoing: false,
        });
    } else if distance > threshold * 0.88 && distance < threshold {
        let fade = band(distance, threshold * 0.88, threshold) as f32;
        selection.draws.push(Draw {
            lod: 0,
            fade,
            outgoing: true,
        });
        selection.draws.push(Draw {
            lod: 1,
            fade,
            outgoing: false,
        });
    } else {
        selection.draws.push(Draw {
            lod: usize::from(distance >= threshold),
            fade: 1.0,
            outgoing: false,
        });
    }
    Ok(selection)
}

fn band(distance: f64, start: f64, end: f64) -> f64 {
    let t = ((distance - start) / (end - start).max(1e-6)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde::Deserialize;

    #[test]
    fn dormant_groups_retain_near_shadows_and_wake_at_draw_boundary() {
        assert!(group_may_contribute(100.0, 10.0, true));
        assert!(!group_may_contribute(100.1, 10.0, true));
        assert!(group_may_contribute(31.9, 10.0, false));
        assert!(!group_may_contribute(32.0, 10.0, false));
        assert!(group_may_contribute(32.0, 10.0, true));
        assert!(group_may_contribute(f32::NAN, 10.0, false));
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
            let actual = select(distance, length, case.frustum_intersects).unwrap();
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
        assert!(select(f64::NAN, 1.0, true).is_err());
        assert!(select(-1.0, 1.0, true).is_err());
        assert!(select(1.0, 0.0, true).is_err());
        assert!(select(1.0, f64::INFINITY, true).is_err());
    }
}
