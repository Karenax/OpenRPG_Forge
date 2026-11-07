//! Bounded CDLOD tile residency: retain unchanged patches instead of uploading the whole surface.
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, pbr::Material, prelude::*,
};
use forge_world::terrain_lod::CdlodNode;
use std::collections::{BTreeMap, BTreeSet, HashMap};
use std::sync::Arc;

type Key = [u64; 4];
fn key(node: &CdlodNode) -> Key {
    [
        node.origin_xz[0].to_bits(),
        node.origin_xz[1].to_bits(),
        node.size_m.to_bits(),
        u64::from(node.lod),
    ]
}
fn valid_node(node: &CdlodNode) -> bool {
    node.size_m.is_finite()
        && node.size_m > 0.0
        && node.origin_xz.iter().all(|value| {
            value.is_finite() && (value + node.size_m).is_finite() && value + node.size_m > *value
        })
}

/// Merge possible future selections by exact tile geometry before allocating
/// any meshes. This inventory does not itself publish any of the views.
pub fn merge_selections(
    selections: impl IntoIterator<Item = Vec<CdlodNode>>,
    maximum_meshes: usize,
) -> Result<Vec<CdlodNode>, String> {
    let mut seen = BTreeSet::new();
    let mut merged = Vec::new();
    for selection in selections {
        for node in selection {
            if !valid_node(&node) {
                return Err("Invalid tile in prepared selection".into());
            }
            if seen.insert(key(&node)) {
                if merged.len() == maximum_meshes {
                    return Err("Prepared tile selections exceed the renderer budget".into());
                }
                merged.push(node);
            }
        }
    }
    Ok(merged)
}
struct Tile {
    mesh: Handle<Mesh>,
    entity: Option<Entity>,
    last_used: u64,
}
#[derive(Default)]
pub struct ResidentTiles {
    tiles: BTreeMap<Key, Tile>,
    entities: HashMap<Entity, Key>,
    root: Option<Entity>,
    root_key: Option<Key>,
    generation: u64,
    lifetime: Arc<()>,
}

/// An unpublished selection. New meshes are held alive without changing the
/// current entities; dropping the value abandons this preparation.
pub struct PreparedTiles<M: Material> {
    root: Entity,
    generation: u64,
    lifetime: Arc<()>,
    material: Handle<M>,
    nodes: Vec<CdlodNode>,
    tiles: BTreeMap<Key, Handle<Mesh>>,
    meshes: Vec<Handle<Mesh>>,
}

/// Identity and registration frame of the renderer request for one candidate.
pub struct TileAdmission {
    pub owner: forge_core::EntityId,
    pub identity: forge_core::ContentHash,
    pub generation: u64,
    pub registration_frame: u32,
}

impl<M: Material> PreparedTiles<M> {
    #[must_use]
    pub fn meshes(&self) -> &[Handle<Mesh>] {
        &self.meshes
    }

    #[must_use]
    pub fn material(&self) -> &Handle<M> {
        &self.material
    }
}

impl ResidentTiles {
    /// Adopt the caller's single prepared patch before the first view selection.
    /// An empty selection hides it while retaining an owned mesh for GPU admission.
    pub(super) fn adopt_root(
        &mut self,
        world: &mut World,
        root: Entity,
        node: &CdlodNode,
    ) -> Result<(), String> {
        let mesh = world
            .get::<Mesh3d>(root)
            .ok_or("Terrain root mesh absent")?
            .0
            .clone();
        self.clear(world);
        self.root = Some(root);
        let key = key(node);
        self.root_key = Some(key);
        self.tiles.insert(
            key,
            Tile {
                mesh,
                entity: Some(root),
                last_used: self.generation,
            },
        );
        self.entities.insert(root, key);
        Ok(())
    }

    #[must_use]
    pub fn owns(&self, world: &World, entity: Entity) -> bool {
        let Some(mesh) = world.get::<Mesh3d>(entity) else {
            return false;
        };
        let Some(tile) = self
            .entities
            .get(&entity)
            .and_then(|key| self.tiles.get(key))
        else {
            return false;
        };
        tile.entity == Some(entity)
            && tile.mesh == mesh.0
            && (Some(entity) == self.root
                || world
                    .get::<ChildOf>(entity)
                    .is_some_and(|parent| Some(parent.parent()) == self.root))
    }
    /// Retire this cache's child entities while leaving its caller-owned root intact.
    pub fn clear(&mut self, world: &mut World) {
        for entity in self.tiles.values().filter_map(|tile| tile.entity) {
            if Some(entity) != self.root
                && world
                    .get::<ChildOf>(entity)
                    .is_some_and(|parent| Some(parent.parent()) == self.root)
            {
                world.despawn(entity);
            }
        }
        *self = Self::default();
    }
    #[must_use]
    pub fn entities(&self) -> Vec<Entity> {
        self.tiles
            .values()
            .filter(|tile| tile.last_used == self.generation)
            .filter_map(|tile| tile.entity)
            .collect()
    }
    #[must_use]
    pub fn counts(&self) -> [usize; 2] {
        [
            self.tiles
                .values()
                .filter(|tile| tile.last_used == self.generation && tile.entity.is_some())
                .count(),
            self.tiles.len(),
        ]
    }

    /// Strong handles to every still-owned cached mesh, including hidden tiles.
    /// Returning no inventory keeps the caller on its existing visible-only
    /// path when the cache is uninitialized, stale, or exceeds the GPU budget.
    #[must_use]
    pub fn retained_meshes(&self, world: &World) -> Option<Vec<Handle<Mesh>>> {
        let root = self.root?;
        if self.tiles.is_empty()
            || self.tiles.len() > crate::render_inventory::MAX_SCOPED_MESHES
            || !self.owns(world, root)
        {
            return None;
        }
        let assets = world.get_resource::<Assets<Mesh>>()?;
        self.tiles
            .values()
            .all(|tile| {
                assets.contains(tile.mesh.id())
                    && tile.entity.is_none_or(|entity| self.owns(world, entity))
            })
            .then(|| self.tiles.values().map(|tile| tile.mesh.clone()).collect())
    }

    /// Conservative CPU-only coverage: every requested patch must already be
    /// active with the exact source geometry and material. A larger or smaller
    /// LOD covering the same ground is deliberately not considered equivalent.
    /// This does not establish propagated visibility, a GPU fence, or pixels.
    #[must_use]
    pub fn covers_exact<M: Material>(&self, world: &World, nodes: &[CdlodNode]) -> bool {
        let Some(root) = self.root.filter(|root| self.owns(world, *root)) else {
            return false;
        };
        let Some(material) = world.get::<MeshMaterial3d<M>>(root) else {
            return false;
        };
        if !world
            .get_resource::<Assets<M>>()
            .is_some_and(|assets| assets.contains(material.id()))
        {
            return false;
        }
        let mut requested = BTreeSet::new();
        nodes.iter().all(|node| {
            if !valid_node(node) || !requested.insert(key(node)) {
                return false;
            }
            self.tiles.get(&key(node)).is_some_and(|tile| {
                tile.last_used == self.generation
                    && world
                        .get_resource::<Assets<Mesh>>()
                        .is_some_and(|assets| assets.contains(tile.mesh.id()))
                    && tile.entity.is_some_and(|entity| {
                        self.owns(world, entity)
                            && world
                                .get::<Visibility>(root)
                                .is_some_and(|visibility| *visibility != Visibility::Hidden)
                            && world
                                .get::<Visibility>(entity)
                                .is_some_and(|visibility| *visibility != Visibility::Hidden)
                            && world
                                .get::<MeshMaterial3d<M>>(entity)
                                .is_some_and(|binding| binding.0 == material.0)
                    })
            })
        })
    }

    /// Prepare a bounded candidate without publishing it to the live scene.
    /// The caller must register `meshes()` and `material()` with the renderer.
    pub fn prepare<M: Material>(
        &self,
        world: &mut World,
        root: Entity,
        nodes: &[CdlodNode],
        maximum_meshes: usize,
        build: impl Fn(&CdlodNode) -> Mesh,
    ) -> Result<PreparedTiles<M>, String> {
        if self.root != Some(root) || !self.owns(world, root) || self.generation == u64::MAX {
            return Err("Tile root or cache generation is not current".into());
        }
        if nodes.len().max(1) > maximum_meshes {
            return Err("Prepared tile selection exceeds the renderer budget".into());
        }
        let mut unique = BTreeSet::new();
        if nodes
            .iter()
            .any(|node| !valid_node(node) || !unique.insert(key(node)))
        {
            return Err("Invalid or duplicate tile in prepared selection".into());
        }
        if !world.contains_resource::<Assets<Mesh>>() {
            return Err("Prepared tile mesh assets unavailable".into());
        }
        let material = world
            .get::<MeshMaterial3d<M>>(root)
            .ok_or("Prepared tile material absent")?
            .0
            .clone();
        let mut tiles = BTreeMap::new();
        for node in nodes {
            let key = key(node);
            let mesh = if let Some(tile) = self.tiles.get(&key) {
                if !world.resource::<Assets<Mesh>>().contains(tile.mesh.id()) {
                    return Err("Cached tile mesh is no longer available".into());
                }
                tile.mesh.clone()
            } else {
                world.resource_mut::<Assets<Mesh>>().add(build(node))
            };
            tiles.insert(key, mesh);
        }
        let meshes = if tiles.is_empty() {
            vec![
                world
                    .get::<Mesh3d>(root)
                    .ok_or("Tile root mesh absent")?
                    .0
                    .clone(),
            ]
        } else {
            tiles.values().cloned().collect()
        };
        Ok(PreparedTiles {
            root,
            generation: self.generation,
            lifetime: Arc::clone(&self.lifetime),
            material,
            nodes: nodes.to_vec(),
            tiles,
            meshes,
        })
    }

    /// Prepare only a bounded batch of missing tiles from a possible view.
    /// Existing tiles are left in place and the candidate does not publish a
    /// scene. Repeating this after admission advances through the same view.
    ///
    /// # Errors
    /// Refuses stale cache ownership, malformed selections or an empty batch
    /// budget before allocating any new mesh.
    pub fn prepare_missing<M: Material>(
        &self,
        world: &mut World,
        root: Entity,
        nodes: &[CdlodNode],
        maximum_meshes: usize,
        maximum_new: usize,
        build: impl Fn(&CdlodNode) -> Mesh,
    ) -> Result<Option<PreparedTiles<M>>, String> {
        if self.root != Some(root) || !self.owns(world, root) || self.generation == u64::MAX {
            return Err("Tile root or cache generation is not current".into());
        }
        if maximum_new == 0 || nodes.len().max(1) > maximum_meshes {
            return Err("Prepared tile batch exceeds the renderer budget".into());
        }
        let assets = world
            .get_resource::<Assets<Mesh>>()
            .ok_or("Prepared tile mesh assets unavailable")?;
        if world.get::<MeshMaterial3d<M>>(root).is_none() {
            return Err("Prepared tile material absent".into());
        }
        let available = maximum_meshes
            .saturating_sub(self.tiles.len())
            .min(maximum_new);
        let mut seen = BTreeSet::new();
        let mut missing = Vec::with_capacity(available.min(nodes.len()));
        for node in nodes {
            let node_key = key(node);
            if !valid_node(node) || !seen.insert(node_key) {
                return Err("Invalid or duplicate tile in prepared selection".into());
            }
            if let Some(tile) = self.tiles.get(&node_key) {
                if !assets.contains(tile.mesh.id()) {
                    return Err("Cached tile mesh is no longer available".into());
                }
            } else if missing.len() < available {
                missing.push(*node);
            }
        }
        if missing.is_empty() {
            return Ok(None);
        }
        self.prepare::<M>(world, root, &missing, maximum_meshes, build)
            .map(Some)
    }

    /// Publish only a still-current candidate whose exact typed sources have a
    /// completed renderer fence after its registration. This does not certify
    /// pixel fidelity or visibility after the selection is published.
    pub fn publish<M: Material>(
        &mut self,
        world: &mut World,
        candidate: &PreparedTiles<M>,
        requested: &[CdlodNode],
        token: &crate::render_inventory::RenderInventoryToken,
        admission: &TileAdmission,
    ) -> Result<(), String> {
        if !forge_world::terrain_lod::same_node_geometry(&candidate.nodes, requested)
            || !self.candidate_is_current(world, candidate)
            || !token.is_current(
                world,
                admission.owner,
                admission.identity,
                admission.generation,
            )
            || !token.covers_frame(world, admission.registration_frame)
            || !token.covers_sources(
                world,
                &candidate.meshes,
                std::slice::from_ref(&candidate.material),
            )
        {
            return Err("Prepared tiles or renderer receipt are no longer current".into());
        }
        for (key, mesh) in &candidate.tiles {
            self.tiles.entry(*key).or_insert_with(|| Tile {
                mesh: mesh.clone(),
                entity: None,
                last_used: self.generation,
            });
        }
        self.select::<M>(world, candidate.root, &candidate.nodes, |_| {
            unreachable!("all prepared meshes were admitted before publication")
        });
        // Even an unchanged publication consumes candidates from the previous lifetime.
        self.lifetime = Arc::new(());
        Ok(())
    }

    /// Retain a GPU-admitted candidate without changing any visible entity.
    /// Its meshes may be a subset of a larger current typed inventory. The
    /// future visible selection still needs its own post-publication fence.
    pub fn retain_prepared<M: Material>(
        &mut self,
        world: &World,
        candidate: &PreparedTiles<M>,
        token: &crate::render_inventory::RenderInventoryToken,
        admission: &TileAdmission,
    ) -> Result<usize, String> {
        if !self.candidate_is_compatible(world, candidate)
            || !token.is_current(
                world,
                admission.owner,
                admission.identity,
                admission.generation,
            )
            || !token.covers_frame(world, admission.registration_frame)
            || !token.covers_subset(
                world,
                &candidate.meshes,
                std::slice::from_ref(&candidate.material),
            )
        {
            return Err("Prepared tiles or renderer receipt are no longer current".into());
        }
        let additional = candidate
            .tiles
            .keys()
            .filter(|key| !self.tiles.contains_key(*key))
            .count();
        if self
            .tiles
            .len()
            .checked_add(additional)
            .is_none_or(|total| total > crate::render_inventory::MAX_SCOPED_MESHES)
        {
            return Err("Prepared tiles exceed the resident renderer budget".into());
        }
        for (key, mesh) in &candidate.tiles {
            self.tiles.entry(*key).or_insert_with(|| Tile {
                mesh: mesh.clone(),
                entity: None,
                last_used: self.generation,
            });
        }
        self.lifetime = Arc::new(());
        Ok(additional)
    }

    fn candidate_is_current<M: Material>(
        &self,
        world: &World,
        candidate: &PreparedTiles<M>,
    ) -> bool {
        self.generation == candidate.generation && self.candidate_is_compatible(world, candidate)
    }

    // Retention does not change the visible selection. A view may move while
    // the renderer prepares a candidate, provided the cache lifetime, material
    // and every overlapping mesh still belong to the same source.
    fn candidate_is_compatible<M: Material>(
        &self,
        world: &World,
        candidate: &PreparedTiles<M>,
    ) -> bool {
        self.root == Some(candidate.root)
            && Arc::ptr_eq(&self.lifetime, &candidate.lifetime)
            && self.owns(world, candidate.root)
            && world
                .get::<MeshMaterial3d<M>>(candidate.root)
                .is_some_and(|material| material.0 == candidate.material)
            && self
                .entities()
                .iter()
                .all(|entity| self.owns(world, *entity))
            && candidate
                .tiles
                .iter()
                .all(|(key, mesh)| self.tiles.get(key).is_none_or(|tile| tile.mesh == *mesh))
    }
    /// A tile contains exactly the source grid and node payload. Mesh/material
    /// handles survive yaw; only newly visited tiles are allocated. Normal
    /// selection retains max(256, active nodes); GPU-admitted future tiles may
    /// temporarily raise the retained total up to the scoped renderer budget.
    #[allow(clippy::too_many_lines)] // One commit preserves the representative root and child tiles together.
    pub fn select<M: Material>(
        &mut self,
        world: &mut World,
        root: Entity,
        nodes: &[CdlodNode],
        build: impl Fn(&CdlodNode) -> Mesh,
    ) {
        #[cfg(feature = "qualification")]
        let _profile = crate::cpu_timing::Span::new(
            if std::any::TypeId::of::<M>() == std::any::TypeId::of::<super::lod::LodMaterial>() {
                "source.terrain.tiles"
            } else if std::any::TypeId::of::<M>()
                == std::any::TypeId::of::<crate::source_water_optics::OpticalMaterial>()
                || std::any::TypeId::of::<M>()
                    == std::any::TypeId::of::<crate::source_ocean_geometry::GeometryMaterial>()
                || std::any::TypeId::of::<M>()
                    == std::any::TypeId::of::<crate::source_ocean_geometry::DiagnosticMaterial>()
            {
                "source.water.tiles"
            } else {
                "source.other.tiles"
            },
        );
        let active: BTreeSet<_> = nodes.iter().map(key).collect();
        let root_changed = self.root != Some(root);
        // The opt-in qualification trace records the actual source node keys,
        // not entity IDs or a guessed GPU admission. It allows a cache policy
        // to be replayed offline without altering the live tile selection.
        #[cfg(feature = "qualification")]
        if std::env::var_os("FORGE_QUALIFY_TILE_HISTORY").is_some() {
            let old: BTreeSet<_> = self
                .tiles
                .iter()
                .filter(|(_, tile)| tile.last_used == self.generation && tile.entity.is_some())
                .map(|(key, _)| *key)
                .collect();
            if root_changed || old != active {
                let frame = world
                    .get_resource::<bevy::diagnostic::FrameCount>()
                    .map_or(0, |frame| frame.0);
                let cached: Vec<_> = self.tiles.keys().copied().collect();
                let checksum = forge_core::canonical_json_sha256(&cached)
                    .expect("finite integer tile keys have a canonical hash");
                let initial = (root_changed || self.generation == 0).then(|| {
                    self.tiles
                        .iter()
                        .map(|(key, tile)| (*key, tile.last_used, tile.entity.is_some()))
                        .collect::<Vec<_>>()
                });
                eprintln!(
                    "QUALIFY_TILE_SELECTION {}",
                    serde_json::json!({
                        "frame": frame,
                        "material": std::any::type_name::<M>(),
                        "root": format!("{root:?}"),
                        "root_changed": root_changed,
                        "prior_generation": self.generation,
                        "prior_root_key": self.root_key,
                        "prior_cache_sha256": checksum,
                        "initial_cache": initial,
                        "nodes": nodes.iter().map(key).collect::<Vec<_>>(),
                    })
                );
            }
        }
        #[cfg(feature = "qualification")]
        if !root_changed && std::env::var_os("FORGE_QUALIFY_TILE_COVERAGE").is_some() {
            let old: BTreeSet<_> = self
                .tiles
                .iter()
                .filter(|(_, tile)| tile.last_used == self.generation && tile.entity.is_some())
                .map(|(key, _)| *key)
                .collect();
            if old != active {
                let frame = world
                    .get_resource::<bevy::diagnostic::FrameCount>()
                    .map_or(0, |frame| frame.0);
                eprintln!(
                    "QUALIFY_TILE_COVERAGE frame={frame} material={} previous={} requested={} added={} removed={} exact_coverage={}",
                    std::any::type_name::<M>(),
                    old.len(),
                    active.len(),
                    active.difference(&old).count(),
                    old.difference(&active).count(),
                    self.covers_exact::<M>(world, nodes)
                );
            }
        }
        if root_changed {
            self.clear(world);
            self.root = Some(root);
        }
        let (previous_count, previous_in_active) = self
            .tiles
            .iter()
            .filter(|(_, tile)| tile.last_used == self.generation && tile.entity.is_some())
            .fold((0, true), |(count, contains), (key, _)| {
                (count + 1, contains && active.contains(key))
            });
        // Re-observing the same set still repairs material/visibility bindings,
        // but must not invalidate an in-flight candidate or age the LRU.
        if root_changed || previous_count != active.len() || !previous_in_active {
            self.generation += 1;
        }
        let representative = self
            .root_key
            .filter(|k| active.contains(k))
            .or_else(|| nodes.first().map(key));
        if representative.is_some() && representative != self.root_key {
            if let Some(old) = self.root_key.and_then(|k| self.tiles.get_mut(&k)) {
                old.entity = None;
            }
            if let Some(tile) = representative.and_then(|k| self.tiles.get_mut(&k))
                && let Some(entity) = tile.entity.take()
            {
                world.despawn(entity);
            }
            self.root_key = representative;
        }
        let material = world
            .get::<MeshMaterial3d<M>>(root)
            .expect("surface material")
            .0
            .clone();
        let consumer = world
            .get::<crate::source_surface::capture::CaptureConsumer>(root)
            .copied();
        for node in nodes {
            let k = key(node);
            let tile = self.tiles.entry(k).or_insert_with(|| Tile {
                mesh: world.resource_mut::<Assets<Mesh>>().add(build(node)),
                entity: None,
                last_used: self.generation,
            });
            tile.last_used = self.generation;
            let entity = if Some(k) == self.root_key {
                if world
                    .get::<Mesh3d>(root)
                    .is_none_or(|mesh| mesh.0.id() != tile.mesh.id())
                {
                    world.entity_mut(root).insert(Mesh3d(tile.mesh.clone()));
                }
                root
            } else {
                *tile.entity.get_or_insert_with(|| {
                    world
                        .spawn((
                            Mesh3d(tile.mesh.clone()),
                            MeshMaterial3d(material.clone()),
                            Transform::IDENTITY,
                            Visibility::Inherited,
                            NoFrustumCulling,
                            NotShadowCaster,
                            ChildOf(root),
                        ))
                        .id()
                })
            };
            tile.entity = Some(entity);
            if world
                .get::<MeshMaterial3d<M>>(entity)
                .is_none_or(|binding| binding.0 != material)
            {
                world
                    .entity_mut(entity)
                    .insert(MeshMaterial3d(material.clone()));
            }
            if let Some(consumer) = consumer
                && world.get::<crate::source_surface::capture::CaptureConsumer>(entity)
                    != Some(&consumer)
            {
                world.entity_mut(entity).insert(consumer);
            } else if consumer.is_none() && entity != root {
                world
                    .entity_mut(entity)
                    .remove::<crate::source_surface::capture::CaptureConsumer>();
            }
            if world.get::<Visibility>(entity) != Some(&Visibility::Inherited) {
                world.entity_mut(entity).insert(Visibility::Inherited);
            }
        }
        for tile in self
            .tiles
            .values()
            .filter(|tile| tile.last_used != self.generation)
        {
            if let Some(entity) = tile.entity
                && world.get::<Visibility>(entity) != Some(&Visibility::Hidden)
            {
                world.entity_mut(entity).insert(Visibility::Hidden);
            }
        }
        if representative.is_none() {
            world.entity_mut(root).insert(Visibility::Hidden);
        }
        let excess = self.tiles.len().saturating_sub(active.len().max(256));
        if excess > 0 {
            let mut oldest: Vec<_> = self
                .tiles
                .iter()
                .filter(|(k, tile)| tile.last_used != self.generation && Some(**k) != self.root_key)
                .map(|(k, tile)| (tile.last_used, *k))
                .collect();
            oldest.sort_unstable();
            for (_, k) in oldest.into_iter().take(excess) {
                if let Some(entity) = self.tiles.remove(&k).and_then(|tile| tile.entity) {
                    world.despawn(entity);
                }
            }
        }
        self.entities.clear();
        self.entities.extend(
            self.tiles
                .iter()
                .filter_map(|(key, tile)| tile.entity.map(|entity| (entity, *key))),
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn node(x: u16) -> CdlodNode {
        CdlodNode {
            origin_xz: [f64::from(x) * 8.0, 0.0],
            size_m: 8.0,
            lod: 0,
        }
    }
    fn setup() -> (World, Entity, ResidentTiles) {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<StandardMaterial>>();
        let mesh = world.resource_mut::<Assets<Mesh>>().add(Cuboid::default());
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        let root = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Transform::IDENTITY,
                Visibility::Inherited,
            ))
            .id();
        (world, root, ResidentTiles::default())
    }
    fn select(tiles: &mut ResidentTiles, world: &mut World, root: Entity, nodes: &[CdlodNode]) {
        tiles.select::<StandardMaterial>(world, root, nodes, |_| Mesh::from(Cuboid::default()));
    }
    #[test]
    fn future_view_union_deduplicates_and_refuses_invalid_or_excess_tiles() {
        let merged =
            merge_selections(vec![vec![node(1), node(0)], vec![node(1), node(2)]], 3).unwrap();
        assert_eq!(merged, vec![node(1), node(0), node(2)]);
        assert!(merge_selections(vec![vec![node(0), node(1)], vec![node(2)]], 2).is_err());
        let mut invalid = node(0);
        invalid.size_m = f64::NAN;
        assert!(merge_selections(vec![vec![invalid]], 3).is_err());
    }
    #[test]
    fn preparation_reuses_live_meshes_and_refuses_duplicates_or_budget_overflow() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        let prior = tiles.tiles[&key(&node(0))].mesh.clone();
        let root_mesh = world.get::<Mesh3d>(root).unwrap().0.clone();
        let prior_entities = tiles.entities();
        let candidate = tiles
            .prepare::<StandardMaterial>(&mut world, root, &[node(0), node(2)], 2, |_| {
                Mesh::from(Cuboid::default())
            })
            .unwrap();
        assert_eq!(candidate.tiles[&key(&node(0))], prior);
        assert_eq!(candidate.meshes().len(), 2);
        assert_eq!(world.get::<Mesh3d>(root).unwrap().0, root_mesh);
        assert_eq!(tiles.entities(), prior_entities);
        assert_eq!(tiles.counts(), [2, 2]);
        assert!(
            tiles
                .prepare::<StandardMaterial>(&mut world, root, &[node(0), node(0)], 2, |_| {
                    Mesh::from(Cuboid::default())
                })
                .is_err()
        );
        assert!(
            tiles
                .prepare::<StandardMaterial>(&mut world, root, &[node(0), node(2)], 1, |_| {
                    Mesh::from(Cuboid::default())
                })
                .is_err()
        );
        assert!(
            tiles
                .prepare::<StandardMaterial>(&mut world, root, &[], 0, |_| Mesh::from(
                    Cuboid::default()
                ))
                .is_err()
        );
        assert!(
            tiles
                .prepare::<StandardMaterial>(
                    &mut world,
                    root,
                    &[CdlodNode {
                        size_m: -1.0,
                        ..node(0)
                    }],
                    2,
                    |_| Mesh::from(Cuboid::default())
                )
                .is_err()
        );
        drop(candidate);
        assert_eq!(tiles.counts(), [2, 2]);
    }

    #[test]
    fn future_preparation_limits_new_meshes_without_changing_the_live_view() {
        use std::cell::Cell;

        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        let original_entities = tiles.entities();
        let original_root = world.get::<Mesh3d>(root).unwrap().0.clone();
        let built = Cell::new(0);
        let future = [node(1), node(2), node(3), node(4)];
        let candidate = tiles
            .prepare_missing::<StandardMaterial>(&mut world, root, &future, 8, 2, |_| {
                built.set(built.get() + 1);
                Mesh::from(Cuboid::default())
            })
            .unwrap()
            .unwrap();
        assert_eq!(built.get(), 2);
        assert_eq!(candidate.nodes, [node(2), node(3)]);
        assert_eq!(tiles.counts(), [2, 2]);
        assert_eq!(tiles.entities(), original_entities);
        assert_eq!(world.get::<Mesh3d>(root).unwrap().0, original_root);

        let duplicate_after_limit = [node(2), node(3), node(4), node(4)];
        assert!(
            tiles
                .prepare_missing::<StandardMaterial>(
                    &mut world,
                    root,
                    &duplicate_after_limit,
                    8,
                    1,
                    |_| {
                        built.set(built.get() + 1);
                        Mesh::from(Cuboid::default())
                    },
                )
                .is_err()
        );
        assert_eq!(built.get(), 2);
        assert!(
            tiles
                .prepare_missing::<StandardMaterial>(&mut world, root, &[node(2)], 2, 1, |_| {
                    built.set(built.get() + 1);
                    Mesh::from(Cuboid::default())
                })
                .unwrap()
                .is_none()
        );
        assert_eq!(built.get(), 2);
    }

    #[test]
    fn future_cache_candidate_survives_a_view_change_but_not_replaced_sources() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        let candidate = tiles
            .prepare_missing::<StandardMaterial>(&mut world, root, &[node(2)], 8, 1, |_| {
                Mesh::from(Cuboid::default())
            })
            .unwrap()
            .unwrap();
        assert!(tiles.candidate_is_current(&world, &candidate));
        select(&mut tiles, &mut world, root, &[node(0)]);
        assert!(!tiles.candidate_is_current(&world, &candidate));
        assert!(tiles.candidate_is_compatible(&world, &candidate));
        select(&mut tiles, &mut world, root, &[node(0), node(2)]);
        assert!(!tiles.candidate_is_compatible(&world, &candidate));
        tiles.clear(&mut world);
        select(&mut tiles, &mut world, root, &[node(0)]);
        assert!(!tiles.candidate_is_compatible(&world, &candidate));
    }

    #[test]
    fn exact_coverage_rejects_missing_hidden_changed_or_ambiguous_patches() {
        let (mut world, root, mut tiles) = setup();
        let selected = [node(0), node(1), node(2)];
        assert!(!tiles.covers_exact::<StandardMaterial>(&world, &[]));
        select(&mut tiles, &mut world, root, &selected);
        assert!(tiles.covers_exact::<StandardMaterial>(&world, &[node(2), node(0)]));
        assert!(!tiles.covers_exact::<StandardMaterial>(&world, &[node(3)]));
        assert!(!tiles.covers_exact::<StandardMaterial>(&world, &[node(0), node(0)]));
        assert!(
            !tiles.covers_exact::<StandardMaterial>(&world, &[CdlodNode { lod: 1, ..node(0) }])
        );
        assert!(!tiles.covers_exact::<StandardMaterial>(
            &world,
            &[CdlodNode {
                size_m: f64::NAN,
                ..node(0)
            }]
        ));
        let child = tiles.tiles[&key(&node(1))].entity.unwrap();
        world.entity_mut(child).insert(Visibility::Hidden);
        assert!(!tiles.covers_exact::<StandardMaterial>(&world, &[node(1)]));
        world.entity_mut(child).insert(Visibility::Inherited);
        let other_material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        world
            .entity_mut(child)
            .insert(MeshMaterial3d(other_material));
        assert!(!tiles.covers_exact::<StandardMaterial>(&world, &[node(1)]));
        let material = world
            .get::<MeshMaterial3d<StandardMaterial>>(root)
            .unwrap()
            .0
            .clone();
        world.entity_mut(child).insert(MeshMaterial3d(material));
        assert!(tiles.covers_exact::<StandardMaterial>(&world, &[node(1)]));
        let mesh = tiles.tiles[&key(&node(1))].mesh.id();
        world.resource_mut::<Assets<Mesh>>().remove(mesh);
        assert!(!tiles.covers_exact::<StandardMaterial>(&world, &[node(1)]));
    }
    #[test]
    fn reorder_and_overlap_retain_meshes_and_entities() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1), node(2)]);
        let original: Vec<_> = tiles
            .tiles
            .iter()
            .map(|(k, t)| (*k, t.mesh.id(), t.entity))
            .collect();
        select(&mut tiles, &mut world, root, &[node(2), node(1), node(0)]);
        assert_eq!(
            original,
            tiles
                .tiles
                .iter()
                .map(|(k, t)| (*k, t.mesh.id(), t.entity))
                .collect::<Vec<_>>()
        );
        select(&mut tiles, &mut world, root, &[node(1), node(2), node(3)]);
        assert_eq!(
            world.get::<Mesh3d>(root).expect("root mesh").0.id(),
            original[1].1
        );
        assert_eq!(tiles.tiles[&key(&node(2))].entity, original[2].2);
        assert_eq!(tiles.counts(), [3, 4]);
    }
    #[test]
    fn indexed_ownership_rejects_changed_meshes_and_foreign_parents() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1), node(2)]);
        select(&mut tiles, &mut world, root, &[node(0), node(2)]);
        let cached = tiles.tiles[&key(&node(1))].mesh.clone();
        let root_mesh = world.get::<Mesh3d>(root).unwrap().0.clone();
        world.entity_mut(root).insert(Mesh3d(cached));
        assert!(
            !tiles.owns(&world, root),
            "A different cached mesh must not prove root ownership"
        );
        world.entity_mut(root).insert(Mesh3d(root_mesh));
        assert!(tiles.owns(&world, root));
        let child = tiles
            .entities()
            .into_iter()
            .find(|entity| *entity != root)
            .unwrap();
        let child_mesh = world.get::<Mesh3d>(child).unwrap().0.clone();
        world
            .entity_mut(child)
            .insert(Mesh3d(Handle::<Mesh>::default()));
        assert!(!tiles.owns(&world, child));
        world.entity_mut(child).insert(Mesh3d(child_mesh));
        assert!(tiles.owns(&world, child));
        let foreign = world
            .spawn((Transform::default(), Visibility::Inherited))
            .id();
        world.entity_mut(child).insert(ChildOf(foreign));
        assert!(!tiles.owns(&world, child));
    }

    #[test]
    fn retained_inventory_includes_hidden_or_unattached_meshes_but_refuses_stale_cache() {
        let (mut world, root, mut tiles) = setup();
        assert!(tiles.retained_meshes(&world).is_none());
        select(&mut tiles, &mut world, root, &[node(0), node(1), node(2)]);
        let old = tiles.tiles[&key(&node(0))].mesh.clone();
        select(&mut tiles, &mut world, root, &[node(1), node(2), node(3)]);
        let retained = tiles.retained_meshes(&world).unwrap();
        assert_eq!(retained.len(), 4);
        assert!(retained.contains(&old));
        assert_eq!(tiles.counts(), [3, 4]);
        let child = tiles
            .entities()
            .into_iter()
            .find(|entity| *entity != root)
            .unwrap();
        let mesh = world.get::<Mesh3d>(child).unwrap().0.clone();
        let foreign = world
            .spawn((Transform::default(), Visibility::Inherited))
            .id();
        world.entity_mut(child).insert(ChildOf(foreign));
        assert!(tiles.retained_meshes(&world).is_none());
        world.entity_mut(child).insert(ChildOf(root));
        world.entity_mut(child).insert(Mesh3d(old.clone()));
        assert!(tiles.retained_meshes(&world).is_none());
        world.entity_mut(child).insert(Mesh3d(mesh));
        assert_eq!(tiles.retained_meshes(&world).unwrap().len(), 4);
        world.resource_mut::<Assets<Mesh>>().remove(old.id());
        assert!(tiles.retained_meshes(&world).is_none());
    }

    #[test]
    fn oversized_retained_inventory_falls_back_without_discarding_current_tiles() {
        let (mut world, root, mut tiles) = setup();
        let nodes: Vec<_> = (0..=crate::render_inventory::MAX_SCOPED_MESHES)
            .map(|x| node(u16::try_from(x).unwrap()))
            .collect();
        select(&mut tiles, &mut world, root, &nodes);
        assert!(tiles.retained_meshes(&world).is_none());
        assert_eq!(tiles.counts(), [nodes.len(), nodes.len()]);
        assert!(
            tiles
                .entities()
                .iter()
                .all(|entity| tiles.owns(&world, *entity))
        );
    }

    #[test]
    fn traversal_is_bounded_and_never_evicts_active_tiles() {
        let (mut world, root, mut tiles) = setup();
        for x in 0..600 {
            select(&mut tiles, &mut world, root, &[node(x), node(x + 1)]);
            assert!(tiles.counts()[1] <= 256);
            assert_eq!(tiles.counts()[0], 2);
            assert!(
                tiles
                    .entities()
                    .iter()
                    .all(|e| world.get::<Mesh3d>(*e).is_some())
            );
            assert!(world.get_entity(root).is_ok());
        }
        let many: Vec<_> = (0..300).map(node).collect();
        select(&mut tiles, &mut world, root, &many);
        assert_eq!(tiles.counts(), [300, 300]);
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        assert_eq!(tiles.counts(), [2, 256]);
    }
    #[test]
    fn empty_selection_hides_then_recovers_the_surface() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        let mesh = tiles.tiles[&key(&node(0))].mesh.id();
        select(&mut tiles, &mut world, root, &[]);
        assert_eq!(tiles.counts()[0], 0);
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Inherited));
        assert_eq!(world.get::<Mesh3d>(root).expect("root mesh").0.id(), mesh);
        assert_eq!(tiles.counts()[0], 2);
    }
    #[test]
    fn an_initially_empty_view_retains_the_adopted_root_and_uses_it_on_return() {
        let (mut world, root, mut tiles) = setup();
        let original = world.get::<Mesh3d>(root).unwrap().0.id();
        tiles.adopt_root(&mut world, root, &node(0)).unwrap();
        select(&mut tiles, &mut world, root, &[]);
        assert_eq!(tiles.counts(), [0, 1]);
        assert!(tiles.owns(&world, root));
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        tiles.select::<StandardMaterial>(&mut world, root, &[node(0)], |_| {
            panic!("adopted patch rebuilt")
        });
        assert_eq!(tiles.counts(), [1, 1]);
        assert_eq!(world.get::<Mesh3d>(root).unwrap().0.id(), original);
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Inherited));
    }
    #[test]
    fn replacing_the_material_refreshes_retained_children_without_rebuilding_meshes() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        let original: Vec<_> = tiles
            .tiles
            .values()
            .map(|t| (t.mesh.id(), t.entity))
            .collect();
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        world
            .entity_mut(root)
            .insert(MeshMaterial3d(material.clone()));
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        assert_eq!(
            original,
            tiles
                .tiles
                .values()
                .map(|t| (t.mesh.id(), t.entity))
                .collect::<Vec<_>>()
        );
        for entity in tiles.entities() {
            assert_eq!(
                world
                    .get::<MeshMaterial3d<StandardMaterial>>(entity)
                    .unwrap()
                    .0,
                material
            );
            assert!(tiles.owns(&world, entity));
        }
    }
    #[test]
    fn an_empty_view_after_a_large_selection_retains_owned_standby_geometry() {
        let (mut world, root, mut tiles) = setup();
        let nodes: Vec<_> = (0..300).map(node).collect();
        select(&mut tiles, &mut world, root, &nodes);
        let original = world.get::<Mesh3d>(root).unwrap().0.id();
        select(&mut tiles, &mut world, root, &[]);
        assert_eq!(tiles.counts(), [0, 256]);
        assert!(tiles.owns(&world, root));
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        select(&mut tiles, &mut world, root, &[node(0)]);
        assert_eq!(world.get::<Mesh3d>(root).unwrap().0.id(), original);
        assert_eq!(tiles.counts(), [1, 256]);
        assert!(tiles.owns(&world, root));
    }
    #[test]
    fn a_new_root_retires_old_children_but_preserves_both_caller_owned_roots() {
        let (mut world, root, mut tiles) = setup();
        select(&mut tiles, &mut world, root, &[node(0), node(1)]);
        let child = tiles.entities().into_iter().find(|e| *e != root).unwrap();
        let material = world
            .get::<MeshMaterial3d<StandardMaterial>>(root)
            .unwrap()
            .clone();
        let mesh = world.get::<Mesh3d>(root).unwrap().clone();
        let next = world
            .spawn((mesh, material, Transform::IDENTITY, Visibility::Inherited))
            .id();
        select(&mut tiles, &mut world, next, &[node(0), node(1)]);
        assert!(world.get_entity(child).is_err());
        assert!(world.get_entity(root).is_ok());
        assert!(!tiles.owns(&world, root));
        assert!(tiles.entities().iter().all(|e| tiles.owns(&world, *e)));
        tiles.clear(&mut world);
        assert_eq!(tiles.counts(), [0, 0]);
        assert!(world.get_entity(next).is_ok());
        assert!(world.get_entity(root).is_ok());
    }
    #[test]
    fn tile_vertex_payload_and_triangles_match_the_merged_source_mesh() {
        use crate::source_terrain::lod::ViewGeometry;
        use bevy::mesh::{Indices, VertexAttributeValues};
        let nodes = [node(0), node(1), node(2)];
        let merged = ViewGeometry::mesh_from_nodes(&nodes)
            .expect("merged mesh")
            .0;
        let split: Vec<_> = nodes
            .iter()
            .map(|n| {
                ViewGeometry::mesh_from_nodes(std::slice::from_ref(n))
                    .expect("tile mesh")
                    .0
            })
            .collect();
        for (attribute, values) in merged.attributes() {
            match values {
                VertexAttributeValues::Float32x3(expected) => {
                    let actual: Vec<_> = split
                        .iter()
                        .flat_map(|m| match m.attribute(attribute.id).expect("attribute") {
                            VertexAttributeValues::Float32x3(v) => v.clone(),
                            _ => panic!("attribute type"),
                        })
                        .collect();
                    assert_eq!(&actual, expected);
                }
                VertexAttributeValues::Float32x4(expected) => {
                    let actual: Vec<_> = split
                        .iter()
                        .flat_map(|m| match m.attribute(attribute.id).expect("attribute") {
                            VertexAttributeValues::Float32x4(v) => v.clone(),
                            _ => panic!("attribute type"),
                        })
                        .collect();
                    assert_eq!(&actual, expected);
                }
                _ => panic!("terrain attribute type"),
            }
        }
        let mut offset = 0;
        let mut actual = Vec::new();
        for m in split {
            let Some(Indices::U32(indices)) = m.indices() else {
                panic!("source indices")
            };
            actual.extend(indices.iter().map(|i| i + offset));
            offset += u32::try_from(m.count_vertices()).expect("tile vertices");
        }
        let Some(Indices::U32(expected)) = merged.indices() else {
            panic!("source indices")
        };
        assert_eq!(expected, &actual);
    }
}
