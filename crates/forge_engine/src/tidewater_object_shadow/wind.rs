// SPDX-License-Identifier: MIT
// Adapted from the Tidewater vegetation wind convention, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see third_party/tidewater/LICENSE.
//! CPU clock for source-compatible animated vegetation wind.
#![allow(clippy::cast_possible_truncation, clippy::cast_precision_loss)] // f64-to-f32 uniform conversions are rejected when the result is non-finite.

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Pose {
    pub time: f32,
    pub index: u32,
    pub offset: [f32; 2],
    pub dir: [f32; 2],
    pub speed: f32,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Clock {
    time: f64,
    index: u32,
    offset: [f64; 2],
    dir: [f64; 2],
    speed: f64,
}

impl Default for Clock {
    fn default() -> Self {
        Self {
            time: 2.0,
            index: 124,
            offset: [0.0; 2],
            dir: [1.0, 0.0],
            speed: 6.0,
        }
    }
}

impl Clock {
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    #[must_use]
    pub fn pose(&self) -> Pose {
        Pose {
            time: self.time as f32,
            index: self.index,
            offset: self.offset.map(|value| value as f32),
            dir: self.dir.map(|value| value as f32),
            speed: self.speed as f32,
        }
    }

    /// Advance one rendered source frame without losing accumulated gust displacement.
    pub fn advance(&mut self, dt: f64, dir: [f64; 2], speed: f64) -> Result<Pose, &'static str> {
        if !dt.is_finite()
            || dt < 0.0
            || !speed.is_finite()
            || speed < 0.0
            || dir.iter().any(|value| !value.is_finite())
        {
            return Err("invalid plant wind motion");
        }
        let gust_speed = 0.7 * speed + 1.5;
        let next_time = self.time + dt;
        let next_offset =
            std::array::from_fn(|index| self.offset[index] + dir[index] * gust_speed * dt);
        let pose = Pose {
            time: next_time as f32,
            index: self.index.wrapping_add(1),
            offset: next_offset.map(|value| value as f32),
            dir: dir.map(|value| value as f32),
            speed: speed as f32,
        };
        if !next_time.is_finite()
            || next_offset.iter().any(|value| !value.is_finite())
            || !pose.time.is_finite()
            || pose.offset.iter().any(|value| !value.is_finite())
            || pose.dir.iter().any(|value| !value.is_finite())
            || !pose.speed.is_finite()
        {
            return Err("invalid plant wind motion");
        }
        self.time = next_time;
        self.index = pose.index;
        self.offset = next_offset;
        self.dir = dir;
        self.speed = speed;
        Ok(pose)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn starts_at_the_source_pose() {
        let clock = Clock::new();
        assert_eq!(
            clock.pose(),
            Pose {
                time: 2.0,
                index: 124,
                offset: [0.0; 2],
                dir: [1.0, 0.0],
                speed: 6.0,
            }
        );
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact source frame constants and unchanged orthogonal components."
    )]
    fn advances_one_source_frame() {
        let mut clock = Clock::new();
        let pose = clock.advance(1.0 / 60.0, [1.0, 0.0], 6.0).unwrap();
        assert_eq!(pose.index, 125);
        assert!((pose.time - (2.0 + 1.0 / 60.0) as f32).abs() < f32::EPSILON);
        assert!((pose.offset[0] - 0.095).abs() < f32::EPSILON);
        assert_eq!(pose.offset[1], 0.0);
        assert_eq!(pose.dir, [1.0, 0.0]);
        assert_eq!(pose.speed, 6.0);
    }

    #[test]
    fn accumulates_direction_changes_without_a_jump() {
        let mut clock = Clock::new();
        clock.advance(1.0 / 60.0, [1.0, 0.0], 6.0).unwrap();
        let pose = clock.advance(1.0 / 60.0, [0.0, 1.0], 6.0).unwrap();
        assert_eq!(pose.index, 126);
        assert!((pose.offset[0] - 0.095).abs() < f32::EPSILON);
        assert!((pose.offset[1] - 0.095).abs() < f32::EPSILON);
    }

    #[test]
    fn rejects_invalid_inputs_without_mutating_the_clock() {
        let mut clock = Clock::new();
        let before = clock.clone();
        for (dt, dir, speed) in [
            (f64::NAN, [1.0, 0.0], 6.0),
            (-1.0, [1.0, 0.0], 6.0),
            (1.0, [f64::INFINITY, 0.0], 6.0),
            (1.0, [1.0, 0.0], -1.0),
            (1.0, [1.0, 0.0], f64::NAN),
            (f64::MAX, [1.0, 0.0], 6.0),
        ] {
            assert!(clock.advance(dt, dir, speed).is_err());
            assert_eq!(clock, before);
        }
    }

    #[test]
    fn wraps_the_source_frame_index() {
        let mut clock = Clock::new();
        clock.index = u32::MAX;
        let pose = clock.advance(0.0, [1.0, 0.0], 6.0).unwrap();
        assert_eq!(pose.index, 0);
        assert_eq!(clock.index, 0);
    }
}
