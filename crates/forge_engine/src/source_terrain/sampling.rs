//! Adapt the shared source height evaluator to an admitted sampled terrain.
use forge_project::TerrainDocument;

// These adapters receive source after another bounded shader transformation.
// Allow that transformation's inserted code without removing the size bound.
const MAX_PREPARED_SHADER_BYTES: usize = 2_000_000;

/// Identity of the actual sampled relief shared by geometry and hill shadows.
/// Paint and journal changes deliberately do not invalidate this heightfield.
pub(crate) fn relief_identity(
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
) -> Result<forge_core::ContentHash, String> {
    let document = plan.document();
    forge_core::canonical_json_sha256(&(
        plan.raster().source(),
        document.height_sampling.base_elevation_m,
        &document.height_deltas,
    ))
    .map_err(|error| error.to_string())
}

/// Replace the source height evaluator for a different sampled domain.
/// The caller must verify and bind the exact raster described by `document`.
/// The fixed reference profile keeps its original shader byte for byte.
pub fn prepare_shader(source: &str, document: &TerrainDocument) -> Result<String, String> {
    document.validate().map_err(|error| error.to_string())?;
    let raster = document
        .sampled_source
        .as_ref()
        .ok_or("Terrain height shader requires a sampled raster")?;
    if source.trim().is_empty()
        || source.len() > 1_000_000
        || source.matches("fn terrainHeightAt(").count() != 1
    {
        return Err("Expected one bounded source height evaluator".into());
    }
    if super::supports_fixed_source_raster(raster) {
        return Ok(source.to_owned());
    }
    if !source.contains("terrainHeightTex") {
        return Err("Terrain height texture binding is absent".into());
    }
    let spacing = document.height_sampling.sample_spacing_m;
    if !spacing.is_normal() {
        return Err("Terrain sample spacing exceeds shader precision".into());
    }
    let origin = [
        -document.extent_m.size_x_m * 0.5,
        -document.extent_m.size_z_m * 0.5,
    ];
    let start = source.find("fn terrainHeightAt(").expect("one evaluator");
    let open = source[start..]
        .find('{')
        .map(|index| start + index)
        .ok_or("Terrain height evaluator has no body")?;
    let signature: String = source[start..open]
        .chars()
        .filter(|character| !character.is_whitespace())
        .collect();
    if signature != "fnterrainHeightAt(xz:vec2f)->f32" {
        return Err("Unsupported terrain height evaluator signature".into());
    }
    let end = function_end(source.as_bytes(), open)?;
    let function = format!(
        r"fn terrainHeightAt(xz:vec2f)->f32 {{
    let f=(xz-vec2f({:?},{:?}))/{:?}-vec2f(0.5);
    if any(f<vec2f(0.0)) || any(f>=vec2f({}.0,{}.0)) {{ return {:?}; }}
    let i=vec2i(floor(f));
    let t=f-vec2f(i);
    let a=textureLoad(terrainHeightTex,i,0).x;
    let b=textureLoad(terrainHeightTex,i+vec2i(1,0),0).x;
    let c=textureLoad(terrainHeightTex,i+vec2i(0,1),0).x;
    let d=textureLoad(terrainHeightTex,i+vec2i(1,1),0).x;
    return mix(mix(a,b,t.x),mix(c,d,t.x),t.y);
}}",
        origin[0],
        origin[1],
        spacing,
        raster.width - 1,
        raster.height - 1,
        raster.outside_height_m
    );
    Ok(format!("{}{function}{}", &source[..start], &source[end..]))
}

/// Restrict morphed terrain vertices to the sampled document's rectangle.
/// A vertex whose unmorphed position reaches a boundary is pinned to that
/// boundary, even when lattice snapping would otherwise pull it inward.
/// Boundary patches remain square; clipped triangles can be degenerate.
/// This alone does not admit a terrain package or connect its renderer.
pub fn clip_vertex_shader(source: &str, document: &TerrainDocument) -> Result<String, String> {
    const ANCHOR: &str = "let P=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);";
    const REPLACEMENT: &str = "let forgeUnmorphedXZ=v.node.xy+v.position.xz*v.node.z;\n    let forgeClippedXZ=forgeTerrainClip(cv.worldXZ,forgeUnmorphedXZ);\n    let P=vec3f(forgeClippedXZ.x,terrainHeightAt(forgeClippedXZ),forgeClippedXZ.y);";
    clip_program(source, document, ANCHOR, REPLACEMENT)
}

/// Apply the same boundary to the shadow caster's instanced grid.
pub(crate) fn clip_shadow_shader(
    source: &str,
    document: &TerrainDocument,
) -> Result<String, String> {
    const ANCHOR: &str = "let world=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);";
    const REPLACEMENT: &str = "let forgeUnmorphedXZ=node.xy+grid*node.z;\n let forgeClippedXZ=forgeTerrainClip(cv.worldXZ,forgeUnmorphedXZ);\n let world=vec3f(forgeClippedXZ.x,terrainHeightAt(forgeClippedXZ),forgeClippedXZ.y);";
    clip_program(source, document, ANCHOR, REPLACEMENT)
}

fn clip_program(
    source: &str,
    document: &TerrainDocument,
    anchor: &str,
    replacement: &str,
) -> Result<String, String> {
    document.validate().map_err(|error| error.to_string())?;
    let raster = document
        .sampled_source
        .as_ref()
        .ok_or("Terrain boundary requires a sampled raster")?;
    if source.trim().is_empty() || source.len() > MAX_PREPARED_SHADER_BYTES {
        return Err("Invalid terrain vertex shader".into());
    }
    if super::supports_fixed_source_raster(raster) {
        return Ok(source.to_owned());
    }
    let half = [
        document.extent_m.size_x_m * 0.5,
        document.extent_m.size_z_m * 0.5,
    ];
    if half.iter().any(|value| !value.is_normal() || *value <= 0.0) {
        return Err("Terrain boundary exceeds shader precision".into());
    }
    if source.matches(anchor).count() != 1 || source.contains("forgeTerrainClip") {
        return Err("Unsupported terrain vertex position expression".into());
    }
    let helper = format!(
        r"fn forgeTerrainClip(morphed:vec2f,unmorphed:vec2f)->vec2f {{
    let low=vec2f({:?},{:?});
    let high=vec2f({:?},{:?});
    let bounded=clamp(morphed,low,high);
    return select(select(bounded,low,unmorphed<=low),high,unmorphed>=high);
}}",
        -half[0], -half[1], half[0], half[1]
    );
    Ok(format!(
        "{}\n{helper}\n",
        source.replacen(anchor, replacement, 1)
    ))
}

/// Adapt the source material's terrain UV mapping to the sampled rectangle.
/// Texture identity, dimensions and the scene's complete resource contract are
/// admitted separately; the reference profile keeps its source unchanged.
pub fn prepare_surface_shader(source: &str, document: &TerrainDocument) -> Result<String, String> {
    const MARKER: &str = "fn terrainUvOf(";
    document.validate().map_err(|error| error.to_string())?;
    let raster = document
        .sampled_source
        .as_ref()
        .ok_or("Terrain surface requires a sampled raster")?;
    if source.trim().is_empty() || source.len() > MAX_PREPARED_SHADER_BYTES {
        return Err("Invalid terrain surface shader".into());
    }
    if super::supports_fixed_source_raster(raster) {
        return Ok(source.to_owned());
    }
    let size = [document.extent_m.size_x_m, document.extent_m.size_z_m];
    let half = size.map(|axis| axis * 0.5);
    if half.iter().any(|axis| !axis.is_normal() || *axis <= 0.0) {
        return Err("Terrain surface domain exceeds shader precision".into());
    }
    if source.matches(MARKER).count() != 1 {
        return Err("Expected one terrain UV evaluator".into());
    }
    let start = source.find(MARKER).expect("one evaluator");
    let open = source[start..]
        .find('{')
        .map(|index| start + index)
        .ok_or("Terrain UV evaluator has no body")?;
    let signature: String = source[start..open]
        .chars()
        .filter(|c| !c.is_whitespace())
        .collect();
    if signature != "fnterrainUvOf(xz:vec2f)->vec2f" {
        return Err("Unsupported terrain UV evaluator signature".into());
    }
    let end = function_end(source.as_bytes(), open)?;
    let function = format!(
        "fn terrainUvOf(xz:vec2f)->vec2f {{ return (xz-vec2f({:?},{:?}))/vec2f({:?},{:?}); }}",
        -half[0], -half[1], size[0], size[1]
    );
    Ok(format!("{}{function}{}", &source[..start], &source[end..]))
}

/// Align the source hill-shadow lookup with the centred square that encloses
/// this terrain. The reference shader remains byte-identical for 2048 m.
pub fn prepare_hill_shadow_shader(
    source: &str,
    document: &TerrainDocument,
) -> Result<String, String> {
    const SEAM: &str = "((P.xz+1024.0)/2048.0)";
    document.validate().map_err(|error| error.to_string())?;
    document
        .sampled_source
        .as_ref()
        .ok_or("Hill shadow requires a sampled raster")?;
    if source.trim().is_empty() || source.len() > MAX_PREPARED_SHADER_BYTES {
        return Err("Invalid terrain hill-shadow shader".into());
    }
    let size = document.extent_m.size_x_m.max(document.extent_m.size_z_m);
    let texel = size / 512.0;
    if !size.is_normal() || size <= 0.0 || !texel.is_normal() || !(1.0 / texel).is_normal() {
        return Err("Terrain hill-shadow domain exceeds shader precision".into());
    }
    if size.to_bits() == 2048.0_f32.to_bits() {
        return Ok(source.to_owned());
    }
    if source.matches(SEAM).count() != 1 {
        return Err("Unsupported terrain hill-shadow coordinate seam".into());
    }
    Ok(source.replacen(
        SEAM,
        &format!("((P.xz-vec2f({:?}))/{size:?})", -size * 0.5),
        1,
    ))
}

/// Water's source shaders use terrain UVs for both rectangular terrain maps
/// and the square hill-shadow map. Bind the latter to the same enclosing
/// square as the shared terrain renderer while retaining the source reference.
pub fn prepare_water_hill_shadow_shader(
    source: &str,
    document: &TerrainDocument,
) -> Result<String, String> {
    const MARKER: &str = "fn terrainSunShadowAt(";
    const TERRAIN_UV: &str = "terrainUvOf( P.xz )";
    document.validate().map_err(|error| error.to_string())?;
    let raster = document
        .sampled_source
        .as_ref()
        .ok_or("Water hill shadow requires a sampled raster")?;
    if source.trim().is_empty() || source.len() > MAX_PREPARED_SHADER_BYTES {
        return Err("Invalid water hill-shadow shader".into());
    }
    if super::supports_fixed_source_raster(raster) {
        return Ok(source.to_owned());
    }
    let size = document.extent_m.size_x_m.max(document.extent_m.size_z_m);
    let texel = size / 512.0;
    if !size.is_normal() || !texel.is_normal() || !(1.0 / texel).is_normal() {
        return Err("Water hill-shadow domain exceeds shader precision".into());
    }
    if source.matches(MARKER).count() != 1 {
        return Err("Expected one water hill-shadow evaluator".into());
    }
    let start = source.find(MARKER).expect("one hill-shadow evaluator");
    let open = source[start..]
        .find('{')
        .map(|index| start + index)
        .ok_or("Water hill-shadow evaluator has no body")?;
    let signature: String = source[start..open]
        .chars()
        .filter(|character| !character.is_whitespace())
        .collect();
    if signature != "fnterrainSunShadowAt(P:vec3f)->f32" {
        return Err("Unsupported water hill-shadow evaluator signature".into());
    }
    let end = function_end(source.as_bytes(), open)?;
    let function = &source[start..end];
    if function.matches(TERRAIN_UV).count() != 1 {
        return Err("Unsupported water hill-shadow coordinates".into());
    }
    let square_uv = format!("((P.xz-vec2f({:?}))/{size:?})", -size * 0.5);
    let function = function.replacen(TERRAIN_UV, &square_uv, 1);
    Ok(format!("{}{function}{}", &source[..start], &source[end..]))
}

/// Locate a function's closing brace without counting braces in WGSL comments.
pub(crate) fn function_end(bytes: &[u8], open: usize) -> Result<usize, String> {
    let mut index = open;
    let mut depth = 0_u32;
    let mut block_comments = 0_u32;
    let mut line_comment = false;
    while index < bytes.len() {
        let current = bytes[index];
        let next = bytes.get(index + 1).copied();
        if line_comment {
            line_comment = current != b'\n';
        } else if block_comments > 0 {
            if current == b'/' && next == Some(b'*') {
                block_comments += 1;
                index += 1;
            } else if current == b'*' && next == Some(b'/') {
                block_comments -= 1;
                index += 1;
            }
        } else if current == b'/' && next == Some(b'/') {
            line_comment = true;
            index += 1;
        } else if current == b'/' && next == Some(b'*') {
            block_comments = 1;
            index += 1;
        } else if current == b'{' {
            depth += 1;
        } else if current == b'}' {
            depth -= 1;
            if depth == 0 {
                return Ok(index + 1);
            }
        }
        index += 1;
    }
    Err("Terrain height evaluator has an unclosed body or comment".into())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        authoring::new_terrain,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };

    fn document(width: u16, height: u16, spacing: f32, outside: f32) -> TerrainDocument {
        let mut doc = new_terrain("sampled", "authoring", "Terrain shader").unwrap();
        doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        doc.extent_m = TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
        doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        doc.height_sampling.sample_spacing_m = spacing;
        doc.height_sampling.base_elevation_m = 0.0;
        doc.height_sampling.amplitude_m = 0.0;
        doc.height_sampling.base_frequency_cycles_per_m = 1.0;
        doc.height_sampling.octaves = 1;
        doc.height_sampling.lacunarity = 2.0;
        doc.height_sampling.persistence = 0.5;
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
            outside_height_m: outside,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&[]),
        });
        doc
    }

    const SOURCE: &str = r"
@group(0) @binding(0) var terrainHeightTex:texture_2d<f32>;
fn terrainHeightAt(xz:vec2f)->f32 {
    // } does not end this function
    /* { nested comment /* } */ } */
    if xz.x > 0.0 { return 1.0; }
    return 0.0;
}
fn sentinel()->u32 { return 1u; }
";

    #[test]
    fn relief_identity_tracks_height_inputs_but_not_paint() {
        use forge_project::{TerrainHeightDelta, TerrainMaterialOverride, TerrainMaterialWeight};
        use forge_world::sampled_terrain::SampledTerrainPlan;

        let bytes = vec![0_u8; 11 * 19 * 4];
        let mut doc = document(11, 19, 0.5, -12.0);
        doc.sampled_source.as_mut().unwrap().samples_sha256 = forge_core::bytes_sha256(&bytes);
        let original = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
        let identity = relief_identity(&original).unwrap();

        let mut painted = doc.clone();
        painted.material_overrides.push(TerrainMaterialOverride {
            sample_x: 2,
            sample_z: 3,
            weights: vec![TerrainMaterialWeight {
                layer_id: painted.material_layers[0].id.clone(),
                weight: u16::MAX,
            }],
        });
        let painted = original.with_document(&painted).unwrap();
        assert_ne!(painted.document_hash(), original.document_hash());
        assert_eq!(relief_identity(&painted).unwrap(), identity);

        let mut elevated = doc.clone();
        elevated.height_sampling.base_elevation_m = 1.0;
        assert_ne!(
            relief_identity(&original.with_document(&elevated).unwrap()).unwrap(),
            identity
        );
        let mut edited = doc.clone();
        edited
            .height_deltas
            .push(TerrainHeightDelta::new(2, 3, 1.0));
        assert_ne!(
            relief_identity(&original.with_document(&edited).unwrap()).unwrap(),
            identity
        );

        let mut different_bytes = bytes.clone();
        different_bytes[0..4].copy_from_slice(&1.0_f32.to_le_bytes());
        doc.sampled_source.as_mut().unwrap().samples_sha256 =
            forge_core::bytes_sha256(&different_bytes);
        let different = SampledTerrainPlan::from_document(&doc, &different_bytes).unwrap();
        assert_ne!(relief_identity(&different).unwrap(), identity);
    }

    #[test]
    fn rectangular_source_shader_uses_its_admitted_domain() {
        let shader = prepare_shader(SOURCE, &document(11, 19, 0.5, -12.0)).unwrap();
        assert!(shader.contains("vec2f(-2.75,-4.75))/0.5"));
        assert!(shader.contains("f>=vec2f(10.0,18.0)"));
        assert!(shader.contains("return -12.0;"));
        assert!(shader.contains("fn sentinel()->u32"));
        assert!(!shader.contains("nested comment"));
        let module = naga::front::wgsl::parse_str(&shader).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
    }

    #[test]
    fn fixed_reference_is_identical_and_invalid_inputs_fail_closed() {
        assert_eq!(
            prepare_shader(SOURCE, &document(2048, 2048, 1.0, -90.0)).unwrap(),
            SOURCE
        );
        let doc = document(11, 19, 0.5, -12.0);
        assert!(prepare_shader("", &doc).is_err());
        let unclosed = "@group(0) @binding(0) var terrainHeightTex:texture_2d<f32>;\nfn terrainHeightAt(xz:vec2f)->f32 {";
        assert_eq!(
            prepare_shader(unclosed, &doc).unwrap_err(),
            "Terrain height evaluator has an unclosed body or comment"
        );
        let bad_signature = "@group(0) @binding(0) var terrainHeightTex:texture_2d<f32>;\nfn terrainHeightAt(x:vec2f)->f32 { return x.x; }";
        assert_eq!(
            prepare_shader(bad_signature, &doc).unwrap_err(),
            "Unsupported terrain height evaluator signature"
        );
        assert!(prepare_shader(&format!("{SOURCE}\n{SOURCE}"), &doc).is_err());
        let tiny = document(8, 8, f32::MIN_POSITIVE * 0.5, 0.0);
        tiny.validate().unwrap();
        assert_eq!(
            prepare_shader(SOURCE, &tiny).unwrap_err(),
            "Terrain sample spacing exceeds shader precision"
        );
    }

    const VERTEX_SOURCE: &str = r"
struct LodInput { position:vec3f, node:vec4f };
struct LodVertex { worldXZ:vec2f };
fn terrainHeightAt(xz:vec2f)->f32 { return xz.x; }
fn vertex(v:LodInput,cv:LodVertex)->vec3f {
    let P=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);
    return P;
}
";

    #[test]
    fn clipped_vertex_shader_uses_domain_and_rejects_unknown_expressions() {
        let doc = document(11, 19, 0.5, -12.0);
        let shader = clip_vertex_shader(VERTEX_SOURCE, &doc).unwrap();
        assert!(shader.contains("let low=vec2f(-2.75,-4.75);"));
        assert!(shader.contains("let high=vec2f(2.75,4.75);"));
        assert!(shader.contains("terrainHeightAt(forgeClippedXZ)"));
        assert!(shader.contains("unmorphed>=high"));
        let module = naga::front::wgsl::parse_str(&shader).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        assert_eq!(
            clip_vertex_shader(VERTEX_SOURCE, &document(2048, 2048, 1.0, -90.0)).unwrap(),
            VERTEX_SOURCE
        );
        assert!(clip_vertex_shader("", &doc).is_err());
        assert!(clip_vertex_shader("fn unrelated() {}", &doc).is_err());
        assert!(clip_vertex_shader(&format!("{VERTEX_SOURCE}\n{VERTEX_SOURCE}"), &doc).is_err());
        assert!(clip_vertex_shader(&shader, &doc).is_err());
        let prepared = format!("//{}\n{VERTEX_SOURCE}", "x".repeat(1_000_001));
        assert!(clip_vertex_shader(&prepared, &doc).is_ok());
        let oversized = format!(
            "//{}\n{VERTEX_SOURCE}",
            "x".repeat(MAX_PREPARED_SHADER_BYTES)
        );
        assert!(clip_vertex_shader(&oversized, &doc).is_err());
        let tiny = document(8, 8, f32::MIN_POSITIVE * 0.5, 0.0);
        // Its boundary is normal even though its sample spacing is not; the
        // height adapter separately rejects that raster before installation.
        assert!(clip_vertex_shader(VERTEX_SOURCE, &tiny).is_ok());
    }

    #[test]
    fn surface_uv_shader_uses_both_axes_and_preserves_reference() {
        let source = r"
fn before()->u32 { return 1u; }
fn terrainUvOf( xz: vec2f ) -> vec2f {
    // A brace in a comment } must not end this function.
    return xz / vec2f(2048.0);
}
fn after()->u32 { return 2u; }
";
        let doc = document(11, 19, 0.5, -12.0);
        let adapted = prepare_surface_shader(source, &doc).unwrap();
        assert!(adapted.contains("(xz-vec2f(-2.75,-4.75))/vec2f(5.5,9.5)"));
        assert!(adapted.contains("fn before()->u32 { return 1u; }"));
        assert!(adapted.contains("fn after()->u32 { return 2u; }"));
        let module = naga::front::wgsl::parse_str(&adapted).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        assert_eq!(
            prepare_surface_shader(source, &document(2048, 2048, 1.0, -90.0)).unwrap(),
            source
        );
        assert!(prepare_surface_shader("", &doc).is_err());
        assert!(prepare_surface_shader("fn missing() {}", &doc).is_err());
        assert!(prepare_surface_shader(&format!("{source}\n{source}"), &doc).is_err());
        assert!(
            prepare_surface_shader("fn terrainUvOf(q:vec2f)->vec2f {return q;}", &doc).is_err()
        );
        assert!(prepare_surface_shader("fn terrainUvOf(xz:vec2f)->vec2f {", &doc).is_err());
        let prepared = format!("//{}\n{source}", "x".repeat(1_000_001));
        assert!(prepare_surface_shader(&prepared, &doc).is_ok());
        let tiny = document(2, 2, f32::from_bits(1), 0.0);
        tiny.validate().unwrap();
        assert_eq!(
            prepare_surface_shader(source, &tiny).unwrap_err(),
            "Terrain surface domain exceeds shader precision"
        );
    }

    #[test]
    fn hill_shadow_lookup_uses_the_same_enclosing_square_as_the_heightfield() {
        let source = include_str!("engine_surface.wgsl");
        let doc = document(11, 19, 0.5, -12.0);
        let adapted = prepare_hill_shadow_shader(source, &doc).unwrap();
        assert!(adapted.contains("((P.xz-vec2f(-4.75))/9.5)"));
        assert_eq!(
            source.split_once("((P.xz+1024.0)/2048.0)").unwrap().0,
            adapted.split_once("((P.xz-vec2f(-4.75))/9.5)").unwrap().0
        );
        assert_eq!(
            source.split_once("((P.xz+1024.0)/2048.0)").unwrap().1,
            adapted.split_once("((P.xz-vec2f(-4.75))/9.5)").unwrap().1
        );
        assert_eq!(
            prepare_hill_shadow_shader(source, &document(2048, 2048, 1.0, -90.0)).unwrap(),
            source
        );
        assert!(prepare_hill_shadow_shader("fn missing() {}", &doc).is_err());
        assert!(prepare_hill_shadow_shader(&format!("{source}\n{source}"), &doc).is_err());

        let function = adapted
            .split_once("fn terrainSunShadowAt(")
            .unwrap()
            .1
            .split_once("struct ObjectShadowSettings")
            .unwrap()
            .0;
        let wgsl = format!(
            "@group(0) @binding(0) var terrainSunShadowTex:texture_2d<f32>;\nfn terrainSunShadowAt({function}"
        );
        let module = naga::front::wgsl::parse_str(&wgsl).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
    }

    #[test]
    fn water_keeps_rectangular_terrain_uv_and_square_hill_shadow_uv() {
        let doc = document(64, 32, 64.0, -12.0);
        let reference = document(2048, 2048, 1.0, -90.0);
        for source in [
            crate::source_breaker_gpu::COMMON_CREST_PROGRAM,
            crate::source_breaker_gpu::COMMON_SPRAY_PROGRAM,
        ] {
            assert_eq!(
                prepare_water_hill_shadow_shader(source, &reference).unwrap(),
                source
            );
            assert_eq!(
                crate::source_shore_field::Domain::REFERENCE
                    .prepare_water_shader(source, &reference)
                    .unwrap(),
                source
            );
            let domain = crate::source_shore_field::Domain::for_terrain(&doc, 3.5).unwrap();
            let water = domain.prepare_water_shader(source, &doc).unwrap();
            assert!(water.contains("vec2f(-2048.0,-1024.0))/vec2f(4096.0,2048.0)"));
            assert!(water.contains("((P.xz-vec2f(-2048.0))/4096.0)"));
            assert!(water.contains("let res = 1024.0;"));
            assert!(
                crate::source_shore_field::Domain::REFERENCE
                    .prepare_water_shader(source, &doc)
                    .is_err()
            );
            let start = water.find("fn terrainSunShadowAt(").unwrap();
            let open = start + water[start..].find('{').unwrap();
            let end = function_end(water.as_bytes(), open).unwrap();
            let wgsl = format!(
                "struct TerrainParams {{ sunBaked:f32, }};\n\
                 @group(0) @binding(0) var<uniform> terrainParams:TerrainParams;\n\
                 @group(0) @binding(1) var terrainSunShadowTex:texture_2d<f32>;\n{}",
                &water[start..end]
            );
            let module = naga::front::wgsl::parse_str(&wgsl).unwrap();
            naga::valid::Validator::new(
                naga::valid::ValidationFlags::all(),
                naga::valid::Capabilities::all(),
            )
            .validate(&module)
            .unwrap();
            assert!(
                prepare_water_hill_shadow_shader(&format!("{source}\n{source}"), &doc).is_err()
            );
        }
        assert!(prepare_water_hill_shadow_shader("fn missing() {}", &doc).is_err());
    }
}
