//! ID output from the existing governed wind shader, with original instance tags.
use crate::vegetation::VisualValleyVegetationMaterial as WindMaterial;
use bevy::{
    camera::{
        primitives::Aabb,
        visibility::{NoAutoAabb, NoFrustumCulling, RenderLayers, VisibilityRange},
    },
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::{render_resource::AsBindGroup, storage::ShaderBuffer},
    shader::ShaderRef,
};
use forge_core::EntityId;
use std::{collections::BTreeMap, sync::Arc};

const SHADER: Handle<Shader> = bevy::asset::uuid_handle!("8b17de8f-054e-4770-a34d-1896d1b9e341");
pub type PickingMaterial = ExtendedMaterial<WindMaterial, Extension>;

/// An actual visible primitive, borrowing the resident geometry and material.
pub struct Draw {
    pub(crate) entity: Entity,
    pub(crate) id: EntityId,
    pub(crate) pick_id: u32,
    pub(crate) mesh: Handle<Mesh>,
    pub(crate) material_id: AssetId<WindMaterial>,
    pub(crate) material: Arc<WindMaterial>,
    pub(crate) tag: u32,
    pub(crate) transform: Transform,
    pub(crate) bounds: Aabb,
    pub(crate) range: VisibilityRange,
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct Extension {
    /// CPU-only link: the ID pass follows the current visible wind uniforms.
    pub source: Handle<WindMaterial>,
    #[storage(250, read_only)]
    #[dependency]
    pub tags: Handle<ShaderBuffer>,
}

impl MaterialExtension for Extension {
    fn vertex_shader() -> ShaderRef {
        SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        SHADER.into()
    }
    fn alpha_mode() -> Option<AlphaMode> {
        Some(AlphaMode::Opaque)
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _pipeline: &bevy::pbr::MaterialExtensionPipeline,
        descriptor: &mut bevy::render::render_resource::RenderPipelineDescriptor,
        _layout: &bevy::mesh::MeshVertexBufferLayoutRef,
        _key: bevy::pbr::MaterialExtensionKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        // IDs must be opaque/depth-writing, while the original material flags
        // and alpha cutoff still execute the normal coverage rejection.
        descriptor
            .vertex
            .shader_defs
            .push("VERTEX_OUTPUT_INSTANCE_INDEX".into());
        if let Some(fragment) = &mut descriptor.fragment {
            fragment
                .shader_defs
                .push("VERTEX_OUTPUT_INSTANCE_INDEX".into());
            fragment.shader_defs.push("MAY_DISCARD".into());
        }
        Ok(())
    }
}

#[must_use]
pub fn shader_id() -> AssetId<Shader> {
    SHADER.id()
}

pub fn shader_source() -> Result<String, String> {
    let source =
        include_str!("../../shaders/visual_valley_vegetation_deform.wgsl").replace("\r\n", "\n");
    let tail = "    out.color = apply_pbr_lighting(pbr_input);\n    out.color = main_pass_post_lighting_processing(pbr_input, out.color);\n    return out;";
    if source.matches(tail).count() != 1
        || source.contains("@binding(250)")
        || !source.contains("mesh_functions::get_tag(")
    {
        return Err("Governed picking shader adapter drift".into());
    }
    // A separate compact record keeps both the original wind/temporal tag and
    // the canonical ID. LODs may have different tags for the same tree.
    let mut derived = source.replace("mesh_functions::get_tag(", "picking_original_tag(");
    derived = derived.replace(tail, "    if pbr_input.material.base_color.a <= 0.0 { discard; }\n    let index = mesh_functions::get_tag(in.instance_index);\n    if index == 0u || index >= arrayLength(&picking_tags) { discard; }\n    let id = picking_tags[index].y;\n    out.color = vec4(vec3(f32(id & 255u), f32((id >> 8u) & 255u), f32((id >> 16u) & 255u)) / 255.0, 1.0);\n    return out;");
    derived.push_str("\n@group(#{MATERIAL_BIND_GROUP}) @binding(250) var<storage, read> picking_tags: array<vec4<u32>>;\nfn picking_original_tag(instance_index: u32) -> u32 {\n    let index = mesh_functions::get_tag(instance_index);\n    if index >= arrayLength(&picking_tags) { return 0u; }\n    return picking_tags[index].x;\n}\n");
    Ok(derived)
}

pub fn install(app: &mut App) -> Result<(), String> {
    let source = shader_source()?;
    if !app.is_plugin_added::<MaterialPlugin<PickingMaterial>>() {
        app.add_plugins(MaterialPlugin::<PickingMaterial>::default());
        app.add_systems(
            PostUpdate,
            sync.in_set(SyncSet)
                .before(bevy::asset::AssetEventSystems)
                .after(crate::source_environment::host::EnvironmentHostSet)
                .after(crate::source_lighting::surfaces::live::SurfaceHostSet),
        );
        #[cfg(feature = "physics-rapier")]
        app.configure_sets(
            PostUpdate,
            SyncSet.after(crate::source_vegetation::scene::streamed::host::PlantFrameSet),
        );
    }
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Picking shader assets absent")?
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "forge-governed-plant-picking.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct SyncSet;

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy asset system parameters"
)]
fn sync(source: Option<Res<Assets<WindMaterial>>>, mut picking: ResMut<Assets<PickingMaterial>>) {
    let Some(source) = source else {
        return;
    };
    if !source.is_changed() {
        return;
    }
    for (_, material) in picking.iter_mut() {
        if let Some(source) = source.get(&material.extension.source) {
            material.base = source.clone();
        }
    }
}

pub(crate) fn stage(
    world: &mut World,
    draws: &[Draw],
    layer: usize,
) -> Result<Vec<Entity>, String> {
    if draws.is_empty() {
        return Ok(Vec::new());
    }
    if !world.contains_resource::<Assets<PickingMaterial>>()
        || !world.contains_resource::<Assets<ShaderBuffer>>()
    {
        return Err("Governed picking material plugin absent".into());
    }
    let mut materials = BTreeMap::new();
    let mut indices = BTreeMap::new();
    let mut records = vec![UVec4::ZERO];
    let mut tags = Vec::new();
    for draw in draws {
        if draw.pick_id == 0 || draw.pick_id > super::MAX_ID {
            return Err("Invalid governed picking identity".into());
        }
        materials
            .entry(draw.material_id)
            .or_insert_with(|| Arc::clone(&draw.material));
        let key = (draw.pick_id, draw.tag);
        let tag = *indices.entry(key).or_insert_with(|| {
            let index = u32::try_from(records.len()).expect("bounded model projection");
            records.push(UVec4::new(draw.tag, draw.pick_id, 0, 0));
            index
        });
        tags.push(tag);
    }
    let buffer = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(records));
    let materials = materials
        .into_iter()
        .map(|(id, source)| {
            let original = world
                .resource_mut::<Assets<WindMaterial>>()
                .get_strong_handle(id)
                .ok_or("Picking source material disappeared")?;
            let material = world
                .resource_mut::<Assets<PickingMaterial>>()
                .add(PickingMaterial {
                    base: (*source).clone(),
                    extension: Extension {
                        source: original,
                        tags: buffer.clone(),
                    },
                });
            Ok((id, material))
        })
        .collect::<Result<BTreeMap<_, _>, String>>()?;
    Ok(draws
        .iter()
        .zip(tags)
        .map(|(draw, tag)| {
            world
                .spawn((
                    Mesh3d(draw.mesh.clone()),
                    MeshMaterial3d(materials[&draw.material_id].clone()),
                    bevy::mesh::MeshTag(tag),
                    draw.transform,
                    draw.bounds,
                    draw.range.clone(),
                    NoAutoAabb,
                    Visibility::Visible,
                    NoFrustumCulling,
                    bevy::light::NotShadowCaster,
                    RenderLayers::layer(layer),
                ))
                .id()
        })
        .collect())
}
