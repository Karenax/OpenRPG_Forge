//! Dedicated procedural surface shader assembled only from a verified fish package.
//! The caller supplies `frame.view`, `frame.time` and the `fishSkin` storage binding.
use forge_assets::tidewater_fish_props::FishPropPackage;

/// The source records the historical Three.js specular read-order difference.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SpecularMode {
    /// Preserve the acquired source's intended dielectric highlights.
    Intended,
    /// Match the historical Three.js zero dielectric specular value.
    ThreeReference,
}

/// Build a fragment-stage library, retaining the full source surface body.
///
/// This does not install a material, lighting receiver or renderer. Its inputs
/// include original rest positions, original fish data and per-instance flags.
///
/// # Errors
/// Rejects missing or non-UTF8 verified payloads and ambiguous helper closures.
pub fn shader_library(package: &FishPropPackage, mode: SpecularMode) -> Result<String, String> {
    let common = text(package, "material/module-0-common.wgsl")?;
    let helpers = [
        "luminance",
        "interleavedGradientNoise",
        "perturbNormalByHeight",
    ]
    .map(|name| extract_function(common, name))
    .into_iter()
    .collect::<Result<Vec<_>, _>>()?
    .join("\n");
    let fish = text(package, "material/module-2-fish.wgsl")?;
    let body = text(package, "material/main-surface.wgsl")?;
    let specular = match mode {
        SpecularMode::Intended => "",
        SpecularMode::ThreeReference => "s.specularIntensity = 0.0;",
    };
    Ok(format!(
        "{helpers}\n{fish}\n\
struct FishSurfaceVaryings {{vFishLocal:vec3f,vFishData:vec4f,vFishInfo:vec4f,vFishFlags:vec4f}};\n\
struct FishSurfaceInput {{vs:FishSurfaceVaryings,pixel:vec2f,P:vec3f,N:vec3f,V:vec3f}};\n\
struct FishSurface {{albedo:vec3f,roughness:f32,metalness:f32,specularIntensity:f32,normal:vec3f,translucency:vec3f,clearcoat:f32,clearcoatRoughness:f32}};\n\
fn fishSurface(in:FishSurfaceInput)->FishSurface {{\nvar s:FishSurface;\n{body}\n{specular}\nreturn s;\n}}\n"
    ))
}

fn text<'a>(package: &'a FishPropPackage, name: &str) -> Result<&'a str, String> {
    let bytes = package
        .file(name)
        .ok_or_else(|| format!("Missing fish shader payload: {name}"))?;
    std::str::from_utf8(bytes).map_err(|error| error.to_string())
}

pub(super) fn extract_function<'a>(source: &'a str, name: &str) -> Result<&'a str, String> {
    let marker = format!("fn {name}(");
    if source.matches(&marker).count() != 1 {
        return Err(format!("Ambiguous fish shader helper: {name}"));
    }
    let start = source.find(&marker).ok_or("Missing shader helper")?;
    let opening = source[start..]
        .find('{')
        .map(|offset| start + offset)
        .ok_or("Shader helper body absent")?;
    let mut depth = 0;
    for (offset, byte) in source.as_bytes()[opening..].iter().enumerate() {
        match byte {
            b'{' => depth += 1,
            b'}' => {
                depth -= 1;
                if depth == 0 {
                    return Ok(&source[start..=opening + offset]);
                }
            }
            _ => {}
        }
    }
    Err(format!("Unclosed fish shader helper: {name}"))
}
