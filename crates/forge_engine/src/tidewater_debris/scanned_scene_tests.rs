use super::*;
use crate::tidewater_assets::{MaterialParameters, materials::LightingBindings};
use forge_core::{AssetId, ContentHash, Transform as DomainTransform};
use forge_project::zone_authoring::placements::ManualPlacement;

fn setup() -> (
    World,
    ResidentDraws,
    BTreeMap<EntityId, Entity>,
    Handle<ShaderBuffer>,
) {
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    world.init_resource::<Assets<ScannedMaterial>>();
    let base = crate::tidewater_assets::materials::prepare(
        &MaterialParameters {
            kind: 0,
            textures: vec!["cpu-test".into()],
            color: [1.0; 4],
            emissive: [0.0, 0.0, 0.0, 1.0],
            surface: [0.85, 0.0, 0.0, 1.0],
        },
        &BTreeMap::from([("cpu-test".into(), Handle::default())]),
        &LightingBindings {
            scene_lighting: crate::tidewater_material::SceneLighting::new(
                [0.0, 1.0, 0.0],
                [1.0; 3],
                [[0.0; 3]; 9],
            )
            .unwrap(),
            environment: Handle::default(),
            hill_shadow: Handle::default(),
            object_shadow: Handle::default(),
            object_settings: crate::tidewater_material::ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            wind: Vec4::ZERO,
            flags: Vec4::ZERO,
            caustics: crate::tidewater_caustics::CausticMaterialInputs::disabled(&mut Assets::<
                Image,
            >::default(
            )),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        },
    )
    .unwrap();
    let buffer = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&[0; 16], RenderAssetUsages::default()));
    let material = world
        .resource_mut::<Assets<ScannedMaterial>>()
        .add(ScannedMaterial {
            base,
            extension: super::super::scanned_material::ScannedExtension {
                heights: Handle::default(),
                terrain_domain: Vec4::ZERO,
                terrain_sun: Vec4::ZERO,
                albedo: Handle::default(),
                normal: Handle::default(),
                arm: Handle::default(),
                instances: buffer.clone(),
            },
        });
    let prototypes = (0..4)
        .flat_map(|asset| {
            (0..3).map(move |lod| super::super::scanned_meshes::Prototype {
                asset,
                lod,
                mesh: Mesh::from(Cuboid::default()),
            })
        })
        .collect();
    let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
    let package = forge_assets::tidewater_debris::scanned::ScannedPackage::load(
        &std::fs::read(root.join("package.json")).unwrap(),
        |name, _| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
    )
    .unwrap();
    let geometry = forge_assets::tidewater_debris::scanned::geometry::prepare(&package).unwrap();
    let resident = ResidentDraws::stage(
        &mut world,
        PreparedMeshes {
            prototypes,
            instances: geometry.instances.into_iter().take(2).collect(),
        },
        material,
    )
    .unwrap();
    let roots = (0..4)
        .map(|i| {
            let placement = placement(i);
            let id = placement.id;
            (
                id,
                world
                    .spawn((
                        Transform::IDENTITY,
                        Visibility::Inherited,
                        ManualPlacementProjection(placement),
                    ))
                    .id(),
            )
        })
        .collect();
    (world, resident, roots, buffer)
}
fn placement(i: usize) -> ManualPlacement {
    ManualPlacement {
        particles: None,
        physics: None,
        id: EntityId::deterministic(format!("scanned-buffer-regression:{i}")),
        name: "cpu-test".into(),
        asset_id: AssetId::deterministic("scanned-buffer-test-asset"),
        asset_hash: ContentHash::from_bytes([1; 32]),
        transform: DomainTransform::IDENTITY,
        locked: false,
    }
}
fn frame(n: usize) -> Frame {
    Frame {
        draws: (0..n)
            .map(|i| super::super::scanned_draws::Draw {
                placement_id: placement(i / 2).id,
                transform: DomainTransform::IDENTITY,
                asset: 0,
                lod: (i % 2).try_into().unwrap(),
                tag: i.try_into().unwrap(),
                casts_shadow: i % 2 == 0,
            })
            .collect(),
        records: (0..n).map(|_| [0.0, 0.3, 0.0, 1.0]).collect(),
    }
}
fn bytes(world: &World, buffer: &Handle<ShaderBuffer>) -> Vec<u8> {
    world
        .resource::<Assets<ShaderBuffer>>()
        .get(buffer)
        .unwrap()
        .data
        .clone()
        .unwrap()
}
#[test]
#[ignore = "Requires the byte-pinned acquired package; run the documented external-fixture replay"]
fn camera_population_and_empty_frames_keep_buffer_size_and_clear_tail() {
    let (mut world, mut resident, roots, buffer) = setup();
    assert_eq!(bytes(&world, &buffer).len(), 64);
    for n in [4, 1, 0, 3, 4, 0] {
        let frame = frame(n);
        resident.synchronize(&mut world, &frame, &roots).unwrap();
        let data = bytes(&world, &buffer);
        assert_eq!(data.len(), 64, "visible population {n}");
        assert!(data[n * 16..].iter().all(|b| *b == 0));
        for (i, record) in frame.records.iter().enumerate() {
            assert_eq!(
                &data[i * 16..(i + 1) * 16],
                record
                    .iter()
                    .flat_map(|v| v.to_le_bytes())
                    .collect::<Vec<_>>()
            );
        }
    }
}
#[test]
#[ignore = "Requires the byte-pinned acquired package; run the documented external-fixture replay"]
fn placement_growth_reserves_then_retains_allocation() {
    let (mut world, mut resident, roots, buffer) = setup();
    for n in [6, 1, 0, 8, 2] {
        resident.synchronize(&mut world, &frame(n), &roots).unwrap();
        assert_eq!(bytes(&world, &buffer).len(), 128);
    }
}
#[test]
#[ignore = "Requires the byte-pinned acquired package; run the documented external-fixture replay"]
fn rejected_frame_keeps_previous_upload_and_entities() {
    let (mut world, mut resident, roots, buffer) = setup();
    resident.synchronize(&mut world, &frame(4), &roots).unwrap();
    let before = bytes(&world, &buffer);
    let entities = resident.entities.clone();
    let mut invalid = frame(6);
    invalid.draws[5].tag = 0;
    assert!(resident.synchronize(&mut world, &invalid, &roots).is_err());
    assert_eq!(before, bytes(&world, &buffer));
    assert_eq!(entities, resident.entities);
}
