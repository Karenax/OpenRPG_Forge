use super::Indicator;
use serde::{Deserialize, Serialize};
use std::path::PathBuf;
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(default)]
pub(super) struct Preferences {
    pub show_fps: bool,
    pub show_frame_ms: bool,
    pub show_clock: bool,
    pub minutes: u16,
}
impl Default for Preferences {
    fn default() -> Self {
        Self {
            show_fps: true,
            show_frame_ms: true,
            show_clock: true,
            minutes: 972,
        }
    }
}
impl Preferences {
    pub fn enabled(&self, indicator: Indicator) -> bool {
        match indicator {
            Indicator::Fps => self.show_fps,
            Indicator::Frame => self.show_frame_ms,
            Indicator::Clock => self.show_clock,
        }
    }
    pub fn field_mut(&mut self, indicator: Indicator) -> &mut bool {
        match indicator {
            Indicator::Fps => &mut self.show_fps,
            Indicator::Frame => &mut self.show_frame_ms,
            Indicator::Clock => &mut self.show_clock,
        }
    }
    pub fn hour_label(&self) -> String {
        format!("{:02}:{:02}", (self.minutes / 60) % 24, self.minutes % 60)
    }
}
#[derive(Serialize, Deserialize)]
struct Document {
    schema: String,
    version: u32,
    display: Preferences,
}
fn path() -> Result<PathBuf, String> {
    std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .map(|root| root.join("OpenRPG Forge/Tidewater/display.json"))
        .ok_or_else(|| "Local preferences directory unavailable".into())
}
pub(super) fn load() -> Preferences {
    let result = (|| -> Result<Preferences, String> {
        let path = path()?;
        if !path.exists() {
            return Ok(Preferences::default());
        }
        if path.metadata().map_err(|e| e.to_string())?.len() > 4096 {
            return Err("Oversized display preferences".into());
        }
        let doc: Document =
            serde_json::from_slice(&std::fs::read(path).map_err(|e| e.to_string())?)
                .map_err(|e| e.to_string())?;
        if doc.schema != "forge.tidewater.display-settings"
            || doc.version != 1
            || doc.display.minutes > 1440
        {
            return Err("Invalid display preferences".into());
        }
        Ok(doc.display)
    })();
    match result {
        Ok(preferences) => preferences,
        Err(error) => {
            bevy::log::warn!(%error,"display preferences rejected");
            Preferences::default()
        }
    }
}
pub(super) fn save(display: &Preferences) -> Result<(), String> {
    if display.minutes > 1440 {
        return Err("Invalid time of day".into());
    }
    let path = path()?;
    std::fs::create_dir_all(path.parent().ok_or("Preferences parent absent")?)
        .map_err(|e| e.to_string())?;
    let temporary = path.with_extension(format!("{}.tmp", std::process::id()));
    let doc = Document {
        schema: "forge.tidewater.display-settings".into(),
        version: 1,
        display: display.clone(),
    };
    std::fs::write(
        &temporary,
        serde_json::to_vec_pretty(&doc).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    std::fs::rename(temporary, path).map_err(|e| e.to_string())
}
