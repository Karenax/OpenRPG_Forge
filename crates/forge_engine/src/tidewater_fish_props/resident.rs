//! Accepted common placements own fish roots; both visual LODs remain resident.
use super::{PreparedFishProps, lod, material::FishMaterial};
use crate::{
    ManualPlacementCollisionExpected, ManualPlacementCollisionNotRequired, ManualPlacementPart,
    ManualPlacementProjection,
};
use bevy::prelude::*;
use forge_assets::tidewater_fish_props as source;
use forge_core::{AssetId, EntityId, Transform as ForgeTransform};
use forge_project::zone_authoring::placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement};
use std::collections::{BTreeMap, BTreeSet};

struct Instance {
    root: Entity,
    parts: [Entity; 2],
    materials: [Handle<FishMaterial>; 2],
    shadow_mesh: Handle<Mesh>,
    casts_shadow: bool,
}

/// The depth adapter must use the source fin mask, never an opaque substitute.
pub struct ShadowInstance {
    pub root: Entity,
    pub mesh: Handle<Mesh>,
    pub world_from_local: Mat4,
    pub time: f32,
    pub frame_index: u32,
}

#[derive(Resource)]
pub struct ResidentFishProps {
    instances: Vec<Instance>,
    authored: Vec<(Entity, EntityId, ForgeTransform)>,
    meshes: Vec<Handle<Mesh>>,
    materials: Vec<Handle<FishMaterial>>,
    time: f32,
    frame_index: u32,
}

impl PreparedFishProps {
    /// Stage only the accepted placement list. An empty list renders no defaults.
    /// Clones receive independent LOD uniforms while sharing their source geometry.
    ///
    /// # Errors
    /// Refuses duplicate identities, altered pins, invalid transforms, missing
    /// templates/materials or absent asset resources before changing the world.
    // Keep preflight and publication together so rejected batches never publish roots.
    #[allow(clippy::too_many_lines)]
    pub fn stage_authored(
        self,
        world: &mut World,
        materials: &BTreeMap<AssetId, Handle<FishMaterial>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentFishProps, String> {
        if placements.len() > MAX_MANUAL_PLACEMENTS {
            return Err("Fish placement limit exceeded".into());
        }
        let templates: BTreeMap<_, _> = self
            .templates
            .into_iter()
            .map(|template| (template.asset_id, template))
            .collect();
        let mut ids = BTreeSet::new();
        let store = world
            .get_resource::<Assets<FishMaterial>>()
            .ok_or("Fish materials absent")?;
        if !world.contains_resource::<Assets<Mesh>>() {
            return Err("Fish meshes absent".into());
        }
        let mut prepared = BTreeMap::new();
        for placement in placements {
            if !ids.insert(placement.id)
                || placement.asset_hash != source::package_hash()
                || source::asset_ordinal(placement.asset_id).is_none()
            {
                return Err("Duplicate or altered common fish placement".into());
            }
            placement
                .transform
                .validate()
                .map_err(|error| error.to_string())?;
            let template = templates
                .get(&placement.asset_id)
                .ok_or("Fish template absent")?;
            if template.lod_meshes.len() != 3 {
                return Err("Fish requires high, low and masked shadow geometry".into());
            }
            let handle = materials
                .get(&placement.asset_id)
                .ok_or("Fish material absent")?;
            let material = store.get(handle).ok_or("Fish material not resident")?;
            prepared.insert(placement.asset_id, material.clone());
        }
        let mut resident = ResidentFishProps {
            instances: Vec::new(),
            authored: Vec::new(),
            meshes: Vec::new(),
            materials: Vec::new(),
            time: 0.0,
            frame_index: 0,
        };
        for (asset, template) in templates {
            let accepted: Vec<_> = placements.iter().filter(|p| p.asset_id == asset).collect();
            if accepted.is_empty() {
                continue;
            }
            let meshes: Vec<_> = template
                .lod_meshes
                .into_iter()
                .map(|mesh| world.resource_mut::<Assets<Mesh>>().add(mesh))
                .collect();
            resident.meshes.extend(meshes.iter().cloned());
            for placement in accepted {
                let root = world
                    .spawn((
                        Name::new(template.source_id.clone()),
                        project(placement.transform),
                        GlobalTransform::default(),
                        Visibility::Inherited,
                        ManualPlacementProjection(placement.clone()),
                        ManualPlacementCollisionNotRequired(placement.id),
                        ManualPlacementCollisionExpected {
                            placement: placement.id,
                            primitives: Vec::new(),
                        },
                    ))
                    .id();
                let handles: [_; 2] = std::array::from_fn(|_| {
                    world
                        .resource_mut::<Assets<FishMaterial>>()
                        .add(prepared[&asset].clone())
                });
                let parts = std::array::from_fn(|lod| {
                    world
                        .spawn((
                            Mesh3d(meshes[lod].clone()),
                            MeshMaterial3d(handles[lod].clone()),
                            Transform::IDENTITY,
                            GlobalTransform::default(),
                            Visibility::Hidden,
                            ChildOf(root),
                            ManualPlacementPart(placement.id),
                        ))
                        .id()
                });
                resident.materials.extend(handles.iter().cloned());
                resident
                    .authored
                    .push((root, placement.id, placement.transform));
                resident.instances.push(Instance {
                    root,
                    parts,
                    materials: handles,
                    shadow_mesh: meshes[2].clone(),
                    casts_shadow: false,
                });
            }
        }
        Ok(resident)
    }
}

impl ResidentFishProps {
    #[must_use]
    pub fn authored_roots(&self) -> &[(Entity, EntityId, ForgeTransform)] {
        &self.authored
    }

    #[must_use]
    pub fn authored_parts(&self, id: EntityId) -> Option<[Entity; 2]> {
        let root = self.authored.iter().find(|(_, placed, _)| *placed == id)?.0;
        self.instances
            .iter()
            .find(|instance| instance.root == root)
            .map(|instance| instance.parts)
    }

    /// The caller supplies the actual camera's sphere/frustum intersection.
    /// Original uniform scales retain the source radius L*0.8; nonuniform author
    /// scales use their largest axis as a conservative bound and LOD length.
    ///
    /// # Errors
    /// Refuses stale roots, transforms, parts/materials or invalid camera/time
    /// before changing visibility or uniforms for any instance.
    pub fn update_view(
        &mut self,
        world: &mut World,
        camera: Vec3,
        mut intersects: impl FnMut(Vec3, f32) -> bool,
        time: f32,
        frame_index: u32,
    ) -> Result<(), String> {
        if !camera.is_finite() || !time.is_finite() {
            return Err("Invalid fish camera or time".into());
        }
        let materials = world
            .get_resource::<Assets<FishMaterial>>()
            .ok_or("Fish materials absent")?;
        let plans = self
            .instances
            .iter()
            .map(|instance| {
                let global = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Fish root transform stale")?;
                let (scale, _, center) = global.to_scale_rotation_translation();
                let length = scale.abs().max_element();
                if !center.is_finite()
                    || !length.is_finite()
                    || length <= 0.0
                    || instance
                        .parts
                        .iter()
                        .any(|part| world.get::<Visibility>(*part).is_none())
                    || instance
                        .materials
                        .iter()
                        .any(|handle| materials.get(handle).is_none())
                {
                    return Err("Stale fish placement or render resource".to_owned());
                }
                let delta = center.as_dvec3() - camera.as_dvec3();
                let distance = delta.length();
                lod::select(
                    distance,
                    f64::from(length),
                    intersects(center, length * 0.8),
                )
            })
            .collect::<Result<Vec<_>, _>>()?;
        world.resource_scope(
            |world, mut materials: Mut<Assets<FishMaterial>>| -> Result<(), String> {
                for (instance, plan) in self.instances.iter_mut().zip(plans) {
                    instance.casts_shadow = plan.shadow;
                    for lod in 0..2 {
                        let draw = plan.draws.iter().find(|draw| draw.lod == lod);
                        *world
                            .entity_mut(instance.parts[lod])
                            .get_mut::<Visibility>()
                            .ok_or("Fish visibility disappeared after preflight")? =
                            if draw.is_some() {
                                Visibility::Inherited
                            } else {
                                Visibility::Hidden
                            };
                        let mut material = materials
                            .get_mut(&instance.materials[lod])
                            .ok_or("Fish material disappeared after preflight")?;
                        material.settings.info.w = time;
                        material.settings.frame_index = frame_index;
                        if let Some(draw) = draw {
                            material.settings.lod =
                                Vec4::new(draw.fade, f32::from(u8::from(draw.outgoing)), 0.0, 0.0);
                        }
                    }
                }
                Ok(())
            },
        )?;
        self.time = time;
        self.frame_index = frame_index;
        Ok(())
    }

    /// Stable full caster inventory; flags retain source distance culling.
    ///
    /// # Errors
    /// Refuses roots without a finite propagated transform.
    pub fn shadow_inventory(&self, world: &World) -> Result<Vec<(ShadowInstance, bool)>, String> {
        self.instances
            .iter()
            .map(|instance| {
                let model = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Fish shadow transform absent")?
                    .to_matrix();
                if !model.is_finite() {
                    return Err("Fish shadow transform invalid".into());
                }
                Ok((
                    ShadowInstance {
                        root: instance.root,
                        mesh: instance.shadow_mesh.clone(),
                        world_from_local: model,
                        time: self.time,
                        frame_index: self.frame_index,
                    },
                    instance.casts_shadow,
                ))
            })
            .collect()
    }

    /// Selected low-detail proxies for a dedicated masked shadow pass.
    ///
    /// # Errors
    /// Refuses a lost source root rather than publishing a stale caster matrix.
    pub fn shadow_instances(&self, world: &World) -> Result<Vec<ShadowInstance>, String> {
        self.instances
            .iter()
            .filter(|instance| instance.casts_shadow)
            .map(|instance| {
                let matrix = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Fish shadow root absent")?
                    .to_matrix();
                if !matrix.is_finite() {
                    return Err("Fish shadow matrix invalid".into());
                }
                Ok(ShadowInstance {
                    root: instance.root,
                    mesh: instance.shadow_mesh.clone(),
                    world_from_local: matrix,
                    time: self.time,
                    frame_index: self.frame_index,
                })
            })
            .collect()
    }

    pub fn clear(self, world: &mut World) {
        for instance in self.instances {
            world.despawn(instance.root);
        }
        for handle in self.meshes {
            world.resource_mut::<Assets<Mesh>>().remove(handle.id());
        }
        for handle in self.materials {
            world
                .resource_mut::<Assets<FishMaterial>>()
                .remove(handle.id());
        }
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
#[cfg(test)]
mod tests {
    use super::*;
    use crate::tidewater_assets::materials::LightingBindings;
    use crate::tidewater_material::{ObjectShadowSettings, SceneLighting};

    fn fixture() -> (
        PreparedFishProps,
        World,
        BTreeMap<AssetId, Handle<FishMaterial>>,
        ManualPlacement,
    ) {
        let root =
            std::path::PathBuf::from(std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").unwrap());
        let package = source::FishPropPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            source::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap();
        let prepared = PreparedFishProps::from_package(&package).unwrap();
        let template = &prepared.templates[0];
        let mut images = Assets::<Image>::default();
        let lighting = LightingBindings {
            scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [3.0; 3], [[0.4; 3]; 9]).unwrap(),
            environment: Handle::default(),
            hill_shadow: Handle::default(),
            object_shadow: Handle::default(),
            object_settings: ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            wind: Vec4::ZERO,
            flags: Vec4::ZERO,
            caustics: crate::tidewater_caustics::CausticMaterialInputs::disabled(&mut images),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        };
        let mut materials = Assets::<FishMaterial>::default();
        let handle = materials.add(
            super::super::material::prepare(template.surface, Handle::default(), &lighting, 0.0)
                .unwrap(),
        );
        let placement = ManualPlacement {
            id: EntityId::deterministic("fish-resident-test"),
            name: "fish".into(),
            asset_id: template.asset_id,
            asset_hash: source::package_hash(),
            transform: template.transform,
            locked: false,
        };
        let handles = BTreeMap::from([(placement.asset_id, handle)]);
        let mut world = World::new();
        world.insert_resource(materials);
        world.insert_resource(Assets::<Mesh>::default());
        (prepared, world, handles, placement)
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn accepted_clones_share_geometry_keep_independent_fades_and_masked_shadow_selection() {
        let (prepared, mut world, handles, placement) = fixture();
        let mut clone = placement.clone();
        clone.id = EntityId::deterministic("fish-resident-clone");
        let mut resident = prepared
            .stage_authored(&mut world, &handles, &[placement.clone(), clone.clone()])
            .unwrap();
        assert_eq!(resident.authored_roots().len(), 2);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 3);
        assert_eq!(world.resource::<Assets<FishMaterial>>().len(), 5);
        assert_ne!(
            resident.instances[0].materials,
            resident.instances[1].materials
        );
        assert_eq!(
            world
                .get::<ManualPlacementProjection>(resident.instances[0].root)
                .unwrap()
                .0,
            placement
        );
        for (index, instance) in resident.instances.iter().enumerate() {
            world
                .entity_mut(instance.root)
                .insert(GlobalTransform::from(Transform::from_xyz(
                    if index == 0 { 10.0 } else { 95.0 },
                    0.0,
                    0.0,
                )));
        }
        resident
            .update_view(&mut world, Vec3::ZERO, |_, _| false, 1.25, 7)
            .unwrap();
        assert_eq!(resident.shadow_instances(&world).unwrap().len(), 1);
        for instance in &resident.instances {
            for part in instance.parts {
                assert_eq!(world.get::<Visibility>(part), Some(&Visibility::Hidden));
            }
        }
        resident
            .update_view(&mut world, Vec3::ZERO, |_, _| true, 2.0, 8)
            .unwrap();
        assert_eq!(
            world.get::<Visibility>(resident.instances[0].parts[0]),
            Some(&Visibility::Inherited)
        );
        assert_eq!(
            world.get::<Visibility>(resident.instances[1].parts[0]),
            Some(&Visibility::Hidden)
        );
        let material = world
            .resource::<Assets<FishMaterial>>()
            .get(&resident.instances[0].materials[0])
            .unwrap();
        assert_eq!(material.settings.info.w.to_bits(), 2.0_f32.to_bits());
        assert_eq!(material.settings.frame_index, 8);
        resident.clear(&mut world);
        assert_eq!(
            world
                .query::<&ManualPlacementProjection>()
                .iter(&world)
                .count(),
            0
        );
        assert_eq!(
            world.query::<&ManualPlacementPart>().iter(&world).count(),
            0
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<FishMaterial>>().len(), 1);
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn removed_inventory_has_no_defaults_and_duplicate_batch_publishes_nothing() {
        let (prepared, mut world, handles, _) = fixture();
        let resident = prepared.stage_authored(&mut world, &handles, &[]).unwrap();
        assert!(resident.authored_roots().is_empty());
        assert_eq!(
            world
                .query::<&ManualPlacementProjection>()
                .iter(&world)
                .count(),
            0
        );
        assert_eq!(
            world.query::<&ManualPlacementPart>().iter(&world).count(),
            0
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        let (prepared, mut world, handles, placement) = fixture();
        assert!(
            prepared
                .stage_authored(&mut world, &handles, &[placement.clone(), placement])
                .is_err()
        );
        assert_eq!(
            world
                .query::<&ManualPlacementProjection>()
                .iter(&world)
                .count(),
            0
        );
        assert_eq!(
            world.query::<&ManualPlacementPart>().iter(&world).count(),
            0
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<FishMaterial>>().len(), 1);
    }
}
