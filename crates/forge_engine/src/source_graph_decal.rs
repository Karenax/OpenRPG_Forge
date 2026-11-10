//! Derive a separate receiver from the three verified Witch House forward stages.
//! The original GLB, vertex stage, prepass and shadow stages are untouched.
use super::SourceGraphShaderArtifact;
use forge_core::bytes_sha256;

const WITCH_FORWARD_PINS: [&str; 3] = [
    "a7548cd4364c946c4b49ecce6745ca44c2d2e4d8c72be6a040c88c1db8a7c4a5",
    "315671d18fada6d6604d2af364d4c26089c0dab62b5adbdcad6987bc4c5cb437",
    "7ae26a34d4290daca1096279e201c9753aca41b79c66c367b2343532d5cbbefc",
];
const HOOK: &str = "var out:FragmentOutput;out.color=source_lighting::apply_pbr_lighting(pbr);";
const DECLARATIONS: &str = "#import forge_source_decal::composition as source_decal\n\
@group(#{MATERIAL_BIND_GROUP}) @binding(117) var source_decal_color: texture_2d<f32>;\n\
@group(#{MATERIAL_BIND_GROUP}) @binding(125) var source_decal_normal: texture_2d<f32>;\n";
const COMPOSE: &str = "let source_decal_pixel = vec2<i32>(input.position.xy);\n\
source_decal::apply(&pbr, textureLoad(source_decal_color, source_decal_pixel, 0),\n\
textureLoad(source_decal_normal, source_decal_pixel, 0));\n";

pub(super) fn forward_variant(
    forward: &SourceGraphShaderArtifact,
) -> Result<Option<SourceGraphShaderArtifact>, &'static str> {
    if !WITCH_FORWARD_PINS.contains(&forward.expected_sha256.to_string().as_str()) {
        return Ok(None);
    }
    if bytes_sha256(forward.source.as_bytes()) != forward.expected_sha256
        || forward.source.matches(HOOK).count() != 1
        || ["@binding(117)", "@binding(125)"]
            .into_iter()
            .any(|binding| forward.source.contains(binding))
    {
        return Err("Le programme Witch House ne correspond pas à son contrat récepteur");
    }
    let source = format!(
        "{DECLARATIONS}{}",
        forward.source.replace(HOOK, &format!("{COMPOSE}{HOOK}"))
    );
    Ok(Some(SourceGraphShaderArtifact {
        expected_sha256: bytes_sha256(source.as_bytes()),
        source,
    }))
}
