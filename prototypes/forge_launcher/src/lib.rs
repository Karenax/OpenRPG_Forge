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
    SaveSettings { settings: LauncherSettings },
    RequestLaunch,
    Window { action: WindowAction },
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
}
