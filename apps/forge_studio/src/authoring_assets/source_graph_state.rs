//! Apply authored node/renderer flags without replacing source LOD decisions.
#[cfg(target_arch = "wasm32")]
use bevy::ecs as bevy_ecs;
use bevy::{
    light::{NotShadowCaster, NotShadowReceiver},
    prelude::*,
};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Component, Clone, Debug)]
pub(super) struct OriginalRendererState {
    pub guid: String,
    pub file_id: String,
    pub enabled: bool,
    pub force_rendering_off: bool,
    pub shadow_casting_mode: u8,
    pub receive_shadows: bool,
}
#[derive(Component, Clone, Debug)]
pub(super) struct OriginalLodGroup(pub Value);
/// The host remains hidden until its caller admits the camera/LOD driver.
#[derive(Component)]
pub(super) struct OriginalLodDriverPending;

pub(super) struct StatePlan {
    pub active_nodes: BTreeMap<usize, bool>,
    pub renderers: BTreeMap<usize, OriginalRendererState>,
    pub lod_groups: BTreeMap<usize, Value>,
}
fn boolean(v: &Value, field: &str) -> Result<bool, String> {
    v.get(field)
        .and_then(Value::as_bool)
        .ok_or_else(|| format!("Missing authored bool: {field}"))
}
fn identity(v: &Value) -> Result<(String, String), String> {
    let guid = v["guid"].as_str().ok_or("Source GUID absent")?;
    let id = v["file_id"]
        .as_str()
        .ok_or("Source component identity absent")?;
    if guid.len() != 32 || !guid.bytes().all(|c| c.is_ascii_hexdigit()) || id.is_empty() {
        return Err("Invalid source component identity".into());
    }
    Ok((guid.into(), id.into()))
}

pub(super) fn prepare(
    doc: &Value,
    parents: &BTreeMap<usize, Option<usize>>,
    draws: &[usize],
) -> Result<Option<StatePlan>, String> {
    let nodes = doc["nodes"].as_array().ok_or("Scene nodes absent")?;
    if !nodes
        .iter()
        .any(|n| n["extras"].get("unity_source_node").is_some())
    {
        return Ok(None);
    }
    let mut sources = BTreeMap::new();
    let mut paths = BTreeMap::new();
    for (index, node) in nodes.iter().enumerate() {
        let source = node["extras"]
            .get("unity_source_node")
            .ok_or("Partial authored node state")?;
        let path = source["path"].as_str().ok_or("Original node path absent")?;
        if paths.insert(path, index).is_some() {
            return Err("Duplicate original node identity".into());
        }
        sources.insert(index, source);
    }
    let mut plan = StatePlan {
        active_nodes: BTreeMap::new(),
        renderers: BTreeMap::new(),
        lod_groups: BTreeMap::new(),
    };
    for (index, parent) in parents {
        let source = *sources
            .get(index)
            .ok_or("Selected node has no authored state")?;
        let declared_parent = source["parent_path"].as_str().filter(|p| !p.is_empty());
        let mapped_parent = declared_parent
            .map(|p| paths.get(p).copied().ok_or("Source parent absent"))
            .transpose()?;
        if mapped_parent != *parent {
            return Err("Source parent differs from admitted hierarchy".into());
        }
        let active = boolean(source, "active_self")?;
        let mut chain = active;
        let mut next = *parent;
        let mut seen = BTreeSet::from([*index]);
        while let Some(ancestor) = next {
            if !seen.insert(ancestor) {
                return Err("Source parent cycle".into());
            }
            chain &= boolean(sources[&ancestor], "active_self")?;
            next = *parents
                .get(&ancestor)
                .ok_or("Selected scene omitted an authored ancestor")?;
        }
        if boolean(source, "active_source_parent_chain")? != chain {
            return Err("Authored active parent chain differs".into());
        }
        plan.active_nodes.insert(*index, active);
        let renderers = source["renderers"]
            .as_array()
            .ok_or("Original renderer list absent")?;
        if renderers.len() > 1 {
            return Err("Multiple source renderers require an explicit primitive binding".into());
        }
        if let Some(renderer) = renderers.first() {
            let (guid, file_id) = identity(&renderer["component"])?;
            if !boolean(&renderer["component"], "exact_asset_identity_available")? {
                return Err("Source renderer identity unqualified".into());
            }
            let shadow = renderer["shadow_casting_mode"]
                .as_u64()
                .ok_or("Original shadow mode absent")?;
            if shadow > 1 {
                return Err(
                    "Two-sided/shadows-only source mode needs qualified pass routing".into(),
                );
            }
            plan.renderers.insert(
                *index,
                OriginalRendererState {
                    guid,
                    file_id,
                    enabled: boolean(renderer, "enabled")?,
                    force_rendering_off: boolean(renderer, "force_rendering_off")?,
                    shadow_casting_mode: shadow as u8,
                    receive_shadows: boolean(renderer, "receive_shadows")?,
                },
            );
        }
    }
    if draws
        .iter()
        .any(|index| !plan.renderers.contains_key(index))
    {
        return Err("Draw lacks an exact original renderer binding".into());
    }
    let groups = doc["extras"]["original_lod_groups"]
        .as_array()
        .ok_or("Original LOD declarations absent")?;
    for group in groups {
        let path = group["node_path"]
            .as_str()
            .ok_or("LOD source node absent")?;
        let index = *paths.get(path).ok_or("LOD group node absent")?;
        if !parents.contains_key(&index) {
            continue;
        }
        identity(&group["source_component_identity"])?;
        boolean(group, "enabled")?;
        let levels = group["levels"]
            .as_array()
            .ok_or("Original LOD levels absent")?;
        let mut previous = f32::INFINITY;
        for level in levels {
            let height = level["screen_relative_height"]
                .as_f64()
                .ok_or("LOD threshold absent")? as f32;
            if !height.is_finite() || !(0.0..=1.0).contains(&height) || height >= previous {
                return Err("Invalid authored LOD threshold order".into());
            }
            previous = height;
            let ids = level["renderer_components"]
                .as_array()
                .ok_or("LOD renderer identities absent")?;
            let declared_paths = level["renderer_node_paths"]
                .as_array()
                .ok_or("LOD renderer paths absent")?;
            if ids.len() != declared_paths.len() {
                return Err("LOD renderer paths/identities differ".into());
            }
            for (id, path) in ids.iter().zip(declared_paths) {
                let member = *paths
                    .get(path.as_str().ok_or("LOD renderer path invalid")?)
                    .ok_or("LOD renderer node absent")?;
                let renderer = plan
                    .renderers
                    .get(&member)
                    .ok_or("LOD renderer outside admitted scene")?;
                let (guid, file_id) = identity(id)?;
                if renderer.guid != guid || renderer.file_id != file_id {
                    return Err("LOD reference changed renderer identity".into());
                }
            }
        }
        if plan.lod_groups.insert(index, group.clone()).is_some() {
            return Err("Duplicate original LOD group".into());
        }
    }
    Ok(Some(plan))
}

impl StatePlan {
    /// Mount has already preflighted hierarchy/draw sets; its root remains hidden.
    pub fn apply(
        &self,
        world: &mut World,
        root: Entity,
        nodes: &BTreeMap<usize, Entity>,
        primitives: &[Entity],
    ) {
        for (index, active) in &self.active_nodes {
            world.entity_mut(nodes[index]).insert(if *active {
                Visibility::Inherited
            } else {
                Visibility::Hidden
            });
        }
        for (index, group) in &self.lod_groups {
            world
                .entity_mut(nodes[index])
                .insert(OriginalLodGroup(group.clone()));
        }
        if !self.lod_groups.is_empty() {
            world.entity_mut(root).insert(OriginalLodDriverPending);
        }
        let by_entity: BTreeMap<_, _> = nodes
            .iter()
            .map(|(index, entity)| (*entity, *index))
            .collect();
        for entity in primitives {
            let parent = world
                .get::<ChildOf>(*entity)
                .expect("Attached primitive parent")
                .parent();
            let original = &self.renderers[&by_entity[&parent]];
            let mut draw = world.entity_mut(*entity);
            draw.insert((
                original.clone(),
                if original.enabled && !original.force_rendering_off {
                    Visibility::Inherited
                } else {
                    Visibility::Hidden
                },
            ));
            if original.shadow_casting_mode == 0 {
                draw.insert(NotShadowCaster);
            }
            if !original.receive_shadows {
                draw.insert(NotShadowReceiver);
            }
        }
    }
}
