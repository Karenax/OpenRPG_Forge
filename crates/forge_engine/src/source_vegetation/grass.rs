// SPDX-License-Identifier: MIT
// Adapted from Tidewater GrassField.js selection, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see third_party/tidewater/LICENSE.
//! Source-compatible grass cell selection and bounded, reusable GPU residency.
use super::{Record, captured::CapturedPlantMaterial, world::SourceMaterial};
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, mesh::MeshTag, prelude::*,
    render::storage::ShaderBuffer,
};
use forge_assets::source_vegetation_package::PlantPackage;
use forge_world::terrain_lod::CdlodView;

const SIDE: usize = 256;
const CELL_COUNT: usize = SIDE * SIDE;
const CAPACITIES: [usize; 3] = [48, 160, 420];
pub mod fields;
#[cfg(feature = "physics-rapier")]
pub mod host;

/// The playable reference grass view cadence, shared across scene hosts.
/// Record only after the selected cells have been published successfully.
#[derive(Clone, Copy, Debug, Default)]
pub struct ViewCadence {
    previous: Option<(Entity, Transform, [u32; 4])>,
}

impl ViewCadence {
    #[must_use]
    pub fn needs_refresh(
        &self,
        camera: Entity,
        transform: &Transform,
        projection: &PerspectiveProjection,
    ) -> bool {
        let lens = [
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ]
        .map(f32::to_bits);
        !self
            .previous
            .is_some_and(|(previous_camera, old, old_lens)| {
                previous_camera == camera
                    && old_lens == lens
                    // An invalid pose must reach source_view's existing refusal,
                    // even if its forward direction appears unchanged.
                    && transform.scale.is_finite()
                    && (transform.scale - Vec3::ONE).abs().max_element() <= 8.0 * f32::EPSILON
                    && transform.rotation.is_finite()
                    && transform.rotation.length_squared() != 0.0
                    && (transform.translation - old.translation)
                        .abs()
                        .cmplt(Vec3::splat(0.05))
                        .all()
                    && (transform.rotation * Vec3::Z - old.rotation * Vec3::Z)
                        .abs()
                        .cmplt(Vec3::splat(1.0e-3))
                        .all()
                    && (transform.rotation * Vec3::Y - old.rotation * Vec3::Y)
                        .abs()
                        .cmplt(Vec3::splat(1.0e-3))
                        .all()
            })
    }

    pub fn record(
        &mut self,
        camera: Entity,
        transform: &Transform,
        projection: &PerspectiveProjection,
    ) {
        let lens = [
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ]
        .map(f32::to_bits);
        self.previous = Some((camera, *transform, lens));
    }
}

/// Grass has field inputs independent from ordinary plant instance records.
pub trait GrassMaterial: SourceMaterial {
    fn validate_grass(&self, _world: &World) -> Result<(), String> {
        Ok(())
    }
    fn grass_buffers(&self) -> (&Handle<ShaderBuffer>, &Handle<ShaderBuffer>);
    fn same_grass_inputs(&self, other: &Self) -> bool;
}

impl GrassMaterial for CapturedPlantMaterial {
    fn grass_buffers(&self) -> (&Handle<ShaderBuffer>, &Handle<ShaderBuffer>) {
        (&self.instances, &self.models)
    }
    fn same_grass_inputs(&self, other: &Self) -> bool {
        self.detail == other.detail
            && self.grass_heights == other.grass_heights
            && self.grass_mask == other.grass_mask
    }
}

/// Validated grass source data shared by the native probe and Studio projection.
pub struct Prepared {
    pub grid: Grid,
    pub meshes: [Mesh; 3],
}

impl Prepared {
    pub fn from_package(package: &PlantPackage) -> Result<Self, String> {
        let decoded = package.grass()?;
        let grid = Grid::new(decoded.flags, decoded.min_y, decoded.max_y)?;
        let meshes = decoded.meshes.map(super::scene::mesh_from_geometry);
        Ok(Self { grid, meshes })
    }
}

/// Validated source grid retained independently from the current camera selection.
pub struct Grid {
    origin: [f64; 2],
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
            origin: [-1024.0; 2],
            flags,
            min_y,
            max_y,
        })
    }

    /// Translate the source-profile grid with its admitted field, retaining 8 m cells.
    #[allow(clippy::cast_possible_truncation)] // Explicit finite/precision check before storing the origin.
    pub fn with_origin(mut self, origin: [f64; 2]) -> Result<Self, String> {
        if origin.iter().any(|&value| {
            !value.is_finite()
                || !(value as f32).is_finite()
                || value as f32 + 8.0 <= value as f32
                || !(value + 2048.0).is_finite()
        }) {
            return Err("Invalid grass field origin or precision".into());
        }
        self.origin = origin;
        Ok(self)
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
        let cell_bounds = |camera: f64, origin: f64| {
            let first = ((camera - 88.0 - origin) / 8.0).floor().clamp(0.0, 255.0);
            let last = ((camera + 88.0 - origin) / 8.0).floor().clamp(0.0, 255.0);
            // Clamped source grid indices are integral and in 0..=255.
            #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
            (first as usize, last as usize)
        };
        let raw_bounds = |camera: f64, origin: f64| {
            (
                ((camera - 88.0 - origin) / 8.0).floor(),
                ((camera + 88.0 - origin) / 8.0).floor(),
            )
        };
        let (raw_first_x, raw_last_x) = raw_bounds(camera_x, self.origin[0]);
        let (raw_first_z, raw_last_z) = raw_bounds(camera_z, self.origin[1]);
        if raw_first_x > 255.0 || raw_last_x < 0.0 || raw_first_z > 255.0 || raw_last_z < 0.0 {
            return Ok(std::array::from_fn(|_| Vec::new()));
        }
        let (first_x, last_x) = cell_bounds(camera_x, self.origin[0]);
        let (first_z, last_z) = cell_bounds(camera_z, self.origin[1]);
        for z in first_z..=last_z {
            for x in first_x..=last_x {
                let index = z * SIDE + x;
                if self.flags[index] == 0 {
                    continue;
                }
                let x0 = self.origin[0]
                    + f64::from(u16::try_from(x).map_err(|_| "Grass x index overflow")?) * 8.0;
                let z0 = self.origin[1]
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

struct Level<M: GrassMaterial> {
    mesh: Handle<Mesh>,
    entities: Vec<Entity>,
    material: Handle<M>,
    buffer: Handle<ShaderBuffer>,
    cells: Vec<[f32; 4]>,
    data: Vec<u8>,
}

/// Fixed grass draw slots. Camera movement changes only storage contents and visibility.
pub struct Resident<M: GrassMaterial = CapturedPlantMaterial> {
    levels: [Level<M>; 3],
    models: Handle<ShaderBuffer>,
    base: M,
    models_data: Vec<u8>,
    visibility: Visibility,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct Report {
    pub counts: [usize; 3],
    pub changed: [bool; 3],
}

impl<M: GrassMaterial> Resident<M> {
    pub fn stage(world: &mut World, base: &M, meshes: [Mesh; 3]) -> Result<Self, String> {
        if !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<M>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Grass asset stores unavailable".into());
        }
        base.validate_grass(world)?;

        // The shared vertex wrapper reads sourceModels[tag] even though the
        // grass generator places blades in world space. Every slot therefore
        // needs a valid identity matrix binding.
        let models_buffer = ShaderBuffer::from(vec![Mat4::IDENTITY; CAPACITIES[2]]);
        let models_data = models_buffer
            .data
            .clone()
            .ok_or("Grass identity model payload unavailable")?;
        let models = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(models_buffer);
        let mut levels = Vec::with_capacity(CAPACITIES.len());
        for (capacity, source_mesh) in CAPACITIES.into_iter().zip(meshes) {
            let storage = ShaderBuffer::from(padded(&[], capacity));
            let data = storage
                .data
                .clone()
                .ok_or("Grass instance payload unavailable")?;
            let buffer = world.resource_mut::<Assets<ShaderBuffer>>().add(storage);
            let mut material = base.clone();
            material.set_source(3, buffer.clone(), models.clone());
            let material = world.resource_mut::<Assets<M>>().add(material);
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
                mesh,
                entities,
                material,
                buffer,
                cells: Vec::new(),
                data,
            });
        }
        let levels: [Level<M>; 3] = levels.try_into().map_err(|_| "Invalid grass level count")?;
        Ok(Self {
            levels,
            models,
            base: base.clone(),
            models_data,
            visibility: Visibility::Hidden,
        })
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
            for (index, level) in self.levels.iter_mut().enumerate() {
                if report.changed[index] {
                    let mut buffer = buffers
                        .get_mut(&level.buffer)
                        .ok_or("Grass resident buffer unavailable")?;
                    buffer.set_data(padded(&cells[index], CAPACITIES[index]));
                    level.data.clone_from(
                        buffer
                            .data
                            .as_ref()
                            .ok_or("Grass instance payload unavailable")?,
                    );
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
        self.visibility = visibility;
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
    pub fn materials(&self) -> Vec<Handle<M>> {
        self.levels
            .iter()
            .map(|level| level.material.clone())
            .collect()
    }

    #[must_use]
    pub fn handles(&self) -> (Vec<Handle<Mesh>>, Vec<Handle<M>>) {
        (
            self.levels.iter().map(|level| level.mesh.clone()).collect(),
            self.materials(),
        )
    }

    #[must_use]
    pub fn intact(&self, world: &World) -> bool {
        let Some(buffers) = world.get_resource::<Assets<ShaderBuffer>>() else {
            return false;
        };
        let Some(materials) = world.get_resource::<Assets<M>>() else {
            return false;
        };
        let Some(meshes) = world.get_resource::<Assets<Mesh>>() else {
            return false;
        };
        if buffers
            .get(&self.models)
            .and_then(|buffer| buffer.data.as_ref())
            != Some(&self.models_data)
        {
            return false;
        }
        self.levels.iter().all(|level| {
            buffers
                .get(&level.buffer)
                .and_then(|buffer| buffer.data.as_ref())
                == Some(&level.data)
                && meshes.contains(level.mesh.id())
                && materials.get(&level.material).is_some_and(|material| {
                    material.kind() == 3
                        && material.same_grass_inputs(&self.base)
                        && material.grass_buffers() == (&level.buffer, &self.models)
                })
                && level.entities.iter().enumerate().all(|(index, &entity)| {
                    world
                        .get::<Mesh3d>(entity)
                        .is_some_and(|mesh| mesh.0 == level.mesh)
                        && world
                            .get::<MeshMaterial3d<M>>(entity)
                            .is_some_and(|mat| mat.0 == level.material)
                        && world
                            .get::<MeshTag>(entity)
                            .is_some_and(|tag| usize::try_from(tag.0) == Ok(index))
                        && world.get::<Transform>(entity) == Some(&Transform::IDENTITY)
                        && world.get::<ChildOf>(entity).is_none()
                        && world.get::<NoFrustumCulling>(entity).is_some()
                        && world.get::<NotShadowCaster>(entity).is_some()
                        && world.get::<Visibility>(entity)
                            == Some(&if index < level.cells.len() {
                                self.visibility
                            } else {
                                Visibility::Hidden
                            })
                })
        })
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

    #[test]
    fn playable_view_cadence_reuses_small_camera_moves_and_refreshes_changed_views() {
        let camera = Entity::from_raw_u32(1).unwrap();
        let replacement = Entity::from_raw_u32(2).unwrap();
        let original = Transform::IDENTITY;
        let projection = PerspectiveProjection::default();
        let mut cadence = ViewCadence::default();
        assert!(cadence.needs_refresh(camera, &original, &projection));
        cadence.record(camera, &original, &projection);
        let small =
            Transform::from_xyz(0.04, 0.0, 0.0).with_rotation(Quat::from_rotation_y(0.0005));
        assert!(!cadence.needs_refresh(camera, &small, &projection));
        let moved = Transform::from_xyz(0.05, 0.0, 0.0);
        assert!(cadence.needs_refresh(camera, &moved, &projection));
        let rolled = Transform::IDENTITY.with_rotation(Quat::from_rotation_z(0.1));
        assert!(cadence.needs_refresh(camera, &rolled, &projection));
        let scaled = Transform::IDENTITY.with_scale(Vec3::splat(2.0));
        assert!(cadence.needs_refresh(camera, &scaled, &projection));
        assert!(cadence.needs_refresh(replacement, &original, &projection));
        let changed_lens = PerspectiveProjection {
            far: projection.far + 1.0,
            ..projection.clone()
        };
        assert!(cadence.needs_refresh(camera, &original, &changed_lens));
    }

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

    #[test]
    fn translated_fields_keep_the_same_source_membership() {
        let original = grid().select(view([0.0; 3])).unwrap();
        let shifted = grid()
            .with_origin([-768.0, -1536.0])
            .unwrap()
            .select(view([256.0, 0.0, -512.0]))
            .unwrap();
        for (original, shifted) in original.into_iter().zip(shifted) {
            let restored: Vec<_> = shifted
                .into_iter()
                .map(|cell| [cell[0] - 256.0, cell[1] + 512.0, cell[2], cell[3]])
                .collect();
            assert_eq!(original, restored);
        }
        assert!(grid().with_origin([f64::INFINITY, 0.0]).is_err());
        assert!(grid().with_origin([f64::MAX, 0.0]).is_err());
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

    fn material() -> CapturedPlantMaterial {
        CapturedPlantMaterial {
            scene_lighting: crate::source_lighting::scene_buffer::HANDLE.clone(),
            params: crate::source_vegetation::Params {
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
            local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
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
        world.init_resource::<Assets<CapturedPlantMaterial>>();
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
            .resource::<Assets<CapturedPlantMaterial>>()
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
                .resource::<Assets<CapturedPlantMaterial>>()
                .get(handle)
                .is_some_and(|material| material.models.id() == models.id())
        }));
        let assets = (
            world.resource::<Assets<Mesh>>().len(),
            world.resource::<Assets<CapturedPlantMaterial>>().len(),
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
                world.resource::<Assets<CapturedPlantMaterial>>().len(),
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

    #[test]
    fn owned_grass_rejects_changed_slots_visibility_and_cpu_payloads() {
        let mut world = world();
        let mut resident = Resident::stage(&mut world, &material(), meshes()).unwrap();
        resident
            .update(
                &mut world,
                &[vec![[0.0; 4]], Vec::new(), Vec::new()],
                Visibility::Visible,
            )
            .unwrap();
        assert!(resident.intact(&world));
        let entity = resident.entities()[0];
        world.entity_mut(entity).insert(MeshTag(17));
        assert!(!resident.intact(&world));
        world.entity_mut(entity).insert(MeshTag(0));
        assert!(resident.intact(&world));
        world.entity_mut(entity).insert(Visibility::Hidden);
        assert!(!resident.intact(&world));
        world.entity_mut(entity).insert(Visibility::Visible);
        assert!(resident.intact(&world));
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .get_mut(&resident.buffers()[0])
            .unwrap()
            .set_data(vec![Vec4::ZERO]);
        assert!(!resident.intact(&world));
    }
}
