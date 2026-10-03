//! ID material preserving source deformation, alpha rejection and LOD dithering.
//! The caller must supply a linear RGBA8 target without MSAA or post processing,
//! matching camera/uniforms and depth-writing occluders. This is not a picker UI.
use bevy::{
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::render_resource::AsBindGroup,
    shader::ShaderRef,
};
pub mod occluders;
pub mod render;

const SHADER: Handle<Shader> = bevy::asset::uuid_handle!("c36df55a-5d16-4e91-8ff5-84b3ae79d450");

#[must_use]
pub fn shader_id() -> AssetId<Shader> {
    SHADER.id()
}
pub const MAX_ID: u32 = 0x00ff_ffff;
pub type PickingMaterial = ExtendedMaterial<super::PlantMaterial, PickingExtension>;

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct PickingExtension {
    /// First ID of the group; `MeshTag` adds the original record index. Zero is
    /// reserved for background and occluders. Validate the entire group range.
    #[uniform(42)]
    pub ids: UVec4,
}

impl PickingExtension {
    pub fn new(first: u32, count: u32) -> Result<Self, String> {
        if first == 0 || count == 0 || first > MAX_ID || count - 1 > MAX_ID - first {
            return Err("Invalid plant picking ID range".into());
        }
        Ok(Self {
            ids: UVec4::new(first, 0, 0, 0),
        })
    }
}

impl MaterialExtension for PickingExtension {
    fn vertex_shader() -> ShaderRef {
        SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

/// Decode a pixel from a linear RGBA8 ID attachment; zero has no plant identity.
#[must_use]
pub fn decode(pixel: [u8; 4]) -> Option<u32> {
    let id = u32::from(pixel[0]) | (u32::from(pixel[1]) << 8) | (u32::from(pixel[2]) << 16);
    (id != 0 && pixel[3] == 255).then_some(id)
}

/// Derive from the verified render source, rejecting adapter drift rather than
/// silently selecting an undeformed or opaque approximation of the plant.
pub fn shader_source(source: &str) -> Result<String, String> {
    if source.is_empty() || source.len() > 1_000_000 || source.contains("@binding(42)") {
        return Err("Invalid plant picking source".into());
    }
    // Acquired shaders combine generated CRLF adapters and LF source blocks.
    let mut result = source.replace("\r\n", "\n");
    for (anchor, replacement) in [
        (
            "struct ProbeOut {",
            "@group(#{MATERIAL_BIND_GROUP}) @binding(42) var<uniform> pickingIds:vec4u;\nstruct ProbeOut {\n    @location(15) @interpolate(flat) pickId:u32,",
        ),
        (
            "    o.clip=position_world_to_clip(source.worldPos);",
            "    o.pickId=pickingIds.x+tag;\n    o.clip=position_world_to_clip(source.worldPos);",
        ),
    ] {
        if result.matches(anchor).count() != 4 {
            return Err(format!("Plant picking adapter drift: {anchor}"));
        }
        result = result.replace(anchor, replacement);
    }
    let anchor = "    sourceSurface(input,&surface);";
    if result.matches(anchor).count() != 4 {
        return Err("Plant picking surface adapter drift".into());
    }
    let mut output = String::with_capacity(result.len());
    let mut rest = result.as_str();
    for _ in 0..4 {
        let (before, after) = rest.split_once(anchor).ok_or("Missing picking surface")?;
        let (_, next) = after
            .split_once("\n}\n\n#endif")
            .ok_or("Missing picking fragment end")?;
        output.push_str(before);
        output.push_str(anchor);
        output.push_str("\n    let id=vs.pickId;\n    return vec4f(vec3f(f32(id & 255u),f32((id >> 8u) & 255u),f32((id >> 16u) & 255u))/255.0,1.0);\n}\n\n#endif");
        rest = next;
    }
    output.push_str(rest);
    Ok(output)
}

pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let derived = super::motion::shader_source(source)?;
    let derived = crate::tidewater_object_shadow::receiver::shader_source(&derived)?;
    crate::tidewater_object_shadow::receiver::install(world)?;
    let derived = shader_source(&derived)?;
    let derived = super::with_source_derivative_directive(derived);
    super::motion::install(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Shader assets unavailable")?
        .insert(
            SHADER.id(),
            Shader::from_wgsl(derived, "tidewater-plant-picking.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}
