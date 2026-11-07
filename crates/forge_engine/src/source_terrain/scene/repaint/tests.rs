use super::*;
use crate::source_terrain::{TerrainMaterial, TerrainSettings, lod::LodExtension, normals, paint};
use bevy::render::storage::ShaderBuffer;

fn plan() -> SampledTerrainPlan {
    let mut d = forge_project::authoring::new_terrain("repaint", "source", "Source").unwrap();
    let bytes = vec![0; 32 * 32 * 4];
    d.schema_version = 4;
    d.extent_m.size_x_m = 32.0;
    d.extent_m.size_z_m = 32.0;
    d.height_sampling.algorithm = forge_project::HeightSamplingAlgorithm::SampledRasterV1;
    d.height_sampling.sample_spacing_m = 1.0;
    d.height_sampling.base_frequency_cycles_per_m = 1.0;
    d.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
        schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 32,
        height: 32,
        origin_xz_m: [-16.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: 0.0,
        encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    SampledTerrainPlan::from_document(&d, &bytes).unwrap()
}

fn painted(plan: &SampledTerrainPlan) -> SampledTerrainPlan {
    let mut d = plan.document().clone();
    d.revision = d.revision.next().unwrap();
    d.material_overrides
        .push(forge_project::TerrainMaterialOverride {
            sample_x: 2,
            sample_z: 3,
            weights: vec![forge_project::TerrainMaterialWeight {
                layer_id: d.material_layers[0].id.clone(),
                weight: u16::MAX,
            }],
        });
    plan.with_document(&d).unwrap()
}

#[test]
fn reuse_ignores_only_paint_and_journal_revision() {
    let plan = plan();
    let package = forge_core::bytes_sha256(b"admitted package");
    let original = key(plan.document(), package).unwrap();
    assert_eq!(original, key(painted(&plan).document(), package).unwrap());
    assert_ne!(
        original,
        key(plan.document(), forge_core::bytes_sha256(b"other package")).unwrap()
    );
    let mut d = plan.document().clone();
    d.height_deltas.push(forge_project::TerrainHeightDelta {
        sample_x: 2,
        sample_z: 3,
        delta_m: 1.0,
    });
    assert_ne!(original, key(&d, package).unwrap());
    d = plan.document().clone();
    d.height_sampling.base_elevation_m = 1.0;
    assert_ne!(original, key(&d, package).unwrap());
    d = plan.document().clone();
    d.sampled_source.as_mut().unwrap().origin_xz_m[0] -= 1.0;
    assert_ne!(original, key(&d, package).unwrap());
    d = plan.document().clone();
    d.material_layers[0].display_name.push('x');
    assert_ne!(original, key(&d, package).unwrap());
}

fn scene(plan: &SampledTerrainPlan) -> (World, ResidentTerrain) {
    let mut world = World::new();
    world.init_resource::<Assets<Image>>();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<LodMaterial>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    paint::install_neutral(&mut world).unwrap();
    let image = world.resource_mut::<Assets<Image>>().add(Image::default());
    let normal = normals::Shared::disabled(&mut world.resource_mut::<Assets<Image>>());
    let material = TerrainMaterial {
        local_lights: Handle::default(),
        paint_weights: paint::BUFFER,
        paint_parameters: default(),
        scene_lighting: Handle::default(),
        normal: normal.image.clone(),
        splat: image.clone(),
        detail: image.clone(),
        linear_detail: image.clone(),
        environment: image.clone(),
        hill_shadow: image.clone(),
        object_shadow: image.clone(),
        object_settings: default(),
        live_shadows: Handle::default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        shore_state: image.clone(),
        shore_lace: image.clone(),
        shore_params: [UVec4::ZERO; 3],
        caustic_fine: image.clone(),
        caustic_broad: image.clone(),
        caustic_params: crate::source_surface::underwater::CausticSampleParams::new(
            1.0,
            2.0,
            Vec3::Y,
            0.0,
        )
        .unwrap(),
        underwater_waves: image.clone(),
        underwater_level: image.clone(),
        underwater_map_frame: Handle::default(),
        settings: TerrainSettings {
            wind: Vec4::ZERO,
            sun: Vec4::ZERO,
            domain: Vec4::ZERO,
            flags: Vec4::ZERO,
        },
    };
    let material = world
        .resource_mut::<Assets<LodMaterial>>()
        .add(LodMaterial {
            base: material,
            extension: LodExtension {
                height: image.clone(),
                corrections: image.clone(),
                settings: crate::source_terrain::lod::LodSettings {
                    morph: [Vec4::ZERO; 9],
                },
            },
        });
    let mesh = world.resource_mut::<Assets<Mesh>>().add(Cuboid::default());
    let entity = world
        .spawn((Mesh3d(mesh), MeshMaterial3d(material.clone())))
        .id();
    let resident = ResidentTerrain {
        document_hash: plan.document_hash(),
        relief: crate::source_terrain::sampling::relief_identity(plan).unwrap(),
        water_level_m: Some(0.0),
        entity: Some(entity),
        material: Some(material),
        normals: normal,
        reuse_key: forge_core::bytes_sha256(b"same geometry"),
        source_linear_detail: image,
    };
    world.insert_resource(resident.clone());
    (world, resident)
}

#[test]
fn repaint_and_clear_keep_geometry_normals_and_nonpaint_material_values() {
    let original = plan();
    let painted = painted(&original);
    let (mut world, initial) = scene(&original);
    let root = initial.entity.unwrap();
    let material = initial.material.clone().unwrap();
    let mesh = world.get::<Mesh3d>(root).unwrap().0.clone();
    let before = world
        .resource::<Assets<LodMaterial>>()
        .get(&material)
        .unwrap()
        .clone();
    let paint = PreparedRepaint {
        expected: initial.clone(),
        next: painted.document_hash(),
        paint: PreparedPaint::new(&painted).unwrap(),
    };
    let current = paint.stage(&mut world).unwrap();
    assert_eq!(current.entity, initial.entity);
    assert_eq!(current.material, initial.material);
    assert_eq!(world.get::<Mesh3d>(root).unwrap().0, mesh);
    assert!(std::sync::Arc::ptr_eq(
        &initial.normals.ready,
        &current.normals.ready
    ));
    assert_eq!(initial.normals.image, current.normals.image);
    let after = world
        .resource::<Assets<LodMaterial>>()
        .get(&material)
        .unwrap();
    assert!(after.base.paint_parameters.frame.w > 0.5);
    let mut restored = after.clone();
    restored.base.paint_weights = before.base.paint_weights.clone();
    restored.base.paint_parameters = before.base.paint_parameters.clone();
    restored.base.linear_detail = before.base.linear_detail.clone();
    same_material(&restored, &before);
    let clear = PreparedRepaint {
        expected: current,
        next: original.document_hash(),
        paint: PreparedPaint::new(&original).unwrap(),
    };
    let cleared = clear.stage(&mut world).unwrap();
    assert_eq!(cleared.document_hash, original.document_hash());
    same_material(
        world
            .resource::<Assets<LodMaterial>>()
            .get(&material)
            .unwrap(),
        &before,
    );
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 1);
}

#[test]
fn stale_or_retired_repaint_cannot_mutate_the_live_material() {
    let original = plan();
    let painted = painted(&original);
    let (mut world, initial) = scene(&original);
    let material = initial.material.clone().unwrap();
    let before = world
        .resource::<Assets<LodMaterial>>()
        .get(&material)
        .unwrap()
        .clone();
    let update = PreparedRepaint {
        expected: initial.clone(),
        next: painted.document_hash(),
        paint: PreparedPaint::new(&painted).unwrap(),
    };
    world.resource_mut::<ResidentTerrain>().document_hash =
        forge_core::bytes_sha256(b"newer terrain");
    assert!(update.stage(&mut world).is_err());
    same_material(
        world
            .resource::<Assets<LodMaterial>>()
            .get(&material)
            .unwrap(),
        &before,
    );
    world.insert_resource(initial.clone());
    world
        .resource_mut::<Assets<Image>>()
        .remove(initial.normals.image.id());
    let update = PreparedRepaint {
        expected: initial,
        next: painted.document_hash(),
        paint: PreparedPaint::new(&painted).unwrap(),
    };
    assert!(update.stage(&mut world).is_err());
    same_material(
        world
            .resource::<Assets<LodMaterial>>()
            .get(&material)
            .unwrap(),
        &before,
    );
}

fn same_material(left: &LodMaterial, right: &LodMaterial) {
    assert_eq!(left.base, right.base);
    assert_eq!(left.extension, right.extension);
}
