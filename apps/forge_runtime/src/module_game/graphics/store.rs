//! Bounded versioned user preference; writes never touch module/authoring data.
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::source_graphics::Settings;
use forge_project::VisualQualityTier;
use serde::{Deserialize, Serialize};
use std::{
    fs::{self, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
};

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Document {
    schema: String,
    version: u32,
    tier: VisualQualityTier,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct CustomDocument {
    schema: String,
    version: u32,
    tier: VisualQualityTier,
    settings: Option<Settings>,
    legacy_sha256: Option<ContentHash>,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Preferences {
    pub tier: VisualQualityTier,
    pub settings: Option<Settings>,
}

pub struct Store {
    path: PathBuf,
    custom_path: PathBuf,
}

#[derive(Clone, Copy)]
enum LegacyGuard {
    Unchecked,
    Exact(Option<ContentHash>),
}

impl Store {
    pub fn new(root: &Path) -> Result<Self, String> {
        let path = root.join("graphics.json");
        let custom_path = root.join("graphics-custom.json");
        forge_delivery::manifest::safe_chain(&path)?;
        forge_delivery::manifest::safe_chain(&custom_path)?;
        Ok(Self { path, custom_path })
    }
    #[cfg(test)]
    pub fn load(&self) -> Result<Option<VisualQualityTier>, String> {
        Ok(self.load_preferences()?.map(|preferences| preferences.tier))
    }

    pub fn load_preferences(&self) -> Result<Option<Preferences>, String> {
        let Some(bytes) = read_bounded(&self.custom_path)? else {
            return Ok(self.load_legacy()?.map(|tier| Preferences {
                tier,
                settings: None,
            }));
        };
        // Settings historically default missing Tidewater fields. A new custom
        // document is stricter: every control must be present exactly once.
        let json: serde_json::Value = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        let object = json
            .as_object()
            .ok_or("Invalid custom graphics preferences")?;
        let settings = object
            .get("settings")
            .ok_or("Custom graphics settings are missing")?;
        if !object.contains_key("legacy_sha256") {
            return Err("Custom graphics preference revision is missing".into());
        }
        if !settings.is_null() {
            let actual = settings
                .as_object()
                .ok_or("Invalid custom graphics settings")?;
            let defaults = serde_json::to_value(Settings::default()).map_err(|e| e.to_string())?;
            let expected = defaults
                .as_object()
                .ok_or("Graphics settings schema unavailable")?;
            if actual.len() != expected.len()
                || actual.keys().any(|key| !expected.contains_key(key))
            {
                return Err("Incomplete custom graphics settings".into());
            }
        }
        let document: CustomDocument =
            serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
        if document.schema != "forge.module-graphics-custom-preferences"
            || document.version != 1
            || document.settings.is_some_and(|settings| !settings.valid())
        {
            return Err("Unsupported custom graphics preferences".into());
        }
        if self.legacy_sha256()? != document.legacy_sha256 {
            return Err("Legacy graphics preferences changed since customization".into());
        }
        Ok(Some(Preferences {
            tier: document.tier,
            settings: document.settings,
        }))
    }

    fn load_legacy(&self) -> Result<Option<VisualQualityTier>, String> {
        forge_delivery::manifest::safe_chain(&self.path)?;
        let mut file = match fs::File::open(&self.path) {
            Ok(file) => file,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(error.to_string()),
        };
        if !file.metadata().map_err(|e| e.to_string())?.is_file() {
            return Err("Graphics preference is not a regular file".into());
        }
        let mut bytes = Vec::new();
        (&mut file)
            .take(4097)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        if bytes.len() > 4096 {
            return Err("Graphics preference exceeds 4096 bytes".into());
        }
        let document: Document = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        if document.schema != "forge.module-graphics-preferences" || document.version != 1 {
            return Err("Unsupported graphics preference version".into());
        }
        Ok(Some(document.tier))
    }

    pub fn save(&self, tier: VisualQualityTier) -> Result<(), String> {
        forge_delivery::manifest::safe_chain(&self.path)?;
        let bytes = serde_json::to_vec_pretty(&Document {
            schema: "forge.module-graphics-preferences".into(),
            version: 1,
            tier,
        })
        .map_err(|e| e.to_string())?;
        self.write(&self.path, "graphics", &bytes, LegacyGuard::Unchecked)
    }

    /// Save an isolated custom preference without replacing legacy `graphics.json`.
    pub fn save_preferences(&self, preferences: Preferences) -> Result<(), String> {
        forge_delivery::manifest::safe_chain(&self.custom_path)?;
        if preferences
            .settings
            .is_some_and(|settings| !settings.valid())
        {
            return Err("Invalid custom graphics settings".into());
        }
        let exists = match fs::symlink_metadata(&self.custom_path) {
            Ok(_) => true,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => false,
            Err(error) => return Err(error.to_string()),
        };
        if preferences.settings.is_none() && !exists {
            return self.save(preferences.tier);
        }
        let legacy_sha256 = self.legacy_sha256()?;
        let bytes = serde_json::to_vec_pretty(&CustomDocument {
            schema: "forge.module-graphics-custom-preferences".into(),
            version: 1,
            tier: preferences.tier,
            settings: preferences.settings,
            legacy_sha256,
        })
        .map_err(|e| e.to_string())?;
        self.write(
            &self.custom_path,
            "graphics-custom",
            &bytes,
            LegacyGuard::Exact(legacy_sha256),
        )
    }

    fn legacy_sha256(&self) -> Result<Option<ContentHash>, String> {
        Ok(read_bounded(&self.path)?.as_deref().map(bytes_sha256))
    }

    fn write(
        &self,
        path: &Path,
        prefix: &str,
        bytes: &[u8],
        expected_legacy: LegacyGuard,
    ) -> Result<(), String> {
        forge_delivery::manifest::safe_chain(path)?;
        let parent = path.parent().ok_or("Graphics preference parent missing")?;
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        for index in 0..16 {
            let temporary = parent.join(format!("{prefix}.{}.{}.tmp", std::process::id(), index));
            forge_delivery::manifest::safe_chain(&temporary)?;
            let mut file = match OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&temporary)
            {
                Ok(file) => file,
                Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => continue,
                Err(error) => return Err(error.to_string()),
            };
            let result = file.write_all(bytes).and_then(|()| file.sync_all());
            drop(file);
            let result = result.and_then(|()| {
                if let LegacyGuard::Exact(expected) = expected_legacy
                    && self.legacy_sha256().ok() != Some(expected)
                {
                    return Err(std::io::Error::other(
                        "Legacy graphics preferences changed during save",
                    ));
                }
                fs::rename(&temporary, path)
            });
            if result.is_err() {
                // Only our create_new staging file is eligible for this cleanup.
                let _ = fs::remove_file(&temporary);
            }
            return result.map_err(|e| e.to_string());
        }
        Err("Graphics preference staging slots are occupied".into())
    }
}

fn read_bounded(path: &Path) -> Result<Option<Vec<u8>>, String> {
    forge_delivery::manifest::safe_chain(path)?;
    let mut file = match fs::File::open(path) {
        Ok(file) => file,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(error.to_string()),
    };
    if !file.metadata().map_err(|e| e.to_string())?.is_file() {
        return Err("Graphics preference is not a regular file".into());
    }
    let mut bytes = Vec::new();
    (&mut file)
        .take(4097)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > 4096 {
        return Err("Graphics preference exceeds 4096 bytes".into());
    }
    Ok(Some(bytes))
}
