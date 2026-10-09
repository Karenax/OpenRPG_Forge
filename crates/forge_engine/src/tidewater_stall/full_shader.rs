//! Full source surface and world lighting, shared by Runtime and Studio.
//!
//! Assembly does not install a GPU pipeline. Callers must provide the actual
//! scene receivers and a matching material bind group before staging a shader.
//! There are deliberately no implicit neutral receiver implementations here.

/// Source-derived shader verified against the frozen Tidewater material on GPU.
pub const LIBRARY: &str = include_str!("full_library.wgsl");

/// Source opaque contact shadow hook, with explicit depth/frame bindings 56/57.
/// Requires the scene's `interleavedGradientNoise` helper. The depth must be the
/// previous opaque image for this view, sampled before its current opaque copy.
pub const CONTACT_SHADOW_RECEIVER: &str = include_str!("contact_shadow.wgsl");

pub const RENDER_CONTACT_DEFINE: &str = "FORGE_RENDER_CONTACT_FRAME";

/// Keep the standalone uniform receiver and offer a render-owned storage frame.
/// The contact algorithm is unchanged; the live key light comes from the same
/// scene receiver as the full BRDF, including GPU-owned atmosphere generations.
pub fn live_contact_receiver() -> Result<String, String> {
    const DECL: &str =
        "@group(#{MATERIAL_BIND_GROUP}) @binding(57) var<uniform> contactFrame:ContactShadowFrame;";
    let source = CONTACT_SHADOW_RECEIVER;
    if source.matches(DECL).count() != 1
        || source.matches("let L = contactFrame.sunDir;").count() != 1
        || source
            .matches("dot( contactFrame.sunColor, contactFrame.sunColor )")
            .count()
            != 1
    {
        return Err("Render contact receiver source seam changed".into());
    }
    let mut result = source.replace("contactFrame.", "currentContactFrame().");
    result = result.replace(DECL, &format!(r"
#ifdef {RENDER_CONTACT_DEFINE}
struct RenderContactFrame {{ contact:ContactShadowFrame, surfaceParameters:vec4f }};
@group(#{{MATERIAL_BIND_GROUP}}) @binding(58) var<storage,read> renderContactFrame:RenderContactFrame;
fn currentContactFrame()->ContactShadowFrame {{
 var frame=renderContactFrame.contact;
 frame.sunDir=sceneLighting.sunDirection.xyz;
 frame.sunColor=sceneLighting.sunColor.xyz;
 return frame;
}}
#else
{DECL}
fn currentContactFrame()->ContactShadowFrame {{ return contactFrame; }}
#endif
"));
    Ok(result)
}

/// Scene adapter with surface animation read from the same render-owned frame.
pub fn bevy_live_shader_source(resources: &str, hooks: &SceneHooks<'_>) -> Result<String, String> {
    const DECL: &str =
        "@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<uniform> stallParameters:vec4f;";
    let adapter = include_str!("full_adapter.wgsl");
    if adapter.matches(DECL).count() != 1 {
        return Err("Render contact surface parameter seam changed".into());
    }
    let adapter = adapter.replace(
        DECL,
        &format!(
            r"
#ifndef {RENDER_CONTACT_DEFINE}
{DECL}
#endif
fn stallFrameParameters()->vec4f {{
#ifdef {RENDER_CONTACT_DEFINE}
 return renderContactFrame.surfaceParameters;
#else
 return stallParameters;
#endif
}}
"
        ),
    );
    let adapter = adapter
        .replace("stallParameters.x", "stallFrameParameters().x")
        .replace("stallParameters.y", "stallFrameParameters().y")
        .replace("stallParameters.z", "stallFrameParameters().z")
        .replace("stallParameters.w", "stallFrameParameters().w");
    shader_source(resources, hooks, &adapter)
}

/// Assemble using the shared Bevy mesh/camera adapter and source animation data.
///
/// # Errors
/// Same mandatory receiver checks as `shader_source`. The caller's resources
/// must also declare the scene-owned `sceneLighting` interface.
pub fn bevy_shader_source(resources: &str, hooks: &SceneHooks<'_>) -> Result<String, String> {
    shader_source(resources, hooks, include_str!("full_adapter.wgsl"))
}

/// Each scene hook must be provided once. Resources belong to the scene adapter,
/// rather than to a particular map or editor object identity.
pub struct SceneHooks<'a> {
    pub direct_modulation: &'a str,
    pub shadow_position: &'a str,
    pub sun_shadow: &'a str,
    pub contact_shadow: &'a str,
    pub local_lights: &'a str,
    pub environment_diffuse: &'a str,
    pub environment_specular: &'a str,
    pub bounce: &'a str,
    pub ambient_modulation: &'a str,
}

impl SceneHooks<'_> {
    fn sources(&self) -> [(&str, &str); 9] {
        [
            ("hookDirectModulation", self.direct_modulation),
            ("hookShadowPosition", self.shadow_position),
            ("sunShadow", self.sun_shadow),
            ("hookContactShadow", self.contact_shadow),
            ("hookLocalLights", self.local_lights),
            ("hookEnvDiffuse", self.environment_diffuse),
            ("hookEnvSpecular", self.environment_specular),
            ("hookBounce", self.bounce),
            ("hookAmbientModulation", self.ambient_modulation),
        ]
    }
}

/// Assemble the complete material while preserving the source surface and BRDF.
///
/// `resources` provides shared declarations/helpers; `entry_points` provides the
/// renderer's mesh/camera adapter. The latter calls `stallSurface` and
/// `shadeSurface` with the scene's current frame. Binding validation and WGSL
/// compilation are still required by the caller before GPU publication.
///
/// # Errors
/// Refuses absent, oversized or duplicate receiver definitions. Missing hooks
/// never silently reduce clearcoat, transmission, environment or shadows.
pub fn shader_source(
    resources: &str,
    hooks: &SceneHooks<'_>,
    entry_points: &str,
) -> Result<String, String> {
    let sources = hooks.sources();
    let size = LIBRARY.len()
        + resources.len()
        + entry_points.len()
        + sources
            .iter()
            .map(|(_, source)| source.len())
            .sum::<usize>();
    if size > 1_000_000 || entry_points.trim().is_empty() {
        return Err("Invalid full stall shader size or absent renderer adapter".into());
    }
    for (name, source) in &sources {
        let marker = format!("fn {name}(");
        if source.matches(&marker).count() != 1 {
            return Err(format!(
                "Full stall shader requires exactly one {name} receiver"
            ));
        }
    }
    let mut result = String::with_capacity(size + 128);
    // Source Shader.js uses this policy for derivatives in material branches.
    result.push_str("diagnostic(off, derivative_uniformity);\n");
    for source in [LIBRARY, resources]
        .into_iter()
        .chain(sources.iter().map(|(_, source)| *source))
        .chain([entry_points])
    {
        result.push_str(source);
        result.push('\n');
    }
    for (name, _) in &sources {
        if result.matches(&format!("fn {name}(")).count() != 1 {
            return Err(format!("Duplicate full stall shader receiver {name}"));
        }
    }
    for name in ["stallSurface", "shadeSurface", "defaultSurface", "BRDF_GGX"] {
        if result.matches(&format!("fn {name}(")).count() != 1 {
            return Err(format!(
                "Full stall material implementation collision: {name}"
            ));
        }
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn render_receiver_keeps_source_algorithm_and_both_frame_paths() {
        let receiver = live_contact_receiver().unwrap();
        assert!(receiver.contains("@binding(57) var<uniform> contactFrame:"));
        assert!(receiver.contains("@binding(58) var<storage,read> renderContactFrame:"));
        assert!(receiver.contains("frame.sunDir=sceneLighting.sunDirection.xyz;"));
        assert!(receiver.contains("frame.sunColor=sceneLighting.sunColor.xyz;"));
        let source_body = CONTACT_SHADOW_RECEIVER
            .split_once("fn hookContactShadow(")
            .unwrap()
            .1;
        let receiver_body = receiver.split_once("fn hookContactShadow(").unwrap().1;
        assert_eq!(
            receiver_body,
            source_body.replace("contactFrame.", "currentContactFrame().")
        );
    }

    // Text-only interface tests. These deliberately do not claim GPU drawing.
    fn hooks() -> SceneHooks<'static> {
        SceneHooks {
            direct_modulation: "fn hookDirectModulation() {}",
            shadow_position: "fn hookShadowPosition() {}",
            sun_shadow: "fn sunShadow() {}",
            contact_shadow: "fn hookContactShadow() {}",
            local_lights: "fn hookLocalLights() {}",
            environment_diffuse: "fn hookEnvDiffuse() {}",
            environment_specular: "fn hookEnvSpecular() {}",
            bounce: "fn hookBounce() {}",
            ambient_modulation: "fn hookAmbientModulation() {}",
        }
    }

    #[test]
    fn refuses_missing_contact_or_duplicate_world_receivers() {
        let mut scene = hooks();
        scene.contact_shadow = "";
        assert!(
            shader_source("", &scene, "adapter")
                .unwrap_err()
                .contains("hookContactShadow")
        );
        scene = hooks();
        assert!(shader_source("fn sunShadow() {}", &scene, "adapter").is_err());
        assert!(shader_source("fn shadeSurface() {}", &scene, "adapter").is_err());
        assert!(shader_source("", &scene, "").is_err());
    }

    #[test]
    fn assembled_library_keeps_every_source_response_unchanged() {
        let output = shader_source("scene declarations", &hooks(), "renderer adapter").unwrap();
        assert!(output.contains(LIBRARY));
        for marker in [
            "ccSpec",
            "ccRad",
            "s.translucency * lightColor",
            "hookBounce( P, N ) * diffuseColor",
            "acc.indirectSpecular *= specAO",
        ] {
            assert!(output.contains(marker), "Missing source response {marker}");
        }
    }

    #[test]
    #[ignore = "Requires explicit isolated GPU fixture input/output directory"]
    fn export_assembled_gpu_fixture() {
        let root =
            std::path::PathBuf::from(std::env::var("FORGE_STALL_FULL_SHADER_GPU_FIXTURE").unwrap());
        let names = hooks().sources().map(|(name, _)| name.to_owned());
        let inputs = names
            .each_ref()
            .map(|name| std::fs::read_to_string(root.join(format!("{name}.wgsl"))).unwrap());
        let scene = SceneHooks {
            direct_modulation: &inputs[0],
            shadow_position: &inputs[1],
            sun_shadow: &inputs[2],
            contact_shadow: &inputs[3],
            local_lights: &inputs[4],
            environment_diffuse: &inputs[5],
            environment_specular: &inputs[6],
            bounce: &inputs[7],
            ambient_modulation: &inputs[8],
        };
        let resources = std::fs::read_to_string(root.join("resources.wgsl")).unwrap();
        let adapter = std::fs::read_to_string(root.join("adapter.wgsl")).unwrap();
        let shader = shader_source(&resources, &scene, &adapter).unwrap();
        use std::io::Write;
        let mut file = std::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(root.join("production-assembled.wgsl"))
            .unwrap();
        file.write_all(shader.as_bytes()).unwrap();
    }
}
