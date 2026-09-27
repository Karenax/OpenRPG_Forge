//! Depth-writing ID-zero materials for terrain and opaque village surfaces.
//! Transparent village materials require a separate interaction policy.
use bevy::{
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::render_resource::AsBindGroup,
    shader::ShaderRef,
};

const TERRAIN: Handle<Shader> = bevy::asset::uuid_handle!("e3741a34-a9ef-4b74-b804-908447e38b24");
const VILLAGE: Handle<Shader> = bevy::asset::uuid_handle!("d4eb392f-6c52-4a24-8fa0-456747da9b12");
pub type TerrainOccluder =
    ExtendedMaterial<crate::tidewater_terrain::lod::LodMaterial, TerrainDepth>;
pub type VillageOccluder =
    ExtendedMaterial<crate::tidewater_material::VillageMaterial, VillageDepth>;

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, Default)]
pub struct TerrainDepth {
    #[uniform(250)]
    pub reserved: Vec4,
}
impl MaterialExtension for TerrainDepth {
    fn fragment_shader() -> ShaderRef {
        TERRAIN.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, Default)]
pub struct VillageDepth {
    #[uniform(250)]
    pub reserved: Vec4,
}
impl MaterialExtension for VillageDepth {
    fn fragment_shader() -> ShaderRef {
        VILLAGE.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

/// Preserve all source alpha rejection before writing background ID zero.
pub fn village_shader(source: &str) -> Result<String, String> {
    if source.is_empty() || source.len() > 1_000_000 {
        return Err("Invalid village occluder shader".into());
    }
    let source = source.replace("\r\n", "\n");
    let anchor = "    if s.alpha < mat.alphaTest { discard; }";
    if source.matches(anchor).count() != 1 {
        return Err("Village alpha adapter drift".into());
    }
    let (prefix, rest) = source.split_once(anchor).ok_or("Missing village alpha")?;
    let (_, suffix) = rest
        .split_once("\n}\n")
        .ok_or("Missing village fragment end")?;
    Ok(format!(
        "{prefix}{anchor}\n    return vec4f(0.0,0.0,0.0,1.0);\n}}\n{suffix}"
    ))
}

pub fn install(app: &mut App, source: &str) -> Result<(), String> {
    let village = village_shader(source)?;
    let shaders = &mut app
        .world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Shader assets unavailable")?;
    shaders
        .insert(
            TERRAIN.id(),
            Shader::from_wgsl(
                "@fragment fn fragment()->@location(0) vec4f { return vec4f(0.0,0.0,0.0,1.0); }",
                "tidewater-terrain-id-zero.wgsl",
            ),
        )
        .map_err(|e| e.to_string())?;
    shaders
        .insert(
            VILLAGE.id(),
            Shader::from_wgsl(village, "tidewater-village-id-zero.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    if !app.is_plugin_added::<MaterialPlugin<TerrainOccluder>>() {
        app.add_plugins(MaterialPlugin::<TerrainOccluder>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<VillageOccluder>>() {
        app.add_plugins(MaterialPlugin::<VillageOccluder>::default());
    }
    Ok(())
}

pub fn village_material(
    base: &crate::tidewater_material::VillageMaterial,
) -> Result<VillageOccluder, String> {
    if !matches!(base.alpha_mode(), AlphaMode::Opaque) {
        return Err("Transparent village occlusion is not yet supported".into());
    }
    Ok(VillageOccluder {
        base: base.clone(),
        extension: VillageDepth::default(),
    })
}
