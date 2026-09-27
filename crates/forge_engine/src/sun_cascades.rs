// SPDX-License-Identifier: MIT
// Adapted from Tidewater engine/render/Shadows.js, copyright 2026 DRG Software
// Solutions LLC, revision 1438b1ab. See third_party/tidewater/LICENSE.
//! Stable standard-depth sun cascades. GPU passes must render every returned
//! index before publishing the updated matrices to receivers.
use bevy::math::{DMat4, DVec3, DVec4};

#[derive(Clone, Debug)]
pub struct CascadeConfig {
    pub size: u32,
    pub splits: Vec<f64>,
    pub light_margin: f64,
    pub normal_bias: Vec<f64>,
}

impl Default for CascadeConfig {
    fn default() -> Self {
        Self {
            size: 2048,
            splits: vec![10.0, 60.0, 400.0],
            light_margin: 200.0,
            normal_bias: vec![0.015, 0.06, 0.3],
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct CascadeView {
    pub world_from_view: DMat4,
    pub near: f64,
    pub fov_degrees: f64,
    pub aspect: f64,
}

#[derive(Clone, Copy, Debug)]
pub struct SunCascade {
    pub world_from_view: DMat4,
    pub view_from_world: DMat4,
    pub clip_from_view: DMat4,
    pub clip_from_world: DMat4,
    pub radius: f64,
    pub near: f64,
    pub far: f64,
    /// Far slice distance, world texel size, normal bias, depth range.
    pub info: [f64; 4],
    /// Nominal near/far split and their seam widths.
    pub blend: [f64; 4],
}

#[derive(Debug)]
pub struct SunCascades {
    config: CascadeConfig,
    cascades: Vec<Option<SunCascade>>,
    dirty: Vec<bool>,
    frame: u64,
    last_sun: DVec3,
    pub sampling_enabled: bool,
}

impl SunCascades {
    pub fn new(config: CascadeConfig) -> Result<Self, &'static str> {
        if !(1..=4).contains(&config.splits.len())
            || !(64..=8192).contains(&config.size)
            || !config.size.is_power_of_two()
            || !config.light_margin.is_finite()
            || !(0.0..=10000.0).contains(&config.light_margin)
            || config
                .splits
                .iter()
                .any(|v| !v.is_finite() || !(0.2..=100_000.0).contains(v))
            || config.splits.windows(2).any(|v| v[0] >= v[1])
            || config.normal_bias.len() != config.splits.len()
            || config
                .normal_bias
                .iter()
                .any(|v| !v.is_finite() || !(0.0..=100.0).contains(v))
        {
            return Err("invalid sun cascade configuration");
        }
        let count = config.splits.len();
        Ok(Self {
            config,
            cascades: vec![None; count],
            dirty: vec![true; count],
            frame: 0,
            last_sun: DVec3::new(0.0, -2.0, 0.0),
            sampling_enabled: false,
        })
    }

    #[must_use]
    pub fn cascades(&self) -> &[Option<SunCascade>] {
        &self.cascades
    }

    /// Use after caster edits or an aborted GPU update, so stale maps cannot
    /// survive their ordinary staggered refresh interval.
    pub fn invalidate(&mut self) {
        self.dirty.fill(true);
    }

    pub fn update(
        &mut self,
        view: CascadeView,
        sun: DVec3,
        enabled: bool,
    ) -> Result<Vec<usize>, &'static str> {
        validate_view(view)?;
        if !sun.is_finite() || (sun.length_squared() - 1.0).abs() > 1e-6 {
            return Err("sun direction must be finite and normalized");
        }
        self.frame = self.frame.wrapping_add(1);
        self.sampling_enabled = enabled && sun.y > -0.05;
        if !enabled {
            return Ok(Vec::new());
        }
        let angle = (self.last_sun.dot(sun) / (self.last_sun.length() * sun.length()))
            .clamp(-1.0, 1.0)
            .acos();
        let moved = angle > 1e-4;
        self.last_sun = sun;
        let mut updated = Vec::new();
        for i in 0..self.cascades.len() {
            let period = match i {
                0 => 1,
                1 => 2,
                _ => 4,
            };
            if moved || self.dirty[i] || self.frame.wrapping_add(i as u64).is_multiple_of(period) {
                let previous_eye = self.cascades[i].map_or(DVec4::W, |c| c.world_from_view.w_axis);
                self.cascades[i] = Some(fit(&self.config, i, view, sun, previous_eye));
                self.dirty[i] = false;
                updated.push(i);
            }
        }
        Ok(updated)
    }
}

fn validate_view(view: CascadeView) -> Result<(), &'static str> {
    let m = view.world_from_view;
    let axes = [
        m.x_axis.truncate(),
        m.y_axis.truncate(),
        m.z_axis.truncate(),
    ];
    if !m.is_finite()
        || m.w_axis.truncate().abs().max_element() > 1e9
        || [m.x_axis.w, m.y_axis.w, m.z_axis.w, m.w_axis.w - 1.0]
            .iter()
            .any(|v| v.abs() > 1e-9)
        || axes.iter().any(|v| (v.length_squared() - 1.0).abs() > 1e-6)
        || axes[0].dot(axes[1]).abs() > 1e-6
        || axes[0].dot(axes[2]).abs() > 1e-6
        || axes[1].dot(axes[2]).abs() > 1e-6
        || (m.determinant() - 1.0).abs() > 1e-6
        || !view.near.is_finite()
        || !(0.001..=0.1).contains(&view.near)
        || !view.fov_degrees.is_finite()
        || !(1.0..=170.0).contains(&view.fov_degrees)
        || !view.aspect.is_finite()
        || !(0.1..=10.0).contains(&view.aspect)
    {
        return Err("invalid rigid sun cascade view");
    }
    Ok(())
}

#[allow(clippy::manual_midpoint)] // Bounded operands; preserve the source operation order.
fn fit(
    config: &CascadeConfig,
    i: usize,
    view: CascadeView,
    sun: DVec3,
    previous_eye: DVec4,
) -> SunCascade {
    let last = config.splits.len() - 1;
    let margin = |distance: f64| {
        let e = distance / config.splits[last];
        (0.25 * e * e).max(0.25 * e) * config.splits[last]
    };
    let x = if i == 0 { 0.0 } else { config.splits[i - 1] };
    let y = config.splits[i];
    let mn = margin(x);
    let mf = margin(y);
    let near_slice = view.near.max(x - mn * 0.5);
    let far_slice = if i == last { y } else { y + mf * 0.5 };
    let tan_y = (view.fov_degrees * std::f64::consts::PI / 360.0).tan();
    let tan_x = tan_y * view.aspect;
    let zc = far_slice.min((near_slice + far_slice) / 2.0 * (1.0 + tan_x * tan_x + tan_y * tan_y));
    let center = view
        .world_from_view
        .transform_point3(DVec3::new(0.0, 0.0, -zc));
    let mut radius: f64 = 0.0;
    for d in [near_slice, far_slice] {
        for sx in [-1.0, 1.0] {
            for sy in [-1.0, 1.0] {
                let corner = view.world_from_view.transform_point3(DVec3::new(
                    sx * tan_x * d,
                    sy * tan_y * d,
                    -d,
                ));
                radius = radius.max(corner.distance(center));
            }
        }
    }
    radius = (radius * 16.0).ceil() / 16.0;
    let z = sun.normalize();
    let up = if sun.y.abs() > 0.99 {
        DVec3::X
    } else {
        DVec3::Y
    };
    let right = up.cross(z).normalize();
    let up = z.cross(right);
    // Source Matrix4.lookAt replaces the basis but retains the previous eye.
    // Therefore snapping is relative to that eye, including when the sun moves.
    let rotation = DMat4::from_cols(
        right.extend(0.0),
        up.extend(0.0),
        z.extend(0.0),
        previous_eye,
    );
    let texel = 2.0 * radius / f64::from(config.size);
    let mut ls = rotation.inverse().transform_point3(center);
    // JavaScript Math.round resolves negative half ties toward +infinity.
    ls.x = (ls.x / texel + 0.5).floor() * texel;
    ls.y = (ls.y / texel + 0.5).floor() * texel;
    let back = radius + config.light_margin;
    ls.z += back;
    let eye = rotation.transform_point3(ls);
    let mut world = rotation;
    world.w_axis = eye.extend(1.0);
    let inverse = world.inverse();
    let near = 0.1;
    let far = back + radius;
    let depth_scale = 1.0 / (far - near);
    let projection = DMat4::from_cols(
        DVec4::new(1.0 / radius, 0.0, 0.0, 0.0),
        DVec4::new(0.0, 1.0 / radius, 0.0, 0.0),
        DVec4::new(0.0, 0.0, -depth_scale, 0.0),
        DVec4::new(0.0, 0.0, -near * depth_scale, 1.0),
    );
    SunCascade {
        world_from_view: world,
        view_from_world: inverse,
        clip_from_view: projection,
        clip_from_world: projection * inverse,
        radius,
        near,
        far,
        info: [far_slice, texel, config.normal_bias[i], far - near],
        blend: [x, y, mn, mf],
    }
}
