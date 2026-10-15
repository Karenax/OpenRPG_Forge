//! Source pier geometry and Rapier volumes in an editable Bevy scene frame.
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementPart,
    ManualPlacementProjection, tidewater_material::VillageMaterial,
    tidewater_mesh::VillageMeshBytes,
};
use bevy::{mesh::VertexAttributeValues, prelude::*};
use bevy_rapier3d::prelude::{Collider, Sensor};
use forge_assets::tidewater_pier::{self as pier, PierPackage};
use forge_core::{
    AssetId, EntityId, Transform as ForgeTransform, UnitQuaternion, Vec3 as ForgeVec3,
};
use forge_project::zone_authoring::placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement};
use serde::Deserialize;
use std::collections::BTreeMap;

#[derive(Deserialize)]
struct Description {
    pier: SourcePier,
}

#[derive(Deserialize)]
struct SourcePier {
    source_id: String,
    source_frame: SourceFrame,
    static_parts: Vec<PartSource>,
    sign: SourceGroup,
    lanterns: Vec<SourceGroup>,
    colliders: SourceColliders,
}

#[derive(Clone, Copy, Deserialize)]
struct SourceFrame {
    translation_m: [f64; 3],
    rotation_xyzw: [f64; 4],
    scale: [f64; 3],
}

#[derive(Deserialize)]
struct PartSource {
    source_role: String,
    material: String,
    attributes: BTreeMap<String, FileReference>,
    indices: FileReference,
}

#[derive(Deserialize)]
struct FileReference {
    file: String,
}

#[derive(Deserialize)]
struct SourceGroup {
    pivot: [f64; 3],
    parts: Vec<PartSource>,
}

#[derive(Deserialize)]
struct SourceColliders {
    boxes: Vec<SourceBox>,
    cylinders: Vec<SourceCylinder>,
}

#[derive(Deserialize)]
struct SourceBox {
    center: [f64; 3],
    half: [f64; 3],
    yaw: f64,
    walkable: bool,
    solid: bool,
    tag: String,
}

#[derive(Deserialize)]
struct SourceCylinder {
    x: f64,
    z: f64,
    radius: f64,
    #[serde(rename = "yMin")]
    y_min: f64,
    #[serde(rename = "yMax")]
    y_max: f64,
    tag: String,
}

struct PreparedPart {
    mesh: Mesh,
    material: String,
}

struct PreparedGroup {
    pivot: Vec3,
    parts: Vec<PreparedPart>,
}

struct PreparedCollider {
    transform: Transform,
    shape: Collider,
    sensor: bool,
}

/// CPU projection of all 28 pinned parts and 215 source collider records.
pub struct PreparedPier {
    source_id: String,
    frame: Transform,
    static_parts: Vec<PreparedPart>,
    sign: PreparedGroup,
    lanterns: Vec<PreparedGroup>,
    colliders: Vec<PreparedCollider>,
    triangles: usize,
    boxes: usize,
    cylinders: usize,
    ladder_sensors: usize,
}

#[derive(Clone)]
pub struct PierTemplate {
    pub source_id: String,
    pub asset_id: AssetId,
    pub transform: ForgeTransform,
}

#[derive(Component)]
pub struct ImportedPier(pub String);

#[derive(Component)]
pub struct PierSwingPivot;

#[derive(Component)]
pub struct PierLanternPivot(pub usize);

#[derive(Resource)]
pub struct ResidentPier {
    roots: Vec<Entity>,
    parts: Vec<Entity>,
    meshes: Vec<Handle<Mesh>>,
    colliders: Vec<Entity>,
    sensors: Vec<Entity>,
    pivots: Vec<Entity>,
    authored: Vec<(Entity, EntityId, ForgeTransform)>,
    pub triangles: usize,
    pub boxes: usize,
    pub cylinders: usize,
    pub ladder_sensors: usize,
}

impl PreparedPier {
    #[allow(
        clippy::too_many_lines,
        reason = "Validates and prepares one pinned geometry, collider, and provenance contract in source order."
    )]
    pub fn from_package(package: &PierPackage) -> Result<Self, String> {
        let source: Description =
            serde_json::from_slice(package.description()).map_err(|error| error.to_string())?;
        let record = source.pier;
        if record.source_id != "tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:pier:0"
            || record.static_parts.len() != 4
            || record.sign.parts.len() != 2
            || record.lanterns.len() != 11
        {
            return Err("Pinned source pier parts differ".into());
        }
        let frame = source_frame(record.source_frame)?;
        let mut triangles = 0;
        let static_parts = prepare_parts(
            package,
            record.static_parts,
            "static",
            Vec3::ZERO,
            &mut triangles,
        )?;
        let sign_pivot = vec3(record.sign.pivot)?;
        let sign = PreparedGroup {
            pivot: sign_pivot,
            parts: prepare_parts(
                package,
                record.sign.parts,
                "swinging-sign",
                sign_pivot,
                &mut triangles,
            )?,
        };
        let mut lanterns = Vec::with_capacity(11);
        for group in record.lanterns {
            let pivot = vec3(group.pivot)?;
            lanterns.push(PreparedGroup {
                pivot,
                parts: prepare_parts(
                    package,
                    group.parts,
                    "hanging-lantern",
                    pivot,
                    &mut triangles,
                )?,
            });
        }
        let boxes = record.colliders.boxes.len();
        let cylinders = record.colliders.cylinders.len();
        let mut ladder_sensors = 0;
        let mut colliders = Vec::with_capacity(boxes + cylinders);
        for source in record.colliders.boxes {
            let center = vec3(source.center)?;
            let half = vec3(source.half)?;
            let yaw = finite(source.yaw)?;
            if half.min_element() <= 0.0 || half.max_element() > 64.0 {
                return Err("Invalid pier box dimensions".into());
            }
            let sensor = !source.solid;
            if sensor {
                if source.tag != "ladder" || !source.walkable {
                    return Err("Unexpected non-solid pier volume".into());
                }
                ladder_sensors += 1;
            }
            colliders.push(PreparedCollider {
                transform: Transform::from_translation(center),
                shape: crate::physics::yawed_box_collider(half, yaw)?,
                sensor,
            });
        }
        for source in record.colliders.cylinders {
            let (x, z, radius, y_min, y_max) = (
                finite(source.x)?,
                finite(source.z)?,
                finite(source.radius)?,
                finite(source.y_min)?,
                finite(source.y_max)?,
            );
            if !matches!(
                source.tag.as_str(),
                "pile" | "lampPost" | "bollard" | "barrel"
            ) || radius <= 0.0
                || radius > 1.0
                || y_max <= y_min
                || y_max - y_min > 20.0
            {
                return Err("Invalid pier cylinder".into());
            }
            colliders.push(PreparedCollider {
                transform: Transform::from_xyz(x, f32::midpoint(y_min, y_max), z),
                shape: Collider::cylinder((y_max - y_min) * 0.5, radius),
                sensor: false,
            });
        }
        if (triangles, boxes, cylinders, ladder_sensors) != (36_194, 122, 93, 8) {
            return Err("Pinned pier totals differ".into());
        }
        Ok(Self {
            source_id: record.source_id,
            frame,
            static_parts,
            sign,
            lanterns,
            colliders,
            triangles,
            boxes,
            cylinders,
            ladder_sensors,
        })
    }

    #[must_use]
    pub fn template(&self) -> PierTemplate {
        PierTemplate {
            source_id: self.source_id.clone(),
            asset_id: pier::asset_id(),
            transform: forge_transform(self.frame),
        }
    }

    pub fn stage(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
    ) -> Result<ResidentPier, String> {
        self.stage_instances(world, materials, None)
    }

    pub fn stage_authored(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentPier, String> {
        self.stage_instances(world, materials, Some(placements))
    }

    #[allow(
        clippy::too_many_lines,
        reason = "Publishes one validated placement batch with its render and physics identities."
    )]
    fn stage_instances(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
        placements: Option<&[ManualPlacement]>,
    ) -> Result<ResidentPier, String> {
        let source_instances = placements.is_none();
        let placements = placements.unwrap_or_default();
        if placements.len() > MAX_MANUAL_PLACEMENTS {
            return Err("Pier placement limit exceeded".into());
        }
        for placement in placements {
            if placement.asset_id != pier::asset_id()
                || placement.asset_hash != pier::package_hash()
            {
                return Err("Authored pier reference differs".into());
            }
            placement
                .transform
                .validate()
                .map_err(|error| error.to_string())?;
        }
        for part in self
            .static_parts
            .iter()
            .chain(self.sign.parts.iter())
            .chain(self.lanterns.iter().flat_map(|group| group.parts.iter()))
        {
            if (source_instances || !placements.is_empty())
                && !materials.contains_key(&part.material)
            {
                return Err("Pier material is not resident".into());
            }
        }
        let entrance = entrance_walk_ramp()?;
        let mut resident = ResidentPier {
            roots: Vec::new(),
            parts: Vec::new(),
            meshes: Vec::new(),
            colliders: Vec::new(),
            sensors: Vec::new(),
            pivots: Vec::new(),
            authored: Vec::new(),
            triangles: 0,
            boxes: 0,
            cylinders: 0,
            ladder_sensors: 0,
        };
        let instances: Vec<Option<&ManualPlacement>> = if source_instances {
            vec![None]
        } else {
            placements.iter().map(Some).collect()
        };
        let meshes = if instances.is_empty() {
            Vec::new()
        } else {
            self.static_parts
                .iter()
                .chain(self.sign.parts.iter())
                .chain(self.lanterns.iter().flat_map(|group| group.parts.iter()))
                .map(|part| world.resource_mut::<Assets<Mesh>>().add(part.mesh.clone()))
                .collect::<Vec<_>>()
        };
        resident.meshes.clone_from(&meshes);
        for placement in instances {
            let frame = placement.map_or(self.frame, |value| project(value.transform));
            let root = world
                .spawn((
                    Name::new(self.source_id.clone()),
                    ImportedPier(self.source_id.clone()),
                    frame,
                    Visibility::Inherited,
                ))
                .id();
            resident.roots.push(root);
            if let Some(placement) = placement {
                resident
                    .authored
                    .push((root, placement.id, placement.transform));
            }
            let mut part_meshes = meshes.iter();
            for (part, mesh) in self.static_parts.iter().zip(part_meshes.by_ref()) {
                stage_part(world, &mut resident, root, part, mesh, placement, materials);
            }
            let sign = world
                .spawn((
                    Name::new("Tidewater pier sign pivot"),
                    PierSwingPivot,
                    Transform::from_translation(self.sign.pivot),
                    Visibility::Inherited,
                    ChildOf(root),
                ))
                .id();
            resident.pivots.push(sign);
            for (part, mesh) in self.sign.parts.iter().zip(part_meshes.by_ref()) {
                stage_part(world, &mut resident, sign, part, mesh, placement, materials);
            }
            for (ordinal, group) in self.lanterns.iter().enumerate() {
                let pivot = world
                    .spawn((
                        Name::new(format!("Tidewater pier lantern {ordinal}")),
                        PierLanternPivot(ordinal),
                        Transform::from_translation(group.pivot),
                        Visibility::Inherited,
                        ChildOf(root),
                    ))
                    .id();
                resident.pivots.push(pivot);
                for (part, mesh) in group.parts.iter().zip(part_meshes.by_ref()) {
                    stage_part(
                        world,
                        &mut resident,
                        pivot,
                        part,
                        mesh,
                        placement,
                        materials,
                    );
                }
            }
            let first_collider = resident.colliders.len();
            for collider in &self.colliders {
                let mut entity =
                    world.spawn((collider.shape.clone(), collider.transform, ChildOf(root)));
                if collider.sensor {
                    entity.insert(Sensor);
                    resident.sensors.push(entity.id());
                } else {
                    if let Some(placement) = placement {
                        entity.insert(ManualPlacementCollision(placement.id));
                    }
                    resident.colliders.push(entity.id());
                }
            }
            // The three source entrance steps are only 0.32 m apart. Keep
            // their exact collision records, but bridge their top faces for
            // Rapier's wider character capsule with one derived walk surface.
            let mut ramp = world.spawn((
                Name::new("Tidewater pier entrance walk support"),
                entrance.shape.clone(),
                entrance.transform,
                ChildOf(root),
            ));
            if let Some(placement) = placement {
                ramp.insert(ManualPlacementCollision(placement.id));
            }
            resident.colliders.push(ramp.id());
            if let Some(placement) = placement {
                world.entity_mut(root).insert((
                    ManualPlacementProjection(placement.clone()),
                    ManualPlacementCollisionExpected {
                        placement: placement.id,
                        primitives: resident.colliders[first_collider..].to_vec(),
                    },
                ));
            }
            resident.boxes += self.boxes;
            resident.cylinders += self.cylinders;
            resident.ladder_sensors += self.ladder_sensors;
        }
        if source_instances
            && (resident.triangles, resident.boxes, resident.cylinders)
                != (self.triangles, self.boxes, self.cylinders)
        {
            return Err("Staged pier differs from pinned source totals".into());
        }
        Ok(resident)
    }
}

fn entrance_walk_ramp() -> Result<PreparedCollider, String> {
    // Local to the pinned source frame (55, 0, -64). Its upper face clears the
    // front edge of each source step by 1.5-2.3 cm, then joins the 2.30 m deck.
    // The lower edge meets the sampled beach; no source mesh is modified.
    let points = [
        Vec3::new(-1.15, 1.10, -1.20),
        Vec3::new(1.15, 1.10, -1.20),
        Vec3::new(-1.15, 1.10, 0.05),
        Vec3::new(1.15, 1.10, 0.05),
        Vec3::new(-1.15, 1.67, -1.20),
        Vec3::new(1.15, 1.67, -1.20),
        Vec3::new(-1.15, 2.34, 0.05),
        Vec3::new(1.15, 2.34, 0.05),
    ];
    Ok(PreparedCollider {
        transform: Transform::IDENTITY,
        shape: Collider::convex_hull(&points).ok_or("Pier entrance support is not convex")?,
        sensor: false,
    })
}

fn prepare_parts(
    package: &PierPackage,
    parts: Vec<PartSource>,
    role: &str,
    pivot: Vec3,
    triangles: &mut usize,
) -> Result<Vec<PreparedPart>, String> {
    let mut prepared = Vec::with_capacity(parts.len());
    for part in parts {
        if part.source_role != role {
            return Err("Pier source part role differs".into());
        }
        let file = |reference: &FileReference| {
            package
                .file(&reference.file)
                .ok_or("Pier mesh payload absent")
        };
        let attr = |name: &str| {
            part.attributes
                .get(name)
                .ok_or("Pier mesh attribute absent")
                .and_then(&file)
        };
        let mut mesh = VillageMeshBytes {
            positions: attr("pos")?,
            normals: attr("nrm")?,
            uv: attr("uv")?,
            tint: attr("tint")?,
            data: attr("data")?,
            indices: file(&part.indices)?,
        }
        .into_mesh()?;
        if pivot != Vec3::ZERO {
            let Some(VertexAttributeValues::Float32x3(vertices)) =
                mesh.attribute_mut(Mesh::ATTRIBUTE_POSITION)
            else {
                return Err("Pier position attribute absent".into());
            };
            for vertex in vertices {
                vertex[0] -= pivot.x;
                vertex[1] -= pivot.y;
                vertex[2] -= pivot.z;
            }
        }
        *triangles += mesh.indices().ok_or("Pier indices absent")?.len() / 3;
        prepared.push(PreparedPart {
            mesh,
            material: part.material,
        });
    }
    Ok(prepared)
}

fn stage_part(
    world: &mut World,
    resident: &mut ResidentPier,
    parent: Entity,
    part: &PreparedPart,
    mesh: &Handle<Mesh>,
    placement: Option<&ManualPlacement>,
    materials: &BTreeMap<String, Handle<VillageMaterial>>,
) {
    resident.triangles += part.mesh.indices().map_or(0, |indices| indices.len() / 3);
    let material = materials[&part.material].clone();
    let mut entity = world.spawn((
        Mesh3d(mesh.clone()),
        MeshMaterial3d(material),
        Transform::IDENTITY,
        Visibility::Inherited,
        ChildOf(parent),
    ));
    if let Some(placement) = placement {
        entity.insert(ManualPlacementPart(placement.id));
    }
    resident.parts.push(entity.id());
}

impl ResidentPier {
    /// Visible source parts, including the nested sign and lantern pivots.
    #[must_use]
    pub fn render_parts(&self) -> &[Entity] {
        &self.parts
    }

    #[must_use]
    pub fn authored_roots(&self) -> &[(Entity, EntityId, ForgeTransform)] {
        &self.authored
    }

    #[must_use]
    pub fn authored_parts(&self, world: &World, id: EntityId) -> Option<Vec<Entity>> {
        let root = self.authored.iter().find(|(_, placed, _)| *placed == id)?.0;
        Some(
            self.parts
                .iter()
                .copied()
                .filter(|&part| {
                    let mut current = part;
                    while let Some(parent) = world.get::<ChildOf>(current) {
                        current = parent.parent();
                        if current == root {
                            return true;
                        }
                    }
                    false
                })
                .collect(),
        )
    }

    #[must_use]
    pub fn is_synchronized(&self, world: &World) -> bool {
        self.parts.len() == self.roots.len() * 28
            && self.pivots.len() == self.roots.len() * 12
            && self.colliders.len() + self.sensors.len()
                == self.boxes + self.cylinders + self.roots.len()
            && self.sensors.len() == self.ladder_sensors
            && self
                .roots
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && self
                .parts
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && self
                .pivots
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && world
                .get_resource::<Assets<Mesh>>()
                .is_some_and(|assets| self.meshes.iter().all(|mesh| assets.contains(mesh.id())))
            && self
                .colliders
                .iter()
                .all(|&entity| crate::physics::static_collider_ready(world, entity))
            && self
                .sensors
                .iter()
                .all(|&entity| crate::physics::static_sensor_ready(world, entity))
    }

    pub fn retire(self, world: &mut World) {
        for entity in self
            .parts
            .into_iter()
            .chain(self.colliders)
            .chain(self.sensors)
            .chain(self.pivots)
        {
            world.despawn(entity);
        }
        for root in self.roots {
            world.despawn(root);
        }
        let mut meshes = world.resource_mut::<Assets<Mesh>>();
        for mesh in self.meshes {
            meshes.remove(mesh.id());
        }
    }
}

fn forge_transform(transform: Transform) -> ForgeTransform {
    ForgeTransform {
        translation_m: ForgeVec3::new(
            transform.translation.x,
            transform.translation.y,
            transform.translation.z,
        ),
        rotation: UnitQuaternion::new(
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w,
        ),
        scale: ForgeVec3::new(transform.scale.x, transform.scale.y, transform.scale.z),
    }
}

fn project(transform: ForgeTransform) -> Transform {
    Transform {
        translation: Vec3::new(
            transform.translation_m.x,
            transform.translation_m.y,
            transform.translation_m.z,
        ),
        rotation: Quat::from_xyzw(
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w,
        ),
        scale: Vec3::new(transform.scale.x, transform.scale.y, transform.scale.z),
    }
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Narrows source coordinates to Bevy float32 and rejects non-finite results."
)]
fn finite(value: f64) -> Result<f32, String> {
    let narrowed = value as f32;
    if !value.is_finite() || !narrowed.is_finite() {
        return Err("Non-finite pier source coordinate".into());
    }
    Ok(narrowed)
}

fn vec3(values: [f64; 3]) -> Result<Vec3, String> {
    Ok(Vec3::new(
        finite(values[0])?,
        finite(values[1])?,
        finite(values[2])?,
    ))
}

fn source_frame(frame: SourceFrame) -> Result<Transform, String> {
    let translation = vec3(frame.translation_m)?;
    let rotation = frame
        .rotation_xyzw
        .map(finite)
        .into_iter()
        .collect::<Result<Vec<_>, _>>()?;
    let scale = vec3(frame.scale)?;
    if rotation != [0.0, 0.0, 0.0, 1.0]
        || scale != Vec3::ONE
        || translation != Vec3::new(55.0, 0.0, -64.0)
    {
        return Err("Pier source frame differs from pinned basis".into());
    }
    Ok(Transform::from_translation(translation))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};
    use forge_core::bytes_sha256;

    fn fixture_part() -> PreparedPart {
        PreparedPart {
            mesh: Mesh::from(Cuboid::new(1.0, 1.0, 1.0)),
            material: "fixture".into(),
        }
    }

    fn fixture() -> PreparedPier {
        PreparedPier {
            source_id: "fixture-pier".into(),
            frame: Transform::from_xyz(55.0, 0.0, -64.0),
            static_parts: (0..4).map(|_| fixture_part()).collect(),
            sign: PreparedGroup {
                pivot: Vec3::new(1.0, 2.0, 3.0),
                parts: (0..2).map(|_| fixture_part()).collect(),
            },
            lanterns: [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0]
                .into_iter()
                .map(|x| PreparedGroup {
                    pivot: Vec3::new(x, 2.0, 0.0),
                    parts: (0..2).map(|_| fixture_part()).collect(),
                })
                .collect(),
            colliders: vec![
                PreparedCollider {
                    transform: Transform::from_xyz(1.0, 0.5, -2.0),
                    shape: crate::physics::yawed_box_collider(Vec3::new(1.0, 0.5, 1.5), 0.6)
                        .unwrap(),
                    sensor: false,
                },
                PreparedCollider {
                    transform: Transform::from_xyz(2.0, 1.5, -2.0),
                    shape: crate::physics::yawed_box_collider(Vec3::new(0.5, 1.5, 0.5), 0.3)
                        .unwrap(),
                    sensor: true,
                },
                PreparedCollider {
                    transform: Transform::from_xyz(-1.0, 1.0, 2.0),
                    shape: Collider::cylinder(1.0, 0.2),
                    sensor: false,
                },
            ],
            triangles: 336,
            boxes: 2,
            cylinders: 1,
            ladder_sensors: 1,
        }
    }

    fn fixture_materials() -> BTreeMap<String, Handle<VillageMaterial>> {
        BTreeMap::from([("fixture".into(), Handle::default())])
    }

    #[test]
    fn pier_authored_empty_projects_no_instances_and_source_stage_stays_explicit() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let empty = fixture()
            .stage_authored(&mut world, &BTreeMap::new(), &[])
            .unwrap();
        assert!(empty.is_synchronized(&world));
        assert!(empty.roots.is_empty());
        assert!(empty.parts.is_empty());
        assert!(empty.colliders.is_empty());
        assert!(empty.sensors.is_empty());
        assert!(empty.pivots.is_empty());
        assert!(world.resource::<Assets<Mesh>>().is_empty());
        let source = fixture().stage(&mut world, &fixture_materials()).unwrap();
        assert_eq!(source.roots.len(), 1);
        assert_eq!(source.triangles, 336);
        assert!(source.authored_roots().is_empty());
    }

    #[test]
    fn pier_authored_common_transform_scales_colliders_sensors_and_ramp_beyond_old_cap() {
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        let baseline = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .add(Mesh::from(Cuboid::new(1.0, 1.0, 1.0)));
        let transform = forge_transform(Transform {
            translation: Vec3::new(5.0, 2.0, -7.0),
            rotation: Quat::from_euler(EulerRot::XYZ, 0.2, 0.7, 0.1),
            scale: Vec3::new(2.0, 1.5, 0.5),
        });
        let mut placements = (0..5)
            .map(|index| ManualPlacement {
                particles: None,
                physics: None,
                id: EntityId::deterministic(format!("pier-common-{index}")),
                name: "Fixture pier".into(),
                asset_id: pier::asset_id(),
                asset_hash: pier::package_hash(),
                transform,
                locked: false,
            })
            .collect::<Vec<_>>();
        placements.sort_by_key(|placement| placement.id);
        let resident = fixture()
            .stage_authored(app.world_mut(), &fixture_materials(), &placements)
            .unwrap();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(resident.roots.len(), 5);
        assert_eq!(resident.meshes.len(), 28);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 29);
        for instance in resident.parts.as_chunks::<28>().0 {
            assert!(
                instance
                    .iter()
                    .zip(&resident.meshes)
                    .all(|(&part, mesh)| { app.world().get::<Mesh3d>(part).unwrap().0 == *mesh })
            );
        }
        assert_eq!(resident.colliders.len(), 15);
        assert_eq!(resident.sensors.len(), 5);
        assert!(resident.is_synchronized(app.world()));
        assert!(crate::manual_placements_ready(app.world_mut(), &placements));
        for &collider in resident.colliders.iter().chain(&resident.sensors) {
            assert!(
                app.world()
                    .get::<Collider>(collider)
                    .unwrap()
                    .scale()
                    .abs_diff_eq(Vec3::new(2.0, 1.5, 0.5), 1.0e-4)
            );
        }
        let before = app.world().entities().len();
        placements[0].transform.scale.y = f32::NAN;
        assert!(
            fixture()
                .stage_authored(app.world_mut(), &fixture_materials(), &placements)
                .is_err()
        );
        assert_eq!(app.world().entities().len(), before);
        placements[0].transform = transform;
        let oversized = vec![placements[0].clone(); MAX_MANUAL_PLACEMENTS + 1];
        assert!(
            fixture()
                .stage_authored(app.world_mut(), &fixture_materials(), &oversized)
                .is_err()
        );
        assert_eq!(app.world().entities().len(), before);
        resident.retire(app.world_mut());
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 1);
        assert!(
            app.world()
                .resource::<Assets<Mesh>>()
                .contains(baseline.id())
        );
        assert!(crate::manual_placements_ready(app.world_mut(), &[]));
    }

    fn shadow_materials(world: &mut World) -> BTreeMap<String, Handle<VillageMaterial>> {
        use crate::tidewater_assets::{
            MaterialParameters,
            materials::{self, LightingBindings},
        };
        use crate::tidewater_material::{ObjectShadowSettings, SceneLighting};
        world.insert_resource(Assets::<Image>::default());
        world.insert_resource(Assets::<VillageMaterial>::default());
        let caustics = crate::tidewater_caustics::CausticMaterialInputs::disabled(
            &mut world.resource_mut::<Assets<Image>>(),
        );
        let lighting = LightingBindings {
            scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [1.0; 3], [[0.0; 3]; 9]).unwrap(),
            environment: Handle::default(),
            hill_shadow: Handle::default(),
            object_shadow: Handle::default(),
            object_settings: ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            wind: Vec4::ZERO,
            flags: Vec4::ZERO,
            caustics,
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        };
        let textures = BTreeMap::from([("test".to_owned(), Handle::<Image>::default())]);
        [0, 1, 6]
            .into_iter()
            .map(|kind| {
                let parameters = MaterialParameters {
                    kind,
                    textures: vec!["test".to_owned()],
                    color: [1.0; 4],
                    emissive: [0.0; 4],
                    surface: [0.0; 4],
                };
                let material = materials::prepare(&parameters, &textures, &lighting).unwrap();
                (
                    format!("material-{kind}"),
                    world
                        .resource_mut::<Assets<VillageMaterial>>()
                        .add(material),
                )
            })
            .collect()
    }

    #[test]
    fn pinned_pier_prepares_source_and_authored_collisions() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_PIER_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = PierPackage::load(&manifest, bytes_sha256(&manifest), |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let prepared = PreparedPier::from_package(&package).unwrap();
        assert_eq!(
            (
                prepared.triangles,
                prepared.boxes,
                prepared.cylinders,
                prepared.ladder_sensors
            ),
            (36_194, 122, 93, 8)
        );
        let template = prepared.template();
        let mut moved = template.transform;
        moved.translation_m.x += 2.0;
        let placement = ManualPlacement {
            particles: None,
            physics: None,
            id: EntityId::deterministic("pier-authored-test"),
            name: "Quai déplacé".into(),
            asset_id: template.asset_id,
            asset_hash: pier::package_hash(),
            transform: moved,
            locked: false,
        };
        let mut world = World::new();
        world.insert_resource(Assets::<Mesh>::default());
        let materials = shadow_materials(&mut world);
        let resident = prepared
            .stage_authored(&mut world, &materials, &[placement])
            .unwrap();
        assert_eq!(resident.parts.len(), 28);
        assert_eq!(resident.pivots.len(), 12);
        assert_eq!(resident.colliders.len(), 208);
        assert_eq!(resident.sensors.len(), 8);
        assert_eq!(resident.ladder_sensors, 8);
        assert_eq!(resident.authored_roots().len(), 1);
        let (root, id, _) = resident.authored_roots()[0];
        let expected = world
            .get::<ManualPlacementCollisionExpected>(root)
            .expect("authored pier colliders must be tracked");
        assert_eq!(expected.primitives.len(), 208);
        let support = *expected.primitives.last().unwrap();
        assert_eq!(
            world.get::<ManualPlacementCollision>(support).unwrap().0,
            id
        );
        assert_eq!(world.get::<ChildOf>(support).unwrap().parent(), root);
        // A standalone authored pier must cast even without ResidentScene.
        world.insert_resource(resident);
        let before = crate::tidewater_object_shadow::scene::opaque_parts(&world).unwrap();
        assert_eq!(before.len(), 27);
        assert_eq!(
            crate::tidewater_object_shadow::scene::buildings(&world)
                .unwrap()
                .len(),
            before.len()
        );
        world.get_mut::<Transform>(root).unwrap().translation.x += 8.0;
        let after = crate::tidewater_object_shadow::scene::opaque_parts(&world).unwrap();
        for ((first_entity, first), (second_entity, second)) in before.iter().zip(&after) {
            assert_eq!(first_entity, second_entity);
            assert!((second.w_axis.x - first.w_axis.x - 8.0).abs() < 0.0001);
        }
    }
}
