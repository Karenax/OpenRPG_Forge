//! Acquired scanned CPU LOD bands; frustum ownership stays with the live camera adapter.
//! Source: Tidewater 1438b1ab `ScannedDebris::update` and `LODFade::bandFade`.

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Draw {
    pub lod: u8,
    pub fade: f32,
    pub outgoing: bool,
}

impl Draw {
    /// The acquired source casts scanned shadows only from the near LOD.
    #[must_use]
    pub const fn casts_shadow(self) -> bool {
        self.lod == 0
    }
}

fn band(distance: f64, start: f64, end: f64) -> f64 {
    let t = (distance - start) / (end - start).max(1e-6);
    if t <= 0.0 {
        0.0
    } else if t >= 1.0 {
        1.0
    } else {
        t * t * (3.0 - 2.0 * t)
    }
}

/// Select source LOD buckets using source double-precision thresholds.
///
/// `frustum_intersects` must test the instance sphere expanded by six metres,
/// matching the source's inclusion of casters just outside the camera view.
///
/// # Errors
/// Rejects nonfinite or negative world distances and sphere radii.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Source emits fade through a Float32Array after double-precision selection."
)]
pub fn select(
    distance_m: f64,
    radius_m: f64,
    frustum_intersects: bool,
) -> Result<Vec<Draw>, String> {
    if !distance_m.is_finite() || !radius_m.is_finite() || distance_m < 0.0 || radius_m < 0.0 {
        return Err("Invalid scanned LOD distance or radius".into());
    }
    let mut draws = Vec::with_capacity(2);
    if !frustum_intersects {
        return Ok(draws);
    }
    let distance = distance_m - radius_m * 6.0;
    let t01 = band(
        distance,
        22.0 * (1.0 - 0.12 / 2.0),
        22.0 * (1.0 + 0.12 / 2.0),
    );
    let t12 = band(
        distance,
        70.0 * (1.0 - 0.12 / 2.0),
        70.0 * (1.0 + 0.12 / 2.0),
    );
    let out = band(distance, 260.0 * (1.0 - 0.12), 260.0);
    if out >= 1.0 {
        return Ok(draws);
    }
    if t01 < 1.0 {
        draws.push(Draw {
            lod: 0,
            fade: t01 as f32,
            outgoing: true,
        });
    }
    if t01 > 0.0 && t12 < 1.0 {
        draws.push(Draw {
            lod: 1,
            fade: if t12 > 0.0 { t12 } else { t01 } as f32,
            outgoing: t12 > 0.0,
        });
    }
    if t12 > 0.0 {
        draws.push(Draw {
            lod: 2,
            fade: (t12 * (1.0 - out)) as f32,
            outgoing: false,
        });
    }
    Ok(draws)
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
