//! Photon-splat caustic map geometry for the pinned Tidewater ocean.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/ocean/Caustics.js` at 1438b1ab,
// copyright 2026 DRG Software Solutions LLC. See `third_party/tidewater/LICENSE`.

use bevy::{prelude::*, render::render_resource::ShaderType};

mod gpu;
pub use gpu::{CausticMaps, prepare_maps, render_maps};

pub const PHOTON_SHADER: &str = include_str!("source_caustics.wgsl");

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct CausticLayerSpec {
    resolution: u32,
    grid: u32,
    margin: f32,
    slope_lod: f32,
    focal_depths_m: [f32; 2],
    /// Zero is the finest FFT cascade, one is the next broader cascade.
    cascade_from_finest: u32,
}

pub const FINE: CausticLayerSpec = CausticLayerSpec {
    resolution: 512,
    // Tidewater expands its 256-cell base grid over [-0.35, 1.35]: ceil(256 * 1.7).
    grid: 436,
    margin: 0.35,
    slope_lod: 1.0,
    focal_depths_m: [1.2, 4.0],
    cascade_from_finest: 0,
};

pub const BROAD: CausticLayerSpec = CausticLayerSpec {
    resolution: 256,
    // Likewise, ceil(128 * 1.7) for the next broader FFT cascade.
    grid: 218,
    margin: 0.35,
    slope_lod: 0.5,
    focal_depths_m: [3.0, 9.0],
    cascade_from_finest: 1,
};

#[derive(Clone, Debug, ShaderType)]
pub struct PhotonParams {
    /// Unit direction from water toward the sun.
    pub sun_direction: Vec4,
    /// FFT tile metres, grid size, cascade index, derivative LOD.
    pub geometry: Vec4,
    /// Focal depth metres, output channel (0 R / 1 G), grid margin.
    pub optics: Vec4,
}

impl CausticLayerSpec {
    #[must_use]
    pub const fn resolution(self) -> u32 {
        self.resolution
    }

    #[must_use]
    pub const fn grid(self) -> u32 {
        self.grid
    }

    // Only FINE/BROAD can be constructed outside this module. Their grid,
    // vertex coordinates, cascade and focal index are all exactly representable
    // by f32; the casts transfer these integer shader parameters losslessly.
    #[expect(
        clippy::cast_precision_loss,
        reason = "pinned caustic bounds fit exactly in f32"
    )]
    pub fn parameters(
        self,
        cascade_count: u32,
        tile_m: f32,
        sun_direction: Vec3,
        focal_plane: usize,
    ) -> Result<PhotonParams, String> {
        if !(2..=8).contains(&cascade_count)
            || self.cascade_from_finest >= cascade_count
            || self.grid == 0
            || focal_plane > 1
            || !tile_m.is_finite()
            || tile_m <= 0.0
            || !sun_direction.is_finite()
            || (sun_direction.length_squared() - 1.0).abs() > 0.001
            || sun_direction.y <= 0.0
        {
            return Err("Invalid Source caustic photon frame".into());
        }
        let cascade = cascade_count - 1 - self.cascade_from_finest;
        Ok(PhotonParams {
            sun_direction: sun_direction.extend(0.0),
            geometry: Vec4::new(tile_m, self.grid as f32, cascade as f32, self.slope_lod),
            optics: Vec4::new(
                self.focal_depths_m[focal_plane],
                focal_plane as f32,
                self.margin,
                0.0,
            ),
        })
    }

    /// Six indexed vertices per grid cell; the GPU transforms each shared
    /// photon once via its post-transform vertex cache.
    #[must_use]
    pub fn grid_indices(self) -> Vec<u32> {
        let row = self.grid + 1;
        let mut indices = Vec::with_capacity((self.grid * self.grid * 6) as usize);
        for y in 0..self.grid {
            for x in 0..self.grid {
                let a = y * row + x;
                indices.extend([a, a + 1, a + row, a + row, a + 1, a + row + 1]);
            }
        }
        indices
    }

    #[must_use]
    #[expect(
        clippy::cast_precision_loss,
        reason = "pinned caustic bounds fit exactly in f32"
    )]
    pub fn source_uv(self, vertex: u32) -> Option<Vec2> {
        let row = self.grid + 1;
        if vertex >= row * row {
            return None;
        }
        let cell = Vec2::new((vertex % row) as f32, (vertex / row) as f32);
        Some(cell / self.grid as f32 * (1.0 + 2.0 * self.margin) - Vec2::splat(self.margin))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_surface::underwater::CausticSampleParams;

    #[test]
    fn pinned_grids_cover_neighboring_fft_tiles_and_share_vertices() {
        for spec in [FINE, BROAD] {
            let indices = spec.grid_indices();
            assert_eq!(indices.len(), (spec.grid * spec.grid * 6) as usize);
            assert_eq!(
                &indices[..6],
                &[0, 1, spec.grid + 1, spec.grid + 1, 1, spec.grid + 2]
            );
            assert!((spec.source_uv(0).unwrap() - Vec2::splat(-0.35)).length() < 1e-5);
            assert!(
                (spec.source_uv((spec.grid + 1).pow(2) - 1).unwrap() - Vec2::splat(1.35)).length()
                    < 1e-5
            );
            assert!(spec.source_uv((spec.grid + 1).pow(2)).is_none());
        }
    }

    #[test]
    fn two_cascades_and_focal_planes_match_the_pinned_reference() {
        let sun = Vec3::new(0.0, 1.0, 0.0);
        let fine = FINE.parameters(4, 32.0, sun, 0).unwrap();
        let broad = BROAD.parameters(4, 128.0, sun, 1).unwrap();
        assert_eq!(fine.geometry, Vec4::new(32.0, 436.0, 3.0, 1.0));
        assert_eq!(fine.optics, Vec4::new(1.2, 0.0, 0.35, 0.0));
        assert_eq!(broad.geometry, Vec4::new(128.0, 218.0, 2.0, 0.5));
        assert_eq!(broad.optics, Vec4::new(9.0, 1.0, 0.35, 0.0));
        assert!(FINE.parameters(1, 32.0, sun, 0).is_err());
        assert!(BROAD.parameters(4, -1.0, sun, 0).is_err());
    }

    #[test]
    fn photon_shader_parses_and_validates_for_gpu() {
        let shader = naga::front::wgsl::parse_str(PHOTON_SHADER).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "The shader control is an exact 0/1 flag, not an approximate optical measurement."
    )]
    fn sample_params_reject_invalid_physical_inputs() {
        let sun = Vec3::Y;
        assert!(CausticSampleParams::new(32.0, 128.0, sun, 0.75).is_ok());
        assert!(CausticSampleParams::new(32.0, 32.0, sun, 0.75).is_err());
        assert!(CausticSampleParams::new(32.0, 128.0, -sun, 0.75).is_err());
        assert!(CausticSampleParams::new(32.0, 128.0, sun, f32::NAN).is_err());
        let enabled = CausticSampleParams::new(32.0, 128.0, sun, 0.75)
            .unwrap()
            .with_underwater(
                [512.0, 256.0, 128.0, 32.0],
                0.0,
                3.0,
                Vec3::ZERO,
                Vec3::ZERO,
            )
            .unwrap();
        assert_eq!(enabled.water_scattering.w, 1.0);
        assert_eq!(
            CausticSampleParams::new(32.0, 128.0, sun, 0.0)
                .unwrap()
                .water_scattering
                .w,
            0.0
        );
        assert!(
            enabled
                .with_underwater(
                    [32.0, 128.0, 256.0, 512.0],
                    0.0,
                    3.0,
                    Vec3::ZERO,
                    Vec3::ZERO,
                )
                .is_err()
        );
    }
}
