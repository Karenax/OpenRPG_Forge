use super::*;
use crate::{
    PathCreateCommand, RiverUpsertCommand, TerrainGenerationPlan, preview_create_path,
    preview_upsert_river,
};
use forge_core::{AssetId, ContentHash, ProjectId};
use forge_project::{
    AuthoredPlacement, AuthoredRiver, MAX_ASSET_PINS_PER_DOCUMENT,
    MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT, NewProjectCommand, PathId, RiverChannelProfile,
    RiverChannelProfileAlgorithm, RiverSurface, WorkspaceDocument, WorkspaceError,
    asset_id_from_stable_key,
};

fn source() -> WorkspaceDocument {
    WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("placement/create"),
        project_id: ProjectId::deterministic("placement/project"),
        module_id: ModuleId::deterministic("placement/module"),
        area_id: AreaId::deterministic("placement/area"),
        project_name: "Projet".to_owned(),
        module_name: "Module".to_owned(),
        area_name: "Vallée".to_owned(),
        seed: 73,
        spawn: Vec3::ZERO,
    })
    .unwrap()
    .document
}

fn pin(key: &str) -> AssetPin {
    let stable_key = format!("pack/{key}");
    AssetPin {
        asset_id: asset_id_from_stable_key(&stable_key),
        stable_key,
        subset_id: "subset-1".to_owned(),
        pack_id: "pack".to_owned(),
        source_version: "1.0".to_owned(),
        author: "Author".to_owned(),
        source_path: format!("gltf/{key}.gltf"),
        scene_path: format!("governed/{key}.gltf"),
        scene_content_hash: ContentHash::from_bytes([1; 32]),
        manifest_hash: ContentHash::from_bytes([2; 32]),
        license_spdx: "CC0-1.0".to_owned(),
        license_evidence_hash: ContentHash::from_bytes([3; 32]),
        base_scale: 0.75,
    }
}

fn command(workspace: &WorkspaceDocument, key: &str, command_key: &str) -> PlaceAssetCommand {
    let module = &workspace.project.modules[0];
    let area = &module.areas[0];
    PlaceAssetCommand {
        command_id: CommandId::deterministic(command_key),
        base_revision: workspace.revision,
        project_id: workspace.project.id,
        module_id: module.id,
        area_id: area.id,
        expected_terrain_document_hash: area.terrain.content_hash().unwrap(),
        expected_path_document_hash: area.paths.content_hash().unwrap(),
        expected_road_document_hash: area.roads.content_hash().unwrap(),
        expected_river_document_hash: area.rivers.content_hash().unwrap(),
        expected_placement_document_hash: area.placements.content_hash().unwrap(),
        asset_pin: pin(key),
        drop_x_m: 7.25,
        drop_z_m: -11.5,
    }
}

#[test]
fn placement_is_deterministic_revisioned_and_uses_canonical_initial_transform() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let command = command(&source, "tree", "placement/place-tree");
    let first = preview_place_asset(&source, &command).unwrap();
    let replay = preview_place_asset(&source, &command).unwrap();

    assert_eq!(first, replay);
    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert_eq!(
        first.edit.document.revision,
        source.revision.next().unwrap()
    );
    assert_eq!(
        first.edit.receipt.new_revision,
        first.edit.document.revision
    );
    assert_eq!(first.placement.id, placement_entity_id(command.command_id));
    assert_eq!(first.placement.asset_id, command.asset_pin.asset_id);
    assert_eq!(first.placement.transform.rotation, UnitQuaternion::IDENTITY);
    assert_eq!(first.placement.transform.scale, Vec3::new(0.75, 0.75, 0.75));
    assert_eq!(
        first.placement.transform.translation_m.x.to_bits(),
        command.drop_x_m.to_bits()
    );
    assert_eq!(
        first.placement.transform.translation_m.z.to_bits(),
        command.drop_z_m.to_bits()
    );
    assert_eq!(*first.edit.receipt.inverse_snapshot, source);
    let area = &first.edit.document.project.modules[0].areas[0];
    assert_eq!(area.placements.asset_pins, vec![command.asset_pin]);
    assert_eq!(area.placements.placements, vec![first.placement]);
}

#[test]
fn placement_height_samples_the_effective_post_river_terrain() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let path_id = PathId::deterministic("placement/river-path");
    let with_path = preview_create_path(
        &source,
        PathCreateCommand {
            command_id: CommandId::deterministic("placement/create-river-path"),
            base_revision: source.revision,
            project_id: source.project.id,
            module_id,
            area_id,
            path_id,
            name: "Rivière".to_owned(),
            trace_xz_m: vec![[0.0, -40.0], [0.0, 40.0]],
        },
    )
    .unwrap()
    .edit
    .document;
    let with_river = preview_upsert_river(
        &with_path,
        RiverUpsertCommand {
            command_id: CommandId::deterministic("placement/create-river"),
            base_revision: with_path.revision,
            project_id: with_path.project.id,
            module_id,
            area_id,
            river: AuthoredRiver {
                path_id,
                channel: RiverChannelProfile {
                    algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                    surface_width_m: 6.0,
                    bed_depth_m: 2.0,
                    bank_blend_width_m: 4.0,
                },
                surface: RiverSurface::ForgeWaterV1,
            },
        },
    )
    .unwrap()
    .edit
    .document;
    let mut command = command(&with_river, "rock", "placement/on-river");
    command.drop_x_m = 0.0;
    command.drop_z_m = 0.0;
    let result = preview_place_asset(&with_river, &command).unwrap();
    let area = with_river.area(module_id, area_id).unwrap();
    let canonical_y = TerrainGenerationPlan::from_document(&area.terrain)
        .unwrap()
        .surface_at(0.0, 0.0)
        .unwrap()
        .height_m;
    let river_projection =
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)
            .unwrap()
            .generate()
            .unwrap();
    let effective_y = TerrainGenerationPlan::from_document(&river_projection.effective_terrain)
        .unwrap()
        .surface_at(0.0, 0.0)
        .unwrap()
        .height_m;

    assert!(effective_y < canonical_y);
    assert_eq!(
        result.placement.transform.translation_m.y.to_bits(),
        effective_y.to_bits()
    );
}

#[test]
fn stale_hashes_bounds_ownership_and_noop_are_atomic() {
    let source = source();
    let before_hash = source.content_hash().unwrap();
    for field in 0..5 {
        let mut command = command(&source, "tree", &format!("placement/stale-{field}"));
        let forged = ContentHash::from_bytes([99; 32]);
        match field {
            0 => command.expected_terrain_document_hash = forged,
            1 => command.expected_path_document_hash = forged,
            2 => command.expected_road_document_hash = forged,
            3 => command.expected_river_document_hash = forged,
            _ => command.expected_placement_document_hash = forged,
        }
        assert!(matches!(
            preview_place_asset(&source, &command),
            Err(PlaceAssetError::Workspace(WorkspaceError::StaleRevision))
        ));
        assert_eq!(source.content_hash().unwrap(), before_hash);
    }

    for (x_m, z_m) in [
        (f32::NAN, 0.0),
        (0.0, f32::INFINITY),
        (TERRAIN_HALF_EXTENT_M, 0.0),
        (0.0, -TERRAIN_HALF_EXTENT_M - 0.01),
    ] {
        let mut invalid = command(&source, "tree", "placement/invalid-drop");
        invalid.drop_x_m = x_m;
        invalid.drop_z_m = z_m;
        assert!(matches!(
            preview_place_asset(&source, &invalid),
            Err(PlaceAssetError::InvalidDrop { .. })
        ));
    }
    let mut wrong_project = command(&source, "tree", "placement/wrong-project");
    wrong_project.project_id = ProjectId::deterministic("other-project");
    assert!(matches!(
        preview_place_asset(&source, &wrong_project),
        Err(PlaceAssetError::WrongProject)
    ));

    let initial_command = command(&source, "tree", "placement/noop");
    let placed = preview_place_asset(&source, &initial_command)
        .unwrap()
        .edit
        .document;
    let mut replay = command(&placed, "tree", "placement/noop");
    replay.asset_pin = initial_command.asset_pin;
    assert!(matches!(
        preview_place_asset(&placed, &replay),
        Err(PlaceAssetError::Workspace(WorkspaceError::NoChange))
    ));
    assert_eq!(source.content_hash().unwrap(), before_hash);
}

#[test]
fn pin_overflow_fails_without_mutating_the_source() {
    let mut full = source();
    let area = &mut full.project.modules[0].areas[0];
    area.placements.asset_pins = (0..MAX_ASSET_PINS_PER_DOCUMENT)
        .map(|index| pin(&format!("limit-{index:03}")))
        .collect();
    area.placements
        .asset_pins
        .sort_by_key(|candidate| candidate.asset_id);
    full.validate().unwrap();
    let before_hash = full.content_hash().unwrap();
    let command = command(&full, "overflow", "placement/overflow");

    assert!(matches!(
        preview_place_asset(&full, &command),
        Err(PlaceAssetError::Workspace(WorkspaceError::Placement(
            forge_project::PlacementDocumentError::TooManyAssetPins(count)
        ))) if count == MAX_ASSET_PINS_PER_DOCUMENT + 1
    ));
    assert_eq!(full.content_hash().unwrap(), before_hash);
}

#[test]
fn placement_overflow_fails_without_mutating_the_source() {
    let mut full = source();
    let area = &mut full.project.modules[0].areas[0];
    let shared = pin("shared");
    area.placements.asset_pins = vec![shared.clone()];
    area.placements.placements = (0..MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT)
        .map(|index| AuthoredPlacement {
            id: EntityId::deterministic(format!("placement/full/{index:04}")),
            asset_id: shared.asset_id,
            transform: Transform {
                translation_m: Vec3::ZERO,
                rotation: UnitQuaternion::IDENTITY,
                scale: Vec3::new(0.75, 0.75, 0.75),
            },
        })
        .collect();
    area.placements
        .placements
        .sort_by_key(|placement| placement.id);
    full.validate().unwrap();
    let before_hash = full.content_hash().unwrap();
    let command = command(&full, "overflow", "placement/instance-overflow");

    assert!(matches!(
        preview_place_asset(&full, &command),
        Err(PlaceAssetError::Workspace(WorkspaceError::Placement(
            forge_project::PlacementDocumentError::TooManyPlacements(count)
        ))) if count == MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT + 1
    ));
    assert_eq!(full.content_hash().unwrap(), before_hash);
}

#[test]
fn forged_pin_identity_is_rejected_before_generation() {
    let source = source();
    let mut command = command(&source, "tree", "placement/forged-pin");
    command.asset_pin.asset_id = AssetId::deterministic("forged");
    assert!(matches!(
        preview_place_asset(&source, &command),
        Err(PlaceAssetError::Placement(
            forge_project::PlacementDocumentError::AssetIdentityMismatch(_)
        ))
    ));
}
