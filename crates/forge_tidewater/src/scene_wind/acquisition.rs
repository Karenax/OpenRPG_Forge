use super::AcquiredFrame;

#[cfg(test)]
mod tests {
    use super::*;

    #[cfg(feature = "physics-rapier")]
    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact acquired clocks and retained payloads must not admit a tolerance"
    )]
    fn retains_independent_source_histories_and_gust_phase() {
        let (bytes, index) =
            forge_tidewater_assets::tidewater_environment::animation_reference().unwrap();
        let frame = AcquiredFrame::from_json(&bytes, index).expect("actual acquired source frame");
        assert_eq!(frame.global_frame, 124);
        assert!((frame.ocean_time_s - frame.global_time_s - 4.0 / 60.0).abs() < 1e-12);
        assert_eq!(frame.cloud_clock[1], frame.ocean_time_s);
        assert!(
            frame
                .vegetation_gust_offset
                .iter()
                .all(|value| *value > 0.0)
        );
        assert_eq!(
            frame.wind_frame().unwrap().time_seconds(),
            frame.global_time_s
        );
    }
}
