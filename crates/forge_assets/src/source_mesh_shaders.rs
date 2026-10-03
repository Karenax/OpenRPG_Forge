//! Renderer-neutral, hash-pinned mesh programs acquired from a source renderer.
//! A loaded package is transport/ABI evidence, not a rendered appearance verdict.
//! No source renderer, filesystem path, GPU handle or neutral resource is retained.

use crate::source_geometry::AssetFileReference;
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

const MAX_FILE: usize = 8 * 1024 * 1024;
const MAX_TOTAL: usize = 64 * 1024 * 1024;

#[derive(Debug, Error)]
pub enum ShaderPackageError {
    #[error("Source mesh shader package: {0}")]
    Invalid(String),
    #[error("Source mesh shader JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("Source mesh shader UTF-8: {0}")]
    Utf8(#[from] std::str::Utf8Error),
}

fn invalid(message: impl Into<String>) -> ShaderPackageError {
    ShaderPackageError::Invalid(message.into())
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceShaderProvenance {
    pub capture: AssetFileReference,
    pub receipt: AssetFileReference,
    pub inputs: AssetFileReference,
    pub license: AssetFileReference,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub credits: Option<AssetFileReference>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceShaderHook {
    pub name: String,
    pub module: String,
    pub code_sha256: ContentHash,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceShaderModule {
    pub name: String,
    pub sha256: ContentHash,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceSide {
    Front,
    Back,
    Double,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceBlend {
    None,
    Normal,
    Premultiplied,
    Additive,
    Add,
    Multiply,
    Min,
    Max,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
// Independent source render switches form a wire record, not a state machine.
#[allow(clippy::struct_excessive_bools)]
pub struct SourceMeshMaterial {
    pub name: String,
    pub side: SourceSide,
    pub transparent: bool,
    pub blending: SourceBlend,
    pub depth_write: bool,
    pub depth_test: bool,
    pub velocity_weight: f64,
    pub vertex_colors: bool,
    pub lit: bool,
    pub defines: BTreeMap<String, i32>,
    pub uniforms: SourceUniformBlock,
    pub vertex_layout: Vec<SourceVertexField>,
    pub vertex_buffers: Vec<SourceVertexBuffer>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub raster: Option<SourceMaterialRaster>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceMaterialRaster {
    pub topology: SourceTopology,
    pub color_write: bool,
    pub depth_compare: Option<SourceDepthCompare>,
    pub depth_bias: i32,
    pub depth_bias_slope_scale: f64,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceVertexField {
    pub name: String,
    pub wgsl: String,
    pub location: u32,
    pub instanced: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceVertexStep {
    Vertex,
    Instance,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceVertexFormat {
    Float32,
    Float32x2,
    Float32x3,
    Float32x4,
}

impl SourceVertexFormat {
    const fn width(self) -> usize {
        match self {
            Self::Float32 => 4,
            Self::Float32x2 => 8,
            Self::Float32x3 => 12,
            Self::Float32x4 => 16,
        }
    }
    const fn wgsl(self) -> &'static str {
        match self {
            Self::Float32 => "f32",
            Self::Float32x2 => "vec2f",
            Self::Float32x3 => "vec3f",
            Self::Float32x4 => "vec4f",
        }
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceVertexBuffer {
    pub array_stride: usize,
    pub step_mode: SourceVertexStep,
    pub attributes: Vec<SourceVertexAttribute>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceVertexAttribute {
    pub shader_location: u32,
    pub offset: usize,
    pub format: SourceVertexFormat,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourcePassKind {
    Main,
    Color,
    Depth,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceTargetFormat {
    Rgba16Float,
    Rgba8Unorm,
    Depth32Float,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceDepthCompare {
    GreaterEqual,
    LessEqual,
    Always,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceMeshPass {
    pub kind: SourcePassKind,
    pub late: bool,
    pub color_formats: Vec<SourceTargetFormat>,
    pub depth_format: SourceTargetFormat,
    pub depth_compare: SourceDepthCompare,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub raster_overrides: Option<SourcePassRasterOverrides>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourcePassRasterOverrides {
    pub cull_mode: Option<SourceCull>,
    pub depth_bias: i32,
    pub depth_bias_slope_scale: f64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceBufferKind {
    Uniform,
    ReadOnlyStorage,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceSamplerKind {
    Filtering,
    NonFiltering,
    Comparison,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceTextureSample {
    Float,
    UnfilterableFloat,
    Depth,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub enum SourceTextureDimension {
    #[serde(rename = "2d")]
    D2,
    #[serde(rename = "2d-array")]
    D2Array,
    #[serde(rename = "cube")]
    Cube,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceBufferLayout {
    #[serde(rename = "type")]
    pub kind: SourceBufferKind,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceSamplerLayout {
    #[serde(rename = "type")]
    pub kind: SourceSamplerKind,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceTextureLayout {
    pub sample_type: SourceTextureSample,
    pub view_dimension: SourceTextureDimension,
    pub multisampled: bool,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceBindingLayout {
    /// Source WebGPU vertex=1, fragment=2. Other stage bits are rejected.
    pub visibility: u32,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub buffer: Option<SourceBufferLayout>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub sampler: Option<SourceSamplerLayout>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub texture: Option<SourceTextureLayout>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceBinding {
    pub name: String,
    pub binding: u32,
    pub declaration: String,
    pub layout: SourceBindingLayout,
    pub uniform: Option<SourceUniformBlock>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceDrawBinding {
    pub group: u32,
    pub binding: u32,
    pub bytes: usize,
    pub dynamic_stride: usize,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceMeshProgram {
    pub material: String,
    pub pass: SourceMeshPass,
    pub defines: BTreeMap<String, i32>,
    pub has_fragment: bool,
    pub code: AssetFileReference,
    pub modules: Vec<SourceShaderModule>,
    pub group0: Vec<SourceBinding>,
    pub group1: Vec<SourceBinding>,
    pub draw_binding: SourceDrawBinding,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub pipeline_state: Option<SourcePipelineState>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceTopology {
    PointList,
    LineList,
    TriangleList,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceCull {
    None,
    Front,
    Back,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceFrontFace {
    Ccw,
    Cw,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourcePrimitiveState {
    pub topology: SourceTopology,
    pub cull_mode: SourceCull,
    pub front_face: SourceFrontFace,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SourceBlendFactor {
    Zero,
    One,
    SrcAlpha,
    OneMinusSrcAlpha,
    Dst,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceBlendOperation {
    Add,
    Min,
    Max,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceBlendComponent {
    pub src_factor: SourceBlendFactor,
    pub dst_factor: SourceBlendFactor,
    pub operation: SourceBlendOperation,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceBlendState {
    pub color: SourceBlendComponent,
    pub alpha: SourceBlendComponent,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceColorTarget {
    pub format: SourceTargetFormat,
    pub blend: Option<SourceBlendState>,
    pub write_mask: u32,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceDepthState {
    pub format: SourceTargetFormat,
    pub depth_write_enabled: bool,
    pub depth_compare: SourceDepthCompare,
    pub depth_bias: i32,
    pub depth_bias_slope_scale: f64,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourceMultisampleState {
    pub count: u32,
    pub mask: u64,
    pub alpha_to_coverage_enabled: bool,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SourcePipelineState {
    pub primitive: SourcePrimitiveState,
    pub targets: Vec<SourceColorTarget>,
    pub depth_stencil: SourceDepthState,
    pub multisample: SourceMultisampleState,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize)]
pub struct SourceStageBindingRequirements {
    pub uniform_buffers: u32,
    pub storage_buffers: u32,
    pub sampled_textures: u32,
    pub samplers: u32,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize)]
pub struct SourceBindingRequirements {
    pub vertex: SourceStageBindingRequirements,
    pub fragment: SourceStageBindingRequirements,
}

impl SourceMeshProgram {
    /// Explicit layouts count every declared visible binding, including resources
    /// eliminated by shader reachability. GPU admission must use these counts.
    #[must_use]
    pub fn declared_requirements(&self) -> SourceBindingRequirements {
        let mut result = SourceBindingRequirements::default();
        // The source Draw profile declares visibility vertex|fragment.
        result.vertex.uniform_buffers = 1;
        result.fragment.uniform_buffers = 1;
        for binding in self.group0.iter().chain(&self.group1) {
            for (flag, stage) in [(1, &mut result.vertex), (2, &mut result.fragment)] {
                if binding.layout.visibility & flag != 0 {
                    if let Some(buffer) = &binding.layout.buffer {
                        match buffer.kind {
                            SourceBufferKind::Uniform => stage.uniform_buffers += 1,
                            SourceBufferKind::ReadOnlyStorage => stage.storage_buffers += 1,
                        }
                    } else if binding.layout.texture.is_some() {
                        stage.sampled_textures += 1;
                    } else if binding.layout.sampler.is_some() {
                        stage.samplers += 1;
                    }
                }
            }
        }
        result
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(untagged)]
pub enum SourceUniformValue {
    Scalar(f64),
    Vector(Vec<f64>),
    Array(Vec<Vec<f64>>),
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceUniformField {
    pub name: String,
    #[serde(rename = "type")]
    pub type_name: String,
    pub offset: usize,
    pub size: usize,
    pub stride: usize,
    pub value: SourceUniformValue,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceUniformBlock {
    #[serde(rename = "struct")]
    pub struct_name: String,
    pub bytes: usize,
    pub fields: Vec<SourceUniformField>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SourceScalarKind {
    Float,
    Uint,
    Sint,
}

/// The explicit supported source ABI. Unsupported types fail before packing.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SourceUniformType {
    pub scalar: SourceScalarKind,
    pub lanes: usize,
    pub elements: usize,
    pub array: bool,
    pub alignment: usize,
    pub stride: usize,
}

impl SourceUniformType {
    pub fn parse(name: &str) -> Result<Self, ShaderPackageError> {
        let (base, elements, array) = if let Some((base, suffix)) = name.split_once('[') {
            let count = suffix
                .strip_suffix(']')
                .and_then(|s| s.parse::<usize>().ok())
                .filter(|&n| (1..=4096).contains(&n))
                .ok_or_else(|| invalid("invalid uniform array count"))?;
            (base, count, true)
        } else {
            (name, 1, false)
        };
        let (scalar, lanes, alignment, stride) = match base {
            "f32" => (SourceScalarKind::Float, 1, 4, 4),
            "u32" => (SourceScalarKind::Uint, 1, 4, 4),
            "i32" => (SourceScalarKind::Sint, 1, 4, 4),
            "vec2f" => (SourceScalarKind::Float, 2, 8, 8),
            "vec3f" => (SourceScalarKind::Float, 3, 16, 12),
            "vec4f" => (SourceScalarKind::Float, 4, 16, 16),
            "mat4x4f" => (SourceScalarKind::Float, 16, 16, 64),
            _ => return Err(invalid(format!("unsupported uniform type {name}"))),
        };
        if array && alignment < 16 {
            return Err(invalid(
                "source uniform array element must be 16-byte aligned",
            ));
        }
        // Source UniformBlock uses a 16-byte minimum stride for uniform arrays.
        Ok(Self {
            scalar,
            lanes,
            elements,
            array,
            alignment: if array { alignment.max(16) } else { alignment },
            stride: if array { align(stride, 16) } else { stride },
        })
    }
}

fn align(n: usize, alignment: usize) -> usize {
    n.div_ceil(alignment) * alignment
}

impl SourceUniformBlock {
    /// Produces the source's little-endian f32/u32/i32 ABI with zero padding.
    /// Current frame values can be supplied through a cloned block; canonical
    /// material defaults and mutable source snapshots remain distinct inputs.
    pub fn pack(&self) -> Result<Vec<u8>, ShaderPackageError> {
        if !identifier(&self.struct_name)
            || self.bytes == 0
            || self.bytes > 1024 * 1024
            || self.fields.is_empty()
            || self.fields.len() > 256
        {
            return Err(invalid("uniform block bounds or identity"));
        }
        let mut data = vec![0; self.bytes];
        let mut names = BTreeSet::new();
        let mut end = 0;
        for field in &self.fields {
            let ty = SourceUniformType::parse(&field.type_name)?;
            let size = ty.stride * ty.elements;
            if !identifier(&field.name)
                || !names.insert(&field.name)
                || field.offset != align(end, ty.alignment)
                || field.size != size
                || field.stride != ty.stride
                || field
                    .offset
                    .checked_add(size)
                    .is_none_or(|n| n > self.bytes)
            {
                return Err(invalid(format!(
                    "uniform {}.{} ABI differs",
                    self.struct_name, field.name
                )));
            }
            let rows: Vec<&[f64]> = match (&field.value, ty.array, ty.lanes) {
                (SourceUniformValue::Scalar(v), false, 1) => vec![std::slice::from_ref(v)],
                (SourceUniformValue::Vector(v), false, _) => vec![v],
                (SourceUniformValue::Array(v), true, _) => v.iter().map(Vec::as_slice).collect(),
                _ => {
                    return Err(invalid(format!(
                        "uniform {} value shape differs",
                        field.name
                    )));
                }
            };
            if rows.len() != ty.elements || rows.iter().any(|v| v.len() != ty.lanes) {
                return Err(invalid(format!(
                    "uniform {} value dimensions differ",
                    field.name
                )));
            }
            for (row, values) in rows.iter().enumerate() {
                for (lane, &value) in values.iter().enumerate() {
                    let bytes = scalar_bytes(ty.scalar, value).map_err(|error| {
                        invalid(format!(
                            "uniform {}.{}: {error}",
                            self.struct_name, field.name
                        ))
                    })?;
                    let start = field.offset + row * ty.stride + lane * 4;
                    data[start..start + 4].copy_from_slice(&bytes);
                }
            }
            end = field.offset + size;
        }
        if self.bytes != align(end, 16) {
            return Err(invalid("uniform block padded size differs"));
        }
        Ok(data)
    }
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn scalar_bytes(kind: SourceScalarKind, value: f64) -> Result<[u8; 4], ShaderPackageError> {
    if !value.is_finite() {
        return Err(invalid("nonfinite uniform scalar"));
    }
    match kind {
        SourceScalarKind::Float if (value as f32).is_finite() => Ok((value as f32).to_le_bytes()),
        SourceScalarKind::Uint
            if value.fract() == 0.0 && (0.0..=f64::from(u32::MAX)).contains(&value) =>
        {
            Ok((value as u32).to_le_bytes())
        }
        SourceScalarKind::Sint
            if value.fract() == 0.0
                && (f64::from(i32::MIN)..=f64::from(i32::MAX)).contains(&value) =>
        {
            Ok((value as i32).to_le_bytes())
        }
        _ => Err(invalid("uniform scalar outside its source ABI")),
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceMeshShaderManifest {
    pub schema: String,
    pub version: u32,
    pub source_commit: String,
    pub provenance: SourceShaderProvenance,
    pub frame: SourceUniformBlock,
    pub materials: Vec<SourceMeshMaterial>,
    pub hooks: Vec<SourceShaderHook>,
    pub programs: Vec<SourceMeshProgram>,
    files: BTreeMap<String, Entry>,
}

pub struct SourceMeshShaderPackage {
    manifest: SourceMeshShaderManifest,
    files: BTreeMap<String, Vec<u8>>,
    hash: ContentHash,
}

impl SourceMeshShaderPackage {
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, ShaderPackageError> {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes).map_err(invalid)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(index, files, expected)
    }

    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, ShaderPackageError>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes).await.map_err(invalid)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(index, files, expected)
    }

    fn finish(
        manifest: SourceMeshShaderManifest,
        files: BTreeMap<String, Vec<u8>>,
        hash: ContentHash,
    ) -> Result<Self, ShaderPackageError> {
        for program in &manifest.programs {
            let code = std::str::from_utf8(
                files
                    .get(&program.code.file)
                    .ok_or_else(|| invalid("program payload missing"))?,
            )?;
            if code.is_empty() || code.contains('\0') {
                return Err(invalid("invalid program text"));
            }
        }
        Ok(Self {
            manifest,
            files,
            hash,
        })
    }

    #[must_use]
    pub fn manifest(&self) -> &SourceMeshShaderManifest {
        &self.manifest
    }
    #[must_use]
    pub fn hash(&self) -> ContentHash {
        self.hash
    }

    /// The program must be a member of this exact verified package.
    pub fn code(&self, program: &SourceMeshProgram) -> Result<&str, ShaderPackageError> {
        if !self
            .manifest
            .programs
            .iter()
            .any(|p| std::ptr::eq(p, program))
        {
            return Err(invalid("program does not belong to this package"));
        }
        Ok(std::str::from_utf8(
            self.files
                .get(&program.code.file)
                .ok_or_else(|| invalid("program payload missing"))?,
        )?)
    }
}

fn parse(
    bytes: &[u8],
    expected: ContentHash,
) -> Result<SourceMeshShaderManifest, ShaderPackageError> {
    if bytes.len() > MAX_FILE || bytes_sha256(bytes) != expected {
        return Err(invalid("manifest hash or size differs"));
    }
    let index: SourceMeshShaderManifest = serde_json::from_slice(bytes)?;
    if index.schema != "forge.source-mesh-shaders"
        || !matches!(index.version, 1 | 2)
        || index.source_commit.len() != 40
        || !index.source_commit.bytes().all(|c| c.is_ascii_hexdigit())
        || index.files.is_empty()
        || index.files.len() > 512
        || index.materials.is_empty()
        || index.materials.len() > 64
        || index.programs.is_empty()
        || index.programs.len() > 256
        || index.hooks.len() > 64
    {
        return Err(invalid("schema or collection bounds differ"));
    }
    let mut total = 0usize;
    for (name, entry) in &index.files {
        total = total
            .checked_add(entry.bytes)
            .ok_or_else(|| invalid("package byte size overflow"))?;
        if !safe_name(name) || entry.bytes == 0 || entry.bytes > MAX_FILE || total > MAX_TOTAL {
            return Err(invalid("package path or file budget differs"));
        }
    }
    for r in [
        &index.provenance.capture,
        &index.provenance.receipt,
        &index.provenance.inputs,
        &index.provenance.license,
    ]
    .into_iter()
    .chain(index.provenance.credits.iter())
    {
        reference(r, &index.files)?;
    }
    index.frame.pack()?;
    let mut materials = BTreeMap::new();
    for material in &index.materials {
        if !identifier(&material.name)
            || materials.insert(&material.name, material).is_some()
            || !material.velocity_weight.is_finite()
            || !(0.0..=1.0).contains(&material.velocity_weight)
        {
            return Err(invalid("material identity or velocity policy differs"));
        }
        material.uniforms.pack()?;
        vertex_layout(material)?;
        match (index.version, &material.raster) {
            (1, None) => {}
            (2, Some(raster)) if valid_slope(raster.depth_bias_slope_scale) => {}
            _ => {
                return Err(invalid(
                    "material raster policy differs from schema version",
                ));
            }
        }
    }
    let mut hooks = BTreeSet::new();
    for hook in &index.hooks {
        if !identifier(&hook.name) || hook.module.is_empty() || !hooks.insert(&hook.name) {
            return Err(invalid("duplicate/invalid shader hook"));
        }
    }
    validate_programs(&index, &materials)?;
    Ok(index)
}

fn validate_programs(
    index: &SourceMeshShaderManifest,
    materials: &BTreeMap<&String, &SourceMeshMaterial>,
) -> Result<(), ShaderPackageError> {
    let mut variants = BTreeSet::new();
    for program in &index.programs {
        let material = materials
            .get(&program.material)
            .ok_or_else(|| invalid("unknown program material"))?;
        if !variants.insert((&program.material, program.pass.kind, program.pass.late)) {
            return Err(invalid("duplicate program variant"));
        }
        reference(&program.code, &index.files)?;
        if program.pass.depth_format != SourceTargetFormat::Depth32Float
            || program
                .pass
                .color_formats
                .contains(&SourceTargetFormat::Depth32Float)
            || (program.pass.kind == SourcePassKind::Depth
                && (!program.pass.color_formats.is_empty() || program.pass.late))
            || (program.pass.kind == SourcePassKind::Main && program.pass.color_formats.len() != 3)
            || (program.pass.kind == SourcePassKind::Color
                && (program.pass.color_formats.len() != 1 || program.pass.late))
            || (program.pass.kind != SourcePassKind::Depth && !program.has_fragment)
        {
            return Err(invalid("program pass policy differs"));
        }
        match (index.version, &program.pipeline_state) {
            (1, None) if program.pass.raster_overrides.is_none() => {}
            (2, Some(state)) => validate_pipeline_state(program, material, state)?,
            _ => return Err(invalid("pipeline state differs from schema version")),
        }
        bindings(&program.group0)?;
        bindings(&program.group1)?;
        if program
            .group0
            .iter()
            .find(|b| b.name == "frame")
            .and_then(|b| b.uniform.as_ref())
            != Some(&index.frame)
            || program
                .group1
                .iter()
                .find(|b| b.name == "mat")
                .and_then(|b| b.uniform.as_ref())
                != Some(&material.uniforms)
        {
            return Err(invalid("program Frame/MaterialParams ABI differs"));
        }
        let draw = &program.draw_binding;
        if draw.group != 2
            || draw.binding != 0
            || draw.bytes != 160
            || draw.dynamic_stride < draw.bytes
            || !draw.dynamic_stride.is_multiple_of(256)
        {
            return Err(invalid("unsupported dynamic Draw ABI"));
        }
        if program.modules.len() > 128 || program.defines.len() > 128 {
            return Err(invalid("program metadata budget differs"));
        }
        let mut modules = BTreeMap::new();
        for module in &program.modules {
            if module.name.is_empty() || modules.insert(&module.name, module.sha256).is_some() {
                return Err(invalid("duplicate/invalid program module"));
            }
        }
        // A module omitted by reachability is allowed; an installed hook present
        // in the composed program must retain the acquired code identity.
        for hook in &index.hooks {
            if modules
                .get(&hook.module)
                .is_some_and(|h| *h != hook.code_sha256)
            {
                return Err(invalid("program lighting hook identity differs"));
            }
        }
    }
    Ok(())
}

fn validate_pipeline_state(
    program: &SourceMeshProgram,
    material: &SourceMeshMaterial,
    state: &SourcePipelineState,
) -> Result<(), ShaderPackageError> {
    let raster = material
        .raster
        .as_ref()
        .ok_or_else(|| invalid("material raster policy missing"))?;
    let overrides = program
        .pass
        .raster_overrides
        .as_ref()
        .ok_or_else(|| invalid("pass raster overrides missing"))?;
    let cull = overrides.cull_mode.unwrap_or(match material.side {
        SourceSide::Front => SourceCull::Back,
        SourceSide::Back => SourceCull::Front,
        SourceSide::Double => SourceCull::None,
    });
    if state.primitive.topology != raster.topology
        || state.primitive.front_face != SourceFrontFace::Ccw
        || state.primitive.cull_mode != cull
        || state.targets.len() != program.pass.color_formats.len()
        || state
            .targets
            .iter()
            .zip(&program.pass.color_formats)
            .enumerate()
            .any(|(i, (t, f))| {
                t.format != *f
                    || t.write_mask != if raster.color_write { 15 } else { 0 }
                    || t.blend != expected_blend(material, &program.pass, i)
            })
        || state.multisample.count != 1
        || state.multisample.mask != u64::from(u32::MAX)
        || state.multisample.alpha_to_coverage_enabled
        || (program.pass.kind == SourcePassKind::Depth
            && program.has_fragment != (program.defines.get("NEEDS_DEPTH_FRAGMENT") == Some(&1)))
    {
        return Err(invalid("explicit source pipeline state differs"));
    }
    validate_depth_state(program, material, raster, overrides, &state.depth_stencil)
}

fn valid_slope(value: f64) -> bool {
    value.is_finite() && value.abs() <= f64::from(f32::MAX)
}

fn validate_depth_state(
    program: &SourceMeshProgram,
    material: &SourceMeshMaterial,
    raster: &SourceMaterialRaster,
    overrides: &SourcePassRasterOverrides,
    state: &SourceDepthState,
) -> Result<(), ShaderPackageError> {
    let compare = if material.depth_test {
        raster.depth_compare.unwrap_or(program.pass.depth_compare)
    } else {
        SourceDepthCompare::Always
    };
    let (bias, slope) = if program.pass.kind == SourcePassKind::Depth {
        (overrides.depth_bias, overrides.depth_bias_slope_scale)
    } else {
        (raster.depth_bias, raster.depth_bias_slope_scale)
    };
    if state.format != program.pass.depth_format
        || state.depth_write_enabled != material.depth_write
        || state.depth_compare != compare
        || state.depth_bias != bias
        || !valid_slope(overrides.depth_bias_slope_scale)
        || !valid_slope(state.depth_bias_slope_scale)
        || state.depth_bias_slope_scale.to_bits() != slope.to_bits()
    {
        return Err(invalid("source depth policy differs"));
    }
    Ok(())
}

fn expected_blend(
    material: &SourceMeshMaterial,
    pass: &SourceMeshPass,
    target: usize,
) -> Option<SourceBlendState> {
    let preset = match (pass.kind, target) {
        (SourcePassKind::Main, 0) if material.transparent || pass.late => material.blending,
        (SourcePassKind::Main, 1) if pass.late => SourceBlend::Premultiplied,
        (SourcePassKind::Main, 2) => SourceBlend::Normal,
        (SourcePassKind::Color, _) => material.blending,
        _ => SourceBlend::None,
    };
    blend_preset(preset)
}

fn blend_preset(preset: SourceBlend) -> Option<SourceBlendState> {
    use SourceBlendFactor::{Dst, One, OneMinusSrcAlpha, SrcAlpha, Zero};
    use SourceBlendOperation::{Add, Max, Min};
    let component = |src_factor, dst_factor, operation| SourceBlendComponent {
        src_factor,
        dst_factor,
        operation,
    };
    let (color, alpha) = match preset {
        SourceBlend::None => return None,
        SourceBlend::Normal => (
            component(SrcAlpha, OneMinusSrcAlpha, Add),
            component(One, OneMinusSrcAlpha, Add),
        ),
        SourceBlend::Premultiplied => (
            component(One, OneMinusSrcAlpha, Add),
            component(One, OneMinusSrcAlpha, Add),
        ),
        SourceBlend::Additive => (component(SrcAlpha, One, Add), component(Zero, One, Add)),
        SourceBlend::Multiply => (component(Dst, Zero, Add), component(Zero, One, Add)),
        SourceBlend::Add => (component(One, One, Add), component(One, One, Add)),
        SourceBlend::Min => (component(One, One, Min), component(One, One, Min)),
        SourceBlend::Max => (component(One, One, Max), component(One, One, Max)),
    };
    Some(SourceBlendState { color, alpha })
}

fn reference(
    r: &AssetFileReference,
    files: &BTreeMap<String, Entry>,
) -> Result<(), ShaderPackageError> {
    let entry = files
        .get(&r.file)
        .ok_or_else(|| invalid("unlisted package reference"))?;
    if entry.bytes != r.bytes || entry.sha256 != r.sha256 {
        return Err(invalid("package reference receipt differs"));
    }
    Ok(())
}
fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), ShaderPackageError> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err(invalid("payload hash or byte length differs"));
    }
    Ok(())
}
fn safe_name(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 240
        && name.split('/').all(|part| {
            !part.is_empty()
                && part != "."
                && part != ".."
                && part
                    .bytes()
                    .all(|c| c.is_ascii_alphanumeric() || matches!(c, b'.' | b'_' | b'-'))
        })
}
fn identifier(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 128
        && name
            .bytes()
            .enumerate()
            .all(|(i, c)| c.is_ascii_alphabetic() || c == b'_' || (i > 0 && c.is_ascii_digit()))
}

fn bindings(bindings: &[SourceBinding]) -> Result<(), ShaderPackageError> {
    if bindings.is_empty() || bindings.len() > 128 {
        return Err(invalid("binding count differs"));
    }
    let mut names = BTreeSet::new();
    let mut slots = BTreeSet::new();
    for binding in bindings {
        let l = &binding.layout;
        if !identifier(&binding.name)
            || !names.insert(&binding.name)
            || !slots.insert(binding.binding)
            || binding.binding >= 128
            || !(1..=3).contains(&l.visibility)
            || usize::from(l.buffer.is_some())
                + usize::from(l.sampler.is_some())
                + usize::from(l.texture.is_some())
                != 1
            || (binding.uniform.is_some() && l.buffer.is_none())
        {
            return Err(invalid("binding identity, stage or resource kind differs"));
        }
        if let Some(block) = &binding.uniform {
            block.pack()?;
        }
    }
    Ok(())
}

fn vertex_layout(material: &SourceMeshMaterial) -> Result<(), ShaderPackageError> {
    if material.vertex_layout.is_empty()
        || material.vertex_layout.len() > 16
        || material.vertex_buffers.is_empty()
        || material.vertex_buffers.len() > 16
    {
        return Err(invalid("vertex layout budget differs"));
    }
    let mut fields = BTreeMap::new();
    for field in &material.vertex_layout {
        if !identifier(&field.name)
            || field.location >= 16
            || fields.insert(field.location, field).is_some()
        {
            return Err(invalid("vertex field identity differs"));
        }
    }
    let mut slots = BTreeSet::new();
    for buffer in &material.vertex_buffers {
        if buffer.array_stride == 0
            || buffer.array_stride > 2048
            || !buffer.array_stride.is_multiple_of(4)
            || buffer.attributes.is_empty()
            || buffer.attributes.len() > 16
        {
            return Err(invalid("vertex buffer budget differs"));
        }
        for attr in &buffer.attributes {
            let field = fields
                .get(&attr.shader_location)
                .ok_or_else(|| invalid("unknown vertex buffer location"))?;
            if !slots.insert(attr.shader_location)
                || !attr.offset.is_multiple_of(4)
                || attr
                    .offset
                    .checked_add(attr.format.width())
                    .is_none_or(|n| n > buffer.array_stride)
                || attr.format.wgsl() != field.wgsl
                || (buffer.step_mode == SourceVertexStep::Instance) != field.instanced
            {
                return Err(invalid("vertex field/buffer ABI differs"));
            }
        }
    }
    if slots.len() != fields.len() {
        return Err(invalid("unbound vertex field"));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn counts_declared_visible_resources_even_without_fragment_shader() {
        let bindings: Vec<SourceBinding> = serde_json::from_value(serde_json::json!([
            {"name":"uniform","binding":0,"declaration":"unused","uniform":null,
             "layout":{"visibility":1,"buffer":{"type":"uniform"}}},
            {"name":"storage","binding":1,"declaration":"unused","uniform":null,
             "layout":{"visibility":2,"buffer":{"type":"read-only-storage"}}},
            {"name":"texture","binding":2,"declaration":"unused","uniform":null,
             "layout":{"visibility":3,"texture":{"sampleType":"float","viewDimension":"2d","multisampled":false}}},
            {"name":"samplerV","binding":3,"declaration":"unused","uniform":null,
             "layout":{"visibility":1,"sampler":{"type":"filtering"}}},
            {"name":"samplerF","binding":4,"declaration":"unused","uniform":null,
             "layout":{"visibility":2,"sampler":{"type":"filtering"}}}
        ])).unwrap();
        let program = SourceMeshProgram {
            material: "material".into(),
            pass: SourceMeshPass {
                kind: SourcePassKind::Depth,
                late: false,
                color_formats: vec![],
                depth_format: SourceTargetFormat::Depth32Float,
                depth_compare: SourceDepthCompare::LessEqual,
                raster_overrides: None,
            },
            defines: BTreeMap::new(),
            has_fragment: false,
            code: AssetFileReference {
                file: "shader.wgsl".into(),
                bytes: 0,
                sha256: bytes_sha256(b""),
            },
            modules: vec![],
            group0: bindings,
            group1: vec![],
            draw_binding: SourceDrawBinding {
                group: 2,
                binding: 0,
                bytes: 160,
                dynamic_stride: 256,
            },
            pipeline_state: None,
        };
        assert_eq!(
            program.declared_requirements(),
            SourceBindingRequirements {
                vertex: SourceStageBindingRequirements {
                    uniform_buffers: 2,
                    storage_buffers: 0,
                    sampled_textures: 1,
                    samplers: 1
                },
                fragment: SourceStageBindingRequirements {
                    uniform_buffers: 1,
                    storage_buffers: 1,
                    sampled_textures: 1,
                    samplers: 1
                },
            }
        );
    }

    fn block() -> SourceUniformBlock {
        SourceUniformBlock {
            struct_name: "Packet".into(),
            bytes: 64,
            fields: vec![
                SourceUniformField {
                    name: "position".into(),
                    type_name: "vec3f".into(),
                    offset: 0,
                    size: 12,
                    stride: 12,
                    value: SourceUniformValue::Vector(vec![1.25, -2.5, 3.0]),
                },
                SourceUniformField {
                    name: "count".into(),
                    type_name: "u32".into(),
                    offset: 12,
                    size: 4,
                    stride: 4,
                    value: SourceUniformValue::Scalar(f64::from(0x0102_0304_u32)),
                },
                SourceUniformField {
                    name: "lights".into(),
                    type_name: "vec4f[2]".into(),
                    offset: 16,
                    size: 32,
                    stride: 16,
                    value: SourceUniformValue::Array(vec![
                        vec![4.0, 5.0, 6.0, 7.0],
                        vec![8.0, 9.0, 10.0, 11.0],
                    ]),
                },
                SourceUniformField {
                    name: "index".into(),
                    type_name: "i32".into(),
                    offset: 48,
                    size: 4,
                    stride: 4,
                    value: SourceUniformValue::Scalar(-19.0),
                },
            ],
        }
    }

    #[test]
    fn packs_source_vectors_integer_lanes_and_padding_without_saturation() {
        let packed = block().pack().unwrap();
        assert_eq!(&packed[..4], &1.25_f32.to_le_bytes());
        assert_eq!(&packed[4..8], &(-2.5_f32).to_le_bytes());
        assert_eq!(&packed[12..16], &0x0102_0304_u32.to_le_bytes());
        assert_eq!(&packed[32..36], &8.0_f32.to_le_bytes());
        assert_eq!(&packed[48..52], &(-19_i32).to_le_bytes());
        assert!(packed[52..].iter().all(|&b| b == 0));
        let json = serde_json::to_vec(&block()).unwrap();
        let reopened: SourceUniformBlock = serde_json::from_slice(&json).unwrap();
        assert_eq!(reopened.pack().unwrap(), packed);
    }

    #[test]
    fn rejects_float_overflow_and_fractional_or_out_of_range_integer_values() {
        let mut packet = block();
        for bad in [f64::NAN, f64::INFINITY, f64::MAX] {
            packet.fields[0].value = SourceUniformValue::Vector(vec![bad, 2.0, 3.0]);
            assert!(packet.pack().is_err());
        }
        packet = block();
        for bad in [-1.0, 1.5, f64::from(u32::MAX) + 1.0] {
            packet.fields[1].value = SourceUniformValue::Scalar(bad);
            assert!(packet.pack().is_err());
        }
        packet = block();
        for bad in [f64::from(i32::MIN) - 1.0, f64::from(i32::MAX) + 1.0, 2.5] {
            packet.fields[3].value = SourceUniformValue::Scalar(bad);
            assert!(packet.pack().is_err());
        }
    }

    #[test]
    fn rejects_wrong_array_dimensions_and_overlapping_layout() {
        let mut packet = block();
        packet.fields[2].offset = 12;
        assert!(packet.pack().is_err());
        packet = block();
        packet.fields[2].value = SourceUniformValue::Array(vec![vec![1.0, 2.0, 3.0]]);
        assert!(packet.pack().is_err());
        packet = block();
        packet.fields[2].stride = 12;
        assert!(packet.pack().is_err());
        packet = block();
        packet.fields[3].name = "count".into();
        assert!(packet.pack().is_err());
    }

    #[test]
    fn rejects_unsupported_abi_and_unbounded_arrays() {
        for ty in [
            "f16",
            "vec3f[0]",
            "vec4f[4097]",
            "vec4f[-1]",
            "vec4f[8][2]",
            "vec2f[2]",
            "mat3x3f",
        ] {
            assert!(SourceUniformType::parse(ty).is_err(), "{ty}");
        }
        let mut packet = block();
        packet.bytes = 1024 * 1024 + 16;
        assert!(packet.pack().is_err());
    }
}
