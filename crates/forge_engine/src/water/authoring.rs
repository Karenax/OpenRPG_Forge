//! Prepared replacement for editor water. No serialized Bevy state and no implicit terrain edit.
use super::{
    PreparedWaterScene, VisualValleyWaterMaterial, WaterRuntimeState, advance_runtime_state,
    mesh_from_geometry, prepare_water_surface,
};
use bevy::{material::OpaqueRendererMethod, prelude::*};
use forge_core::{ContentHash, EntityId};
use forge_project::TerrainDocument;
use forge_project::zone_authoring::ZoneAuthoringHead;

mod readiness;
#[cfg(test)]
mod tests;
pub use readiness::ready;

/// Install the existing water shaders/materials without spawning a proof-scene river.
pub struct AuthoringWaterPlugin;

impl Plugin for AuthoringWaterPlugin {
    fn build(&self, app: &mut App) {
        super::register_water_assets(app);
        app.init_resource::<AuthoringWaterState>()
            .add_systems(Update, animate);
    }
}

pub struct PreparedAuthoringWater {
    source_hash: ContentHash,
    terrain_hash: ContentHash,
    terrain: TerrainDocument,
    bodies: Vec<(EntityId, PreparedWaterScene, WaterRuntimeState)>,
}

impl PreparedAuthoringWater {
    /// Derive terrain and every water mesh before touching any live asset storage.
    pub fn prepare(head: &ZoneAuthoringHead) -> Result<Self, String> {
        let projected = forge_world::river_projection::project(head)?;
        Self::from_projection(&projected)
    }

    /// Reuse an already derived scene; mesh preparation still validates the water/terrain pair.
    pub fn from_projection(
        projected: &forge_world::river_projection::RiverProjection,
    ) -> Result<Self, String> {
        let bodies = projected
            .waters
            .iter()
            .map(|(id, water)| {
                let mut prepared =
                    prepare_water_surface(&projected.terrain, water).map_err(|e| e.to_string())?;
                // Initial editor adapter is explicitly opaque forward. Screen-space optics
                // and their camera prepasses must be qualified before opt-in, not assumed.
                prepared.water_material.base.opaque_render_method = OpaqueRendererMethod::Forward;
                // Banks share the editor camera. Leaving the proof-scene Deferred
                // material here makes Bevy queue a missing deferred prepass phase.
                prepared.river_bank_material.base.opaque_render_method =
                    OpaqueRendererMethod::Forward;
                Ok((*id, prepared, WaterRuntimeState::from_profile(water)))
            })
            .collect::<Result<Vec<_>, String>>()?;
        Ok(Self {
            source_hash: projected.source_hash,
            terrain_hash: projected
                .terrain
                .content_hash()
                .map_err(|e| e.to_string())?,
            terrain: projected.terrain.clone(),
            bodies,
        })
    }

    #[must_use]
    pub const fn terrain(&self) -> &TerrainDocument {
        &self.terrain
    }

    #[must_use]
    pub const fn source_hash(&self) -> ContentHash {
        self.source_hash
    }

    #[must_use]
    pub const fn terrain_hash(&self) -> ContentHash {
        self.terrain_hash
    }

    #[must_use]
    pub fn river_count(&self) -> usize {
        self.bodies.len()
    }
}

#[derive(Default, Resource)]
pub struct AuthoringWaterState {
    source_hash: Option<ContentHash>,
    terrain_hash: Option<ContentHash>,
    owned: Vec<OwnedWater>,
}

struct OwnedWater {
    id: EntityId,
    entities: [Entity; 2],
    meshes: [Handle<Mesh>; 2],
    material: Handle<VisualValleyWaterMaterial>,
    bank: Handle<crate::VisualValleyTerrainMaterial>,
    detail: Handle<Image>,
    animation: WaterRuntimeState,
}

impl AuthoringWaterState {
    /// CPU projection identity only; does not acknowledge asynchronous GPU upload.
    #[must_use]
    pub const fn source_hash(&self) -> Option<ContentHash> {
        self.source_hash
    }
}

/// Caller must register the normal water/terrain material plugins and this state.
/// Refuse stale batches before mutation; replace only after the matching terrain is accepted.
pub fn replace(
    world: &mut World,
    expected_head: ContentHash,
    batch: PreparedAuthoringWater,
) -> Result<(), String> {
    if expected_head != batch.source_hash {
        return Err("Stale authoring water head".into());
    }
    let terrain_hash = batch.terrain.content_hash().map_err(|e| e.to_string())?;
    if world
        .get_resource::<crate::TerrainRuntimeState>()
        .is_none_or(|r| r.cache().source_document_hash != terrain_hash)
    {
        return Err("Authoring water awaits its exact terrain projection".into());
    }
    if !world.contains_resource::<AuthoringWaterState>()
        || !world.contains_resource::<Assets<Mesh>>()
        || !world.contains_resource::<Assets<VisualValleyWaterMaterial>>()
        || !world.contains_resource::<Assets<crate::VisualValleyTerrainMaterial>>()
        || !world.contains_resource::<Assets<Image>>()
    {
        return Err("Authoring water asset storage is not initialized".into());
    }
    let mut owned = Vec::new();
    for (id, mut prepared, animation) in batch.bodies {
        let water_mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(mesh_from_geometry(prepared.water_geometry));
        let bank_mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(mesh_from_geometry(prepared.river_bank_geometry));
        let detail = world
            .resource_mut::<Assets<Image>>()
            .add(crate::visual_valley::terrain_detail_image());
        prepared.river_bank_material.extension.detail_texture = detail.clone();
        let material = world
            .resource_mut::<Assets<VisualValleyWaterMaterial>>()
            .add(prepared.water_material);
        let bank = world
            .resource_mut::<Assets<crate::VisualValleyTerrainMaterial>>()
            .add(prepared.river_bank_material);
        let water_entity = world
            .spawn((
                Name::new(format!("Authored river {id}")),
                Mesh3d(water_mesh.clone()),
                MeshMaterial3d(material.clone()),
                Transform::IDENTITY,
            ))
            .id();
        let bank_entity = world
            .spawn((
                Name::new(format!("Authored river {id} banks")),
                Mesh3d(bank_mesh.clone()),
                MeshMaterial3d(bank.clone()),
                Transform::IDENTITY,
            ))
            .id();
        owned.push(OwnedWater {
            id,
            entities: [water_entity, bank_entity],
            meshes: [water_mesh, bank_mesh],
            material,
            bank,
            detail,
            animation,
        });
    }
    let old = std::mem::replace(
        &mut world.resource_mut::<AuthoringWaterState>().owned,
        owned,
    );
    retire(world, old);
    world.resource_mut::<AuthoringWaterState>().source_hash = Some(expected_head);
    world.resource_mut::<AuthoringWaterState>().terrain_hash = Some(terrain_hash);
    Ok(())
}

/// Retire only this adapter's resources, even when the terrain/session no longer exists.
pub fn clear(world: &mut World) {
    let Some(mut state) = world.get_resource_mut::<AuthoringWaterState>() else {
        return;
    };
    state.source_hash = None;
    state.terrain_hash = None;
    let old = std::mem::take(&mut state.owned);
    retire(world, old);
}

fn retire(world: &mut World, old: Vec<OwnedWater>) {
    for item in old {
        for entity in item.entities {
            world.despawn(entity);
        }
        for mesh in item.meshes {
            if let Some(mut assets) = world.get_resource_mut::<Assets<Mesh>>() {
                assets.remove(mesh.id());
            }
        }
        if let Some(mut assets) = world.get_resource_mut::<Assets<VisualValleyWaterMaterial>>() {
            assets.remove(item.material.id());
        }
        if let Some(mut assets) =
            world.get_resource_mut::<Assets<crate::VisualValleyTerrainMaterial>>()
        {
            assets.remove(item.bank.id());
        }
        if let Some(mut assets) = world.get_resource_mut::<Assets<Image>>() {
            assets.remove(item.detail.id());
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::cast_possible_truncation,
    reason = "Bevy system wrappers and phases bounded to a single revolution"
)]
pub fn animate(
    time: Res<Time>,
    mut state: ResMut<AuthoringWaterState>,
    mut materials: ResMut<Assets<VisualValleyWaterMaterial>>,
) {
    for body in &mut state.owned {
        advance_runtime_state(&mut body.animation, time.delta_secs());
        if let Some(mut material) = materials.get_mut(&body.material) {
            material.extension.flow_phase.z = body.animation.current_phase_radians as f32;
            material.extension.flow_phase.w = body.animation.previous_phase_radians as f32;
        }
    }
}
