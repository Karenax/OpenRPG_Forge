//! ID output from the actual world-lit plant shader and its live frame buffers.
use super::PickingExtension;
use crate::source_vegetation::world::WorldPlantMaterial;
use bevy::{
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::render_resource::AsBindGroup,
    shader::ShaderRef,
};

const SHADER: Handle<Shader> = bevy::asset::uuid_handle!("26935fd3-f895-454f-8407-d85053fce474");
pub type PickingMaterial = ExtendedMaterial<WorldPlantMaterial, Extension>;

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct Extension {
    #[uniform(42)]
    pub ids: UVec4,
}

impl MaterialExtension for Extension {
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

#[must_use]
pub fn shader_id() -> AssetId<Shader> {
    SHADER.id()
}

/// Keep the shared world vertex/surface evaluator, including wind, billboard
/// orientation, alpha rejection and overlapping LOD masks. Only its output changes.
pub fn shader_source(source: &str) -> Result<String, String> {
    let mut result = crate::source_vegetation::world::shader::derive(source)?;
    for (anchor, replacement, count) in [
        (
            "struct ProbeOut {",
            "struct ProbeOut {\n    @location(15) @interpolate(flat) pickId:u32,",
            4,
        ),
        (
            "    o.clip=position_world_to_clip(source.worldPos);",
            "    o.pickId=pickingIds.x+tag;\n    o.clip=position_world_to_clip(source.worldPos);",
            1,
        ),
        (
            "fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool)->@location(0) vec4f {",
            "fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool)->@location(0) vec4f {\n    let surface=forge_surface(vs,front);\n    let id=vs.pickId;\n    return vec4f(vec3f(f32(id & 255u),f32((id >> 8u) & 255u),f32((id >> 16u) & 255u))/255.0,1.0);\n}\nfn unusedWorldFragment(vs:ProbeOut,front:bool)->vec4f {",
            1,
        ),
    ] {
        if result.matches(anchor).count() != count {
            return Err(format!("World plant picking adapter drift: {anchor}"));
        }
        result = result.replace(anchor, replacement);
    }
    result
        .push_str("\n@group(#{MATERIAL_BIND_GROUP}) @binding(42) var<uniform> pickingIds:vec4u;\n");
    Ok(result)
}

pub fn install(app: &mut App, source: &str) -> Result<(), String> {
    let source = shader_source(source)?;
    if !app.is_plugin_added::<MaterialPlugin<PickingMaterial>>() {
        app.add_plugins(MaterialPlugin::<PickingMaterial>::default());
    }
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Shader assets unavailable")?
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "forge-world-plant-picking.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

pub fn material(
    base: &WorldPlantMaterial,
    first: u32,
    count: u32,
) -> Result<PickingMaterial, String> {
    Ok(PickingMaterial {
        base: base.clone(),
        extension: Extension {
            ids: PickingExtension::new(first, count)?.ids,
        },
    })
}
