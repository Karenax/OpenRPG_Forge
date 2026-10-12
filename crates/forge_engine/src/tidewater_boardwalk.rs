//! Source boardwalk meshes and walkable volumes in the existing Bevy/Rapier world.
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementPart,
    ManualPlacementProjection, tidewater_material::VillageMaterial,
    tidewater_mesh::VillageMeshBytes,
};
use bevy::{light::NotShadowCaster, prelude::*};
use bevy_rapier3d::prelude::Collider;
use forge_assets::tidewater_boardwalk::{self as boardwalk, BoardwalkPackage};
use forge_core::{
    AssetId, EntityId, Transform as ForgeTransform, UnitQuaternion, Vec3 as ForgeVec3,
};
use forge_project::zone_authoring::placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement};
use serde::Deserialize;
use std::collections::BTreeMap;

#[derive(Deserialize)]
struct Description {
    boardwalks: Vec<SegmentSource>,
}

#[derive(Deserialize)]
struct SegmentSource {
    source_id: String,
    source_frame: SourceFrame,
    parts: Vec<PartSource>,
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
    material: String,
    attributes: BTreeMap<String, FileReference>,
    indices: FileReference,
}

#[derive(Deserialize)]
struct FileReference {
    file: String,
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

struct PreparedCollider {
    transform: Transform,
    shape: Collider,
}

struct PreparedSegment {
    source_id: String,
    asset_id: AssetId,
    frame: Transform,
    parts: Vec<PreparedPart>,
    colliders: Vec<PreparedCollider>,
    boxes: usize,
    cylinders: usize,
}

/// Verified CPU projection; source mesh and collider bytes are never persisted as ECS handles.
pub struct PreparedBoardwalk {
    segments: Vec<PreparedSegment>,
    triangles: usize,
    boxes: usize,
    cylinders: usize,
}

#[derive(Clone)]
pub struct BoardwalkTemplate {
    pub source_id: String,
    pub asset_id: AssetId,
    pub transform: ForgeTransform,
}

/// Source identity is useful for proving which immutable object a presented frame contains.
#[derive(Component)]
pub struct ImportedBoardwalk(pub String);

#[derive(Resource)]
pub struct ResidentBoardwalk {
    roots: Vec<Entity>,
    parts: Vec<Entity>,
    meshes: Vec<Handle<Mesh>>,
    colliders: Vec<Entity>,
    authored: Vec<(Entity, EntityId, ForgeTransform)>,
    pub triangles: usize,
    pub boxes: usize,
    pub cylinders: usize,
}

impl PreparedBoardwalk {
    #[allow(
        clippy::too_many_lines,
        reason = "Validates and prepares one pinned geometry, collider, and provenance contract in source order."
    )]
    pub fn from_package(package: &BoardwalkPackage) -> Result<Self, String> {
        let description: Description =
            serde_json::from_slice(package.description()).map_err(|e| e.to_string())?;
        if description.boardwalks.len() != 3 {
            return Err("Three source boardwalk segments required".into());
        }
        let mut segments = Vec::with_capacity(3);
        let mut triangles = 0;
        let mut boxes = 0;
        let mut cylinders = 0;
        for (ordinal, record) in description.boardwalks.into_iter().enumerate() {
            if record.source_id
                != format!("tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:boardwalk:{ordinal}")
                || record.parts.is_empty()
                || record.parts.len() > 4
            {
                return Err("Boardwalk source segment differs".into());
            }
            let frame = source_frame(record.source_frame)?;
            let mut parts = Vec::with_capacity(record.parts.len());
            for part in record.parts {
                let file = |reference: &FileReference| {
                    package
                        .file(&reference.file)
                        .ok_or("Boardwalk mesh payload absent")
                };
                let attr = |name: &str| {
                    part.attributes
                        .get(name)
                        .ok_or("Boardwalk mesh attribute absent")
                        .and_then(&file)
                };
                let mesh = VillageMeshBytes {
                    positions: attr("pos")?,
                    normals: attr("nrm")?,
                    uv: attr("uv")?,
                    tint: attr("tint")?,
                    data: attr("data")?,
                    indices: file(&part.indices)?,
                }
                .into_mesh()?;
                triangles += mesh.indices().ok_or("Boardwalk indices absent")?.len() / 3;
                parts.push(PreparedPart {
                    mesh,
                    material: part.material,
                });
            }
            let segment_boxes = record.colliders.boxes.len();
            let segment_cylinders = record.colliders.cylinders.len();
            let mut colliders = Vec::new();
            for source in record.colliders.boxes {
                if source.tag != "boardwalk" || !source.walkable || !source.solid {
                    return Err("Unexpected boardwalk collision role".into());
                }
                let center = vec3(source.center)?;
                let half = vec3(source.half)?;
                let yaw = finite(source.yaw)?;
                if half.min_element() <= 0.0 || half.max_element() > 20.0 {
                    return Err("Invalid boardwalk collision extent".into());
                }
                colliders.push(PreparedCollider {
                    transform: Transform::from_translation(center),
                    shape: crate::physics::yawed_box_collider(half, yaw)?,
                });
                boxes += 1;
            }
            for source in record.colliders.cylinders {
                if source.tag != "pathLight" {
                    return Err("Unexpected boardwalk cylinder role".into());
                }
                let (x, z, radius, y_min, y_max) = (
                    finite(source.x)?,
                    finite(source.z)?,
                    finite(source.radius)?,
                    finite(source.y_min)?,
                    finite(source.y_max)?,
                );
                if radius <= 0.0 || radius > 1.0 || y_max <= y_min || y_max - y_min > 8.0 {
                    return Err("Invalid boardwalk light pole collider".into());
                }
                colliders.push(PreparedCollider {
                    transform: Transform::from_xyz(x, f32::midpoint(y_min, y_max), z),
                    shape: Collider::cylinder((y_max - y_min) * 0.5, radius),
                });
                cylinders += 1;
            }
            segments.push(PreparedSegment {
                source_id: record.source_id,
                asset_id: boardwalk::asset_id(ordinal).ok_or("Boardwalk asset ID absent")?,
                frame,
                parts,
                colliders,
                boxes: segment_boxes,
                cylinders: segment_cylinders,
            });
        }
        if triangles != 7_564 || boxes != 69 || cylinders != 3 {
            return Err("Boardwalk source geometry or colliders incomplete".into());
        }
        Ok(Self {
            segments,
            triangles,
            boxes,
            cylinders,
        })
    }

    #[must_use]
    pub fn templates(&self) -> Vec<BoardwalkTemplate> {
        self.segments
            .iter()
            .map(|segment| BoardwalkTemplate {
                source_id: segment.source_id.clone(),
                asset_id: segment.asset_id,
                transform: forge_transform(segment.frame),
            })
            .collect()
    }

    pub fn stage(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
    ) -> Result<ResidentBoardwalk, String> {
        self.stage_instances(world, materials, None)
    }

    /// Replace the three inherited source instances with accepted, persistent placements.
    /// An empty authored set projects no instances; `stage` retains the source originals.
    pub fn stage_authored(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentBoardwalk, String> {
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
    ) -> Result<ResidentBoardwalk, String> {
        let source_instances = placements.is_none();
        let placements = placements.unwrap_or_default();
        if placements.len() > MAX_MANUAL_PLACEMENTS {
            return Err("Boardwalk placement limit exceeded".into());
        }
        for placement in placements {
            if boardwalk::asset_ordinal(placement.asset_id).is_none()
                || placement.asset_hash != boardwalk::package_hash()
            {
                return Err("Authored boardwalk reference differs from its source".into());
            }
            placement.transform.validate().map_err(|e| e.to_string())?;
        }
        for segment in &self.segments {
            if !source_instances
                && !placements
                    .iter()
                    .any(|placement| placement.asset_id == segment.asset_id)
            {
                continue;
            }
            for part in &segment.parts {
                if !materials.contains_key(&part.material) {
                    return Err("Boardwalk material is not resident".into());
                }
            }
        }
        let mut resident = ResidentBoardwalk {
            roots: Vec::new(),
            parts: Vec::new(),
            meshes: Vec::new(),
            colliders: Vec::new(),
            authored: Vec::new(),
            triangles: 0,
            boxes: 0,
            cylinders: 0,
        };
        for segment in self.segments {
            let instances: Vec<Option<&ManualPlacement>> = if source_instances {
                vec![None]
            } else {
                placements
                    .iter()
                    .filter(|placement| placement.asset_id == segment.asset_id)
                    .map(Some)
                    .collect()
            };
            if instances.is_empty() {
                continue;
            }
            let meshes = segment
                .parts
                .iter()
                .map(|part| world.resource_mut::<Assets<Mesh>>().add(part.mesh.clone()))
                .collect::<Vec<_>>();
            resident.meshes.extend(meshes.iter().cloned());
            for placement in instances {
                let frame = placement.map_or(segment.frame, |value| project(value.transform));
                let root = world
                    .spawn((
                        Name::new(segment.source_id.clone()),
                        ImportedBoardwalk(segment.source_id.clone()),
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
                let first_collider = resident.colliders.len();
                for (part, mesh) in segment.parts.iter().zip(&meshes) {
                    resident.triangles +=
                        part.mesh.indices().map_or(0, |indices| indices.len() / 3);
                    let material = materials[&part.material].clone();
                    let mut entity = world.spawn((
                        Mesh3d(mesh.clone()),
                        MeshMaterial3d(material),
                        Transform::IDENTITY,
                        Visibility::Inherited,
                        NotShadowCaster,
                        ChildOf(root),
                    ));
                    if let Some(placement) = placement {
                        entity.insert(ManualPlacementPart(placement.id));
                    }
                    resident.parts.push(entity.id());
                }
                for collider in &segment.colliders {
                    let mut entity =
                        world.spawn((collider.shape.clone(), collider.transform, ChildOf(root)));
                    if let Some(placement) = placement {
                        entity.insert(ManualPlacementCollision(placement.id));
                    }
                    let entity = entity.id();
                    resident.colliders.push(entity);
                }
                if let Some(placement) = placement {
                    world.entity_mut(root).insert((
                        ManualPlacementProjection(placement.clone()),
                        ManualPlacementCollisionExpected {
                            placement: placement.id,
                            primitives: resident.colliders[first_collider..].to_vec(),
                        },
                    ));
                }
                resident.boxes += segment.boxes;
                resident.cylinders += segment.cylinders;
            }
        }
        if source_instances
            && (resident.triangles, resident.boxes, resident.cylinders)
                != (self.triangles, self.boxes, self.cylinders)
        {
            return Err("Staged boardwalk differs from pinned source totals".into());
        }
        Ok(resident)
    }
}

impl ResidentBoardwalk {
    /// Visible source parts used by the shared opaque depth pass.
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
                    world
                        .get::<ChildOf>(part)
                        .is_some_and(|parent| parent.parent() == root)
                })
                .collect(),
        )
    }

    #[must_use]
    pub fn is_synchronized(&self, world: &World) -> bool {
        self.parts.len() >= self.roots.len()
            && self.colliders.len() == self.boxes + self.cylinders
            && self
                .roots
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && self
                .parts
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && world
                .get_resource::<Assets<Mesh>>()
                .is_some_and(|assets| self.meshes.iter().all(|mesh| assets.contains(mesh.id())))
            && self
                .colliders
                .iter()
                .all(|&entity| crate::physics::static_collider_ready(world, entity))
    }

    pub fn retire(self, world: &mut World) {
        for entity in self.parts.into_iter().chain(self.colliders) {
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
        return Err("Non-finite boardwalk source coordinate".into());
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
    if rotation != [0.0, 0.0, 0.0, 1.0] || scale != Vec3::ONE {
        return Err("Boardwalk source frame must retain its pinned basis".into());
    }
    Ok(Transform::from_translation(translation))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};
    use forge_core::bytes_sha256;

    fn fixture() -> PreparedBoardwalk {
        PreparedBoardwalk {
            segments: (0..3)
                .map(|ordinal| PreparedSegment {
                    source_id: format!("fixture-boardwalk-{ordinal}"),
                    asset_id: boardwalk::asset_id(ordinal).unwrap(),
                    frame: Transform::from_xyz([0.0, 10.0, 20.0][ordinal], 0.0, 0.0),
                    parts: vec![PreparedPart {
                        mesh: Mesh::from(Cuboid::new(2.0, 1.0, 3.0)),
                        material: "fixture".into(),
                    }],
                    colliders: vec![
                        PreparedCollider {
                            transform: Transform::from_xyz(1.0, 0.5, -2.0),
                            shape: crate::physics::yawed_box_collider(
                                Vec3::new(1.0, 0.5, 1.5),
                                0.6,
                            )
                            .unwrap(),
                        },
                        PreparedCollider {
                            transform: Transform::from_xyz(-1.0, 1.0, 2.0),
                            shape: Collider::cylinder(1.0, 0.2),
                        },
                    ],
                    boxes: 1,
                    cylinders: 1,
                })
                .collect(),
            triangles: 36,
            boxes: 3,
            cylinders: 3,
        }
    }

    fn materials() -> BTreeMap<String, Handle<VillageMaterial>> {
        BTreeMap::from([("fixture".into(), Handle::default())])
    }

    #[test]
    fn boardwalk_authored_empty_projects_no_instances_and_source_stage_stays_explicit() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let empty = fixture()
            .stage_authored(&mut world, &BTreeMap::new(), &[])
            .unwrap();
        assert!(empty.is_synchronized(&world));
        assert!(empty.roots.is_empty());
        assert!(empty.parts.is_empty());
        assert!(empty.colliders.is_empty());
        assert!(world.resource::<Assets<Mesh>>().is_empty());
        let source = fixture().stage(&mut world, &materials()).unwrap();
        assert_eq!(source.roots.len(), 3);
        assert_eq!(source.triangles, 36);
        assert!(source.authored_roots().is_empty());
    }

    #[test]
    fn boardwalk_authored_common_transform_scales_real_colliders_beyond_old_cap() {
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
        let mut placements = (0..17)
            .map(|index| ManualPlacement {
                physics: None,
                id: EntityId::deterministic(format!("boardwalk-common-{index}")),
                name: "Fixture boardwalk".into(),
                asset_id: boardwalk::asset_id(0).unwrap(),
                asset_hash: boardwalk::package_hash(),
                transform,
                locked: false,
            })
            .collect::<Vec<_>>();
        placements.sort_by_key(|placement| placement.id);
        let resident = fixture()
            .stage_authored(app.world_mut(), &materials(), &placements)
            .unwrap();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(resident.roots.len(), 17);
        assert_eq!(resident.meshes.len(), 1);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 2);
        assert!(
            resident
                .parts
                .iter()
                .all(|&part| { app.world().get::<Mesh3d>(part).unwrap().0 == resident.meshes[0] })
        );
        assert!(resident.is_synchronized(app.world()));
        assert!(crate::manual_placements_ready(app.world_mut(), &placements));
        for &collider in &resident.colliders {
            assert!(
                app.world()
                    .get::<Collider>(collider)
                    .unwrap()
                    .scale()
                    .abs_diff_eq(Vec3::new(2.0, 1.5, 0.5), 1.0e-4)
            );
        }
        let before = app.world().entities().len();
        placements[0].transform.scale.x = 0.0;
        assert!(
            fixture()
                .stage_authored(app.world_mut(), &materials(), &placements)
                .is_err()
        );
        assert_eq!(app.world().entities().len(), before);
        placements[0].transform = transform;
        let oversized = vec![placements[0].clone(); MAX_MANUAL_PLACEMENTS + 1];
        assert!(
            fixture()
                .stage_authored(app.world_mut(), &materials(), &oversized)
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

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Verifies exact authored translation projected without arithmetic changes."
    )]
    fn pinned_boardwalk_prepares_exact_walkable_source() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_BOARDWALK_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = BoardwalkPackage::load(&manifest, bytes_sha256(&manifest), |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let prepared = PreparedBoardwalk::from_package(&package).unwrap();
        assert_eq!(prepared.segments.len(), 3);
        assert_eq!(
            (prepared.triangles, prepared.boxes, prepared.cylinders),
            (7_564, 69, 3)
        );
        let template = prepared.templates().remove(0);
        let mut moved = template.transform;
        moved.translation_m.x += 2.0;
        let placement = ManualPlacement {
            physics: None,
            id: EntityId::deterministic("boardwalk-authored-test"),
            name: "Passerelle déplacée".into(),
            asset_id: template.asset_id,
            asset_hash: boardwalk::package_hash(),
            transform: moved,
            locked: false,
        };
        let mut world = World::new();
        world.insert_resource(Assets::<Mesh>::default());
        let materials = BTreeMap::from([
            (
                "material-0".to_owned(),
                Handle::<VillageMaterial>::default(),
            ),
            (
                "material-1".to_owned(),
                Handle::<VillageMaterial>::default(),
            ),
        ]);
        let resident = prepared
            .stage_authored(&mut world, &materials, std::slice::from_ref(&placement))
            .unwrap();
        assert_eq!(resident.roots.len(), 1);
        assert_eq!(resident.parts.len(), 3);
        assert_eq!(resident.colliders.len(), 47);
        assert_eq!(
            resident.authored_roots()[0].1,
            EntityId::deterministic("boardwalk-authored-test")
        );
        let root = resident.authored_roots()[0].0;
        assert_eq!(
            world.get::<Transform>(root).unwrap().translation.x,
            moved.translation_m.x
        );
        assert_eq!(
            world.get::<ManualPlacementProjection>(root).unwrap().0,
            placement
        );
        let expected = world.get::<ManualPlacementCollisionExpected>(root).unwrap();
        assert_eq!(expected.placement, placement.id);
        assert_eq!(expected.primitives, resident.colliders);
        assert!(expected.primitives.iter().all(|&entity| {
            world
                .get::<ManualPlacementCollision>(entity)
                .is_some_and(|marker| marker.0 == placement.id)
                && world
                    .get::<ChildOf>(entity)
                    .is_some_and(|parent| parent.parent() == root)
        }));
        assert_eq!(
            resident
                .authored_parts(&world, resident.authored_roots()[0].1)
                .unwrap()
                .len(),
            3
        );
        let meshes = resident.meshes.iter().map(Handle::id).collect::<Vec<_>>();
        resident.retire(&mut world);
        assert!(world.get_entity(root).is_err());
        assert!(
            meshes
                .into_iter()
                .all(|id| !world.resource::<Assets<Mesh>>().contains(id))
        );
    }
}
