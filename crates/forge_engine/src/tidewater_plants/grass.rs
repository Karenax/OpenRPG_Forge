// SPDX-License-Identifier: MIT
// Adapted from Tidewater GrassField.js selection, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see third_party/tidewater/LICENSE.
//! Source-compatible grass cell selection and bounded, reusable GPU residency.
use super::{PlantMaterial, Record};
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, mesh::MeshTag, prelude::*,
    render::storage::ShaderBuffer,
};
use forge_assets::tidewater_plants::PlantPackage;
use forge_world::cdlod::CdlodView;
use serde::Deserialize;

const SIDE: usize = 256;
const CELL_COUNT: usize = SIDE * SIDE;
const CAPACITIES: [usize; 3] = [48, 160, 420];

#[derive(Deserialize)]
struct Description {
    schema: String,
    version: u32,
    cells_per_side: usize,
    flags: String,
    min_y: String,
    max_y: String,
    levels: Vec<GrassLevel>,
}

#[derive(Deserialize)]
struct GrassLevel {
    #[serde(flatten)]
    geometry: super::scene::LevelDescription,
}

/// Validated grass source data shared by the native probe and Studio projection.
pub struct Prepared {
    pub grid: Grid,
    pub meshes: [Mesh; 3],
}

impl Prepared {
    pub fn from_package(package: &PlantPackage) -> Result<Self, String> {
        let get = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Missing grass resource: {name}"))
        };
        let description: Description =
            serde_json::from_slice(get("grass.json")?).map_err(|error| error.to_string())?;
        if description.schema != "forge.tidewater.grass-probe"
            || description.version != 1
            || description.cells_per_side != SIDE
            || description.levels.len() != 3
        {
            return Err("Unsupported grass description".into());
        }
        let heights = |name: &str| -> Result<Vec<f32>, String> {
            let bytes = get(name)?;
            if bytes.len() != CELL_COUNT * size_of::<f32>() {
                return Err(format!("Invalid grass height payload: {name}"));
            }
            Ok(bytes
                .as_chunks::<4>()
                .0
                .iter()
                .map(|chunk| f32::from_le_bytes(*chunk))
                .collect())
        };
        let grid = Grid::new(
            get(&description.flags)?.to_vec(),
            heights(&description.min_y)?,
            heights(&description.max_y)?,
        )?;
        let meshes = description
            .levels
            .iter()
            .map(|level| {
                super::scene::mesh(&level.geometry, package.files())
                    .map_err(|error| error.to_string())
            })
            .collect::<Result<Vec<_>, _>>()?
            .try_into()
            .map_err(|_| "Invalid grass level count")?;
        Ok(Self { grid, meshes })
    }
}

/// Validated source grid retained independently from the current camera selection.
pub struct Grid {
    flags: Vec<u8>,
    min_y: Vec<f32>,
    max_y: Vec<f32>,
}

impl Grid {
    pub fn new(flags: Vec<u8>, min_y: Vec<f32>, max_y: Vec<f32>) -> Result<Self, String> {
        if flags.len() != CELL_COUNT || min_y.len() != CELL_COUNT || max_y.len() != CELL_COUNT {
            return Err("Invalid grass grid dimensions".into());
        }
        if flags.iter().any(|flag| *flag > 1)
            || min_y.iter().zip(&max_y).any(|(minimum, maximum)| {
                !minimum.is_finite() || !maximum.is_finite() || minimum > maximum
            })
        {
            return Err("Invalid grass grid values".into());
        }
        Ok(Self {
            flags,
            min_y,
            max_y,
        })
    }

    /// Reproduce source z/x traversal, distance/frustum tests, and three LOD budgets exactly.
    #[expect(
        clippy::cast_possible_truncation,
        reason = "Source cells are bounded to a 256 by 256 grid before conversion to f32"
    )]
    pub fn select(&self, view: CdlodView) -> Result<[Vec<[f32; 4]>; 3], String> {
        if !view.position.iter().all(|value| value.is_finite())
            || view.planes.iter().flatten().any(|value| !value.is_finite())
        {
            return Err("Invalid grass view".into());
        }
        let mut result: [Vec<[f32; 4]>; 3] = std::array::from_fn(|_| Vec::new());
        let [camera_x, _, camera_z] = view.position;
        let cell_bounds = |camera: f64| {
            let first = ((camera - 88.0 + 1024.0) / 8.0).floor().clamp(0.0, 255.0);
            let last = ((camera + 88.0 + 1024.0) / 8.0).floor().clamp(0.0, 255.0);
            // Clamped source grid indices are integral and in 0..=255.
            #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
            (first as usize, last as usize)
        };
        let raw_bounds = |camera: f64| {
            (
                ((camera - 88.0 + 1024.0) / 8.0).floor(),
                ((camera + 88.0 + 1024.0) / 8.0).floor(),
            )
        };
        let (raw_first_x, raw_last_x) = raw_bounds(camera_x);
        let (raw_first_z, raw_last_z) = raw_bounds(camera_z);
        if raw_first_x > 255.0 || raw_last_x < 0.0 || raw_first_z > 255.0 || raw_last_z < 0.0 {
            return Ok(std::array::from_fn(|_| Vec::new()));
        }
        let (first_x, last_x) = cell_bounds(camera_x);
        let (first_z, last_z) = cell_bounds(camera_z);
        for z in first_z..=last_z {
            for x in first_x..=last_x {
                let index = z * SIDE + x;
                if self.flags[index] == 0 {
                    continue;
                }
                let x0 = -1024.0
                    + f64::from(u16::try_from(x).map_err(|_| "Grass x index overflow")?) * 8.0;
                let z0 = -1024.0
                    + f64::from(u16::try_from(z).map_err(|_| "Grass z index overflow")?) * 8.0;
                let dx = (x0 - camera_x).max(0.0).max(camera_x - x0 - 8.0);
                let dz = (z0 - camera_z).max(0.0).max(camera_z - z0 - 8.0);
                let distance = dx.hypot(dz);
                if distance > 88.0 {
                    continue;
                }
                let lo = [x0, f64::from(self.min_y[index]) - 0.5, z0];
                let hi = [x0 + 8.0, f64::from(self.max_y[index]) + 1.8, z0 + 8.0];
                if view.planes.iter().any(|plane| {
                    (0..3)
                        .map(|axis| {
                            plane[axis]
                                * if plane[axis] >= 0.0 {
                                    hi[axis]
                                } else {
                                    lo[axis]
                                }
                        })
                        .sum::<f64>()
                        + plane[3]
                        < 0.0
                }) {
                    continue;
                }
                let level = if distance < 18.0 && result[0].len() < CAPACITIES[0] {
                    0
                } else if distance < 46.0 && result[1].len() < CAPACITIES[1] {
                    1
                } else if distance >= 46.0 && result[2].len() < CAPACITIES[2] {
                    2
                } else {
                    continue;
                };
                result[level].push([x0 as f32, z0 as f32, 0.0, 0.0]);
            }
        }
        Ok(result)
    }
}

struct Level {
    entities: Vec<Entity>,
    material: Handle<PlantMaterial>,
    buffer: Handle<ShaderBuffer>,
    cells: Vec<[f32; 4]>,
}

/// Fixed grass draw slots. Camera movement changes only storage contents and visibility.
pub struct Resident {
    levels: [Level; 3],
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct Report {
    pub counts: [usize; 3],
    pub changed: [bool; 3],
}

impl Resident {
    pub fn stage(
        world: &mut World,
        base: &PlantMaterial,
        meshes: [Mesh; 3],
    ) -> Result<Self, String> {
        if !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<PlantMaterial>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Grass asset stores unavailable".into());
        }

        // The shared vertex wrapper reads sourceModels[tag] even though the
        // grass generator places blades in world space. Every slot therefore
        // needs a valid identity matrix binding.
        let models = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![Mat4::IDENTITY; CAPACITIES[2]]));
        let mut levels = Vec::with_capacity(CAPACITIES.len());
        for (capacity, source_mesh) in CAPACITIES.into_iter().zip(meshes) {
            let buffer = world
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(ShaderBuffer::from(padded(&[], capacity)));
            let mut material = base.clone();
            material.kind = 3;
            material.instances = buffer.clone();
            material.models = models.clone();
            let material = world.resource_mut::<Assets<PlantMaterial>>().add(material);
            let mesh = world.resource_mut::<Assets<Mesh>>().add(source_mesh);
            let mut entities = Vec::with_capacity(capacity);
            for index in 0..capacity {
                let tag = u32::try_from(index).map_err(|_| "Grass slot index overflow")?;
                entities.push(
                    world
                        .spawn((
                            Mesh3d(mesh.clone()),
                            MeshMaterial3d(material.clone()),
                            MeshTag(tag),
                            Transform::IDENTITY,
                            NoFrustumCulling,
                            NotShadowCaster,
                            Visibility::Hidden,
                        ))
                        .id(),
                );
            }
            levels.push(Level {
                entities,
                material,
                buffer,
                cells: Vec::new(),
            });
        }
        let levels: [Level; 3] = levels.try_into().map_err(|_| "Invalid grass level count")?;
        Ok(Self { levels })
    }

    pub fn update(
        &mut self,
        world: &mut World,
        cells: &[Vec<[f32; 4]>; 3],
        visibility: Visibility,
    ) -> Result<Report, String> {
        for index in 0..CAPACITIES.len() {
            if cells[index].len() > CAPACITIES[index]
                || cells[index]
                    .iter()
                    .flatten()
                    .any(|value| !value.is_finite())
                || self.levels[index].entities.len() != CAPACITIES[index]
            {
                return Err("Invalid grass resident selection".into());
            }
        }
        if !world.contains_resource::<Assets<ShaderBuffer>>()
            || self.levels.iter().any(|level| {
                level
                    .entities
                    .iter()
                    .any(|entity| world.get_entity(*entity).is_err())
            })
        {
            return Err("Grass resident state unavailable".into());
        }
        {
            let buffers = world.resource::<Assets<ShaderBuffer>>();
            if self
                .levels
                .iter()
                .any(|level| buffers.get(&level.buffer).is_none())
            {
                return Err("Grass resident buffer unavailable".into());
            }
        }

        let changed = std::array::from_fn(|index| self.levels[index].cells != cells[index]);
        let report = Report {
            counts: std::array::from_fn(|index| cells[index].len()),
            changed,
        };
        for (index, level) in self.levels.iter_mut().enumerate() {
            if report.changed[index] {
                level.cells.clone_from(&cells[index]);
            }
        }
        {
            let mut buffers = world.resource_mut::<Assets<ShaderBuffer>>();
            for (index, level) in self.levels.iter().enumerate() {
                if report.changed[index] {
                    let mut buffer = buffers
                        .get_mut(&level.buffer)
                        .ok_or("Grass resident buffer unavailable")?;
                    buffer.set_data(padded(&cells[index], CAPACITIES[index]));
                }
            }
        }
        for (index, level) in self.levels.iter().enumerate() {
            for (slot, entity) in level.entities.iter().enumerate() {
                let desired = if slot < report.counts[index] {
                    visibility
                } else {
                    Visibility::Hidden
                };
                if world.get::<Visibility>(*entity) != Some(&desired) {
                    world.entity_mut(*entity).insert(desired);
                }
            }
        }
        Ok(report)
    }
    #[must_use]
    pub fn entities(&self) -> Vec<Entity> {
        self.levels
            .iter()
            .flat_map(|level| level.entities.iter().copied())
            .collect()
    }

    #[must_use]
    pub fn materials(&self) -> Vec<Handle<PlantMaterial>> {
        self.levels
            .iter()
            .map(|level| level.material.clone())
            .collect()
    }

    #[must_use]
    pub fn buffers(&self) -> Vec<Handle<ShaderBuffer>> {
        self.levels
            .iter()
            .map(|level| level.buffer.clone())
            .collect()
    }

    pub fn retire(self, world: &mut World) {
        for entity in self.entities() {
            world.despawn(entity);
        }
    }
}

fn padded(cells: &[[f32; 4]], capacity: usize) -> Vec<Record> {
    let mut records: Vec<_> = cells
        .iter()
        .copied()
        .map(|position| Record {
            i_pos: Vec4::from_array(position),
            i_dat: Vec4::ZERO,
        })
        .collect();
    records.resize(
        capacity,
        Record {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
        },
    );
    records
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, mesh::PrimitiveTopology};

    fn view(position: [f64; 3]) -> CdlodView {
        CdlodView {
            position,
            planes: [[0.0; 4]; 6],
        }
    }

    fn grid() -> Grid {
        Grid::new(
            vec![1; CELL_COUNT],
            vec![0.0; CELL_COUNT],
            vec![0.0; CELL_COUNT],
        )
        .expect("test grid")
    }

    #[test]
    fn source_selection_preserves_ordered_membership_and_lod_caps() {
        let grid = grid();
        let first = grid.select(view([0.0; 3])).expect("first selection");
        let second = grid
            .select(view([320.0, 0.0, 320.0]))
            .expect("second selection");
        let again = grid.select(view([0.0; 3])).expect("again selection");
        assert_eq!(first, again);
        assert_ne!(first, second);
        assert!(first.iter().any(|cells| !cells.is_empty()));
        assert!(
            first
                .iter()
                .zip(CAPACITIES)
                .all(|(cells, capacity)| cells.len() <= capacity)
        );
    }

    fn flagged(cells: &[(usize, usize)]) -> Grid {
        let mut flags = vec![0; CELL_COUNT];
        for (x, z) in cells {
            flags[z * SIDE + x] = 1;
        }
        Grid::new(flags, vec![0.0; CELL_COUNT], vec![0.0; CELL_COUNT]).expect("flagged grid")
    }

    #[test]
    fn source_bounds_exclude_the_left_tangent_cell_but_keep_the_next_cell() {
        let selected = flagged(&[(116, 128), (117, 128)])
            .select(view([0.0; 3]))
            .expect("bounded selection");
        let cells: Vec<_> = selected.into_iter().flatten().collect();
        assert!(!cells.contains(&[-96.0, 0.0, 0.0, 0.0]));
        assert!(cells.contains(&[-88.0, 0.0, 0.0, 0.0]));
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "The boundary cell coordinate is an exact integer-valued source sentinel."
    )]
    fn source_bounds_keep_the_negative_edge_and_exclude_the_positive_edge() {
        let grid = flagged(&[(0, 128), (255, 128)]);
        let left = grid
            .select(view([-1112.0, 0.0, 0.0]))
            .expect("negative edge");
        let right = grid
            .select(view([1112.0, 0.0, 0.0]))
            .expect("positive edge");
        assert!(left.into_iter().flatten().any(|cell| cell[0] == -1024.0));
        assert!(right.into_iter().flatten().next().is_none());
    }

    #[test]
    fn source_frustum_and_nonfinite_views_are_rejected() {
        let grid = flagged(&[(117, 128)]);
        let mut clipped = view([0.0; 3]);
        clipped.planes[0] = [1.0, 0.0, 0.0, 0.0];
        assert!(
            grid.select(clipped)
                .expect("clipped selection")
                .into_iter()
                .flatten()
                .next()
                .is_none()
        );
        assert!(grid.select(view([f64::NAN, 0.0, 0.0])).is_err());
    }
    #[test]
    fn grid_rejects_invalid_inputs() {
        assert!(
            Grid::new(
                vec![0; CELL_COUNT - 1],
                vec![0.0; CELL_COUNT],
                vec![0.0; CELL_COUNT]
            )
            .is_err()
        );
        assert!(
            Grid::new(
                vec![2; CELL_COUNT],
                vec![0.0; CELL_COUNT],
                vec![0.0; CELL_COUNT]
            )
            .is_err()
        );
        assert!(
            Grid::new(
                vec![0; CELL_COUNT],
                vec![f32::NAN; CELL_COUNT],
                vec![0.0; CELL_COUNT]
            )
            .is_err()
        );
        assert!(
            Grid::new(
                vec![0; CELL_COUNT],
                vec![1.0; CELL_COUNT],
                vec![0.0; CELL_COUNT]
            )
            .is_err()
        );
    }

    fn material() -> PlantMaterial {
        PlantMaterial {
            params: super::super::Params {
                i_pos: Vec4::ZERO,
                i_dat: Vec4::ZERO,
                wind: Vec4::ZERO,
                gust_lod: Vec4::ZERO,
                sun_night: Vec4::ZERO,
                lod_end_frame: Vec4::ZERO,
            },
            detail: default(),
            leaf: default(),
            impostor_a: default(),
            impostor_b: default(),
            instances: default(),
            models: default(),
            motion: default(),
            environment: default(),
            hill_shadow: default(),
            object_shadow: default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            object_settings: default(),
            live_shadows: default(),
            grass_heights: default(),
            grass_mask: default(),
            kind: 0,
        }
    }

    fn meshes() -> [Mesh; 3] {
        std::array::from_fn(|_| {
            Mesh::new(
                PrimitiveTopology::TriangleList,
                RenderAssetUsages::default(),
            )
        })
    }

    fn world() -> World {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<PlantMaterial>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        world
    }

    #[test]
    fn resident_keeps_assets_handles_and_entities_across_a_b_a_updates() {
        let mut world = world();
        let mut resident =
            Resident::stage(&mut world, &material(), meshes()).expect("stage resident");
        let entities = resident.entities();
        let materials = resident.materials();
        let buffers = resident.buffers();
        let models = world
            .resource::<Assets<PlantMaterial>>()
            .get(&materials[0])
            .expect("grass material")
            .models
            .clone();
        assert!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&models)
                .is_some()
        );
        assert!(materials.iter().all(|handle| {
            world
                .resource::<Assets<PlantMaterial>>()
                .get(handle)
                .is_some_and(|material| material.models.id() == models.id())
        }));
        let assets = (
            world.resource::<Assets<Mesh>>().len(),
            world.resource::<Assets<PlantMaterial>>().len(),
            world.resource::<Assets<ShaderBuffer>>().len(),
        );
        let a = [vec![[0.0; 4]], Vec::new(), Vec::new()];
        let b = [Vec::new(), vec![[24.0, 8.0, 0.0, 0.0]], Vec::new()];
        assert_eq!(
            resident
                .update(&mut world, &a, Visibility::Visible)
                .expect("A")
                .counts,
            [1, 0, 0]
        );
        assert_eq!(
            resident
                .update(&mut world, &b, Visibility::Visible)
                .expect("B")
                .counts,
            [0, 1, 0]
        );
        assert_eq!(
            resident
                .update(&mut world, &a, Visibility::Visible)
                .expect("A again")
                .counts,
            [1, 0, 0]
        );
        assert_eq!(resident.entities(), entities);
        assert_eq!(resident.materials(), materials);
        assert_eq!(resident.buffers(), buffers);
        assert_eq!(
            (
                world.resource::<Assets<Mesh>>().len(),
                world.resource::<Assets<PlantMaterial>>().len(),
                world.resource::<Assets<ShaderBuffer>>().len(),
            ),
            assets
        );
    }

    #[test]
    fn resident_hides_empty_slots_and_rejects_invalid_updates_without_mutation() {
        let mut world = world();
        let mut resident =
            Resident::stage(&mut world, &material(), meshes()).expect("stage resident");
        let handles = resident.buffers();
        resident
            .update(
                &mut world,
                &[Vec::new(), Vec::new(), Vec::new()],
                Visibility::Visible,
            )
            .expect("empty selection");
        assert!(
            resident
                .entities()
                .iter()
                .all(|entity| { world.get::<Visibility>(*entity) == Some(&Visibility::Hidden) })
        );
        assert!(
            resident
                .update(
                    &mut world,
                    &[vec![[f32::NAN; 4]], Vec::new(), Vec::new()],
                    Visibility::Visible,
                )
                .is_err()
        );
        assert_eq!(resident.buffers(), handles);
    }
}
