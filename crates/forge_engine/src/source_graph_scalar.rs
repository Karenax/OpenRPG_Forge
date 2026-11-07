//! Checked scalar projection shared by imported scene adapters.
use serde_json::Value;
/// Project a declared scalar only when it remains finite on the GPU.
pub fn number(value: &Value, field: &str) -> Result<f32, String> {
    let source = value
        .as_f64()
        .ok_or_else(|| format!("{field} is not numeric"))?;
    let projected: f32 =
        serde_json::from_value(value.clone()).map_err(|error| format!("{field}: {error}"))?;
    if !source.is_finite() || !projected.is_finite() {
        return Err(format!(
            "{field} cannot be represented as a finite GPU scalar"
        ));
    }
    Ok(projected)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn refuses_non_numeric_and_gpu_overflow() {
        for v in [
            serde_json::json!(null),
            serde_json::json!("2"),
            serde_json::json!(1e100),
            serde_json::json!(-1e100),
        ] {
            assert!(number(&v, "scene scalar").is_err());
        }
        for v in [0.0_f32, -0.0, 1.25, -83.5, f32::MAX, f32::MIN_POSITIVE] {
            assert_eq!(
                number(&serde_json::json!(f64::from(v)), "scene scalar")
                    .unwrap()
                    .to_bits(),
                v.to_bits()
            );
        }
    }
}
