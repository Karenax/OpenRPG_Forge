#![deny(unsafe_code)]
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Default, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub enum QualityPreset {
    Balanced,
    High,
    #[default]
    Ultra,
}

impl QualityPreset {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Balanced => "Équilibré",
            Self::High => "Élevé",
            Self::Ultra => "Ultra",
        }
    }
}

#[derive(Clone, Copy, Debug, Default, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub enum DisplayMode {
    Windowed,
    #[default]
    Borderless,
    Fullscreen,
}

impl DisplayMode {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Windowed => "Fenêtré",
            Self::Borderless => "Fenêtré plein écran",
            Self::Fullscreen => "Plein écran",
        }
    }
}

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LauncherSettings {
    pub quality: QualityPreset,
    pub display_mode: DisplayMode,
    pub master_volume: u8,
    pub music_volume: u8,
    pub reduce_motion: bool,
    pub subtitles: bool,
}

impl Default for LauncherSettings {
    fn default() -> Self {
        Self {
            quality: QualityPreset::Ultra,
            display_mode: DisplayMode::Borderless,
            master_volume: 82,
            music_volume: 64,
            reduce_motion: false,
            subtitles: true,
        }
    }
}

impl LauncherSettings {
    pub fn validate(self) -> Result<Self, String> {
        if self.master_volume > 100 {
            return Err("Le volume principal doit rester compris entre 0 et 100.".to_owned());
        }
        if self.music_volume > 100 {
            return Err("Le volume de la musique doit rester compris entre 0 et 100.".to_owned());
        }
        Ok(self)
    }
}

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub enum WindowAction {
    Minimize,
    ToggleMaximize,
    Close,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(tag = "kind", rename_all = "kebab-case")]
pub enum LauncherCommand {
    Snapshot,
    GameView,
    InstallerStatus,
    PrepareInstall {
        destination: String,
    },
    ApplyInstall,
    EnsurePlayerEntry,
    ReverifyInstalled,
    LaunchInstalled,
    PrepareRollback,
    ApplyRollback,
    StartUninstall,
    ApplyUninstall,
    CleanupUninstallHelper,
    SaveSettings {
        settings: LauncherSettings,
    },
    RequestLaunch,
    RequestEntryLaunch {
        selection: forge_delivery::installed_modules::ModuleSelection,
    },
    ListModules,
    RequestModuleLaunch {
        selection: forge_delivery::installed_modules::ModuleSelection,
    },
    RequestResume {
        resume: forge_delivery::installed_modules::ResumeSelection,
    },
    ReviewLegacySave {
        selection: forge_delivery::installed_modules::ModuleSelection,
    },
    ImportLegacySave {
        resume: forge_delivery::installed_modules::ResumeSelection,
    },
    GameStatus {
        launch_id: u32,
    },
    Window {
        action: WindowAction,
    },
}

/// Process observation only; a running process is not proof that the game is ready.
#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(tag = "state", rename_all = "kebab-case")]
pub enum GameProcessStatus {
    Running,
    Exited {
        success: bool,
        exit_code: Option<i32>,
    },
    Superseded,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
pub struct InstalledAdventure {
    pub name: String,
    pub version: String,
    pub zones: Vec<forge_delivery::installed_modules::ModuleSelection>,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
pub struct RejectedAdventure {
    pub pack_id: String,
    pub reason: String,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(tag = "kind", rename_all = "kebab-case")]
pub enum LauncherResponse {
    NotInstaller,
    InstallerReady {
        title: String,
        destination: String,
    },
    InstallPrepared {
        destination: String,
        update: bool,
        already_installed: bool,
        entry: Option<String>,
        entry_error: Option<String>,
    },
    InstallCompleted {
        entry: Option<String>,
        entry_error: Option<String>,
    },
    PlayerEntryReady {
        path: String,
    },
    InstallRevalidated {
        entry: Option<String>,
        entry_error: Option<String>,
    },
    InstalledLauncherStarted {
        process_id: u32,
    },
    RollbackPrepared {
        available: bool,
    },
    RollbackCompleted,
    UninstallHelperReady {
        title: String,
        path: String,
    },
    UninstallHelperStarted {
        path: String,
    },
    UninstallCompleted {
        path: String,
        preserved_files: usize,
        preserved_packages: usize,
        #[serde(default)]
        counts_partial: bool,
    },
    UninstallNeedsAttention {
        path: String,
        reason: String,
    },
    UninstallCleanupRequested,
    Snapshot {
        settings: LauncherSettings,
        runtime_connected: bool,
        prototype_version: String,
    },
    SettingsSaved {
        settings: LauncherSettings,
    },
    GameView {
        game: forge_delivery::standalone::GameView,
        runtime_connected: bool,
        #[serde(default)]
        legacy_installation_found: bool,
    },
    LegacySaveReviewed {
        saved_hash: String,
    },
    LegacySaveImported,
    Modules {
        modules: Vec<InstalledAdventure>,
        rejected: Vec<RejectedAdventure>,
    },
    LaunchBlocked {
        code: String,
        reason: String,
    },
    LaunchStarted {
        process_id: u32,
        #[serde(default, skip_serializing_if = "Option::is_none")]
        launch_id: Option<u32>,
    },
    GameStatus {
        launch_id: u32,
        status: GameProcessStatus,
    },
    WindowActionAccepted {
        action: WindowAction,
    },
    Rejected {
        reason: String,
    },
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn default_settings_are_valid_and_accessible() {
        let settings = LauncherSettings::default()
            .validate()
            .expect("defaults must stay valid");
        assert_eq!(settings.quality, QualityPreset::Ultra);
        assert_eq!(settings.display_mode, DisplayMode::Borderless);
        assert!(settings.subtitles);
        assert!(!settings.reduce_motion);
    }

    #[test]
    fn settings_reject_out_of_range_volumes() {
        let invalid = LauncherSettings {
            master_volume: 101,
            ..LauncherSettings::default()
        };
        assert!(invalid.validate().unwrap_err().contains("0 et 100"));
    }

    #[test]
    fn command_contract_round_trips_through_json() {
        let command = LauncherCommand::SaveSettings {
            settings: LauncherSettings::default(),
        };
        let encoded = serde_json::to_string(&command).expect("command must serialize");
        let decoded: LauncherCommand =
            serde_json::from_str(&encoded).expect("command must deserialize");
        assert_eq!(decoded, command);
    }

    #[test]
    fn process_status_contract_keeps_legacy_launch_responses_readable() {
        let legacy = serde_json::json!({"kind": "launch-started", "process_id": 42});
        let decoded: LauncherResponse = serde_json::from_value(legacy.clone()).unwrap();
        assert_eq!(serde_json::to_value(decoded).unwrap(), legacy);
        let command = LauncherCommand::GameStatus { launch_id: 7 };
        assert_eq!(
            serde_json::to_value(command).unwrap(),
            serde_json::json!({"kind": "game-status", "launch_id": 7})
        );
        let status = LauncherResponse::GameStatus {
            launch_id: 7,
            status: GameProcessStatus::Exited {
                success: false,
                exit_code: Some(9),
            },
        };
        let json = serde_json::to_value(&status).unwrap();
        assert_eq!(json["status"]["state"], "exited");
        assert_eq!(json["status"]["exit_code"], 9);
        assert_eq!(
            serde_json::from_value::<LauncherResponse>(json).unwrap(),
            status
        );
    }

    #[test]
    fn uninstall_response_accepts_the_previous_shape_and_partial_counts() {
        let mut value = serde_json::json!({
            "kind": "uninstall-completed", "path": "game/helper",
            "preserved_files": 0, "preserved_packages": 0
        });
        assert!(matches!(
            serde_json::from_value::<LauncherResponse>(value.clone()).unwrap(),
            LauncherResponse::UninstallCompleted {
                counts_partial: false,
                ..
            }
        ));
        value["counts_partial"] = true.into();
        assert!(matches!(
            serde_json::from_value::<LauncherResponse>(value).unwrap(),
            LauncherResponse::UninstallCompleted {
                counts_partial: true,
                ..
            }
        ));
    }

    #[test]
    fn module_launch_accepts_ids_only_and_rejects_invalid_or_extra_fields() {
        let selection = serde_json::json!({
            "module_id": "b65b7a97-77d4-58a1-9a0f-3304d680ff2c",
            "area_id": "30e53b52-88ff-50bf-8ded-18283e0a48d6",
        });
        let command = serde_json::json!({
            "kind": "request-module-launch", "selection": selection,
        });
        let parsed: LauncherCommand = serde_json::from_value(command.clone()).unwrap();
        assert_eq!(serde_json::to_value(parsed).unwrap(), command);
        let mut invalid = command.clone();
        invalid["selection"]["area_id"] = "invalid".into();
        assert!(serde_json::from_value::<LauncherCommand>(invalid).is_err());
        let mut path_override = command;
        path_override["selection"]["executable"] = "foreign.exe".into();
        assert!(serde_json::from_value::<LauncherCommand>(path_override).is_err());
        let entry = serde_json::json!({
            "kind": "request-entry-launch", "selection": selection,
        });
        let parsed: LauncherCommand = serde_json::from_value(entry.clone()).unwrap();
        assert_eq!(serde_json::to_value(parsed).unwrap(), entry);
    }

    #[test]
    fn standalone_game_view_round_trips_without_paths_or_executable_arguments() {
        let selection = serde_json::json!({
            "module_id": "b65b7a97-77d4-58a1-9a0f-3304d680ff2c",
            "area_id": "30e53b52-88ff-50bf-8ded-18283e0a48d6",
        });
        let response = serde_json::json!({
            "kind": "game-view",
            "runtime_connected": true,
            "game": {
                "game_id": "62284c67-e993-5a8e-a6e8-9932ea8d0cf1",
                "title": "Les deux criques",
                "summary": "Une aventure.",
                "entry": selection,
                "modules": [{"name": "Le port", "version": "1.0", "zones": [
                    {"name": "La crique", "selection": selection, "resume": {"status":"absent"}, "pending_resume": false}
                ]}],
            },
        });
        let parsed: LauncherResponse = serde_json::from_value(response.clone()).unwrap();
        assert_eq!(serde_json::to_value(parsed).unwrap(), response);
        let command: LauncherCommand = serde_json::from_value(serde_json::json!({
            "kind": "game-view"
        }))
        .unwrap();
        assert_eq!(command, LauncherCommand::GameView);
    }

    #[test]
    fn resume_command_accepts_only_selection_and_pinned_save() {
        let mut command = serde_json::json!({
            "kind": "request-resume",
            "resume": {
                "selection": {
                    "module_id": "b65b7a97-77d4-58a1-9a0f-3304d680ff2c",
                    "area_id": "30e53b52-88ff-50bf-8ded-18283e0a48d6"
                },
                "saved_hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            }
        });
        let parsed: LauncherCommand = serde_json::from_value(command.clone()).unwrap();
        assert_eq!(serde_json::to_value(parsed).unwrap(), command);
        command["resume"]["path"] = "foreign-save.json".into();
        assert!(serde_json::from_value::<LauncherCommand>(command).is_err());
    }
}
