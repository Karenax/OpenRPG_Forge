//! Corner-grid author deltas, kept separate from the cell-centred source raster.
use forge_project::TerrainDocument;

/// Corner deltas for an admitted terrain, with a 1x1 neutral texture when empty.
/// The caller must supply an original height evaluator for the same domain.
pub struct Corrections {
    pub width: u32,
    pub height: u32,
    pub bytes: Vec<u8>,
    pub elevation: f32,
    domain: Option<Domain>,
    fixed_source_profile: bool,
}

struct Domain {
    origin: [f32; 2],
    spacing: f32,
    intervals: [u32; 2],
}

impl Corrections {
    /// Whether the admitted raster matches the fixed source terrain and water
    /// programs, including their outside height, and their resource layouts.
    #[must_use]
    pub const fn supports_fixed_source_profile(&self) -> bool {
        self.fixed_source_profile
    }

    /// Compute kernels reserve an unused detail texture slot; keep binding counts stable.
    pub fn shader_existing(&self, source: &str) -> Result<String, String> {
        if source.matches("fn terrainHeightAt(").count() != 1
            || source.matches("terrainDetailTex").count() != 1
        {
            return Err(
                "expected one height evaluator and an unused terrain detail binding".into(),
            );
        }
        let source = source
            .replacen("fn terrainHeightAt(", "fn forgeOriginalTerrainHeightAt(", 1)
            .replace("terrainDetailTex", "forgeTerrainDeltas");
        Ok(format!(
            "{source}\nconst forgeBaseElevation:f32={:?};\n{}",
            self.elevation,
            self.functions()
        ))
    }
    pub fn from_document(doc: &TerrainDocument) -> Result<Self, String> {
        doc.validate().map_err(|e| e.to_string())?;
        let fixed = doc.extent_m.size_x_m.to_bits() == 2048.0_f32.to_bits()
            && doc.extent_m.size_z_m.to_bits() == 2048.0_f32.to_bits()
            && doc.height_sampling.sample_spacing_m.to_bits() == 1.0_f32.to_bits();
        let fixed_source_profile = doc
            .sampled_source
            .as_ref()
            .is_some_and(crate::source_terrain::supports_fixed_source_raster);
        let domain = if fixed {
            None
        } else {
            let source = doc
                .sampled_source
                .as_ref()
                .ok_or("Non-reference terrain corrections require an admitted sampled raster")?;
            // Document validation ties this centered source to f32 extent and spacing.
            let origin = [-doc.extent_m.size_x_m * 0.5, -doc.extent_m.size_z_m * 0.5];
            Some(Domain {
                origin,
                spacing: doc.height_sampling.sample_spacing_m,
                intervals: [source.width, source.height],
            })
        };
        let intervals = domain.as_ref().map_or([2048, 2048], |d| d.intervals);
        let [width, height] = if doc.height_deltas.is_empty() {
            [1, 1]
        } else {
            intervals.map(|value| value + 1)
        };
        let mut values = vec![0.0_f32; width as usize * height as usize];
        for delta in &doc.height_deltas {
            values[usize::from(delta.sample_z) * width as usize + usize::from(delta.sample_x)] =
                delta.delta_m;
        }
        Ok(Self {
            width,
            height,
            bytes: values.into_iter().flat_map(f32::to_le_bytes).collect(),
            elevation: doc.height_sampling.base_elevation_m,
            domain,
            fixed_source_profile,
        })
    }

    pub fn shader(&self, source: &str, group: &str, binding: u32) -> Result<String, String> {
        if source.matches("fn terrainHeightAt(").count() != 1 {
            return Err("expected one original terrain height evaluator".into());
        }
        let source = source.replacen("fn terrainHeightAt(", "fn forgeOriginalTerrainHeightAt(", 1);
        Ok(format!(
            "{source}\n@group({group}) @binding({binding}) var forgeTerrainDeltas:texture_2d<f32>;\nconst forgeBaseElevation:f32={:?};\n{}",
            self.elevation,
            self.functions()
        ))
    }

    fn functions(&self) -> String {
        let Some(domain) = &self.domain else {
            // Preserve the exact source program for the established reference path.
            return FUNCTIONS.to_owned();
        };
        FUNCTIONS
            .replace(
                "xz+vec2f(1024.0)",
                &format!(
                    "(xz-vec2f({:?}, {:?})) / {:?}",
                    domain.origin[0], domain.origin[1], domain.spacing
                ),
            )
            .replace(
                "vec2f(2048.0)",
                &format!(
                    "vec2f({}.0, {}.0)",
                    domain.intervals[0], domain.intervals[1]
                ),
            )
            .replace(
                "vec2i(2047)",
                &format!(
                    "vec2i({}, {})",
                    domain.intervals[0] - 1,
                    domain.intervals[1] - 1
                ),
            )
    }
}

pub(crate) const FUNCTIONS: &str = r"
fn terrainHeightAt(xz:vec2f)->f32 {
    let base=forgeOriginalTerrainHeightAt(xz);
    let g=xz+vec2f(1024.0);
    if textureDimensions(forgeTerrainDeltas).x == 1u || any(g < vec2f(0.0)) || any(g > vec2f(2048.0)) {
        return base+forgeBaseElevation;
    }
    let i=min(vec2i(floor(g)),vec2i(2047));
    let t=g-vec2f(i);
    let a=textureLoad(forgeTerrainDeltas,i,0).x;
    let b=textureLoad(forgeTerrainDeltas,i+vec2i(1,0),0).x;
    let c=textureLoad(forgeTerrainDeltas,i+vec2i(0,1),0).x;
    let d=textureLoad(forgeTerrainDeltas,i+vec2i(1,1),0).x;
    return base+forgeBaseElevation+mix(mix(a,b,t.x),mix(c,d,t.x),t.y);
}
";

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        TerrainHeightDelta,
        authoring::new_terrain,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };
    use forge_world::sampled_terrain::SampledTerrainPlan;

    fn fixture(width: u16, height: u16, spacing: f32) -> SampledTerrainPlan {
        let mut doc = new_terrain("sampled", "authoring", "Correction test").unwrap();
        doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        doc.extent_m = TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
        doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        doc.height_sampling.sample_spacing_m = spacing;
        doc.height_sampling.base_elevation_m = 3.0;
        doc.height_sampling.amplitude_m = 0.0;
        doc.height_sampling.base_frequency_cycles_per_m = 1.0;
        doc.height_sampling.octaves = 1;
        doc.height_sampling.lacunarity = 2.0;
        doc.height_sampling.persistence = 0.5;
        let bytes = vec![0_u8; usize::from(width) * usize::from(height) * 4];
        doc.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: u32::from(width),
            height: u32::from(height),
            origin_xz_m: [
                -f64::from(doc.extent_m.size_x_m) * 0.5,
                -f64::from(doc.extent_m.size_z_m) * 0.5,
            ],
            sample_spacing_m: f64::from(spacing),
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&bytes),
        });
        doc.height_deltas = vec![
            TerrainHeightDelta::new(0, 0, -2.0),
            TerrainHeightDelta::new(2, 3, 4.0),
            TerrainHeightDelta::new(width, height, 8.0),
        ];
        SampledTerrainPlan::from_document(&doc, &bytes).unwrap()
    }

    #[test]
    fn rectangle_uses_its_own_corner_lattice_and_valid_wgsl() {
        let plan = fixture(11, 19, 0.5);
        let edits = Corrections::from_document(plan.document()).unwrap();
        assert_eq!((edits.width, edits.height), (12, 20));
        assert!(!edits.supports_fixed_source_profile());
        assert_eq!(
            crate::source_breaker_gpu::prepare_program(
                crate::source_breaker_gpu::COMMON_CREST_PROGRAM,
                Some(&edits)
            )
            .unwrap_err(),
            "Breaker program requires its admitted fixed raster profile"
        );
        assert_eq!(edits.bytes.len(), 12 * 20 * 4);
        for (x, z, value) in [(0, 0, -2.0_f32), (2, 3, 4.0), (11, 19, 8.0)] {
            let index = (z * 12 + x) * 4;
            assert_eq!(&edits.bytes[index..index + 4], &value.to_le_bytes());
        }
        let source = "fn terrainHeightAt(xz:vec2f)->f32 { return xz.x; }";
        let shader = edits.shader(source, "0", 0).unwrap();
        assert!(shader.contains("(xz-vec2f(-2.75, -4.75)) / 0.5"));
        assert!(shader.contains("vec2f(11.0, 19.0)"));
        assert!(shader.contains("vec2i(10, 18)"));
        let module = naga::front::wgsl::parse_str(&shader).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        let existing = edits
            .shader_existing("@group(0) @binding(1) var terrainDetailTex:texture_2d<f32>;\nfn terrainHeightAt(xz:vec2f)->f32 { return xz.x; }")
            .unwrap();
        naga::front::wgsl::parse_str(&existing).unwrap();
        assert!(existing.contains("vec2f(11.0, 19.0)"));
    }

    #[test]
    fn reference_shader_is_unchanged_and_neutral_texture_is_small() {
        let mut plan = fixture(2048, 2048, 1.0);
        let fixed = Corrections::from_document(plan.document()).unwrap();
        assert_eq!((fixed.width, fixed.height), (2049, 2049));
        assert!(fixed.supports_fixed_source_profile());
        assert!(
            crate::source_breaker_gpu::prepare_program(
                crate::source_breaker_gpu::COMMON_CREST_PROGRAM,
                Some(&fixed)
            )
            .is_ok()
        );
        assert_eq!(fixed.functions(), FUNCTIONS);
        let mut different_outside = plan.document().clone();
        different_outside
            .sampled_source
            .as_mut()
            .unwrap()
            .outside_height_m = 0.0;
        let different_outside = Corrections::from_document(&different_outside).unwrap();
        assert_eq!(different_outside.functions(), FUNCTIONS);
        assert!(!different_outside.supports_fixed_source_profile());
        assert!(
            crate::source_breaker_gpu::prepare_program(
                crate::source_breaker_gpu::COMMON_CREST_PROGRAM,
                Some(&different_outside)
            )
            .is_err()
        );
        let mut doc = plan.document().clone();
        doc.height_deltas.clear();
        plan = SampledTerrainPlan::from_document(&doc, &vec![0; 2048 * 2048 * 4]).unwrap();
        let empty = Corrections::from_document(plan.document()).unwrap();
        assert_eq!((empty.width, empty.height, empty.bytes.len()), (1, 1, 4));
        let mut doc = fixture(11, 19, 0.5).document().clone();
        doc.height_deltas.clear();
        let empty = Corrections::from_document(&doc).unwrap();
        assert_eq!((empty.width, empty.height, empty.bytes.len()), (1, 1, 4));
        assert!(empty.functions().contains("vec2f(11.0, 19.0)"));
    }
}
