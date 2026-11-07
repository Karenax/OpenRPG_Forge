//! Prepare verified fish instance templates and meshes without substituting a material.
#[path = "tidewater_fish_props/lod.rs"]
pub mod lod;
#[cfg(test)]
#[path = "tidewater_fish_props/material.rs"]
mod material;
#[cfg(test)]
#[path = "tidewater_fish_props/pose_reference.rs"]
mod pose_reference;
#[path = "tidewater_fish_props/resident.rs"]
mod resident;
#[cfg(test)]
#[path = "tidewater_fish_props/shadow.rs"]
mod shadow;

#[cfg(test)]
use forge_engine::source_fish::geometry::{FISH_DATA, REST_POSITION};
use forge_engine::source_fish::prepare::{self, FishPropTemplate};
#[cfg(test)]
use forge_tidewater_assets::tidewater_fish_props as fish_props;
use forge_tidewater_assets::tidewater_fish_props::FishPropPackage;

#[cfg(test)]
mod description_fixture {
    use serde::Deserialize;
    use std::collections::BTreeMap;
    #[derive(Deserialize)]
    pub(super) struct Description {
        pub prototypes: Vec<Prototype>,
        pub instances: Vec<Instance>,
    }
    #[derive(Deserialize)]
    pub(super) struct Prototype {
        pub key: String,
        pub lods: Vec<Geometry>,
    }
    #[derive(Deserialize)]
    pub(super) struct Geometry {
        pub vertices: usize,
        pub triangles: usize,
        pub attributes: BTreeMap<String, Attribute>,
    }
    #[derive(Deserialize)]
    pub(super) struct Attribute {
        pub file: String,
    }
    #[derive(Deserialize)]
    pub(super) struct Instance {
        pub source_id: String,
        pub prototype: String,
        pub record_f32: [f32; 16],
    }
}
#[cfg(test)]
use description_fixture::Description;

/// Verified CPU meshes; no GPU handles, colliders or render entities are published here.
pub struct PreparedFishProps {
    pub templates: Vec<FishPropTemplate>,
}

impl PreparedFishProps {
    #[must_use]
    pub fn templates(&self) -> Vec<prepare::FishPropsTemplate> {
        prepare::summaries(&self.templates)
    }

    /// Prepare the admitted village profile using its own declared identities.
    ///
    /// # Errors
    /// Rejects invalid profiles, identities, poses or source geometry.
    pub fn from_package(package: &FishPropPackage) -> Result<Self, String> {
        Ok(Self {
            templates: prepare::profile(package)?,
        })
    }

    /// Prepare the five displayed game fish, excluding the parked catch pool.
    ///
    /// # Errors
    /// Rejects invalid profiles, identities, poses or source geometry.
    pub fn from_game_package(
        package: &forge_tidewater_assets::tidewater_game_fish::GameFishPackage,
    ) -> Result<Self, String> {
        Ok(Self {
            templates: prepare::profile(package)?,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{mesh::VertexAttributeValues, prelude::Mesh};
    use std::path::Path;

    fn assert_same_prepared(expected: &[FishPropTemplate], actual: &[FishPropTemplate]) {
        assert_eq!(expected.len(), actual.len());
        for (a, b) in expected.iter().zip(actual) {
            assert_eq!(
                (&a.source_id, a.asset_id, a.transform),
                (&b.source_id, b.asset_id, b.transform)
            );
            assert_eq!(
                a.surface.pattern_and_seed.to_bits(),
                b.surface.pattern_and_seed.to_bits()
            );
            assert_eq!(
                a.surface.source_length_m.to_bits(),
                b.surface.source_length_m.to_bits()
            );
            assert_eq!(
                a.surface.flags.map(f32::to_bits),
                b.surface.flags.map(f32::to_bits)
            );
            assert_eq!(a.lod_meshes.len(), b.lod_meshes.len());
            for (left, right) in a.lod_meshes.iter().zip(&b.lod_meshes) {
                for attribute in [
                    Mesh::ATTRIBUTE_POSITION,
                    Mesh::ATTRIBUTE_NORMAL,
                    REST_POSITION,
                    FISH_DATA,
                ] {
                    assert_eq!(left.attribute(attribute), right.attribute(attribute));
                }
                assert_eq!(left.indices(), right.indices());
            }
        }
    }

    #[test]
    #[ignore = "requires the captured GameFish source package"]
    fn game_stand_excludes_pool_and_preserves_three_source_lods() {
        let root = std::env::var("FORGE_TIDEWATER_GAME_FISH_PACKAGE")
            .expect("Real game fish package required");
        let root = Path::new(&root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = forge_tidewater_assets::tidewater_game_fish::GameFishPackage::load(
            &manifest,
            forge_tidewater_assets::tidewater_game_fish::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap();
        let prepared = PreparedFishProps::from_game_package(&package).unwrap();
        let catalog = package.catalog().unwrap();
        let profile = forge_assets::source_fish_payload::CatalogFishProfile::new(
            &package,
            "geometry/game-fish.json",
            (18, 23),
            &catalog,
            forge_tidewater_assets::tidewater_game_fish::package_hash(),
        )
        .unwrap();
        assert_same_prepared(&prepared.templates, &prepare::profile(&profile).unwrap());
        let description: Description = serde_json::from_slice(package.description()).unwrap();
        assert_eq!(prepared.templates.len(), 5);
        for (ordinal, template) in (18..23).zip(prepared.templates) {
            assert_eq!(
                Some(template.asset_id),
                forge_tidewater_assets::tidewater_game_fish::asset_id(ordinal)
            );
            let instance = &description.instances[ordinal];
            assert_eq!(template.source_id, instance.source_id);
            assert_eq!(
                template.surface.pattern_and_seed.to_bits(),
                instance.record_f32[8].to_bits()
            );
            assert_eq!(
                template.surface.flags.map(f32::to_bits),
                instance.record_f32[12..16]
                    .iter()
                    .map(|v| v.to_bits())
                    .collect::<Vec<_>>()
                    .as_slice()
            );
            let prototype = description
                .prototypes
                .iter()
                .find(|p| p.key == instance.prototype)
                .unwrap();
            assert_eq!(template.lod_meshes.len(), 3);
            for (mesh, geometry) in template.lod_meshes.iter().zip(&prototype.lods) {
                assert_eq!(mesh.count_vertices(), geometry.vertices);
                assert_eq!(mesh.indices().unwrap().len(), geometry.triangles * 3);
                for (name, attribute) in [("position", REST_POSITION), ("aData", FISH_DATA)] {
                    let source = package
                        .file(&format!("geometry/{}", geometry.attributes[name].file))
                        .unwrap();
                    let actual = match mesh.attribute(attribute).unwrap() {
                        VertexAttributeValues::Float32x3(values) => values
                            .iter()
                            .flat_map(|v| v.iter().flat_map(|f| f.to_le_bytes()))
                            .collect::<Vec<_>>(),
                        VertexAttributeValues::Float32x4(values) => values
                            .iter()
                            .flat_map(|v| v.iter().flat_map(|f| f.to_le_bytes()))
                            .collect::<Vec<_>>(),
                        _ => panic!("Unexpected fish attribute format"),
                    };
                    assert_eq!(actual, source);
                }
            }
        }
        stage_verified_game_fish_attachments(&package);
    }

    #[allow(
        clippy::too_many_lines,
        reason = "Replay the real five-fish attachment lifecycle and propagated poses"
    )]
    fn stage_verified_game_fish_attachments(
        package: &forge_tidewater_assets::tidewater_game_fish::GameFishPackage,
    ) {
        use bevy::{prelude::*, render::storage::ShaderBuffer};
        use forge_engine::source_fish::{clock, material, resident};
        use forge_engine::source_lighting::SceneLighting;
        use forge_engine::source_shadows::ObjectShadowSettings;
        use forge_engine::source_surface::LightingBindings;

        #[derive(serde::Deserialize)]
        struct Layout {
            stand: Stand,
            stand_y: f32,
        }
        #[derive(serde::Deserialize)]
        struct Stand {
            x: f32,
            z: f32,
            yaw: f32,
        }
        let layout: Layout = serde_json::from_slice(package.description()).unwrap();
        let parent_pose =
            bevy::prelude::Transform::from_xyz(layout.stand.x, layout.stand_y, layout.stand.z)
                .with_rotation(Quat::from_rotation_y(layout.stand.yaw));
        let inverse = GlobalTransform::from(parent_pose).affine().inverse();
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
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::transform::TransformPlugin));
        app.world_mut().init_resource::<Assets<Mesh>>();
        app.world_mut().init_resource::<Assets<ShaderBuffer>>();
        app.world_mut()
            .init_resource::<Assets<material::FishMaterial>>();
        let parent = app
            .world_mut()
            .spawn((parent_pose, GlobalTransform::default()))
            .id();
        let shared_clock = clock::create(app.world_mut()).unwrap();
        let prepared = PreparedFishProps::from_game_package(package).unwrap();
        let mut expected = Vec::new();
        let mut attachments = Vec::new();
        for template in prepared.templates {
            let original = forge_engine::domain_transform_to_bevy(template.transform);
            expected.push(original.to_matrix());
            let pose = bevy::prelude::Transform::from_matrix(Mat4::from(
                inverse * GlobalTransform::from(original).affine(),
            ));
            let meshes = template
                .lod_meshes
                .into_iter()
                .map(|mesh| app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh))
                .collect();
            let value =
                material::prepare(template.surface, Handle::default(), &lighting, 0.0).unwrap();
            let handle = app
                .world_mut()
                .resource_mut::<Assets<material::FishMaterial>>()
                .add(value);
            attachments.push(resident::Attachment {
                meshes,
                material: handle,
                pose,
            });
        }
        let mut resident = resident::stage_attachments(
            app.world_mut(),
            parent,
            forge_core::EntityId::deterministic("game-fish-attachment-replay"),
            &attachments,
            &shared_clock,
            lod::POLICY,
        )
        .unwrap();
        app.update();
        let actual = resident.shadow_inventory(app.world()).unwrap();
        assert_eq!(actual.len(), 5);
        for ((instance, _), expected) in actual.iter().zip(expected) {
            assert!(instance.world_from_local.abs_diff_eq(expected, 1e-4));
        }
        resident
            .update_view(
                app.world_mut(),
                parent_pose.translation,
                |_, _| true,
                1.25,
                7,
            )
            .unwrap();
        assert_eq!(resident.shadow_instances(app.world()).unwrap().len(), 5);
        resident.clear(app.world_mut());
        assert!(app.world().get_entity(parent).is_ok());
        assert!(
            app.world()
                .resource::<Assets<ShaderBuffer>>()
                .get(&shared_clock)
                .is_some()
        );
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 15);
        assert_eq!(
            app.world()
                .resource::<Assets<material::FishMaterial>>()
                .len(),
            5
        );
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn all_source_templates_retain_lod_and_material_inputs() {
        let root =
            std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").expect("Real fish package required");
        let root = Path::new(&root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = FishPropPackage::load(&manifest, fish_props::package_hash(), |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let prepared = PreparedFishProps::from_package(&package).unwrap();
        let catalog = package.catalog().unwrap();
        let profile = forge_assets::source_fish_payload::CatalogFishProfile::new(
            &package,
            "geometry/fish-props.json",
            (18, 78),
            &catalog,
            fish_props::package_hash(),
        )
        .unwrap();
        assert_same_prepared(&prepared.templates, &prepare::profile(&profile).unwrap());
        assert_eq!(prepared.templates.len(), 78);
        let mut vertices = 0;
        for (ordinal, template) in prepared.templates.iter().enumerate() {
            assert_eq!(Some(template.asset_id), fish_props::asset_id(ordinal));
            assert_eq!(template.lod_meshes.len(), 3);
            for mesh in &template.lod_meshes {
                vertices += mesh.count_vertices();
                assert!(matches!(
                    mesh.attribute(REST_POSITION),
                    Some(VertexAttributeValues::Float32x3(_))
                ));
                assert!(matches!(
                    mesh.attribute(FISH_DATA),
                    Some(VertexAttributeValues::Float32x4(_))
                ));
            }
            assert_eq!(
                template.lod_meshes[1].indices(),
                template.lod_meshes[2].indices()
            );
            assert_eq!(
                template.lod_meshes[1].attribute(Mesh::ATTRIBUTE_POSITION),
                template.lod_meshes[2].attribute(Mesh::ATTRIBUTE_POSITION)
            );
        }
        assert_eq!(vertices, 169_709);
    }
}
