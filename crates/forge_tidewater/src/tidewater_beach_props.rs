//! Verified separately placeable beach props in the shared Bevy/Rapier world.
use bevy::prelude::*;
use bevy_rapier3d::prelude::Collider;
use forge_core::{
    AssetId, EntityId, Transform as ForgeTransform, UnitQuaternion, Vec3 as ForgeVec3,
};
use forge_project::zone_authoring::placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement};
use forge_tidewater_assets::tidewater_beach_props::{self as beach_props, BeachPropPackage};
use serde::Deserialize;
use std::collections::BTreeMap;
use {
    crate::{tidewater_material::VillageMaterial, tidewater_mesh::VillageMeshBytes},
    forge_engine::{
        ManualPlacementCollision, ManualPlacementCollisionExpected,
        ManualPlacementCollisionNotRequired, ManualPlacementPart, ManualPlacementProjection,
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
    source: BeachPropsLight,
}

/// Source light coordinates and intensity are retained; photometric calibration is separate.
#[derive(Component, Clone)]
pub struct BeachPropsLight {
    pub color_linear: Vec3,
    pub intensity_source: f32,
}

/// Verified CPU projection; source bytes never become serialized Bevy handles.
pub struct PreparedBeachProps {
    props: Vec<PreparedProp>,
    triangles: usize,
}

#[derive(Clone)]
pub struct BeachPropsTemplate {
    pub source_id: String,
    pub kind: String,
    pub asset_id: AssetId,
    pub transform: ForgeTransform,
}

#[derive(Component)]
pub struct ImportedBeachPropsProp(pub String);

#[derive(Resource)]
pub struct ResidentBeachProps {
    roots: Vec<Entity>,
    parts: Vec<Entity>,
    meshes: Vec<Handle<Mesh>>,
    colliders: Vec<Entity>,
    collider_counts: Vec<(Entity, usize)>,
    lights: Vec<Entity>,
    authored: Vec<(Entity, EntityId, ForgeTransform)>,
    pub triangles: usize,
}

impl PreparedBeachProps {
    #[allow(
        clippy::too_many_lines,
        reason = "Validates and prepares one pinned geometry, collider, and provenance contract in source order."
    )]
    /// Prepare the verified geometry and collider inventory.
    ///
    /// # Errors
    /// Rejects missing or invalid geometry, transforms, roles or source totals.
    pub fn from_package(package: &BeachPropPackage) -> Result<Self, String> {
        let description: Description =
            serde_json::from_slice(package.description()).map_err(|e| e.to_string())?;
        if description.props.len() != 43 {
            return Err("Forty-three source beach props required".into());
        }
        let mut props = Vec::with_capacity(43);
        let mut triangles = 0;
        for (ordinal, source) in description.props.into_iter().enumerate() {
            const KINDS: [&str; 43] = [
                "rowboat",
                "rowboat",
                "rowboat",
                "rowboat",
                "wreck",
                "oar",
                "oar",
                "netRack",
                "netRack",
                "netRack",
                "fishRack",
                "barrel",
                "barrel",
                "crate",
                "crate",
                "trap",
                "trap",
                "trap",
                "crate",
                "trap",
                "trap",
                "trap",
                "trap",
                "barrel",
                "crate",
                "crate",
                "trap",
                "trap",
                "barrel",
                "crate",
                "crate",
                "barrel",
                "trap",
                "trap",
                "crate",
                "buoy",
                "buoy",
                "buoy",
                "buoy",
                "buoy",
                "ropeCoil",
                "ropeCoil",
                "buoyFence",
            ];
            let expected_kind = KINDS[ordinal];
            if source.source_id
                != format!(
                    "tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:beach-prop:{ordinal}"
                )
                || source.kind != expected_kind
                || source.parts.is_empty()
                || source.parts.len() > 3
            {
                return Err("Source beach_props prop differs".into());
            }
            let frame = source_frame(source.source_frame)?;
            let mut parts = Vec::with_capacity(source.parts.len());
            for part in source.parts {
                let file = |reference: &FileReference| {
                    package
                        .file(&reference.file)
                        .ok_or("BeachProps mesh payload absent")
                };
                let attr = |name: &str| {
                    part.attributes
                        .get(name)
                        .ok_or("BeachProps mesh attribute absent")
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
                triangles += mesh.indices().ok_or("BeachProps indices absent")?.len() / 3;
                parts.push(PreparedPart {
                    mesh,
                    material: part.material,
                });
            }
            let mut colliders = Vec::new();
            for box_source in source.colliders.boxes {
                if !matches!(
                    box_source.tag.as_str(),
                    "wreck" | "rack" | "crate" | "trap" | "rowboat"
                ) || !box_source.solid
                {
                    return Err("Unexpected beach_props box collision role".into());
                }
                let center = vec3(box_source.center)?;
                let half = vec3(box_source.half)?;
                if half.min_element() <= 0.0 || half.max_element() > 32.0 {
                    return Err("Invalid beach_props box extent".into());
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
                if !matches!(cylinder.tag.as_str(), "post" | "rack" | "barrel") {
                    return Err("Unexpected beach_props cylinder collision role".into());
                }
                let (x, z, radius, y_min, y_max) = (
                    finite(cylinder.x)?,
                    finite(cylinder.z)?,
                    finite(cylinder.radius)?,
                    finite(cylinder.y_min)?,
                    finite(cylinder.y_max)?,
                );
                if radius <= 0.0 || radius > 1.0 || y_max <= y_min || y_max - y_min > 4.0 {
                    return Err("Invalid beach_props cylinder extent".into());
                }
                colliders.push(PreparedCollider {
                    transform: Transform::from_xyz(x, f32::midpoint(y_min, y_max), z),
                    shape: Collider::cylinder((y_max - y_min) * 0.5, radius),
                });
            }
            if colliders.len() > 5 || !source.lights.is_empty() {
                return Err("BeachProps collider or lantern light absent".into());
            }
            let mut lights = Vec::new();
            for light in source.lights {
                if light.kind != "lantern" || light.intensity <= 0.0 {
                    return Err("Unexpected beach_props light".into());
                }
                let position = vec3(light.position)?;
                let color = vec3(light.color)?;
                lights.push(PreparedLight {
                    position,
                    source: BeachPropsLight {
                        color_linear: color,
                        intensity_source: finite(light.intensity)?,
                    },
                });
            }
            props.push(PreparedProp {
                source_id: source.source_id,
                kind: source.kind,
                asset_id: beach_props::asset_id(ordinal).ok_or("BeachProps asset ID absent")?,
                frame,
                parts,
                colliders,
                lights,
            });
        }
        if triangles != 18_807 {
            return Err("BeachProps source geometry incomplete".into());
        }
        Ok(Self { props, triangles })
    }

    #[must_use]
    pub fn templates(&self) -> Vec<BeachPropsTemplate> {
        self.props
            .iter()
            .map(|prop| BeachPropsTemplate {
                source_id: prop.source_id.clone(),
                kind: prop.kind.clone(),
                asset_id: prop.asset_id,
                transform: forge_transform(prop.frame),
            })
            .collect()
    }

    /// Stage the explicit source inventory.
    ///
    /// # Errors
    /// Rejects missing resident materials or inconsistent source totals.
    pub fn stage(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
    ) -> Result<ResidentBeachProps, String> {
        self.stage_instances(world, materials, None)
    }

    /// Stage only the accepted common placements.
    ///
    /// # Errors
    /// Rejects invalid placement transforms, references, limits or missing materials.
    pub fn stage_authored(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentBeachProps, String> {
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
    ) -> Result<ResidentBeachProps, String> {
        let source_instances = placements.is_none();
        let placements = placements.unwrap_or_default();
        if placements.len() > MAX_MANUAL_PLACEMENTS {
            return Err("BeachProps placement limit exceeded".into());
        }
        for placement in placements {
            if beach_props::asset_ordinal(placement.asset_id).is_none()
                || placement.asset_hash != beach_props::package_hash()
            {
                return Err("Authored beach_props reference differs from its source".into());
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
                    return Err("BeachProps material is not resident".into());
                }
            }
        }
        let mut resident = ResidentBeachProps {
            roots: Vec::new(),
            parts: Vec::new(),
            meshes: Vec::new(),
            colliders: Vec::new(),
            collider_counts: Vec::new(),
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
                        ImportedBeachPropsProp(prop.source_id.clone()),
                        frame,
                        Visibility::Inherited,
                    ))
                    .id();
                resident.roots.push(root);
                resident.collider_counts.push((root, prop.colliders.len()));
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
                    if prop.colliders.is_empty() {
                        world
                            .entity_mut(root)
                            .insert(ManualPlacementCollisionNotRequired(placement.id));
                    }
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
            return Err("Staged beach_props differs from pinned source totals".into());
        }
        Ok(resident)
    }
}

impl ResidentBeachProps {
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
            && self.collider_counts.len() == self.roots.len()
            && self.collider_counts.iter().all(|&(root, expected)| {
                self.colliders
                    .iter()
                    .filter(|&&entity| {
                        world
                            .get::<ChildOf>(entity)
                            .is_some_and(|parent| parent.parent() == root)
                    })
                    .count()
                    == expected
            })
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
        return Err("Non-finite beach_props source coordinate".into());
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
        return Err("BeachProps source frame must retain its pinned basis".into());
    }
    Ok(Transform::from_translation(translation))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

    fn fixture() -> PreparedBeachProps {
        let root = std::path::PathBuf::from(
            std::env::var_os("FORGE_TIDEWATER_BEACH_PROP_PACKAGE")
                .expect("The pinned package fixture is required for this integration test"),
        );
        let bytes = std::fs::read(root.join("package.json")).unwrap();
        let package = BeachPropPackage::load(&bytes, beach_props::package_hash(), |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        PreparedBeachProps::from_package(&package).unwrap()
    }

    fn materials() -> BTreeMap<String, Handle<VillageMaterial>> {
        ["material-0", "material-1", "material-6"]
            .into_iter()
            .map(|name| (name.to_owned(), Handle::default()))
            .collect()
    }

    #[test]
    #[ignore = "Requires the hash-pinned beach prop package"]
    fn source_inventory_and_empty_authored_scene_remain_distinct() {
        let prepared = fixture();
        assert_eq!(prepared.props.len(), 43);
        assert_eq!(prepared.triangles, 18_807);
        assert_eq!(
            prepared
                .props
                .iter()
                .map(|prop| prop.parts.len())
                .sum::<usize>(),
            78
        );
        assert_eq!(
            prepared
                .props
                .iter()
                .filter(|prop| prop.colliders.is_empty())
                .count(),
            14
        );
        assert!(prepared.props.iter().any(|prop| prop.colliders.len() == 5));
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let empty = prepared
            .stage_authored(&mut world, &BTreeMap::new(), &[])
            .unwrap();
        assert!(empty.is_synchronized(&world));
        assert!(world.resource::<Assets<Mesh>>().is_empty());
        empty.retire(&mut world);
        let resident = fixture().stage(&mut world, &materials()).unwrap();
        assert_eq!(resident.roots.len(), 43);
        assert_eq!(resident.render_parts().len(), 78);
        assert_eq!(resident.triangles, 18_807);
        assert!(resident.authored_roots().is_empty());
        assert!(!resident.is_synchronized(&world));
        resident.retire(&mut world);
        assert!(world.resource::<Assets<Mesh>>().is_empty());
    }

    #[test]
    #[ignore = "Requires the hash-pinned beach prop package"]
    fn all_source_prototypes_use_common_projection_and_real_physics() {
        let prepared = fixture();
        let mut placements = prepared
            .templates()
            .into_iter()
            .enumerate()
            .map(|(ordinal, template)| ManualPlacement {
                particles: None,
                physics: None,
                id: EntityId::deterministic(format!("beach-common-{ordinal}")),
                name: template.kind,
                asset_id: template.asset_id,
                asset_hash: beach_props::package_hash(),
                transform: template.transform,
                locked: false,
            })
            .collect::<Vec<_>>();
        placements.sort_by_key(|placement| placement.id);
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        let resident = prepared
            .stage_authored(app.world_mut(), &materials(), &placements)
            .unwrap();
        assert!(!forge_engine::manual_placements_ready(
            app.world_mut(),
            &placements
        ));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(resident.is_synchronized(app.world()));
        assert!(forge_engine::manual_placements_ready(
            app.world_mut(),
            &placements
        ));
        let zero = resident
            .collider_counts
            .iter()
            .filter(|(_, count)| *count == 0)
            .count();
        assert_eq!(zero, 14);
        for &(root, id, _) in resident.authored_roots() {
            assert!(!resident.authored_parts(app.world(), id).unwrap().is_empty());
            if app
                .world()
                .get::<ManualPlacementCollisionNotRequired>(root)
                .is_some()
            {
                assert!(
                    app.world()
                        .get::<ManualPlacementCollisionExpected>(root)
                        .unwrap()
                        .primitives
                        .is_empty()
                );
            }
        }
        // A collider kept alive but detached from its instance must invalidate readiness.
        let collider = resident.colliders[0];
        let parent = app.world().get::<ChildOf>(collider).unwrap().parent();
        app.world_mut().entity_mut(collider).remove::<ChildOf>();
        assert!(!resident.is_synchronized(app.world()));
        assert!(!forge_engine::manual_placements_ready(
            app.world_mut(),
            &placements
        ));
        app.world_mut().entity_mut(collider).insert(ChildOf(parent));
        assert!(resident.is_synchronized(app.world()));
        resident.retire(app.world_mut());
        assert!(app.world().resource::<Assets<Mesh>>().is_empty());
        assert!(forge_engine::manual_placements_ready(app.world_mut(), &[]));
    }
}
