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

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(tag = "kind", rename_all = "kebab-case")]
pub enum LauncherCommand {
    Snapshot,
    SaveSettings {
        settings: LauncherSettings,
    },
    RequestLaunch,
    ListModules,
    RequestModuleLaunch {
        selection: forge_delivery::installed_modules::ModuleSelection,
    },
    Window {
        action: WindowAction,
    },
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
    Snapshot {
        settings: LauncherSettings,
        runtime_connected: bool,
        prototype_version: String,
    },
    SettingsSaved {
        settings: LauncherSettings,
    },
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
    }
}
