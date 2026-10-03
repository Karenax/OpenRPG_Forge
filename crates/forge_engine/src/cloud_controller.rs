// SPDX-License-Identifier: MIT
// Adapted from Tidewater SkyProClouds.js, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see third_party/tidewater/LICENSE.
//! CPU cloud clock, camera history and sampling schedule, independent of rendering.
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_precision_loss,
    clippy::cast_sign_loss,
    clippy::many_single_char_names,
    clippy::struct_excessive_bools
)]

#[derive(Clone, Copy, Debug)]
pub struct Camera {
    pub position: [f64; 3],
    /// Column basis of the world matrix, forward looking into the scene.
    pub right: [f64; 3],
    pub up: [f64; 3],
    pub forward: [f64; 3],
    pub fov_degrees: f64,
    pub aspect: f64,
    pub zoom: f64,
}

#[derive(Clone, Copy, Debug)]
pub struct Input {
    pub camera: Camera,
    pub output: [u32; 2],
    pub resolution_scale: f64,
    pub coverage: f64,
    pub wind_direction: [f64; 2],
    pub light_direction: [f64; 3],
    pub underwater: bool,
    pub dt: f64,
}

#[derive(Clone, Debug)]
pub struct Output {
    pub frame: [[f32; 4]; 34],
    pub settings: [[f32; 4]; 13],
    pub public: [[f32; 4]; 5],
    pub weather_dispatch: bool,
    pub shadow_full: bool,
    pub panorama_full: bool,
    pub trace_view: bool,
    pub view_index: usize,
    pub source_size: [u32; 2],
    pub history_size: [u32; 2],
}

#[derive(Clone, Debug, Default)]
pub struct Controller {
    frame_index: u64,
    elapsed: f64,
    wind: [f64; 3],
    previous_camera: Option<[[f64; 4]; 4]>,
    previous_light: [f64; 3],
    previous_coverage: Option<f64>,
    previous_size: Option<([u32; 2], f64)>,
    history_valid: bool,
    panorama_ready: bool,
    shadow_ready: bool,
    weather_ready: bool,
    shadow_center: [f64; 2],
    next_view: usize,
    public: [[f32; 4]; 5],
}

fn length(v: [f64; 3]) -> f64 {
    v[0].hypot(v[1]).hypot(v[2])
}
fn dot(a: [f64; 3], b: [f64; 3]) -> f64 {
    a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
}
fn normalize(v: [f64; 3]) -> [f64; 3] {
    let n = length(v);
    v.map(|x| x / if n == 0.0 { 1.0 } else { n })
}
fn f32s(v: [f64; 4]) -> [f32; 4] {
    v.map(|x| x as f32)
}

const EVOLUTION: f64 = 60.8 * 12.0 / 89.0;

impl Controller {
    pub fn reset_history(&mut self) {
        self.history_valid = false;
    }
    pub fn invalidate(&mut self) {
        self.reset_history();
        self.panorama_ready = false;
    }

    /// Reject invalid inputs before changing state. The source caps elapsed dt at 0.1 s.
    #[allow(clippy::too_many_lines)]
    pub fn update(&mut self, input: Input) -> Result<Output, &'static str> {
        let c = input.camera;
        let scalars = [
            input.dt,
            input.resolution_scale,
            input.coverage,
            c.fov_degrees,
            c.aspect,
            c.zoom,
        ];
        if scalars.iter().any(|v| !v.is_finite())
            || c.position
                .iter()
                .chain(c.right.iter())
                .chain(c.up.iter())
                .chain(c.forward.iter())
                .chain(input.light_direction.iter())
                .chain(input.wind_direction.iter())
                .any(|v| !v.is_finite())
            || input.output.contains(&0)
            || input.output.iter().any(|v| *v > 16384)
            || !(0.0..=1.0).contains(&input.coverage)
            || !(0.0..=2.0).contains(&input.resolution_scale)
            || input.resolution_scale == 0.0
            || !(0.0..180.0).contains(&c.fov_degrees)
            || c.fov_degrees == 0.0
            || c.aspect <= 0.0
            || c.zoom <= 0.0
            || [c.right, c.up, c.forward, input.light_direction]
                .iter()
                .any(|v| length(*v) < 1e-12)
        {
            return Err("invalid cloud controller input");
        }
        if self.frame_index == 0 {
            self.public = [
                [1.0, 0.0, 0.0, 0.0],
                [0.0, 1.0, 0.0, 0.0],
                [0.0, 0.0, -1.0, 0.0],
                [1.0, 1.0, 0.0, 0.0],
                [0.0, 0.0, 12000.0, 0.85],
            ];
        }
        let dt = input.dt.clamp(0.0, 0.1);
        let size = (input.output, input.resolution_scale);
        if self.previous_size != Some(size) {
            self.history_valid = false;
            self.previous_size = Some(size);
        }
        if self.previous_coverage != Some(input.coverage) {
            self.invalidate();
            self.previous_coverage = Some(input.coverage);
        }
        let wl = input.wind_direction[0].hypot(input.wind_direction[1]);
        let wind = input
            .wind_direction
            .map(|v| v / if wl == 0.0 { 1.0 } else { wl });
        let weather_dispatch = !self.weather_ready;
        self.weather_ready = true;
        let tan = (c.fov_degrees * 0.5 * (std::f64::consts::PI / 180.0)).tan() / c.zoom;
        let right = normalize(c.right);
        let up = normalize(c.up);
        let forward = normalize(c.forward);
        let current = [
            [c.position[0], c.position[1], c.position[2], tan],
            [right[0], right[1], right[2], c.aspect],
            [up[0], up[1], up[2], 0.0],
            [forward[0], forward[1], forward[2], 0.0],
        ];
        if self.previous_camera.is_none_or(|p| {
            length(std::array::from_fn(|i| c.position[i] - p[0][i])) > 1000.0
                || dot(forward, [p[3][0], p[3][1], p[3][2]]) < 0.7
                || (tan - p[0][3]).abs() > 0.01
        }) {
            self.history_valid = false;
        }
        if dot(input.light_direction, self.previous_light) < 0.999 {
            self.invalidate();
        }
        self.previous_light = input.light_direction;
        let previous = if self.history_valid {
            self.previous_camera.unwrap_or(current)
        } else {
            current
        };
        self.previous_camera = Some(current);
        let mut frame = [[0.0_f32; 4]; 34];
        for i in 0..4 {
            frame[i] = f32s(current[i]);
            frame[i + 4] = f32s(previous[i]);
        }
        let source_size = input
            .output
            .map(|v| ((f64::from(v) * input.resolution_scale / 8.0).ceil() as u32).max(1));
        let history_size = source_size.map(|v| v * 4);
        frame[8] = f32s([
            f64::from(input.output[0]) * input.resolution_scale,
            f64::from(input.output[1]) * input.resolution_scale,
            f64::from(history_size[0]),
            f64::from(history_size[1]),
        ]);
        let index = self.frame_index % 16;
        let mut lx = 0;
        let mut ly = 0;
        for bit in 0..2 {
            let pair = (index >> (bit * 2)) & 3;
            lx |= ((pair >> 1) ^ (pair & 1)) << (1 - bit);
            ly |= (pair & 1) << (1 - bit);
        }
        frame[9] = [
            lx as f32,
            ly as f32,
            4.0,
            if self.history_valid { 1.0 } else { 0.0 },
        ];
        self.elapsed += dt;
        let dx = wind[0] * 12.0 * dt;
        let dz = wind[1] * 12.0 * dt;
        self.wind[0] += dx;
        self.wind[1] += dz;
        self.wind[2] += EVOLUTION * dt;
        frame[10] = f32s([(self.frame_index % 4096) as f64, self.elapsed, dt, 0.0]);
        frame[11] = f32s([self.wind[0], self.wind[1], self.wind[2], 0.0]);
        frame[12] = f32s([dx, dz, 0.0, 0.0]);
        frame[13] = [25.0, 256.0, 6.0, 0.5];
        frame[14] = f32s([0.9, (EVOLUTION * dt / 100.0).min(1.0), 0.0, 0.0]);
        frame[15] = [0.0, 3.0, 0.0, 0.0];
        let [x, y, z] = input.light_direction;
        let t = normalize(if y.abs() > 0.99 {
            [0.0, -z, y]
        } else {
            [z, 0.0, -x]
        });
        let b = [
            y * t[2] - z * t[1],
            z * t[0] - x * t[2],
            x * t[1] - y * t[0],
        ];
        frame[18] = [0.0, 0.0, 0.0, 25.0];
        for i in 1..8 {
            let growth = 1.7_f64.powf(i as f64);
            let mid = 25.0 * ((growth - 1.0) / 0.7 + growth * 0.5);
            let angle = i as f64 * 2.399_963;
            let radius = ((i as f64 + 0.5) / 6.0).sqrt();
            let u = angle.cos() * radius * 0.05 * mid;
            let v = angle.sin() * radius * 0.05 * mid;
            frame[18 + i] = f32s([
                x * mid + t[0] * u + b[0] * v,
                y * mid + t[1] * u + b[1] * v,
                z * mid + t[2] * u + b[2] * v,
                25.0 * growth,
            ]);
            let footprint = mid * 0.1;
            frame[26 + i] = f32s([
                (footprint * 64.0 / 7500.0).log2().max(0.0),
                (footprint * 64.0 / (7500.0 * 0.13)).log2().max(0.0),
                0.0,
                0.0,
            ]);
        }
        let phase = self.frame_index % 4;
        let shadow_full = !self.shadow_ready;
        if phase == 0 || shadow_full {
            // Math.round ties toward +infinity, unlike Rust round for negative halves.
            self.shadow_center = [c.position[0], c.position[2]].map(|v| {
                let value = ((v / 187.5) + 0.5).floor() * 187.5;
                if value == 0.0 {
                    value.copysign(v)
                } else {
                    value
                }
            });
        }
        frame[17] = f32s([
            self.shadow_center[0],
            self.shadow_center[1],
            12000.0,
            if shadow_full { -1.0 } else { phase as f64 },
        ]);
        self.shadow_ready = true;
        let panorama_full = !self.panorama_ready;
        frame[16] = if panorama_full {
            [0.0, 0.0, 1.0, 0.0]
        } else {
            [(index % 4) as f32, (index / 4) as f32, 4.0, 0.0]
        };
        self.panorama_ready = true;
        let view_index = self.next_view;
        if input.underwater {
            self.public[3][2] = 0.0;
            self.history_valid = false;
        } else {
            self.public[0] = f32s([right[0], right[1], right[2], 0.0]);
            self.public[1] = f32s([up[0], up[1], up[2], 0.0]);
            self.public[2] = f32s([forward[0], forward[1], forward[2], 0.0]);
            self.public[3] = f32s([tan * c.aspect, tan, 1.0, 0.0]);
            self.history_valid = true;
            self.next_view = 1 - self.next_view;
        }
        self.public[4] = f32s([self.shadow_center[0], self.shadow_center[1], 12000.0, 0.85]);
        self.frame_index = self.frame_index.wrapping_add(1);
        Ok(Output {
            frame,
            settings: settings(input.coverage, wind),
            public: self.public,
            weather_dispatch,
            shadow_full,
            panorama_full,
            trace_view: !input.underwater,
            view_index,
            source_size,
            history_size,
        })
    }
}

fn settings(coverage: f64, wind: [f64; 2]) -> [[f32; 4]; 13] {
    [
        [4000.0, 5200.0, 0.019, coverage],
        [29000.0, 7500.0, 0.69, 0.13],
        [0.24, 2.15, 1.0, 0.13],
        [0.54, 0.095, 0.0, 0.0],
        [20000.0, 45000.0, 0.12, 0.0],
        [1.0, 0.7, 0.7, 0.99],
        [0.88, 0.13, 0.65, 0.0],
        [
            0.009_134_058_699_157_796,
            0.015_208_514_418_949_472,
            0.018_500_220_124_016_652,
            0.0,
        ],
        [wind[0], wind[1], 1750.0, 0.0],
        [0.62, 25000.0, 45000.0, 0.0],
        [4.0, 5.0, 0.0, 1.32],
        [6.0, 6.0, 1.0, 0.13],
        [0.26, 0.0, 0.0, 0.0],
    ]
    .map(f32s)
}

#[must_use]
pub fn bytes<const N: usize>(values: &[[f32; 4]; N]) -> Vec<u8> {
    values
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    fn input() -> Input {
        Input {
            camera: Camera {
                position: [0.0, 14.0, -90.0],
                right: [1.0, 0.0, 0.0],
                up: [0.0, 1.0, 0.0],
                forward: [0.0, 0.0, -1.0],
                fov_degrees: 62.0,
                aspect: 16.0 / 9.0,
                zoom: 1.0,
            },
            output: [1920, 1080],
            resolution_scale: 1.0,
            coverage: 0.49,
            wind_direction: [1.0, 0.0],
            light_direction: [0.0, 1.0, 0.0],
            underwater: false,
            dt: 1.0 / 60.0,
        }
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact GPU flags and the source lattice cycle, including signed-zero evidence."
    )]
    fn warm_start_lattice_cycle_and_negative_zero() {
        let mut c = Controller::default();
        let mut lattice = std::collections::BTreeSet::new();
        for step in 0..32 {
            let o = c.update(input()).unwrap();
            assert_eq!(o.weather_dispatch, step == 0);
            assert_eq!(o.shadow_full, step == 0);
            assert_eq!(o.panorama_full, step == 0);
            assert_eq!(o.view_index, step % 2);
            assert_eq!(o.source_size, [240, 135]);
            assert_eq!(o.history_size, [960, 540]);
            assert_eq!(o.frame[9][3], if step == 0 { 0.0 } else { 1.0 });
            assert_eq!(o.public[4][1].to_bits(), (-0.0_f32).to_bits());
            if step < 16 {
                lattice.insert((o.frame[9][0].to_bits(), o.frame[9][1].to_bits()));
            }
        }
        assert_eq!(lattice.len(), 16);
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact 0/1 invalidation flags from the source controller."
    )]
    fn cuts_coverage_light_and_underwater_invalidate_the_correct_passes() {
        let mut c = Controller::default();
        let mut i = input();
        c.update(i).unwrap();
        i.camera.position[0] = 1001.0;
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[9][3], 0.0);
        assert!(!o.panorama_full);
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[9][3], 1.0);
        i.coverage = 0.85;
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[9][3], 0.0);
        assert!(o.panorama_full);
        assert!(!o.weather_dispatch);
        i.light_direction = [1.0, 0.0, 0.0];
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[9][3], 0.0);
        assert!(o.panorama_full);
        i.underwater = true;
        let o = c.update(i).unwrap();
        assert!(!o.trace_view);
        assert_eq!(o.public[3][2], 0.0);
        i.underwater = false;
        let o = c.update(i).unwrap();
        assert!(o.trace_view);
        assert_eq!(o.frame[9][3], 0.0);
        assert_eq!(o.public[3][2], 1.0);
        i.output = [1280, 720];
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[9][3], 0.0);
        assert_eq!(o.source_size, [160, 90]);
        c.invalidate();
        assert!(c.update(i).unwrap().panorama_full);
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact clamp endpoints and unchanged uniform values after refused input."
    )]
    fn invalid_input_is_atomic_and_elapsed_time_is_clamped() {
        let mut c = Controller::default();
        let mut i = input();
        c.update(i).unwrap();
        let mut before = c.clone();
        i.coverage = f64::NAN;
        assert!(c.update(i).is_err());
        i = input();
        assert_eq!(
            bytes(&c.update(i).unwrap().frame),
            bytes(&before.update(i).unwrap().frame)
        );
        i.dt = 100.0;
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[10][2], 0.1);
        i.dt = -3.0;
        let o = c.update(i).unwrap();
        assert_eq!(o.frame[10][2], 0.0);
        assert_eq!(o.frame[12], [0.0; 4]);
    }
}
