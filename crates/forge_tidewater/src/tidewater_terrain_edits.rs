//! Corner-grid author deltas, kept separate from the cell-centred source raster.
use forge_project::TerrainDocument;

pub struct Corrections {
    pub side: u32,
    pub bytes: Vec<u8>,
    pub elevation: f32,
}

impl Corrections {
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
            "{source}\nconst forgeBaseElevation:f32={:?};\n{FUNCTIONS}",
            self.elevation
        ))
    }
    pub fn from_document(doc: &TerrainDocument) -> Result<Self, String> {
        doc.validate().map_err(|e| e.to_string())?;
        if doc.extent_m.size_x_m.to_bits() != 2048.0_f32.to_bits()
            || doc.extent_m.size_z_m.to_bits() != 2048.0_f32.to_bits()
            || doc.height_sampling.sample_spacing_m.to_bits() != 1.0_f32.to_bits()
        {
            return Err("Terrain corrections require the 2048 metre unit grid".into());
        }
        let side = if doc.height_deltas.is_empty() {
            1
        } else {
            2049
        };
        let mut values = vec![0.0_f32; side as usize * side as usize];
        for delta in &doc.height_deltas {
            values[usize::from(delta.sample_z) * side as usize + usize::from(delta.sample_x)] =
                delta.delta_m;
        }
        Ok(Self {
            side,
            bytes: values.into_iter().flat_map(f32::to_le_bytes).collect(),
            elevation: doc.height_sampling.base_elevation_m,
        })
    }

    pub fn shader(&self, source: &str, group: &str, binding: u32) -> Result<String, String> {
        if source.matches("fn terrainHeightAt(").count() != 1 {
            return Err("expected one original terrain height evaluator".into());
        }
        let source = source.replacen("fn terrainHeightAt(", "fn forgeOriginalTerrainHeightAt(", 1);
        Ok(format!(
            "{source}\n@group({group}) @binding({binding}) var forgeTerrainDeltas:texture_2d<f32>;\nconst forgeBaseElevation:f32={:?};\n{FUNCTIONS}",
            self.elevation
        ))
    }
}

const FUNCTIONS: &str = r"
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
