//! Map authored waveform frequency onto the shared source clock and gust transport.
use crate::source_wind::vegetation::Clock;
use bevy::{math::DVec4, prelude::*};

/// Prepare a candidate only. The host commits its clock after the GPU-facing
/// frame update succeeds; failed publication cannot consume the previous pose.
pub(crate) fn advance(
    clock: &Clock,
    delta_seconds: f64,
    direction: [f64; 2],
    speed: f64,
    frequency_hz: f64,
) -> Result<(Clock, Vec4, Vec4), String> {
    if !delta_seconds.is_finite()
        || delta_seconds < 0.0
        || !frequency_hz.is_finite()
        || !(0.0..=10.0).contains(&frequency_hz)
    {
        return Err("Invalid authored wind frequency or scene delta".into());
    }
    let mut candidate = clock.clone();
    let pose = candidate.advance(delta_seconds.min(0.1), direction, speed)?;
    let phase = candidate.time_seconds() * std::f64::consts::TAU * frequency_hz;
    let wind = DVec4::new(direction[0], direction[1], speed, phase).as_vec4();
    let index = u8::try_from(pose.index % 64).map_err(|e| e.to_string())?;
    let gust = Vec4::new(pose.offset[0], pose.offset[1], f32::from(index), 0.0);
    super::Frame::validate(Vec3::ZERO, wind, gust)?;
    Ok((candidate, wind, gust))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn module_gusts_and_counter_match_reference_while_frequency_only_changes_waveform() {
        let mut reference = Clock::from_acquired(0.0, 62, [0.0; 2], [1.0, 0.0], 7.0).unwrap();
        let mut module = reference.clone();
        for (delta, direction) in [
            (1.0_f64 / 60.0, [1.0, 0.0]),
            (0.25, [0.0, 1.0]),
            (0.0, [0.0, -1.0]),
            (0.031, [-1.0, 0.0]),
        ] {
            let expected = reference.advance(delta.min(0.1), direction, 7.0).unwrap();
            let (next, wind, gust) = advance(&module, delta, direction, 7.0, 0.2).unwrap();
            assert_eq!(next, reference);
            assert_eq!(
                gust.xy().to_array().map(f32::to_bits),
                expected.offset.map(f32::to_bits)
            );
            assert_eq!(
                gust.z.to_bits(),
                f32::from(u8::try_from(expected.index % 64).unwrap()).to_bits()
            );
            let phase = DVec4::new(
                direction[0],
                direction[1],
                7.0,
                reference.time_seconds() * std::f64::consts::TAU * 0.2,
            )
            .as_vec4();
            assert_eq!(
                wind.to_array().map(f32::to_bits),
                phase.to_array().map(f32::to_bits)
            );
            module = next;
        }
        let (_, frozen, transported) = advance(&module, 0.1, [1.0, 0.0], 7.0, 0.0).unwrap();
        assert_eq!(frozen.w.to_bits(), 0.0_f32.to_bits());
        assert_ne!(transported.xy(), Vec2::from_array(module.pose().offset));
    }

    #[test]
    fn invalid_samples_preserve_source_clock_and_finite_gpu_contract() {
        let clock = Clock::new();
        let before = clock.clone();
        for (delta, direction, speed, frequency) in [
            (f64::NAN, [1.0, 0.0], 7.0, 0.2),
            (-1.0, [1.0, 0.0], 7.0, 0.2),
            (0.1, [0.0, 0.0], 7.0, 0.2),
            (0.1, [1.0, 0.0], 26.0, 0.2),
            (0.1, [1.0, 0.0], 7.0, f64::NAN),
            (0.1, [1.0, 0.0], 7.0, 10.1),
        ] {
            assert!(advance(&clock, delta, direction, speed, frequency).is_err());
            assert_eq!(clock, before);
        }
    }

    #[test]
    fn material_time_is_shared_seconds_even_when_vegetation_frequency_is_zero() {
        let initial = Clock::from_acquired(2.0, 0, [0.0; 2], [1.0, 0.0], 7.0).unwrap();
        let (frozen, vegetation_frozen, _) = advance(&initial, 0.1, [1.0, 0.0], 7.0, 0.0).unwrap();
        let (moving, vegetation_moving, _) = advance(&initial, 0.1, [1.0, 0.0], 7.0, 0.2).unwrap();
        assert_eq!(frozen, moving);
        assert_eq!(
            frozen.pose().material_uniform(),
            Vec4::new(1.0, 0.0, 7.0, 2.1)
        );
        assert_eq!(vegetation_frozen.w.to_bits(), 0.0_f32.to_bits());
        assert_ne!(vegetation_moving.w.to_bits(), vegetation_frozen.w.to_bits());
        let (paused, _, _) = advance(&frozen, 0.0, [1.0, 0.0], 7.0, 0.0).unwrap();
        assert_eq!(
            paused.pose().material_uniform(),
            frozen.pose().material_uniform()
        );
    }
}
