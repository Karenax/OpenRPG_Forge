//! Depth-writing ID-zero materials for terrain, opaque village and authored
//! `StandardMaterial` surfaces. Transparent meshes need a separate alpha policy.
use bevy::{
    camera::visibility::{NoFrustumCulling, RenderLayers},
    light::NotShadowCaster,
    mesh::skinning::{SkinnedMesh, SkinnedMeshInverseBindposes},
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::render_resource::AsBindGroup,
    shader::ShaderRef,
};
use std::collections::HashMap;

const TERRAIN: Handle<Shader> = bevy::asset::uuid_handle!("e3741a34-a9ef-4b74-b804-908447e38b24");
const VILLAGE: Handle<Shader> = bevy::asset::uuid_handle!("d4eb392f-6c52-4a24-8fa0-456747da9b12");
const STANDARD: Handle<Shader> = bevy::asset::uuid_handle!("1f137e2c-0645-4edb-8f06-d1c74ca02a43");

#[must_use]
pub fn shader_ids() -> [AssetId<Shader>; 3] {
    [TERRAIN.id(), VILLAGE.id(), STANDARD.id()]
}
pub type TerrainOccluder =
    ExtendedMaterial<crate::tidewater_terrain::lod::LodMaterial, TerrainDepth>;
pub type VillageOccluder =
    ExtendedMaterial<crate::tidewater_material::VillageMaterial, VillageDepth>;
pub type StandardOccluder = ExtendedMaterial<StandardMaterial, StandardDepth>;

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

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, Default)]
pub struct StandardDepth {
    #[uniform(250)]
    pub reserved: Vec4,
}
impl MaterialExtension for StandardDepth {
    fn fragment_shader() -> ShaderRef {
        STANDARD.into()
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
    shaders
        .insert(
            STANDARD.id(),
            Shader::from_wgsl(
                "@fragment fn fragment()->@location(0) vec4f { return vec4f(0.0,0.0,0.0,1.0); }",
                "forge-standard-id-zero.wgsl",
            ),
        )
        .map_err(|e| e.to_string())?;
    if !app.is_plugin_added::<MaterialPlugin<TerrainOccluder>>() {
        app.add_plugins(MaterialPlugin::<TerrainOccluder>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<VillageOccluder>>() {
        app.add_plugins(MaterialPlugin::<VillageOccluder>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<StandardOccluder>>() {
        app.add_plugins(MaterialPlugin::<StandardOccluder>::default());
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

struct Draw<M: Material, K> {
    mesh: Handle<Mesh>,
    material: M,
    source: K,
    transform: Transform,
    global_transform: GlobalTransform,
    parent: Option<Entity>,
    skin: Option<SkinnedMesh>,
}

impl<M: Material, K> Draw<M, K> {
    fn stage(self, world: &mut World, material: Handle<M>, layer: usize) -> Entity {
        let mut entity = world.spawn((
            Mesh3d(self.mesh),
            MeshMaterial3d(material),
            self.transform,
            self.global_transform,
            Visibility::Visible,
            NoFrustumCulling,
            NotShadowCaster,
            RenderLayers::layer(layer),
        ));
        if let Some(parent) = self.parent {
            // Preserve the exact affine world pose through later propagation,
            // including rotated children of parents with nonuniform scale.
            entity.insert(ChildOf(parent));
        }
        if let Some(skin) = self.skin {
            // The same world owns the source joints. Reuse their real pose and
            // inverse bind matrices rather than drawing the mesh in bind pose.
            entity.insert(skin);
        }
        entity.id()
    }
}

fn skin_for_draw(
    world: &World,
    entity: Entity,
    mesh: &Mesh,
) -> Result<Option<SkinnedMesh>, String> {
    let skinned_layout = mesh.contains_attribute(Mesh::ATTRIBUTE_JOINT_INDEX)
        && mesh.contains_attribute(Mesh::ATTRIBUTE_JOINT_WEIGHT);
    let skin = world.get::<SkinnedMesh>(entity);
    if skinned_layout != skin.is_some() {
        return Err("Picking occluder skin does not match its mesh layout".into());
    }
    if let Some(skin) = skin {
        let poses = world
            .get_resource::<Assets<SkinnedMeshInverseBindposes>>()
            .and_then(|assets| assets.get(&skin.inverse_bindposes))
            .ok_or("Picking occluder inverse bind poses unavailable")?;
        if skin.joints.is_empty()
            || poses.len() < skin.joints.len()
            || skin.joints.iter().any(|&joint| {
                world
                    .get::<GlobalTransform>(joint)
                    .is_none_or(|pose| !pose.to_matrix().is_finite())
            })
        {
            return Err("Picking occluder joint palette unavailable".into());
        }
    }
    Ok(skin.cloned())
}

/// A validated snapshot of visible terrain, village and opaque authored meshes.
/// Transparent surfaces are deliberately click-through until a separate alpha
/// interaction policy exists. This captures the current ECS view, not the
/// canonical source revision; callers must also bind the authoring head/camera.
pub struct Prepared {
    terrain: Vec<Draw<TerrainOccluder, AssetId<crate::tidewater_terrain::lod::LodMaterial>>>,
    village: Vec<Draw<VillageOccluder, AssetId<crate::tidewater_material::VillageMaterial>>>,
    standard: Vec<Draw<StandardOccluder, AssetId<StandardMaterial>>>,
    pub transparent_skipped: usize,
    pub standard_skipped: usize,
}

impl Prepared {
    #[allow(
        clippy::too_many_lines,
        reason = "validates three material families in one ECS snapshot"
    )]
    pub fn from_world(world: &World) -> Result<Self, String> {
        let meshes = world
            .get_resource::<Assets<Mesh>>()
            .ok_or("Picking meshes unavailable")?;
        let terrain_materials = world
            .get_resource::<Assets<crate::tidewater_terrain::lod::LodMaterial>>()
            .ok_or("Picking terrain materials unavailable")?;
        let village_materials = world
            .get_resource::<Assets<crate::tidewater_material::VillageMaterial>>()
            .ok_or("Picking village materials unavailable")?;
        let standard_materials = world.get_resource::<Assets<StandardMaterial>>();
        let mut terrain = Vec::new();
        let mut village = Vec::new();
        let mut standard = Vec::new();
        let mut transparent_skipped = 0;
        let mut standard_skipped = 0;
        for entity in world.iter_entities() {
            if entity.get::<Visibility>() == Some(&Visibility::Hidden)
                || entity
                    .get::<InheritedVisibility>()
                    .is_some_and(|visibility| !visibility.get())
            {
                continue;
            }
            let Some(mesh) = entity.get::<Mesh3d>() else {
                continue;
            };
            let terrain_handle =
                entity.get::<MeshMaterial3d<crate::tidewater_terrain::lod::LodMaterial>>();
            let village_handle =
                entity.get::<MeshMaterial3d<crate::tidewater_material::VillageMaterial>>();
            let standard_handle = entity.get::<MeshMaterial3d<StandardMaterial>>();
            if terrain_handle.is_none() && village_handle.is_none() && standard_handle.is_none() {
                continue;
            }
            let source_mesh = meshes
                .get(&mesh.0)
                .ok_or("Picking occluder mesh disappeared")?;
            let parent = entity.get::<ChildOf>().map(ChildOf::parent);
            let transform = entity.get::<Transform>().copied().unwrap_or_default();
            let global_transform = entity
                .get::<GlobalTransform>()
                .copied()
                .ok_or("Picking occluder hierarchy is not propagated")?;
            if !transform.translation.is_finite()
                || !transform.rotation.is_finite()
                || !transform.scale.is_finite()
                || !global_transform.to_matrix().is_finite()
            {
                return Err("Invalid picking occluder transform".into());
            }
            if let Some(handle) = terrain_handle {
                let base = terrain_materials
                    .get(&handle.0)
                    .ok_or("Picking terrain material disappeared")?;
                terrain.push(Draw {
                    mesh: mesh.0.clone(),
                    material: TerrainOccluder {
                        base: base.clone(),
                        extension: TerrainDepth::default(),
                    },
                    source: handle.0.id(),
                    transform,
                    global_transform,
                    parent,
                    skin: skin_for_draw(world, entity.id(), source_mesh)?,
                });
            }
            if let Some(handle) = village_handle {
                let base = village_materials
                    .get(&handle.0)
                    .ok_or("Picking village material disappeared")?;
                if matches!(base.alpha_mode(), AlphaMode::Blend) {
                    transparent_skipped += 1;
                    continue;
                }
                village.push(Draw {
                    mesh: mesh.0.clone(),
                    material: village_material(base)?,
                    source: handle.0.id(),
                    transform,
                    global_transform,
                    parent,
                    skin: skin_for_draw(world, entity.id(), source_mesh)?,
                });
            }
            if let Some(handle) = standard_handle {
                let base = standard_materials
                    .ok_or("Picking standard materials unavailable")?
                    .get(&handle.0)
                    .ok_or("Picking standard material disappeared")?;
                if !matches!(base.alpha_mode(), AlphaMode::Opaque) {
                    standard_skipped += 1;
                    continue;
                }
                standard.push(Draw {
                    mesh: mesh.0.clone(),
                    material: StandardOccluder {
                        base: base.clone(),
                        extension: StandardDepth::default(),
                    },
                    source: handle.0.id(),
                    transform,
                    global_transform,
                    parent,
                    skin: skin_for_draw(world, entity.id(), source_mesh)?,
                });
            }
        }
        if terrain.is_empty() {
            return Err("Picking requires a visible terrain occluder".into());
        }
        Ok(Self {
            terrain,
            village,
            standard,
            transparent_skipped,
            standard_skipped,
        })
    }

    #[must_use]
    pub fn counts(&self) -> (usize, usize) {
        (self.terrain.len(), self.village.len())
    }

    #[must_use]
    pub fn standard_count(&self) -> usize {
        self.standard.len()
    }

    /// No validation remains after preparation; all entities are owned by the
    /// offscreen pass and retired with it.
    pub(super) fn stage(self, world: &mut World, layer: usize) -> Vec<Entity> {
        let mut entities =
            Vec::with_capacity(self.terrain.len() + self.village.len() + self.standard.len());
        let mut terrain_handles = HashMap::new();
        for draw in self.terrain {
            let handle = terrain_handles
                .entry(draw.source)
                .or_insert_with(|| {
                    world
                        .resource_mut::<Assets<TerrainOccluder>>()
                        .add(draw.material.clone())
                })
                .clone();
            entities.push(draw.stage(world, handle, layer));
        }
        let mut village_handles = HashMap::new();
        for draw in self.village {
            let handle = village_handles
                .entry(draw.source)
                .or_insert_with(|| {
                    world
                        .resource_mut::<Assets<VillageOccluder>>()
                        .add(draw.material.clone())
                })
                .clone();
            entities.push(draw.stage(world, handle, layer));
        }
        let mut standard_handles = HashMap::new();
        for draw in self.standard {
            let handle = standard_handles
                .entry(draw.source)
                .or_insert_with(|| {
                    world
                        .resource_mut::<Assets<StandardOccluder>>()
                        .add(draw.material.clone())
                })
                .clone();
            entities.push(draw.stage(world, handle, layer));
        }
        entities
    }
}

#[cfg(test)]
mod tests;
