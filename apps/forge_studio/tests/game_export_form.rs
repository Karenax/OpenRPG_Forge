//! The export button prepares only a saved, completely configured game.
#![cfg(not(target_arch = "wasm32"))]
#[path = "../src/workspace_controls/create/game_export_form.rs"]
mod game_export_form;

use forge_core::{AreaId, AssetId, ContentHash, ModuleId, ProjectId};
use forge_project::{
    VisualQualityTier,
    authoring::workspace::{WorkspaceHostState, WorkspaceRecoveryReview, WorkspaceRecoveryVersion},
    workspace::{
        WorkspaceHead, ZoneReference,
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults, PresentationAsset,
        },
    },
};
use game_export_form::reviewed_workspace_hash;

fn fixture() -> (WorkspaceHostState, ProjectId) {
    let project = ProjectId::deterministic("export-form-project");
    let module = ModuleId::deterministic("export-form-module");
    let area = AreaId::deterministic("export-form-area");
    let mut head = WorkspaceHead::default()
        .with_project(project, "Jeu".into())
        .unwrap()
        .with_module(project, module, "Module".into())
        .unwrap();
    head.projects[0].modules[0].zones.push(ZoneReference {
        area_id: area,
        archive_hash: ContentHash::from_bytes([1; 32]),
        head_hash: ContentHash::from_bytes([2; 32]),
    });
    let game = GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: "Jeu".into(),
        summary: String::new(),
        icon: None,
        logo: None,
        background: None,
        contents: vec![GameContent {
            module_id: module,
            areas: vec![area],
        }],
        entry: GameLocation {
            module_id: module,
            area_id: area,
        },
        defaults: PlayerDefaults {
            quality: VisualQualityTier::High,
            display_mode: DisplayMode::Borderless,
            master_volume: 80,
            music_volume: 60,
            reduce_motion: false,
            subtitles: true,
        },
    };
    let head = head
        .with_game_configuration(project, None, Some(game))
        .unwrap();
    (
        WorkspaceHostState {
            head: Some(head),
            modules: Vec::new(),
            dirty: false,
            receipt: None,
            recovery: None,
            history: None,
        },
        project,
    )
}

#[test]
fn export_readiness_requires_saved_reviewed_complete_game_without_unsupported_media() {
    let (mut state, project) = fixture();
    let hash = state.head.as_ref().unwrap().content_hash().unwrap();
    assert_eq!(
        reviewed_workspace_hash(&state, project, false, false).unwrap(),
        hash
    );
    assert!(
        reviewed_workspace_hash(&state, project, true, false)
            .unwrap_err()
            .contains("saisies")
    );
    assert!(
        reviewed_workspace_hash(&state, project, false, true)
            .unwrap_err()
            .contains("propositions")
    );
    state.dirty = true;
    assert!(
        reviewed_workspace_hash(&state, project, false, false)
            .unwrap_err()
            .contains("Sauvegardez")
    );
    state.dirty = false;
    state.recovery = Some(WorkspaceRecoveryReview {
        saved: None,
        pending: WorkspaceRecoveryVersion {
            archive_hash: ContentHash::from_bytes([3; 32]),
            head: None,
            error: None,
        },
    });
    assert!(
        reviewed_workspace_hash(&state, project, false, false)
            .unwrap_err()
            .contains("reprise")
    );
    state.recovery = None;
    assert!(
        reviewed_workspace_hash(&state, ProjectId::deterministic("other"), false, false).is_err()
    );

    let mut partial = state.clone();
    partial.head.as_mut().unwrap().projects[0].modules[0]
        .zones
        .push(ZoneReference {
            area_id: AreaId::deterministic("another-area"),
            archive_hash: ContentHash::from_bytes([4; 32]),
            head_hash: ContentHash::from_bytes([5; 32]),
        });
    assert!(reviewed_workspace_hash(&partial, project, false, false).is_err());

    let mut with_icon = state.clone();
    let head = with_icon.head.as_mut().unwrap();
    head.projects[0].game.as_mut().unwrap().icon = Some(PresentationAsset {
        asset_id: AssetId::deterministic("game-icon"),
        content_hash: ContentHash::from_bytes([6; 32]),
    });
    // Resolution and licence admission belong to the native exporter, after the
    // same saved-workspace check as a project without presentation images.
    assert_eq!(
        reviewed_workspace_hash(&with_icon, project, false, false).unwrap(),
        with_icon.head.as_ref().unwrap().content_hash().unwrap()
    );
}
