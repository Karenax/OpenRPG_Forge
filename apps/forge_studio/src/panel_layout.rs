//! Versioned presentation preferences, independent of documents and the renderer.
use serde::{Deserialize, Serialize};

/// Preserve map height on dense displays (e.g. Full HD at 150% scaling).
pub(crate) fn compact_library(logical_height: f64) -> bool {
    logical_height.is_finite() && logical_height < 820.0
}

/// Reserve room for the map, bottom workspace and application chrome.
pub(crate) fn bottom_dock_available(body_height: f64) -> bool {
    body_height.is_finite() && body_height >= 960.0
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub(crate) enum Dock {
    Left,
    #[default]
    Right,
    Bottom,
    Floating,
}

impl Dock {
    pub(crate) const fn key(self) -> &'static str {
        match self {
            Self::Left => "left",
            Self::Right => "right",
            Self::Bottom => "bottom",
            Self::Floating => "floating",
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct PanelPlacement {
    pub dock: Dock,
    pub x: f64,
    pub y: f64,
    pub collapsed: bool,
}

impl Default for PanelPlacement {
    fn default() -> Self {
        Self {
            dock: Dock::Right,
            x: 280.0,
            y: 72.0,
            collapsed: false,
        }
    }
}

impl PanelPlacement {
    /// Adapt presentation without rewriting the user's saved preference.
    pub(crate) fn for_surface(mut self, body_height: f64) -> Self {
        if self.dock == Dock::Bottom && !bottom_dock_available(body_height) {
            self.dock = Dock::Right;
        }
        self
    }
    pub(crate) fn constrain(&mut self, width: f64, height: f64) {
        if self.dock == Dock::Floating {
            self.move_to(self.x, self.y, width, height);
        }
    }
    /// Always retain the complete title bar within the available surface.
    pub(crate) fn move_to(&mut self, x: f64, y: f64, width: f64, height: f64) {
        if ![x, y, width, height].iter().all(|v| v.is_finite()) {
            return;
        }
        self.dock = Dock::Floating;
        self.x = x.clamp(8.0, (width - 368.0).max(8.0));
        self.y = y.clamp(56.0, (height - 160.0).max(56.0));
    }
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Preferences {
    version: u8,
    panels: [PanelPlacement; 8],
}

pub(crate) fn decode_preferences(
    json: &str,
    width: f64,
    height: f64,
) -> Option<[PanelPlacement; 8]> {
    if json.len() > 8192 {
        return None;
    }
    let mut preferences: Preferences = serde_json::from_str(json).ok()?;
    if preferences.version != 1
        || preferences
            .panels
            .iter()
            .any(|p| !p.x.is_finite() || !p.y.is_finite())
    {
        return None;
    }
    for panel in &mut preferences.panels {
        panel.constrain(width, height);
    }
    Some(preferences.panels)
}

pub(crate) fn encode_preferences(panels: [PanelPlacement; 8]) -> Result<String, serde_json::Error> {
    serde_json::to_string(&Preferences { version: 1, panels })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn bottom_dock_adapts_without_losing_the_saved_choice() {
        let saved = PanelPlacement {
            dock: Dock::Bottom,
            ..PanelPlacement::default()
        };
        assert_eq!(saved.for_surface(668.0).dock, Dock::Right);
        assert_eq!(saved.for_surface(959.0).dock, Dock::Right);
        assert_eq!(saved.for_surface(f64::NAN).dock, Dock::Right);
        assert_eq!(saved.for_surface(960.0).dock, Dock::Bottom);
        assert_eq!(saved.dock, Dock::Bottom);
        assert_eq!(
            PanelPlacement {
                dock: Dock::Left,
                ..saved
            }
            .for_surface(668.0)
            .dock,
            Dock::Left
        );
    }
    #[test]
    fn library_starts_compact_on_short_logical_surfaces() {
        for height in [600.0, 720.0, 810.0] {
            assert!(compact_library(height));
        }
        for height in [820.0, 960.0, 1080.0, 1440.0, 2160.0, f64::NAN] {
            assert!(!compact_library(height));
        }
    }
    #[test]
    fn motion_keeps_header_reachable() {
        let mut panel = PanelPlacement::default();
        panel.move_to(-900.0, 9000.0, 1280.0, 720.0);
        assert_eq!((panel.x, panel.y), (8.0, 560.0));
        assert_eq!(panel.dock, Dock::Floating);
    }
    #[test]
    fn invalid_geometry_does_not_poison_layout() {
        let mut panel = PanelPlacement::default();
        let before = panel;
        panel.move_to(f64::NAN, 1.0, 1000.0, 800.0);
        assert_eq!(panel, before);
    }
    #[test]
    fn topics_keep_independent_layouts() {
        let mut panels = [PanelPlacement::default(); 8];
        panels[2].dock = Dock::Bottom;
        panels[4].dock = Dock::Left;
        assert_eq!(panels[0].dock, Dock::Right);
        assert_eq!(panels[2].dock.key(), "bottom");
        assert_eq!(panels[4].dock.key(), "left");
    }
    #[test]
    fn preferences_round_trip_and_recover_on_smaller_screen() {
        let mut panels = [PanelPlacement::default(); 8];
        panels[5].move_to(2900.0, 1600.0, 3840.0, 2100.0);
        panels[5].collapsed = true;
        let json = encode_preferences(panels).unwrap();
        assert_eq!(decode_preferences(&json, 3840.0, 2100.0), Some(panels));
        let restored = decode_preferences(&json, 1280.0, 656.0).unwrap();
        assert_eq!((restored[5].x, restored[5].y), (912.0, 496.0));
        assert!(restored[5].collapsed);
        assert_eq!(restored[0], PanelPlacement::default());
    }
    #[test]
    fn unknown_version_and_corrupt_preferences_are_rejected() {
        let json = encode_preferences([PanelPlacement::default(); 8]).unwrap();
        assert!(
            decode_preferences(
                &json.replace("\"version\":1", "\"version\":2"),
                1280.0,
                720.0
            )
            .is_none()
        );
        assert!(decode_preferences("{}", 1280.0, 720.0).is_none());
        assert!(decode_preferences(&" ".repeat(8193), 1280.0, 720.0).is_none());
    }
}
