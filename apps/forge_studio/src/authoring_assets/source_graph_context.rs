//! Dynamic uniforms from the declared object basis and current host frame.
//! This preserves runtime coordinates/clocks; it does not qualify GPU parity.
use bevy::prelude::*;
use forge_engine::source_graph::SourceGraphUniforms;
use serde_json::Value;

#[derive(Clone, Copy)]
pub(super) struct ObjectBasis {
    pub source_from_target: Mat4,
    pub target_from_source: Mat4,
}

fn affine(matrix: Mat4) -> Result<(), String> {
    if !matrix.is_finite()
        || !matrix.determinant().is_finite()
        || matrix.determinant() == 0.0
        || matrix.x_axis.w != 0.0
        || matrix.y_axis.w != 0.0
        || matrix.z_axis.w != 0.0
        || matrix.w_axis.w.to_bits() != 1.0_f32.to_bits()
    {
        return Err("Current source frame must be finite, affine and invertible".into());
    }
    Ok(())
}

fn row_major(value: &Value) -> Result<Mat4, String> {
    let values = value.as_array().ok_or("Declared source basis absent")?;
    if values.len() != 16 {
        return Err("Declared source basis must contain 16 values".into());
    }
    let mut rows = [0.0; 16];
    for (i, value) in values.iter().enumerate() {
        rows[i] = number(value, "Source basis")?;
    }
    let matrix = Mat4::from_cols_array(&rows).transpose();
    affine(matrix)?;
    Ok(matrix)
}

impl ObjectBasis {
    /// Existing explicitly framed generic scenes retain their old frame API.
    /// A partially declared mapping is an error, never an identity default.
    pub fn from_document(doc: &Value) -> Result<Option<Self>, String> {
        let Some(binding) = doc["extras"].get("source_frame_binding") else {
            return Ok(None);
        };
        let basis = Self {
            source_from_target: row_major(&binding["source_object_from_target_object"])?,
            target_from_source: row_major(&binding["target_object_from_source_object"])?,
        };
        let product = basis.source_from_target * basis.target_from_source;
        if product
            .to_cols_array()
            .iter()
            .zip(Mat4::IDENTITY.to_cols_array())
            .any(|(a, b)| (a - b).abs() > 1e-4)
        {
            return Err("Declared source object mappings are not inverse".into());
        }
        Ok(Some(basis))
    }
}

/// Supplied by the shared viewport's coordinate/clock/sampling policy, never
/// inferred from an asset name, a capture timestamp or the presentation camera.
pub(super) struct HostFrame {
    pub source_world_from_target_world: Mat4,
    pub source_absolute_world_from_target_world: Mat4,
    pub time_parameters: Vec4,
    pub previous_time_parameters: Vec4,
    pub global_mip_bias: f32,
    pub source_float_min: f32,
}

impl HostFrame {
    pub fn uniform(
        &self,
        basis: ObjectBasis,
        target_world_from_target_object: Mat4,
        admitted_normal_modes: [Vec4; 2],
    ) -> Result<SourceGraphUniforms, String> {
        for matrix in [
            basis.source_from_target,
            basis.target_from_source,
            self.source_world_from_target_world,
            self.source_absolute_world_from_target_world,
            target_world_from_target_object,
        ] {
            affine(matrix)?;
        }
        // ShaderGraph ObjectNode uses lengths of the UNITY_MATRIX_M columns.
        // Compose the source matrix, including current author scale/rotation.
        let source_absolute_world_from_source_object = self.source_absolute_world_from_target_world
            * target_world_from_target_object
            * basis.target_from_source;
        affine(source_absolute_world_from_source_object)?;
        let x = source_absolute_world_from_source_object
            .x_axis
            .truncate()
            .length();
        let z = source_absolute_world_from_source_object
            .z_axis
            .truncate()
            .length();
        if x <= 0.0
            || z <= 0.0
            || !x.is_finite()
            || !z.is_finite()
            || !self.time_parameters.is_finite()
            || !self.previous_time_parameters.is_finite()
            || !self.global_mip_bias.is_finite()
            || !self.source_float_min.is_finite()
            || self.source_float_min <= 0.0
            || admitted_normal_modes.iter().any(|m| !m.is_finite())
        {
            return Err("Source clock, scale, decoder or sampling inputs are invalid".into());
        }
        let uniforms = SourceGraphUniforms {
            source_object_from_target_object: basis.source_from_target,
            target_object_from_source_object: basis.target_from_source,
            source_world_from_target_world: self.source_world_from_target_world,
            source_absolute_world_from_target_world: self.source_absolute_world_from_target_world,
            source_time_parameters: self.time_parameters,
            source_previous_time_parameters: self.previous_time_parameters,
            source_scale_mip_min: Vec4::new(
                x.recip(),
                z.recip(),
                self.global_mip_bias,
                self.source_float_min,
            ),
            normal_decode_modes: admitted_normal_modes,
        };
        super::source_graph_resources::validate_uniforms(&uniforms)?;
        Ok(uniforms)
    }
}

/// Convert an explicit presentation/floating-origin translation to the source
/// world. E.g. preview's +1024 Y must not become authored procedural coordinates.
pub(super) fn world_mapping(
    source_from_author_world: Mat4,
    target_presentation_offset: Vec3,
) -> Result<Mat4, String> {
    affine(source_from_author_world)?;
    if !target_presentation_offset.is_finite() {
        return Err("Nonfinite presentation offset".into());
    }
    let mapping = source_from_author_world * Mat4::from_translation(-target_presentation_offset);
    affine(mapping)?;
    Ok(mapping)
}

/// HDRP `HDCamera`'s explicit current/previous time parameter convention.
/// The viewport owns the times; this helper does not select a simulation clock.
pub(super) fn time_parameters(seconds: f32) -> Result<Vec4, String> {
    if !seconds.is_finite() {
        return Err("Nonfinite source time".into());
    }
    Ok(Vec4::new(seconds, seconds.sin(), seconds.cos(), 0.0))
}

#[path = "source_graph_scalar.rs"]
mod scalar;
pub(super) use scalar::number;
