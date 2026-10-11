// The production import binding is exercised without GPU, WebView or WASM.
#[path = "../src/authoring_assets/imported_buildings/source_objects/binding.rs"]
mod binding;

use binding::SourcePose;
use forge_core::{AssetId, CommandId, ContentHash, EntityId, ModuleId, Transform, Vec3};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4,
    authoring::creation::{NewTerrainSettings, prepare_module_terrain},
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::ZoneEditSession,
        source_objects::{SourceObjectState, resolve},
    },
};

fn raster_session(key: &str, pin: ContentHash) -> ZoneEditSession {
    let mut terrain = new_terrain(key, "raster", "Imported source").unwrap();
    terrain.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    terrain.height_sampling.sample_spacing_m = 1.0;
    terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    terrain.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: pin,
    });
    ZoneEditSession::from_initial_terrain(terrain).unwrap()
}

fn verified_geometry(pin: ContentHash) -> (SampledTerrainSource, [f32; 2]) {
    let session = raster_session("binding-package-geometry", pin);
    (
        session.head().base.terrain.sampled_source.clone().unwrap(),
        [512.0; 2],
    )
}

fn associated_dataset(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    pin: Option<ContentHash>,
) -> Result<bool, String> {
    let (geometry, extent) = verified_geometry(pin.unwrap_or(ContentHash::from_bytes([0; 32])));
    binding::associated_dataset(head, pin, Some((&geometry, extent)))
}

fn template(session: &ZoneEditSession) -> ManualPlacement {
    binding::placement(
        session.head().base.terrain.area_id,
        "verified-source-key",
        "Verified source object".into(),
        AssetId::deterministic("binding-asset"),
        ContentHash::from_bytes([9; 32]),
        Transform::from_translation(Vec3::new(0.0, 2.5, -8.0)),
    )
}

fn edit(session: &mut ZoneEditSession, source: &ManualPlacement, action: PlacementEdit, key: &str) {
    let request = session
        .head()
        .prepare_source_object(source.clone(), action, CommandId::deterministic(key))
        .unwrap();
    session.execute(&request).unwrap();
}

fn regenerate(session: &mut ZoneEditSession, pin: ContentHash) {
    let mut base = session.head().base.clone();
    base.terrain.sampled_source.as_mut().unwrap().samples_sha256 = pin;
    let request = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("binding-regeneration"))
        .unwrap();
    session.execute(&request).unwrap();
}

#[test]
fn global_library_does_not_inherit_objects_in_procedural_or_other_imported_maps() {
    let pin = ContentHash::from_bytes([1; 32]);
    let procedural = ZoneEditSession::from_initial_terrain(
        new_terrain("binding-procedural", "zone", "Unrelated zone").unwrap(),
    )
    .unwrap();
    let other = raster_session("binding-other", ContentHash::from_bytes([2; 32]));
    let matching = raster_session("binding-matching", pin);
    for session in [&procedural, &other] {
        let inherited = associated_dataset(session.head(), Some(pin)).unwrap();
        assert!(!inherited);
        assert!(
            binding::select_source(
                session.head(),
                &template(session),
                inherited,
                SourcePose::PackageTransform
            )
            .unwrap()
            .is_none()
        );
    }
    assert!(!associated_dataset(matching.head(), None).unwrap());
    let inherited = associated_dataset(matching.head(), Some(pin)).unwrap();
    assert!(inherited);
    assert_eq!(
        binding::select_source(
            matching.head(),
            &template(&matching),
            inherited,
            SourcePose::PackageTransform
        )
        .unwrap(),
        Some(template(&matching))
    );
    // Valid copies of the dataset have no project/module-name restriction.
    let copied = raster_session("another-project", pin);
    assert!(associated_dataset(copied.head(), Some(pin)).unwrap());
}

#[test]
fn binding_uses_verified_height_descriptor_pin_instead_of_fixture_pin() {
    let height = ContentHash::from_bytes([3; 32]);
    let fixture = ContentHash::from_bytes([4; 32]);
    let descriptor = serde_json::to_vec(
        &serde_json::json!({"height_sha256": height, "source_fixture_sha256": fixture}),
    )
    .unwrap();
    assert_eq!(binding::height_pin(&descriptor).unwrap(), height);
    let session = raster_session("binding-descriptor", height);
    assert!(
        associated_dataset(
            session.head(),
            Some(binding::height_pin(&descriptor).unwrap())
        )
        .unwrap()
    );
    assert!(!associated_dataset(session.head(), Some(fixture)).unwrap());
    assert!(binding::height_pin(br#"{"source_fixture_sha256":"ignored"}"#).is_err());
    assert!(binding::height_pin(br#"{"height_sha256":"invalid"}"#).is_err());
}

#[test]
fn absent_terrain_package_or_height_pin_never_inherits_even_with_matching_samples() {
    let pin = ContentHash::from_bytes([12; 32]);
    let mut session = raster_session("binding-package-absent", pin);
    let (geometry, extent) = verified_geometry(pin);
    assert!(!binding::associated_dataset(session.head(), Some(pin), None).unwrap());
    assert!(!binding::associated_dataset(session.head(), None, Some((&geometry, extent))).unwrap());
    let source = template(&session);
    edit(
        &mut session,
        &source,
        PlacementEdit::SetLocked {
            id: source.id,
            locked: true,
        },
        "binding-package-absent-lock",
    );
    let retained =
        binding::select_source(session.head(), &source, false, SourcePose::PackageTransform)
            .unwrap()
            .unwrap();
    assert_eq!(retained, source);
    assert!(resolve(session.head(), &retained).unwrap()[0].locked);
}

#[test]
fn same_sample_hash_requires_full_verified_spatial_frame() {
    let pin = ContentHash::from_bytes([13; 32]);
    let session = raster_session("binding-frame", pin);
    let (geometry, extent) = verified_geometry(pin);
    assert!(
        binding::associated_dataset(session.head(), Some(pin), Some((&geometry, extent))).unwrap()
    );
    for mutation in 0..6 {
        let mut head = session.head().clone();
        let terrain = &mut head.base.terrain;
        let source = terrain.sampled_source.as_mut().unwrap();
        match mutation {
            0 => source.origin_xz_m[0] += 1.0,
            1 => {
                // Same sample count and bytes, but a different rectangular interpretation.
                source.width = 1024;
                source.height = 256;
                source.origin_xz_m = [-512.0, -128.0];
                terrain.extent_m.size_x_m = 1024.0;
                terrain.extent_m.size_z_m = 256.0;
            }
            2 => {
                source.sample_spacing_m = 2.0;
                source.origin_xz_m = [-512.0; 2];
                terrain.height_sampling.sample_spacing_m = 2.0;
                terrain.extent_m.size_x_m = 1024.0;
                terrain.extent_m.size_z_m = 1024.0;
            }
            3 => source.outside_height_m = -1.0,
            4 => terrain.extent_m.size_x_m = 1024.0,
            _ => terrain.height_sampling.sample_spacing_m = 2.0,
        }
        assert!(!binding::associated_dataset(&head, Some(pin), Some((&geometry, extent))).unwrap());
        assert!(
            binding::select_source(
                &head,
                &template(&session),
                false,
                SourcePose::PackageTransform
            )
            .unwrap()
            .is_none()
        );
    }
}

#[test]
fn projection_resources_match_same_dataset_in_distinct_module_maps() {
    let pin = ContentHash::from_bytes([21; 32]);
    let session = raster_session("projection-resource-modules", pin);
    let initial = &session.head().base.terrain;
    let (geometry, extent) = verified_geometry(pin);
    let mut areas = Vec::new();
    for module_name in ["Mountain adventure", "Harbour adventure"] {
        let module = ModuleId::deterministic(module_name);
        let mut terrain = prepare_module_terrain(
            initial.project_id,
            module,
            "map",
            module_name,
            NewTerrainSettings::default(),
        )
        .unwrap();
        terrain.schema_version = initial.schema_version;
        terrain.sampled_source = initial.sampled_source.clone();
        terrain.height_sampling = initial.height_sampling;
        terrain.extent_m = initial.extent_m;
        // Authoring retouches do not change the immutable sampled-source binding.
        terrain.height_sampling.base_elevation_m = 4.0;
        assert!(
            binding::associated_terrain(&terrain, Some(pin), Some((&geometry, extent))).unwrap()
        );
        areas.push(terrain.area_id);
    }
    assert_ne!(areas[0], areas[1]);
    assert_ne!(areas[0], initial.area_id);
}

#[test]
fn projection_resources_refuse_other_height_pin_or_spatial_frame() {
    let pin = ContentHash::from_bytes([22; 32]);
    let session = raster_session("projection-resource-mismatch", pin);
    let terrain = &session.head().base.terrain;
    let (geometry, extent) = verified_geometry(pin);
    let wrong_pin = ContentHash::from_bytes([23; 32]);
    assert!(
        !binding::associated_terrain(terrain, Some(wrong_pin), Some((&geometry, extent))).unwrap()
    );
    let mut wrong_geometry = geometry.clone();
    wrong_geometry.origin_xz_m[0] += 1.0;
    assert!(
        !binding::associated_terrain(terrain, Some(pin), Some((&wrong_geometry, extent))).unwrap()
    );
    assert!(
        !binding::associated_terrain(terrain, Some(pin), Some((&geometry, [1024.0, 512.0])))
            .unwrap()
    );
    assert!(!binding::associated_terrain(terrain, None, Some((&geometry, extent))).unwrap());
    assert!(!binding::associated_terrain(terrain, Some(pin), None).unwrap());
    let mut changed = terrain.clone();
    changed.sampled_source.as_mut().unwrap().samples_sha256 = wrong_pin;
    assert!(!binding::associated_terrain(&changed, Some(pin), Some((&geometry, extent))).unwrap());
    changed.sampled_source.as_mut().unwrap().width = 0;
    assert!(binding::associated_terrain(&changed, Some(pin), Some((&geometry, extent))).is_err());
}

#[test]
fn edited_static_snapshot_and_copy_survive_regeneration_to_unrelated_raster() {
    let pin = ContentHash::from_bytes([5; 32]);
    let mut session = raster_session("binding-retained", pin);
    let source = template(&session);
    let transform = Transform::from_translation(Vec3::new(11.0, 7.0, -22.0));
    edit(
        &mut session,
        &source,
        PlacementEdit::Transform {
            id: source.id,
            transform,
        },
        "binding-transform",
    );
    let copy = EntityId::deterministic("binding-copy");
    edit(
        &mut session,
        &source,
        PlacementEdit::Duplicate {
            source_id: source.id,
            id: copy,
            transform,
        },
        "binding-duplicate",
    );
    let before = resolve(session.head(), &source).unwrap();
    regenerate(&mut session, ContentHash::from_bytes([6; 32]));
    assert!(!associated_dataset(session.head(), Some(pin)).unwrap());
    let retained =
        binding::select_source(session.head(), &source, false, SourcePose::PackageTransform)
            .unwrap()
            .unwrap();
    assert_eq!(retained, source);
    assert_eq!(resolve(session.head(), &retained).unwrap(), before);
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
    assert_eq!(resolve(reopened.head(), &retained).unwrap(), before);
}

#[test]
fn terrain_derived_height_stays_frozen_and_deleted_source_never_resurrects() {
    let mut session = raster_session("binding-height", ContentHash::from_bytes([7; 32]));
    let mut package_pose = template(&session);
    package_pose.transform.translation_m.y = 0.0;
    let mut acquired = package_pose.clone();
    acquired.transform.translation_m.y = 17.75;
    let copy = EntityId::deterministic("binding-height-copy");
    edit(
        &mut session,
        &acquired,
        PlacementEdit::Duplicate {
            source_id: acquired.id,
            id: copy,
            transform: acquired.transform,
        },
        "binding-height-duplicate",
    );
    regenerate(&mut session, ContentHash::from_bytes([8; 32]));
    let retained = binding::select_source(
        session.head(),
        &package_pose,
        false,
        SourcePose::TerrainHeight,
    )
    .unwrap()
    .unwrap();
    assert_eq!(retained, acquired);
    assert_eq!(resolve(session.head(), &retained).unwrap()[0], acquired);
    edit(
        &mut session,
        &retained,
        PlacementEdit::Remove { id: retained.id },
        "binding-height-remove-root",
    );
    assert_eq!(resolve(session.head(), &retained).unwrap()[0].id, copy);
    edit(
        &mut session,
        &retained,
        PlacementEdit::Remove { id: copy },
        "binding-height-remove-copy",
    );
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.compact_archive_bytes().unwrap()).unwrap();
    let retained = binding::select_source(
        reopened.head(),
        &package_pose,
        false,
        SourcePose::TerrainHeight,
    )
    .unwrap()
    .unwrap();
    assert!(resolve(reopened.head(), &retained).unwrap().is_empty());
    let record = &reopened
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap()[0];
    assert_eq!(record.source, acquired);
    assert!(matches!(record.state, SourceObjectState::Removed));
    assert!(record.copies.is_empty());
}

#[test]
fn retained_records_require_exact_package_snapshot_and_verified_identity_and_pin() {
    let mut session = raster_session("binding-provenance", ContentHash::from_bytes([10; 32]));
    let source = template(&session);
    edit(
        &mut session,
        &source,
        PlacementEdit::SetLocked {
            id: source.id,
            locked: true,
        },
        "binding-lock",
    );
    for mutation in 0..5 {
        let mut head = session.head().clone();
        let snapshot = &mut head.corrections.source_object_edits.as_mut().unwrap()[0].source;
        match mutation {
            0 => snapshot.asset_hash = ContentHash::from_bytes([11; 32]),
            1 => snapshot.asset_id = AssetId::deterministic("unverified-binding-asset"),
            2 => snapshot.transform.translation_m.x = -0.0,
            3 => snapshot.name = "Unverified source name".into(),
            _ => snapshot.locked = true,
        }
        assert!(
            binding::select_source(&head, &source, false, SourcePose::PackageTransform).is_err()
        );
    }
    let mut head = session.head().clone();
    head.corrections.source_object_edits.as_mut().unwrap()[0]
        .source
        .id = EntityId::deterministic("unverified-source-id");
    assert!(
        binding::select_source(&head, &source, false, SourcePose::PackageTransform)
            .unwrap()
            .is_none()
    );
    let mut changed_package = source.clone();
    changed_package.transform.translation_m.z += 1.0;
    assert!(
        binding::select_source(
            session.head(),
            &changed_package,
            false,
            SourcePose::TerrainHeight
        )
        .is_err()
    );
}
