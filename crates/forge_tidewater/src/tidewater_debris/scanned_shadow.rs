//! Dedicated depth entries retaining the acquired scanned LOD coverage predicate.
use forge_tidewater_assets::tidewater_debris::scanned::ScannedPackage;

/// Build source depth coverage without making transitioning casters opaque.
/// Uniform layout: two matrices, iData vec4, frameIndex and three u32 pads.
/// The scene must submit only near-LOD prototypes with their matching asset ordinal.
///
/// # Errors
/// Refuses missing/non-UTF8 acquired modules or drifted source coverage seams.
pub fn shader_source(package: &ScannedPackage) -> Result<String, String> {
    let text = |name| {
        std::str::from_utf8(package.file(name).ok_or("Scanned depth module absent")?)
            .map_err(|e| e.to_string())
    };
    let lod = text(
        "material/module-dbf4d515e1bceccfca7bbb1e74bbf0e1f0f8abf3013c6850c72624bdd80d5887.wgsl",
    )?;
    let vertex = text("material/scanned-vertex.wgsl")?;
    let shadow = text("material/scanned-shadow.wgsl")?;
    if !lod.contains("let f = frame.frameIndex;")
        || !vertex.contains("o.vIData = v.iData;")
        || shadow.trim()
            != "return lodFadeVisible( in.pixel, in.vs.vIData.z, in.vs.vIData.w > 0.5 );"
    {
        return Err("Scanned source depth coverage differs".into());
    }
    Ok(format!(
        "struct Frame {{clip_from_world:mat4x4f,world_from_local:mat4x4f,iData:vec4f,frameIndex:u32,pad0:u32,pad1:u32,pad2:u32}};\n\
@group(0) @binding(0) var<uniform> frame:Frame;\n\
{lod}\n\
struct SourceVertex {{position:vec3f,aAsset:f32,iData:vec4f}};\n\
struct SourceOutput {{vIData:vec4f}};\n\
struct ShadowInput {{vs:SourceOutput,pixel:vec2f}};\n\
fn sourceVertex(input:SourceVertex)->SourceVertex {{var v=input;var o:SourceOutput;\n{vertex}\nreturn v;}}\n\
fn sourceShadow(in:ShadowInput)->bool {{\n{shadow}\n}}\n\
struct Output {{@builtin(position) position:vec4f,@location(0) data:vec4f}};\n\
@vertex fn vertex(@location(0) position:vec3f)->Output {{\n\
let v=sourceVertex(SourceVertex(position,frame.iData.x,frame.iData));\n\
var o:Output;o.position=frame.clip_from_world*frame.world_from_local*vec4f(v.position,1.0);o.data=v.iData;return o;\n}}\n\
@fragment fn fragment(in:Output) {{if(!sourceShadow(ShadowInput(SourceOutput(in.data),in.position.xy))){{discard;}}}}\n"
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    #[ignore = "Requires acquired package and a fresh shader output path"]
    fn export_exact_source_depth_entries() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let package = ScannedPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            |name, _| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
        )
        .unwrap();
        let shader = shader_source(&package).unwrap();
        if let Ok(path) = std::env::var("FORGE_DEBRIS_SCANNED_DEPTH_SHADER_OUTPUT") {
            use std::io::Write;
            std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(path)
                .unwrap()
                .write_all(shader.as_bytes())
                .unwrap();
        }
        assert!(shader.contains(
            std::str::from_utf8(package.file("material/scanned-shadow.wgsl").unwrap()).unwrap()
        ));
    }
}
