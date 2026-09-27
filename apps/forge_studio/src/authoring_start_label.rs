//! Presentation of persisted starts, preserving the distinction between absent and zero heading.
use forge_project::zone_authoring::start::ZoneStart;

pub(super) fn describe(start: Option<&ZoneStart>) -> String {
    start.map_or_else(
        || "aucun départ enregistré (départ par défaut)".into(),
        |start| {
            let heading = start
                .facing_degrees
                .map_or_else(|| "cap par défaut".into(), |angle| format!("cap {angle}°"));
            format!("X {} m · Z {} m · {heading}", start.x_m, start.z_m)
        },
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn start_label_distinguishes_absent_heading_from_explicit_zero() {
        let mut start = ZoneStart {
            x_m: 40.0,
            z_m: -2.0,
            facing_degrees: None,
        };
        assert_eq!(describe(Some(&start)), "X 40 m · Z -2 m · cap par défaut");
        start.facing_degrees = Some(0.0);
        assert_eq!(describe(Some(&start)), "X 40 m · Z -2 m · cap 0°");
        start.facing_degrees = Some(90.0);
        assert_eq!(describe(Some(&start)), "X 40 m · Z -2 m · cap 90°");
        assert!(describe(None).contains("aucun départ enregistré"));
    }
}
