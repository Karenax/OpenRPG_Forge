//! Five separately placeable source plaza props in the shared Bevy/Rapier world.
use bevy::prelude::*;
use bevy_rapier3d::prelude::Collider;
use forge_core::{
    AssetId, EntityId, Transform as ForgeTransform, UnitQuaternion, Vec3 as ForgeVec3,
};
use forge_project::zone_authoring::placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement};
use forge_tidewater_assets::tidewater_plaza::{self as plaza, PlazaPackage};
use serde::Deserialize;
use std::collections::BTreeMap;
use {
    crate::{tidewater_material::VillageMaterial, tidewater_mesh::VillageMeshBytes},
    forge_engine::{
        ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementPart,
        ManualPlacementProjection,
    },
};

#[derive(Deserialize)]
struct Description {
    props: Vec<PropSource>,
}

#[derive(Deserialize)]
struct PropSource {
    source_id: String,
    kind: String,
    source_frame: SourceFrame,
    parts: Vec<PartSource>,
    colliders: SourceColliders,
    lights: Vec<SourceLight>,
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

#[derive(Deserialize)]
struct SourceLight {
    position: [f64; 3],
    color: [f64; 3],
    intensity: f64,
    kind: String,
}

struct PreparedPart {
    mesh: Mesh,
    material: String,
}

struct PreparedCollider {
    transform: Transform,
    shape: Collider,
}

struct PreparedProp {
    source_id: String,
    kind: String,
    asset_id: AssetId,
    frame: Transform,
    parts: Vec<PreparedPart>,
    colliders: Vec<PreparedCollider>,
    lights: Vec<PreparedLight>,
}

struct PreparedLight {
    position: Vec3,
    source: PlazaLight,
}

/// Source light coordinates and intensity are retained; photometric calibration is separate.
#[derive(Component, Clone)]
pub struct PlazaLight {
    pub color_linear: Vec3,
    pub intensity_source: f32,
}

/// Verified CPU projection; source bytes never become serialized Bevy handles.
pub struct PreparedPlaza {
    props: Vec<PreparedProp>,
    triangles: usize,
}

#[derive(Clone)]
pub struct PlazaTemplate {
    pub source_id: String,
    pub kind: String,
    pub asset_id: AssetId,
    pub transform: ForgeTransform,
}

#[derive(Component)]
pub struct ImportedPlazaProp(pub String);

#[derive(Resource)]
pub struct ResidentPlaza {
    roots: Vec<Entity>,
    parts: Vec<Entity>,
    meshes: Vec<Handle<Mesh>>,
    colliders: Vec<Entity>,
    lights: Vec<Entity>,
    authored: Vec<(Entity, EntityId, ForgeTransform)>,
    pub triangles: usize,
}

impl PreparedPlaza {
    #[allow(
        clippy::too_many_lines,
        reason = "Validates and prepares one pinned geometry, collider, and provenance contract in source order."
    )]
    pub fn from_package(package: &PlazaPackage) -> Result<Self, String> {
        let description: Description =
            serde_json::from_slice(package.description()).map_err(|e| e.to_string())?;
        if description.props.len() != 5 {
            return Err("Five source plaza props required".into());
        }
        let mut props = Vec::with_capacity(5);
        let mut triangles = 0;
        for (ordinal, source) in description.props.into_iter().enumerate() {
            let expected_kind = match ordinal {
                0 | 1 => "bench",
                2 => "lampPost",
                _ => "barrel",
            };
            if source.source_id
                != format!(
                    "tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:plaza-prop:{ordinal}"
                )
                || source.kind != expected_kind
                || source.parts.is_empty()
                || source.parts.len() > 3
            {
                return Err("Source plaza prop differs".into());
            }
            let frame = source_frame(source.source_frame)?;
            let mut parts = Vec::with_capacity(source.parts.len());
            for part in source.parts {
                let file = |reference: &FileReference| {
                    package
                        .file(&reference.file)
                        .ok_or("Plaza mesh payload absent")
                };
                let attr = |name: &str| {
                    part.attributes
                        .get(name)
                        .ok_or("Plaza mesh attribute absent")
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
                triangles += mesh.indices().ok_or("Plaza indices absent")?.len() / 3;
                parts.push(PreparedPart {
                    mesh,
                    material: part.material,
                });
            }
            let mut colliders = Vec::new();
            for box_source in source.colliders.boxes {
                if box_source.tag != expected_kind || !box_source.solid {
                    return Err("Unexpected plaza box collision role".into());
                }
                let center = vec3(box_source.center)?;
                let half = vec3(box_source.half)?;
                if half.min_element() <= 0.0 || half.max_element() > 4.0 {
                    return Err("Invalid plaza box extent".into());
                }
                colliders.push(PreparedCollider {
                    transform: Transform::from_translation(center),
                    shape: forge_engine::physics::yawed_box_collider(
                        half,
                        finite(box_source.yaw)?,
                    )?,
                });
            }
            for cylinder in source.colliders.cylinders {
                if cylinder.tag != expected_kind {
                    return Err("Unexpected plaza cylinder collision role".into());
                }
                let (x, z, radius, y_min, y_max) = (
                    finite(cylinder.x)?,
                    finite(cylinder.z)?,
                    finite(cylinder.radius)?,
                    finite(cylinder.y_min)?,
                    finite(cylinder.y_max)?,
                );
                if radius <= 0.0 || radius > 1.0 || y_max <= y_min || y_max - y_min > 4.0 {
                    return Err("Invalid plaza cylinder extent".into());
                }
                colliders.push(PreparedCollider {
                    transform: Transform::from_xyz(x, f32::midpoint(y_min, y_max), z),
                    shape: Collider::cylinder((y_max - y_min) * 0.5, radius),
                });
            }
            if colliders.len() != 1 || source.lights.len() != usize::from(ordinal == 2) {
                return Err("Plaza collider or lantern light absent".into());
            }
            let mut lights = Vec::new();
            for light in source.lights {
                if light.kind != "lantern" || light.intensity <= 0.0 {
                    return Err("Unexpected plaza light".into());
                }
                let position = vec3(light.position)?;
                let color = vec3(light.color)?;
                lights.push(PreparedLight {
                    position,
                    source: PlazaLight {
                        color_linear: color,
                        intensity_source: finite(light.intensity)?,
                    },
                });
            }
            props.push(PreparedProp {
                source_id: source.source_id,
                kind: source.kind,
                asset_id: plaza::asset_id(ordinal).ok_or("Plaza asset ID absent")?,
                frame,
                parts,
                colliders,
                lights,
            });
        }
        if triangles != 1_207 {
            return Err("Plaza source geometry incomplete".into());
        }
        Ok(Self { props, triangles })
    }

    #[must_use]
    pub fn templates(&self) -> Vec<PlazaTemplate> {
        self.props
            .iter()
            .map(|prop| PlazaTemplate {
                source_id: prop.source_id.clone(),
                kind: prop.kind.clone(),
                asset_id: prop.asset_id,
                transform: forge_transform(prop.frame),
            })
            .collect()
    }

    pub fn stage(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
    ) -> Result<ResidentPlaza, String> {
        self.stage_instances(world, materials, None)
    }

    pub fn stage_authored(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentPlaza, String> {
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
    ) -> Result<ResidentPlaza, String> {
        let source_instances = placements.is_none();
        let placements = placements.unwrap_or_default();
        if placements.len() > MAX_MANUAL_PLACEMENTS {
            return Err("Plaza placement limit exceeded".into());
        }
        for placement in placements {
            if plaza::asset_ordinal(placement.asset_id).is_none()
                || placement.asset_hash != plaza::package_hash()
            {
                return Err("Authored plaza reference differs from its source".into());
            }
            placement.transform.validate().map_err(|e| e.to_string())?;
        }
        for prop in &self.props {
            if !source_instances
                && !placements
                    .iter()
                    .any(|placement| placement.asset_id == prop.asset_id)
            {
                continue;
            }
            for part in &prop.parts {
                if !materials.contains_key(&part.material) {
                    return Err("Plaza material is not resident".into());
                }
            }
        }
        let mut resident = ResidentPlaza {
            roots: Vec::new(),
            parts: Vec::new(),
            meshes: Vec::new(),
            colliders: Vec::new(),
            lights: Vec::new(),
            authored: Vec::new(),
            triangles: 0,
        };
        for prop in self.props {
            let instances: Vec<Option<&ManualPlacement>> = if source_instances {
                vec![None]
            } else {
                placements
                    .iter()
                    .filter(|placement| placement.asset_id == prop.asset_id)
                    .map(Some)
                    .collect()
            };
            if instances.is_empty() {
                continue;
            }
            let meshes = prop
                .parts
                .iter()
                .map(|part| world.resource_mut::<Assets<Mesh>>().add(part.mesh.clone()))
                .collect::<Vec<_>>();
            resident.meshes.extend(meshes.iter().cloned());
            for placement in instances {
                let frame = placement.map_or(prop.frame, |value| project(value.transform));
                let root = world
                    .spawn((
                        Name::new(prop.source_id.clone()),
                        ImportedPlazaProp(prop.source_id.clone()),
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
                for (part, mesh) in prop.parts.iter().zip(&meshes) {
                    resident.triangles +=
                        part.mesh.indices().map_or(0, |indices| indices.len() / 3);
                    let material = materials[&part.material].clone();
                    let mut entity = world.spawn((
                        Mesh3d(mesh.clone()),
                        MeshMaterial3d(material),
                        Transform::IDENTITY,
                        Visibility::Inherited,
                        ChildOf(root),
                    ));
                    if let Some(placement) = placement {
                        entity.insert(ManualPlacementPart(placement.id));
                    }
                    resident.parts.push(entity.id());
                }
                for collider in &prop.colliders {
                    let mut entity =
                        world.spawn((collider.shape.clone(), collider.transform, ChildOf(root)));
                    if let Some(placement) = placement {
                        entity.insert(ManualPlacementCollision(placement.id));
                    }
                    resident.colliders.push(entity.id());
                }
                for light in &prop.lights {
                    let entity = world
                        .spawn((
                            light.source.clone(),
                            Transform::from_translation(light.position),
                            Visibility::Inherited,
                            ChildOf(root),
                        ))
                        .id();
                    resident.lights.push(entity);
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
            }
        }
        if source_instances && resident.triangles != self.triangles {
            return Err("Staged plaza differs from pinned source totals".into());
        }
        Ok(resident)
    }
}

impl ResidentPlaza {
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
            && self.colliders.len() == self.roots.len()
            && self
                .roots
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && self
                .parts
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && self
                .lights
                .iter()
                .all(|&entity| world.get_entity(entity).is_ok())
            && world
                .get_resource::<Assets<Mesh>>()
                .is_some_and(|assets| self.meshes.iter().all(|mesh| assets.contains(mesh.id())))
            && self
                .colliders
                .iter()
                .all(|&entity| forge_engine::physics::static_collider_ready(world, entity))
    }

    pub fn retire(self, world: &mut World) {
        for entity in self
            .parts
            .into_iter()
            .chain(self.colliders)
            .chain(self.lights)
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
        return Err("Non-finite plaza source coordinate".into());
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
        return Err("Plaza source frame must retain its pinned basis".into());
    }
    Ok(Transform::from_translation(translation))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

    fn fixture() -> PreparedPlaza {
        PreparedPlaza {
            props: (0..5)
                .map(|ordinal| PreparedProp {
                    source_id: format!("fixture-plaza-{ordinal}"),
                    kind: "fixture".into(),
                    asset_id: plaza::asset_id(ordinal).unwrap(),
                    frame: Transform::from_xyz([0.0, 10.0, 20.0, 30.0, 40.0][ordinal], 0.0, 0.0),
                    parts: vec![PreparedPart {
                        mesh: Mesh::from(Cuboid::new(2.0, 1.0, 3.0)),
                        material: "fixture".into(),
                    }],
                    colliders: vec![PreparedCollider {
                        transform: Transform::from_xyz(1.0, 0.5, -2.0),
                        shape: forge_engine::physics::yawed_box_collider(
                            Vec3::new(1.0, 0.5, 1.5),
                            0.6,
                        )
                        .unwrap(),
                    }],
                    lights: if ordinal == 2 {
                        vec![PreparedLight {
                            position: Vec3::new(0.0, 2.0, 0.0),
                            source: PlazaLight {
                                color_linear: Vec3::ONE,
                                intensity_source: 1.0,
                            },
                        }]
                    } else {
                        Vec::new()
                    },
                })
                .collect(),
            triangles: 60,
        }
    }

    fn materials() -> BTreeMap<String, Handle<VillageMaterial>> {
        BTreeMap::from([("fixture".into(), Handle::default())])
    }

    #[test]
    fn plaza_authored_empty_projects_no_instances_and_source_stage_stays_explicit() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let empty = fixture()
            .stage_authored(&mut world, &BTreeMap::new(), &[])
            .unwrap();
        assert!(empty.is_synchronized(&world));
        assert!(empty.roots.is_empty());
        assert!(empty.parts.is_empty());
        assert!(empty.colliders.is_empty());
        assert!(empty.lights.is_empty());
        assert!(world.resource::<Assets<Mesh>>().is_empty());
        let source = fixture().stage(&mut world, &materials()).unwrap();
        assert_eq!(source.roots.len(), 5);
        assert_eq!(source.triangles, 60);
        assert_eq!(source.lights.len(), 1);
        assert!(source.authored_roots().is_empty());
    }

    #[test]
    fn plaza_authored_common_transform_scales_colliders_and_lights_beyond_old_cap() {
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
        let mut placements = (0..65)
            .map(|index| ManualPlacement {
                particles: None,
                physics: None,
                id: EntityId::deterministic(format!("plaza-common-{index}")),
                name: "Fixture plaza light".into(),
                asset_id: plaza::asset_id(2).unwrap(),
                asset_hash: plaza::package_hash(),
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
        assert_eq!(resident.roots.len(), 65);
        assert_eq!(resident.meshes.len(), 1);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 2);
        assert!(
            resident
                .parts
                .iter()
                .all(|&part| { app.world().get::<Mesh3d>(part).unwrap().0 == resident.meshes[0] })
        );
        assert_eq!(resident.lights.len(), 65);
        assert!(resident.is_synchronized(app.world()));
        assert!(forge_engine::manual_placements_ready(
            app.world_mut(),
            &placements
        ));
        for &collider in &resident.colliders {
            assert!(
                app.world()
                    .get::<Collider>(collider)
                    .unwrap()
                    .scale()
                    .abs_diff_eq(Vec3::new(2.0, 1.5, 0.5), 1.0e-4)
            );
        }
        let light_position = project(transform).transform_point(Vec3::new(0.0, 2.0, 0.0));
        for &light in &resident.lights {
            assert!(
                app.world()
                    .get::<GlobalTransform>(light)
                    .unwrap()
                    .translation()
                    .abs_diff_eq(light_position, 1.0e-4)
            );
        }
        let before = app.world().entities().len();
        placements[0].transform.scale.z = -1.0;
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
        assert!(forge_engine::manual_placements_ready(app.world_mut(), &[]));
    }

    #[test]
    fn pinned_plaza_prepares_source_and_authored_collisions() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_PLAZA_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = PlazaPackage::load(&manifest, plaza::package_hash(), |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let prepared = PreparedPlaza::from_package(&package).unwrap();
        assert_eq!(prepared.props.len(), 5);
        assert_eq!(prepared.triangles, 1_207);
        let templates = prepared.templates();
        assert_eq!(templates.len(), 5);
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let materials = ["material-0", "material-1"]
            .into_iter()
            .map(|name| (name.to_owned(), Handle::<VillageMaterial>::default()))
            .collect();
        let resident = prepared.stage(&mut world, &materials).unwrap();
        assert_eq!(resident.roots.len(), 5);
        assert_eq!(resident.colliders.len(), 5);
        assert_eq!(resident.lights.len(), 1);
        resident.retire(&mut world);
    }
}
