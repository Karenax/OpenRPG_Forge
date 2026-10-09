//! Closed, renderer-neutral fragment equations for imported source materials.
//! This contract preserves signed/HDR colour and source roughness above one.
//! It does not assert lighting, importer packing or rendered Unity parity.
use serde::{Deserialize, Serialize};

pub const SOURCE_FRAGMENT_EXTENSION: &str = "OPENRPG_source_fragment";

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceFragmentContract {
    pub schema_version: u32,
    pub source_program_sha256: String,
    /// Rows: dot(row.xyz, sampled linear base RGB) + row.w.
    pub linear_rgb_rows: [[f64; 4]; 3],
    /// Source linear packed RGBA coefficients plus a signed offset.
    pub metallic: [f64; 5],
    pub occlusion: [f64; 5],
    pub perceptual_roughness: [f64; 5],
    /// Unity `NormalStrength`: (x*s, y*s, 1 + (z-1)*saturate(s)).
    pub normal_strength: f64,
    /// Basis adaptation, independently audited on source/target corner frames.
    pub target_tangent_space_y_sign: f64,
    /// Optional original PNG stored directly in the autonomous GLB binary chunk.
    /// The adapter decodes this into f32 without an 8-bit intermediate.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub normal_png: Option<SourcePngBufferView>,
    /// Version 2 preserves source RGB mask blends and world texture projections.
    /// Its texture importer and renderer require separate qualification.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub world_blend: Option<SourceWorldBlendProgram>,
    /// Version 4: surface sampling scale without requiring inactive world images.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub surface_uv_scale: Option<[f64; 2]>,
    /// Version 5: source nonlinear power after affine colour, without world textures.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub base_color_power: Option<f64>,
    /// Version 6: original base PNG with explicit Unity import color space.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub base_png: Option<SourceBasePng>,
    pub double_sided_normal_mode: SourceBackfaceMode,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum SourceBackfaceMode {
    SingleSided,
    Flip,
    Mirror,
    None,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourcePngBufferView {
    pub buffer_view: u32,
    pub sha256: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceBasePng {
    pub image: SourcePngBufferView,
    pub srgb: bool,
}

/// Closed source graph arithmetic, independent of the renderer and texture decoder.
/// Projection uses successive lerps, not normalized triplanar weights.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent source texture transfer flags are preserved by the versioned wire contract"
)]
pub struct SourceWorldBlendProgram {
    pub red_tint: [f64; 3],
    pub green_tint: [f64; 3],
    pub blue_tint: [f64; 3],
    pub base_saturation: f64,
    pub base_power: f64,
    pub mask_uv_scale: [f64; 2],
    /// Version 3: authored surface sampling scale, independent of RGB mask UVs.
    #[serde(default = "unit_uv_scale", skip_serializing_if = "is_unit_uv_scale")]
    pub surface_uv_scale: [f64; 2],
    /// Version 3 retains an explicitly disabled source RGB blend branch.
    #[serde(default = "enabled", skip_serializing_if = "is_enabled")]
    pub blend_mask_enabled: bool,
    pub noise_period: f64,
    pub noise_contrast: f64,
    /// Authored source world axes from target world axes; rotations/reflections.
    pub source_axes_from_target: [[f64; 3]; 3],
    pub blend_mask: SourcePngBufferView,
    pub noise_texture: SourcePngBufferView,
    pub noise_mask: SourcePngBufferView,
    pub blend_mask_srgb: bool,
    pub noise_texture_srgb: bool,
    pub noise_mask_srgb: bool,
}

fn unit_uv_scale() -> [f64; 2] {
    [1.0, 1.0]
}
#[allow(
    clippy::float_cmp,
    reason = "Only an exact authored identity scale may be omitted from serialization"
)]
fn is_unit_uv_scale(value: &[f64; 2]) -> bool {
    *value == unit_uv_scale()
}
fn enabled() -> bool {
    true
}
#[allow(
    clippy::trivially_copy_pass_by_ref,
    reason = "Serde skip_serializing_if requires a borrowed field"
)]
fn is_enabled(value: &bool) -> bool {
    *value
}

impl SourceWorldBlendProgram {
    pub fn validate(&self) -> Result<(), &'static str> {
        let coefficients = self
            .red_tint
            .iter()
            .chain(self.green_tint.iter())
            .chain(self.blue_tint.iter())
            .chain(self.mask_uv_scale.iter())
            .chain(self.surface_uv_scale.iter())
            .chain([
                &self.base_saturation,
                &self.base_power,
                &self.noise_period,
                &self.noise_contrast,
            ])
            .chain(self.source_axes_from_target.iter().flatten());
        if coefficients
            .into_iter()
            .any(|v| !v.is_finite() || v.abs() > 65536.0)
        {
            return Err("nonfinite or unbounded world blend coefficient");
        }
        if self.noise_period == 0.0 || self.base_power <= 0.0 {
            return Err("world blend needs nonzero period and positive base power");
        }
        for a in 0..3 {
            for b in 0..3 {
                let dot: f64 = self.source_axes_from_target[a]
                    .iter()
                    .zip(self.source_axes_from_target[b])
                    .map(|(x, y)| x * y)
                    .sum();
                if (dot - if a == b { 1.0 } else { 0.0 }).abs() > 1e-6 {
                    return Err("world source axes must preserve distances and normal angles");
                }
            }
        }
        for image in [&self.blend_mask, &self.noise_texture, &self.noise_mask] {
            if image.sha256.len() != 64 || !image.sha256.bytes().all(|b| b.is_ascii_hexdigit()) {
                return Err("world blend PNG SHA-256 required");
            }
        }
        Ok(())
    }

    #[must_use]
    pub fn source_vector(&self, target: [f64; 3]) -> [f64; 3] {
        self.source_axes_from_target
            .map(|row| row.iter().zip(target).map(|(a, b)| a * b).sum())
    }

    /// PNG/glTF image UV -> source Unity UV -> authored scale -> image UV.
    /// Mesh UVs and their authored tangent basis remain unchanged.
    #[must_use]
    pub fn surface_image_uv(&self, target_uv: [f64; 2]) -> [f64; 2] {
        if is_unit_uv_scale(&self.surface_uv_scale) {
            return target_uv;
        }
        [
            target_uv[0] * self.surface_uv_scale[0],
            1.0 - (1.0 - target_uv[1]) * self.surface_uv_scale[1],
        ]
    }

    /// Source subgraph samples XY, ZY and XZ after division by -abs(period).
    #[must_use]
    pub fn projection_uvs(&self, target_position: [f64; 3]) -> [[f64; 2]; 3] {
        let p = self
            .source_vector(target_position)
            .map(|v| v / -self.noise_period.abs());
        [[p[0], p[1]], [p[2], p[1]], [p[0], p[2]]]
    }

    /// Inputs are already in the declared source texture transfer space.
    #[must_use]
    pub fn projected_rgba(&self, samples: [[f64; 4]; 3], target_normal: [f64; 3]) -> [f64; 4] {
        let n = self.source_vector(target_normal);
        std::array::from_fn(|c| {
            let xy_zy = samples[0][c] + (samples[1][c] - samples[0][c]) * n[0].abs();
            xy_zy + (samples[2][c] - xy_zy) * n[1].abs()
        })
    }

    /// Receives the existing affine RGB result and independently sampled masks.
    #[must_use]
    pub fn evaluate_rgb(
        &self,
        base: [f64; 3],
        mask: [f64; 3],
        noise_red: f64,
        noise_mask: [f64; 3],
    ) -> [f64; 3] {
        let luminance = base
            .iter()
            .zip([0.212_672_9, 0.715_152_2, 0.072_175_0])
            .map(|(a, b)| a * b)
            .sum::<f64>();
        let midpoint = 0.5_f64.powf(2.2);
        std::array::from_fn(|c| {
            let saturated = luminance + (base[c] - luminance) * self.base_saturation;
            let mut color = saturated.powf(self.base_power);
            if self.blend_mask_enabled {
                for (tint, weight) in [
                    (self.red_tint[c], mask[0]),
                    (self.green_tint[c], mask[1]),
                    (self.blue_tint[c], mask[2]),
                ] {
                    color += (tint - color) * weight;
                }
            }
            let noise_mask = (noise_mask[c] - midpoint) * self.noise_contrast + midpoint;
            let noise = noise_red + (0.0 - noise_red) * noise_mask;
            color + (0.0 - color) * noise
        })
    }
}

impl SourceFragmentContract {
    /// The bound limits allocations/overflow; it never clips valid samples.
    pub fn validate(&self) -> Result<(), &'static str> {
        if !matches!(
            (self.schema_version, self.world_blend.is_some()),
            (1 | 5 | 6, false) | (2 | 3, true) | (4, _)
        ) {
            return Err("unsupported source fragment schema");
        }
        if let Some(scale) = self.surface_uv_scale {
            if !matches!(self.schema_version, 4..=6) {
                return Err("independent surface UV scale requires source schema 4 or 5");
            }
            if scale.iter().any(|v| !v.is_finite() || v.abs() > 65536.0) {
                return Err("nonfinite or unbounded surface UV scale");
            }
            if self
                .world_blend
                .as_ref()
                .is_some_and(|w| !is_unit_uv_scale(&w.surface_uv_scale))
            {
                return Err("surface UV scale must have a single authored owner");
            }
        } else if self.schema_version == 4 {
            return Err("source schema 4 needs an explicit surface UV scale");
        }
        if let Some(power) = self.base_color_power {
            if !matches!(self.schema_version, 5 | 6) {
                return Err("independent colour power requires source schema 5");
            }
            if !power.is_finite() || power <= 0.0 || power > 65536.0 {
                return Err("nonfinite or unbounded source colour power");
            }
        } else if matches!(self.schema_version, 5 | 6) {
            return Err("source schema 5 needs an explicit colour power");
        }
        if self.base_png.is_some() != (self.schema_version == 6) {
            return Err("original base PNG declaration requires source schema 6");
        }
        if let Some(base) = &self.base_png
            && (base.image.sha256.len() != 64
                || !base.image.sha256.bytes().all(|b| b.is_ascii_hexdigit()))
        {
            return Err("original base PNG SHA-256 required");
        }
        if let Some(program) = &self.world_blend {
            program.validate()?;
            if self.schema_version == 2
                && (!is_unit_uv_scale(&program.surface_uv_scale) || !program.blend_mask_enabled)
            {
                return Err("surface UV scale or disabled RGB blend requires source schema 3");
            }
        }
        if self.source_program_sha256.len() != 64
            || !self
                .source_program_sha256
                .bytes()
                .all(|b| b.is_ascii_hexdigit())
        {
            return Err("source fragment program SHA-256 required");
        }
        let coefficients = self
            .linear_rgb_rows
            .iter()
            .flatten()
            .chain(self.metallic.iter())
            .chain(self.occlusion.iter())
            .chain(self.perceptual_roughness.iter())
            .chain(std::iter::once(&self.normal_strength));
        if coefficients
            .into_iter()
            .any(|v| !v.is_finite() || v.abs() > 65536.0)
        {
            return Err("nonfinite or unbounded source fragment coefficient");
        }
        if !matches!(self.target_tangent_space_y_sign, -1.0 | 1.0) {
            return Err("source normal basis sign must be exactly minus or plus one");
        }
        if let Some(image) = &self.normal_png
            && (image.sha256.len() != 64 || !image.sha256.bytes().all(|b| b.is_ascii_hexdigit()))
        {
            return Err("source normal PNG SHA-256 required");
        }
        Ok(())
    }

    /// Effective authored sampling scale; legacy contracts keep identity UVs.
    #[must_use]
    pub fn effective_surface_uv_scale(&self) -> [f64; 2] {
        self.surface_uv_scale.unwrap_or_else(|| {
            self.world_blend
                .as_ref()
                .map_or(unit_uv_scale(), |w| w.surface_uv_scale)
        })
    }

    /// Sampling only: retains mesh UVs, geometry and authored tangent frames.
    #[must_use]
    pub fn surface_image_uv(&self, target_uv: [f64; 2]) -> [f64; 2] {
        let scale = self.effective_surface_uv_scale();
        if is_unit_uv_scale(&scale) {
            return target_uv;
        }
        [
            target_uv[0] * scale[0],
            1.0 - (1.0 - target_uv[1]) * scale[1],
        ]
    }

    #[must_use]
    pub fn evaluate(
        &self,
        linear_base: [f64; 3],
        packed: [f64; 4],
        normal_rgb: [f64; 3],
    ) -> SourceFragmentValues {
        let channel = |coefficients: &[f64; 5]| {
            coefficients[4]
                + coefficients[..4]
                    .iter()
                    .zip(packed)
                    .map(|(a, b)| a * b)
                    .sum::<f64>()
        };
        let s = self.normal_strength;
        let n = normal_rgb.map(|v| v * 2.0 - 1.0);
        SourceFragmentValues {
            linear_rgb: self
                .linear_rgb_rows
                .map(|row| {
                    row[3]
                        + row[..3]
                            .iter()
                            .zip(linear_base)
                            .map(|(a, b)| a * b)
                            .sum::<f64>()
                })
                .map(|v| self.base_color_power.map_or(v, |power| v.powf(power))),
            metallic: channel(&self.metallic),
            occlusion: channel(&self.occlusion),
            perceptual_roughness: channel(&self.perceptual_roughness),
            normal_ts: [n[0] * s, n[1] * s, 1.0 + (n[2] - 1.0) * s.clamp(0.0, 1.0)],
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct SourceFragmentValues {
    pub linear_rgb: [f64; 3],
    pub metallic: f64,
    pub occlusion: f64,
    pub perceptual_roughness: f64,
    pub normal_ts: [f64; 3],
}

#[cfg(test)]
mod tests {
    use super::*;
    fn source() -> SourceFragmentContract {
        SourceFragmentContract {
            schema_version: 1,
            source_program_sha256: "a".repeat(64),
            linear_rgb_rows: [
                [1.1, 0.0, 0.0, -0.021_763_764_082_403_12],
                [0.0, 1.1, 0.0, -0.021_763_764_082_403_12],
                [0.0, 0.0, 1.1, -0.021_763_764_082_403_12],
            ],
            metallic: [0.0, 0.0, 1.0, 0.0, 0.0],
            occlusion: [1.0, 0.0, 0.0, 0.0, 0.0],
            perceptual_roughness: [0.0, 2.0, 0.0, 0.0, 0.0],
            normal_strength: 0.5,
            target_tangent_space_y_sign: -1.0,
            normal_png: None,
            world_blend: None,
            surface_uv_scale: None,
            base_color_power: None,
            base_png: None,
            double_sided_normal_mode: SourceBackfaceMode::SingleSided,
        }
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Retains exact source-contract results and signed tangent-space components after JSON round trip."
    )]
    fn source_signed_and_above_one_values_survive_round_trip() {
        let original = source();
        original.validate().unwrap();
        let restored: SourceFragmentContract =
            serde_json::from_str(&serde_json::to_string(&original).unwrap()).unwrap();
        assert_eq!(original, restored);
        let v = restored.evaluate([0.0, 1.0, 0.5], [0.7, 0.8, 0.3, 1.0], [0.75, 0.25, 0.75]);
        assert!(v.linear_rgb[0] < 0.0);
        assert!(v.linear_rgb[1] > 1.0);
        assert_eq!(v.perceptual_roughness, 1.6);
        assert_eq!(v.normal_ts, [0.25, -0.25, 0.75]);
    }
    #[test]
    fn original_base_png_schema_is_explicit_and_legacy_preserved() {
        let mut c = source();
        assert!(serde_json::to_value(&c).unwrap().get("base_png").is_none());
        c.schema_version = 6;
        c.base_color_power = Some(1.0);
        c.surface_uv_scale = Some([6.0, 6.0]);
        assert!(c.validate().is_err());
        c.base_png = Some(SourceBasePng {
            image: SourcePngBufferView {
                buffer_view: 3,
                sha256: "a".repeat(64),
            },
            srgb: false,
        });
        c.validate().unwrap();
        let value = serde_json::to_value(&c).unwrap();
        let decoded: SourceFragmentContract = serde_json::from_value(value.clone()).unwrap();
        assert_eq!(decoded, c);
        assert!(!decoded.base_png.unwrap().srgb);
        for version in 1..=5 {
            c.schema_version = version;
            assert!(c.validate().is_err());
        }
        c.schema_version = 6;
        c.base_png.as_mut().unwrap().image.sha256 = "z".repeat(64);
        assert!(c.validate().is_err());
        let mut missing = value.clone();
        missing["base_png"].as_object_mut().unwrap().remove("srgb");
        assert!(serde_json::from_value::<SourceFragmentContract>(missing).is_err());
        let mut unknown = value;
        unknown["base_png"]["unexpected"] = serde_json::json!(true);
        assert!(serde_json::from_value::<SourceFragmentContract>(unknown).is_err());
    }

    #[test]
    fn malformed_source_contracts_are_rejected_without_coercion() {
        let mut c = source();
        c.schema_version = 2;
        assert!(c.validate().is_err());
        c = source();
        c.normal_strength = f64::NAN;
        assert!(c.validate().is_err());
        c = source();
        c.linear_rgb_rows[0][0] = f64::INFINITY;
        assert!(c.validate().is_err());
        c = source();
        c.source_program_sha256 = "not-a-pin".into();
        assert!(c.validate().is_err());
        let mut value = serde_json::to_value(source()).unwrap();
        value["unexpected"] = true.into();
        assert!(serde_json::from_value::<SourceFragmentContract>(value).is_err());
    }

    fn world_program() -> SourceWorldBlendProgram {
        let image = || SourcePngBufferView {
            buffer_view: 0,
            sha256: "b".repeat(64),
        };
        SourceWorldBlendProgram {
            red_tint: [0.169_811_31, 0.121_135_354, 0.106_532_566],
            green_tint: [0.405_660_4, 0.373_513_73, 0.365_477_06],
            blue_tint: [0.396_226_4, 0.348_255_6, 0.330_811_68],
            base_saturation: 1.0,
            base_power: 1.0,
            mask_uv_scale: [1.0, 1.0],
            surface_uv_scale: unit_uv_scale(),
            blend_mask_enabled: true,
            noise_period: 1.1,
            noise_contrast: 1.0,
            source_axes_from_target: [[-1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]],
            blend_mask: image(),
            noise_texture: image(),
            noise_mask: image(),
            blend_mask_srgb: false,
            noise_texture_srgb: true,
            noise_mask_srgb: true,
        }
    }

    #[test]
    fn world_projection_preserves_source_sign_and_successive_lerps() {
        let p = world_program();
        p.validate().unwrap();
        let uv = p.projection_uvs([2.2, -3.3, 4.4]);
        for (actual, expected) in uv
            .into_iter()
            .flatten()
            .zip([2.0, 3.0, -4.0, 3.0, 2.0, -4.0])
        {
            assert!((actual - expected).abs() < 1e-12);
        }
        let projected = p.projected_rgba([[0.0; 4], [10.0; 4], [20.0; 4]], [0.6, 0.8, 0.0]);
        for actual in projected {
            assert!((actual - 17.2).abs() < 1e-12);
        }
    }

    #[test]
    fn world_blend_retains_hdr_and_source_rgb_noise_mask_channels() {
        let p = world_program();
        let base = [2.0, 3.0, 4.0];
        let result = p.evaluate_rgb(base, [0.0; 3], 0.5, [0.0, 0.5, 1.0]);
        for (actual, expected) in result.into_iter().zip([1.0, 2.25, 4.0]) {
            assert!((actual - expected).abs() < 1e-12);
        }
    }

    #[test]
    fn surface_uv_scale_and_disabled_source_blend_require_v3() {
        let mut c = source();
        c.schema_version = 2;
        c.world_blend = Some(world_program());
        let legacy = serde_json::to_value(&c).unwrap();
        assert!(legacy["world_blend"].get("surface_uv_scale").is_none());
        assert!(legacy["world_blend"].get("blend_mask_enabled").is_none());
        c.world_blend.as_mut().unwrap().surface_uv_scale = [3.0, 3.0];
        assert!(c.validate().is_err());
        c.schema_version = 3;
        c.world_blend.as_mut().unwrap().blend_mask_enabled = false;
        c.validate().unwrap();
        let p = c.world_blend.as_ref().unwrap();
        let uv = p.surface_image_uv([0.25, 0.75]);
        assert!((uv[0] - 0.75).abs() < 1e-12 && (uv[1] - 0.25).abs() < 1e-12);
        let unmasked = p.evaluate_rgb([2.0, 3.0, 4.0], [0.9, 0.7, 0.8], 0.5, [0.0, 0.5, 1.0]);
        for (actual, expected) in unmasked.into_iter().zip([1.0, 2.25, 4.0]) {
            assert!((actual - expected).abs() < 1e-12);
        }
        let restored: SourceFragmentContract =
            serde_json::from_value(serde_json::to_value(&c).unwrap()).unwrap();
        assert_eq!(c, restored);
        c.schema_version = 2;
        c.world_blend.as_mut().unwrap().surface_uv_scale = unit_uv_scale();
        assert!(c.validate().is_err());
        c.schema_version = 3;
        c.world_blend.as_mut().unwrap().surface_uv_scale[0] = f64::NAN;
        assert!(c.validate().is_err());
    }

    #[test]
    fn independent_colour_power_requires_v5_without_world_textures() {
        let mut c = source();
        let legacy = serde_json::to_value(&c).unwrap();
        assert!(legacy.get("base_color_power").is_none());
        c.linear_rgb_rows = [
            [2.0, 0.0, 0.0, 0.0],
            [0.0, 2.0, 0.0, 0.0],
            [0.0, 0.0, 2.0, 0.0],
        ];
        c.base_color_power = Some(0.75);
        for version in 1..=4 {
            c.schema_version = version;
            assert!(c.validate().is_err());
        }
        c.schema_version = 5;
        c.surface_uv_scale = Some([2.0, 3.0]);
        c.validate().unwrap();
        assert!(c.world_blend.is_none());
        let value = c.evaluate([0.2, 0.4, 0.6], [0.2, 0.8, 0.3, 1.0], [0.5, 0.5, 1.0]);
        for (actual, raw) in value.linear_rgb.into_iter().zip([0.4_f64, 0.8, 1.2]) {
            assert!((actual - raw.powf(0.75)).abs() < 1e-12);
        }
        assert!(value.linear_rgb[2] > 1.0 && value.perceptual_roughness > 1.0);
        let restored: SourceFragmentContract =
            serde_json::from_value(serde_json::to_value(&c).unwrap()).unwrap();
        assert_eq!(restored, c);
        c.world_blend = Some(world_program());
        assert!(c.validate().is_err());
        c.world_blend = None;
        for power in [0.0, -1.0, f64::NAN, f64::INFINITY, 65537.0] {
            c.base_color_power = Some(power);
            assert!(c.validate().is_err());
        }
        c.base_color_power = None;
        assert!(c.validate().is_err());
    }

    #[test]
    fn independent_surface_uv_scale_requires_v4_without_world_textures() {
        let mut c = source();
        let legacy = serde_json::to_value(&c).unwrap();
        assert!(legacy.get("surface_uv_scale").is_none());
        let identity = c.surface_image_uv([-0.0, -0.0]);
        assert_eq!(identity[0].to_bits(), (-0.0_f64).to_bits());
        assert_eq!(identity[1].to_bits(), (-0.0_f64).to_bits());
        c.surface_uv_scale = Some([2.0, 3.0]);
        for version in 1..=3 {
            c.schema_version = version;
            assert!(c.validate().is_err());
        }
        c.schema_version = 4;
        c.validate().unwrap();
        assert!(c.world_blend.is_none());
        let uv = c.surface_image_uv([0.25, 0.75]);
        assert!((uv[0] - 0.5).abs() < 1e-12 && (uv[1] - 0.25).abs() < 1e-12);
        let restored: SourceFragmentContract =
            serde_json::from_value(serde_json::to_value(&c).unwrap()).unwrap();
        assert_eq!(restored, c);
        c.world_blend = Some(world_program());
        c.validate().unwrap();
        c.world_blend.as_mut().unwrap().surface_uv_scale = [2.0, 2.0];
        assert!(c.validate().is_err());
        c.world_blend = None;
        for scale in [[f64::NAN, 1.0], [1.0, f64::INFINITY], [65537.0, 1.0]] {
            c.surface_uv_scale = Some(scale);
            assert!(c.validate().is_err());
        }
        c.surface_uv_scale = None;
        assert!(c.validate().is_err());
    }

    #[test]
    fn world_program_schema_pins_and_axes_are_checked_without_affecting_v1() {
        let mut c = source();
        let legacy_json = serde_json::to_value(&c).unwrap();
        assert!(legacy_json.get("world_blend").is_none());
        c.world_blend = Some(world_program());
        assert!(c.validate().is_err());
        c.schema_version = 2;
        c.validate().unwrap();
        let restored: SourceFragmentContract =
            serde_json::from_value(serde_json::to_value(&c).unwrap()).unwrap();
        assert_eq!(c, restored);
        c.world_blend.as_mut().unwrap().noise_period = 0.0;
        assert!(c.validate().is_err());
        c.world_blend = Some(world_program());
        c.world_blend.as_mut().unwrap().noise_mask.sha256 = "missing".into();
        assert!(c.validate().is_err());
        c.world_blend = Some(world_program());
        c.world_blend.as_mut().unwrap().source_axes_from_target[0][0] = -2.0;
        assert!(c.validate().is_err());
        let mut value = serde_json::to_value(restored).unwrap();
        value["world_blend"]["unexpected"] = true.into();
        assert!(serde_json::from_value::<SourceFragmentContract>(value).is_err());
    }
}
