//! Tidewater's verified fish identities feed the common engine residency.
use super::{PreparedFishProps, lod};
use bevy::prelude::*;
use forge_core::AssetId;
use forge_engine::source_fish::{
    material::FishMaterial,
    prepare,
    resident::{self, ResidentFishProps},
};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater_assets::tidewater_fish_props as source;
use std::collections::BTreeMap;

impl PreparedFishProps {
    /// Stage acquired village fish through the engine's generic resident host.
    ///
    /// # Errors
    /// Rejects altered Tidewater identities or pins and any invalid engine batch.
    pub fn stage_authored(
        self,
        world: &mut World,
        materials: &BTreeMap<AssetId, Handle<FishMaterial>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentFishProps, String> {
        if placements.iter().any(|placement| {
            placement.asset_hash != source::package_hash()
                || source::asset_ordinal(placement.asset_id).is_none()
        }) {
            return Err("Altered Tidewater fish placement".into());
        }
        let templates = prepare::resident_templates(self.templates, source::package_hash());
        resident::stage_authored(templates, world, materials, placements, lod::POLICY)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::storage::ShaderBuffer;
    use forge_core::EntityId;
    use forge_engine::source_fish::material as fish_material;
    use forge_engine::source_lighting::SceneLighting;
    use forge_engine::source_shadows::ObjectShadowSettings;
    use forge_engine::source_surface::LightingBindings;
    use forge_engine::{ManualPlacementPart, ManualPlacementProjection};

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
            caustics: forge_engine::source_surface::underwater::CausticMaterialInputs::disabled(
                &mut images,
            ),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        };
        let mut materials = Assets::<FishMaterial>::default();
        let handle = materials.add(
            fish_material::prepare(template.surface, Handle::default(), &lighting, 0.0).unwrap(),
        );
        let placement = ManualPlacement {
            particles: None,
            physics: None,
            local_lights: None,
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
        world.insert_resource(Assets::<ShaderBuffer>::default());
        (prepared, world, handles, placement)
    }

    fn assert_source_shadow_cache(world: &World, resident: &ResidentFishProps) {
        let inventory = resident.shadow_inventory(world).unwrap();
        assert!(!inventory.is_empty());
        let expected: Vec<_> = inventory
            .iter()
            .map(|(instance, enabled)| {
                (
                    forge_engine::source_fish::shadow::masked_mesh(
                        world
                            .resource::<Assets<Mesh>>()
                            .get(&instance.mesh)
                            .unwrap(),
                    )
                    .unwrap(),
                    instance.world_from_local,
                    *enabled,
                )
            })
            .collect();
        let mut cache = forge_engine::source_fish::shadow::cache::MaskedFishCache::default();
        let frame = cache
            .frame(
                world,
                inventory,
                Handle::default(),
                std::sync::Arc::new("verified source mask".into()),
                2.0,
            )
            .unwrap()
            .unwrap();
        for ((mesh, pose), (original, transform, enabled)) in
            frame.meshes.iter().zip(&frame.poses).zip(expected)
        {
            assert_eq!(mesh.vertices, original.vertices);
            assert_eq!(mesh.indices, original.indices);
            assert_eq!(pose.model, transform);
            assert_eq!(pose.enabled, enabled);
        }
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
        let first_root = resident
            .authored_roots()
            .iter()
            .find(|(_, id, _)| *id == placement.id)
            .unwrap()
            .0;
        let second_root = resident
            .authored_roots()
            .iter()
            .find(|(_, id, _)| *id == clone.id)
            .unwrap()
            .0;
        let first_parts = resident.authored_parts(placement.id).unwrap();
        let second_parts = resident.authored_parts(clone.id).unwrap();
        let first_materials = first_parts.map(|part| {
            world
                .get::<MeshMaterial3d<FishMaterial>>(part)
                .unwrap()
                .0
                .clone()
        });
        let second_materials = second_parts.map(|part| {
            world
                .get::<MeshMaterial3d<FishMaterial>>(part)
                .unwrap()
                .0
                .clone()
        });
        assert_ne!(first_materials, second_materials);
        assert_eq!(
            world
                .get::<ManualPlacementProjection>(first_root)
                .unwrap()
                .0,
            placement
        );
        for (root, x) in [(first_root, 10.0), (second_root, 95.0)] {
            world
                .entity_mut(root)
                .insert(GlobalTransform::from(Transform::from_xyz(x, 0.0, 0.0)));
        }
        resident
            .update_view(&mut world, Vec3::ZERO, |_, _| false, 1.25, 7)
            .unwrap();
        assert_eq!(resident.shadow_instances(&world).unwrap().len(), 1);
        for part in first_parts.into_iter().chain(second_parts) {
            assert_eq!(world.get::<Visibility>(part), Some(&Visibility::Hidden));
        }
        resident
            .update_view(&mut world, Vec3::ZERO, |_, _| true, 2.0, 8)
            .unwrap();
        assert_eq!(
            world.get::<Visibility>(first_parts[0]),
            Some(&Visibility::Inherited)
        );
        assert_eq!(
            world.get::<Visibility>(second_parts[0]),
            Some(&Visibility::Hidden)
        );
        assert_source_shadow_cache(&world, &resident);
        let material = world
            .resource::<Assets<FishMaterial>>()
            .get(&first_materials[0])
            .unwrap();
        let expected: Vec<_> = [2.0_f32.to_bits(), 8, 1, 0]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&material.clock)
                .unwrap()
                .data,
            Some(expected)
        );
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

        let (prepared, mut world, handles, placement) = fixture();
        world.remove_resource::<Assets<ShaderBuffer>>();
        assert!(
            prepared
                .stage_authored(&mut world, &handles, &[placement])
                .is_err()
        );
        assert_eq!(
            world
                .query::<&ManualPlacementProjection>()
                .iter(&world)
                .count(),
            0
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<FishMaterial>>().len(), 1);
    }

    #[test]
    #[ignore = "Requires the byte-pinned fish package; run the documented external-fixture replay"]
    fn authored_clones_share_a_clock_without_time_only_material_changes() {
        use bevy::render::storage::ShaderBuffer;

        let (prepared, mut world, handles, placement) = fixture();
        world.init_resource::<Assets<ShaderBuffer>>();
        let mut clone = placement.clone();
        clone.id = EntityId::deterministic("fish-shared-clock-clone");
        let mut resident = prepared
            .stage_authored(&mut world, &handles, &[placement.clone(), clone.clone()])
            .unwrap();
        let parts = [placement.id, clone.id].map(|id| resident.authored_parts(id).unwrap()[0]);
        let materials = parts.map(|part| {
            world
                .get::<MeshMaterial3d<FishMaterial>>(part)
                .unwrap()
                .0
                .clone()
        });
        let store = world.resource::<Assets<FishMaterial>>();
        let clock = store.get(&materials[0]).unwrap().clock.clone();
        assert_ne!(clock.id(), forge_engine::source_fish::clock::NEUTRAL.id());
        assert_eq!(store.get(&materials[1]).unwrap().clock, clock);
        resident
            .update_view(&mut world, Vec3::new(10.0, 0.0, 0.0), |_, _| true, 0.0, 0)
            .unwrap();
        let before = materials.each_ref().map(|handle| {
            world
                .resource::<Assets<FishMaterial>>()
                .get(handle)
                .unwrap()
                .settings
                .clone()
        });
        resident
            .update_view(&mut world, Vec3::new(10.0, 0.0, 0.0), |_, _| true, 1.25, 7)
            .unwrap();
        for (handle, expected) in materials.iter().zip(before) {
            assert_eq!(
                world
                    .resource::<Assets<FishMaterial>>()
                    .get(handle)
                    .unwrap()
                    .settings,
                expected
            );
        }
        let expected: Vec<_> = [1.25_f32.to_bits(), 7, 1, 0]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&clock)
                .unwrap()
                .data,
            Some(expected)
        );
        resident.clear(&mut world);
        assert!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&clock)
                .is_none()
        );
    }
}
