//! Sparse canonical paint projected into the existing source terrain material.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageFilterMode, ImageSampler},
    prelude::*,
    render::{
        render_resource::{BufferUsages, ShaderType},
        storage::ShaderBuffer,
    },
};
use forge_world::sampled_terrain::SampledTerrainPlan;

pub const MAX_PAINT_FIELD_BYTES: usize = 128 * 1024 * 1024;
pub const BUFFER: Handle<ShaderBuffer> =
    bevy::asset::uuid_handle!("c5146a32-270e-48ed-9b50-1afd71cba65e");

#[derive(Clone, Debug, Default, PartialEq, ShaderType)]
pub struct Parameters {
    pub palette: crate::terrain_palette::Parameters,
    /// Corner-grid origin X/Z, sample spacing, enabled flag.
    pub frame: Vec4,
    pub shape: UVec4,
}

pub struct Inputs {
    pub parameters: Parameters,
    pub weights: Handle<ShaderBuffer>,
    pub detail: Option<Handle<Image>>,
}

impl Default for Inputs {
    fn default() -> Self {
        Self {
            parameters: Parameters::default(),
            weights: BUFFER,
            detail: None,
        }
    }
}

pub fn install_neutral(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Terrain paint buffer assets absent")?;
    if let Some(buffer) = buffers.get(&BUFFER) {
        if buffer.data.as_deref() != Some(&[0; 16])
            || !buffer
                .buffer_description
                .usage
                .contains(BufferUsages::STORAGE | BufferUsages::COPY_DST)
        {
            return Err("Terrain paint neutral buffer identity is occupied".into());
        }
    } else {
        buffers
            .insert(
                BUFFER.id(),
                ShaderBuffer::new(&[0; 16], RenderAssetUsages::all()),
            )
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

pub struct PreparedPaint {
    parameters: Parameters,
    weights: Option<ShaderBuffer>,
}

impl PreparedPaint {
    /// Reuse the world's authored-weight evaluator. The crop has one zero border
    /// sample on every side; disjoint strokes never fill their intervening space.
    pub fn new(plan: &SampledTerrainPlan) -> Result<Self, String> {
        let doc = plan.document();
        if doc.material_overrides.is_empty() {
            return Ok(Self {
                parameters: Parameters::default(),
                weights: None,
            });
        }
        if doc.material_layers.len() != 3 {
            return Err("Source terrain paint requires the canonical three-role palette".into());
        }
        let min_x = doc
            .material_overrides
            .iter()
            .map(|s| s.sample_x)
            .min()
            .ok_or("Paint absent")?;
        let max_x = doc
            .material_overrides
            .iter()
            .map(|s| s.sample_x)
            .max()
            .ok_or("Paint absent")?;
        let min_z = doc
            .material_overrides
            .iter()
            .map(|s| s.sample_z)
            .min()
            .ok_or("Paint absent")?;
        let max_z = doc
            .material_overrides
            .iter()
            .map(|s| s.sample_z)
            .max()
            .ok_or("Paint absent")?;
        let (width, height, bytes) = field_layout([min_x, min_z], [max_x, max_z])?;
        let mut data = vec![0; bytes];
        for sample in &doc.material_overrides {
            let weights = plan
                .authored_material_weights(sample.sample_x, sample.sample_z)
                .ok_or("Canonical authored paint sample disappeared")?;
            let x = usize::from(sample.sample_x - min_x) + 1;
            let z = usize::from(sample.sample_z - min_z) + 1;
            let offset = (z * width as usize + x) * 16;
            for (slot, value) in [weights.grass, weights.earth, weights.rock, 1.0]
                .into_iter()
                .enumerate()
            {
                data[offset + slot * 4..offset + slot * 4 + 4]
                    .copy_from_slice(&value.to_le_bytes());
            }
        }
        let spacing = doc.height_sampling.sample_spacing_m;
        let parameters = Parameters {
            frame: Vec4::new(
                -doc.extent_m.size_x_m * 0.5 + (f32::from(min_x) - 1.0) * spacing,
                -doc.extent_m.size_z_m * 0.5 + (f32::from(min_z) - 1.0) * spacing,
                spacing,
                1.0,
            ),
            shape: UVec4::new(width, height, 0, 0),
            ..default()
        };
        let weights = ShaderBuffer::new(&data, RenderAssetUsages::all());
        Ok(Self {
            parameters,
            weights: Some(weights),
        })
    }

    /// An unpainted document preserves its exact source program, with no paint reads.
    pub fn shader(&self, source: &str) -> Result<String, String> {
        if self.weights.is_none() {
            return Ok(source.to_owned());
        }
        Self::editable_shader(source)
    }

    /// Keep imports stable while a live scene alternates painted/empty documents.
    /// Empty paint parameters disable the field without replacing this program.
    pub(crate) fn editable_shader(source: &str) -> Result<String, String> {
        let seam = "sourceSurface(input,&surface);";
        let specular = "surface.roughness,surface.metalness,1.0,vec3f(0.0),surface.ao,";
        let normal = "let N0 = normalize( vec3f( nr.x, sqrt( max( 1.0 - nr.x * nr.x - nr.y * nr.y, 0.0025 ) ), nr.y ) );";
        if source.matches(seam).count() != 1
            || source.matches(specular).count() != 1
            || source.matches(normal).count() != 1
            || source.contains("@binding(7)")
            || source.matches("smpLinearRepeat").count() != 3
        {
            return Err("Source terrain authored-paint shader seam drift".into());
        }
        let source = source
            .replace(normal, &format!("{normal}\nforgePaintNormal = N0;"))
            .replace(seam, &format!("{seam}\n{APPLY}"))
            .replace(
                specular,
                "surface.roughness,surface.metalness,forgePaintSpecular,vec3f(0.0),surface.ao,",
            );
        Ok(format!("{IMPORTS}\n{source}\n{FIELD}"))
    }

    pub fn stage(self, world: &mut World) -> Result<Inputs, String> {
        let Some(weights) = self.weights else {
            return Ok(Inputs::default());
        };
        if !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Terrain paint staging assets absent".into());
        }
        let mut detail = crate::visual_valley::terrain_detail_image();
        let ImageSampler::Descriptor(sampler) = &mut detail.sampler else {
            return Err("Authored palette detail sampler changed".into());
        };
        // Slot 7 also supplies the original source's linear-repeat sampler.
        // Keep its mip filter; the authored R8 tile itself has a single mip.
        sampler.mipmap_filter = ImageFilterMode::Linear;
        Ok(Inputs {
            parameters: self.parameters,
            weights: world.resource_mut::<Assets<ShaderBuffer>>().add(weights),
            detail: Some(world.resource_mut::<Assets<Image>>().add(detail)),
        })
    }
}

fn field_layout(minimum: [u16; 2], maximum: [u16; 2]) -> Result<(u32, u32, usize), String> {
    let width = u32::from(
        maximum[0]
            .checked_sub(minimum[0])
            .ok_or("Paint crop inverted")?,
    ) + 3;
    let height = u32::from(
        maximum[1]
            .checked_sub(minimum[1])
            .ok_or("Paint crop inverted")?,
    ) + 3;
    let bytes = (width as usize)
        .checked_mul(height as usize)
        .and_then(|n| n.checked_mul(16))
        .ok_or("Source paint field size overflow")?;
    if bytes > MAX_PAINT_FIELD_BYTES {
        return Err("Source paint field exceeds its bounded derived buffer budget".into());
    }
    Ok((width, height, bytes))
}

const IMPORTS: &str = r#"
#import "embedded://forge_engine/shaders/terrain_palette.wgsl"::{VisualValleyTerrainExtension, normalized_material_weights, authored_terrain_surface}
struct ForgeTerrainPaint { palette: VisualValleyTerrainExtension, frame: vec4f, shape: vec4u, }
@group(#{MATERIAL_BIND_GROUP}) @binding(60) var<storage, read> forgePaintWeights: array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(61) var<uniform> forgePaint: ForgeTerrainPaint;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var forgePaintDetail: texture_2d<f32>;
var<private> forgePaintNormal: vec3f;
"#;

const APPLY: &str = r"
    var forgePaintSpecular = 1.0;
    if forgePaint.frame.w >= 0.5 {
    let forgePaintSample = forgeTerrainPaintAt(input.P.xz);
    let forgePaintCoverage = clamp(forgePaintSample.w, 0.0, 1.0);
    let forgeAuthored = authored_terrain_surface(normalized_material_weights(forgePaintSample.xyz),
        input.P, forgePaintNormal, forgePaint.palette, forgePaintDetail, smpLinearRepeat);
    if forgePaintCoverage > 0.0 {
        surface.albedo = mix(surface.albedo, forgeAuthored.base_color.xyz, forgePaintCoverage);
        surface.normal = select(normalize(mix(surface.normal, forgeAuthored.normal, forgePaintCoverage)),
            forgeAuthored.normal, forgePaintCoverage >= 1.0);
        surface.roughness = mix(surface.roughness, forgeAuthored.perceptual_roughness, forgePaintCoverage);
        surface.metalness = mix(surface.metalness, forgeAuthored.metallic, forgePaintCoverage);
        forgePaintSpecular = mix(1.0, 4.0 * forgeAuthored.reflectance.x * forgeAuthored.reflectance.x, forgePaintCoverage);
    }
    }
";

const FIELD: &str = r"
fn forgeTerrainPaintLoad(p: vec2i) -> vec4f {
    if any(p < vec2i(0)) || any(p >= vec2i(forgePaint.shape.xy)) { return vec4f(0.0); }
    return forgePaintWeights[u32(p.y) * forgePaint.shape.x + u32(p.x)];
}
fn forgeTerrainPaintAt(xz: vec2f) -> vec4f {
    if forgePaint.frame.w < 0.5 || any(forgePaint.shape.xy == vec2u(0u)) { return vec4f(0.0); }
    let g = (xz - forgePaint.frame.xy) / forgePaint.frame.z;
    let p = vec2i(floor(g));
    let t = fract(g);
    let a = forgeTerrainPaintLoad(p);
    let b = forgeTerrainPaintLoad(p + vec2i(1, 0));
    let c = forgeTerrainPaintLoad(p + vec2i(0, 1));
    let d = forgeTerrainPaintLoad(p + vec2i(1, 1));
    // Same a/c/b, b/c/d diagonal as the canonical raster's corner-grid mesh.
    if t.x + t.y <= 1.0 { return a * (1.0 - t.x - t.y) + b * t.x + c * t.y; }
    return d * (t.x + t.y - 1.0) + b * (1.0 - t.y) + c * (1.0 - t.x);
}
";

#[cfg(test)]
#[path = "paint/tests.rs"]
mod tests;
