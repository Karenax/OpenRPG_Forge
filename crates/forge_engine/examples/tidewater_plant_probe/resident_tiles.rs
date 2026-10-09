//! Bounded CDLOD tile residency: retain unchanged patches instead of uploading the whole surface.
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, pbr::Material, prelude::*,
};
use forge_world::cdlod::CdlodNode;
use std::collections::{BTreeMap, BTreeSet};

type Key = [u64; 4];
fn key(node: &CdlodNode) -> Key {
    [
        node.origin_xz[0].to_bits(),
        node.origin_xz[1].to_bits(),
        node.size_m.to_bits(),
        u64::from(node.lod),
    ]
}
struct Tile {
    mesh: Handle<Mesh>,
    entity: Option<Entity>,
    last_used: u64,
}
#[derive(Default)]
pub(super) struct ResidentTiles {
    tiles: BTreeMap<Key, Tile>,
    root_key: Option<Key>,
    generation: u64,
}
impl ResidentTiles {
    pub(super) fn entities(&self) -> Vec<Entity> {
        self.tiles
            .values()
            .filter(|tile| tile.last_used == self.generation)
            .filter_map(|tile| tile.entity)
            .collect()
    }
    pub(super) fn counts(&self) -> [usize; 2] {
        [
            self.tiles
                .values()
                .filter(|tile| tile.last_used == self.generation && tile.entity.is_some())
                .count(),
            self.tiles.len(),
        ]
    }
    /// A tile contains exactly the source grid and node payload. Mesh/material
    /// handles survive yaw; only newly visited tiles are allocated. At most
    /// max(256, active nodes) tiles are retained for each surface.
    #[allow(clippy::too_many_lines)] // One commit preserves the representative root and child tiles together.
    pub(super) fn select<M: Material>(
        &mut self,
        world: &mut World,
        root: Entity,
        nodes: &[CdlodNode],
        build: impl Fn(&CdlodNode) -> Mesh,
    ) {
        self.generation += 1;
        let active: BTreeSet<_> = nodes.iter().map(key).collect();
        let representative = self
            .root_key
            .filter(|k| active.contains(k))
            .or_else(|| nodes.first().map(key));
        if representative != self.root_key {
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
            .get::<forge_engine::tidewater_refraction::CaptureConsumer>(root)
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
            if let Some(consumer) = consumer
                && world.get::<forge_engine::tidewater_refraction::CaptureConsumer>(entity)
                    != Some(&consumer)
            {
                world.entity_mut(entity).insert(consumer);
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
        let mut oldest: Vec<_> = self
            .tiles
            .iter()
            .filter(|(_, tile)| tile.last_used != self.generation)
            .map(|(k, tile)| (tile.last_used, *k))
            .collect();
        oldest.sort_unstable();
        for (_, k) in oldest.into_iter().take(excess) {
            if let Some(entity) = self.tiles.remove(&k).and_then(|tile| tile.entity) {
                world.despawn(entity);
            }
        }
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
    fn tile_vertex_payload_and_triangles_match_the_merged_source_mesh() {
        use bevy::mesh::{Indices, VertexAttributeValues};
        use forge_engine::tidewater_terrain::lod::ViewGeometry;
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
