//! Resolve an explicit native data directory before Tauri creates any webview.
use std::{ffi::OsString, path::PathBuf};
use tauri::utils::config::WindowConfig;

pub(super) const DATA_ROOT_ENV: &str = "FORGE_WEBVIEW_DATA_ROOT";

pub(super) fn data_directory() -> Result<Option<PathBuf>, String> {
    parse_directory(std::env::var_os(DATA_ROOT_ENV))
}

fn parse_directory(value: Option<OsString>) -> Result<Option<PathBuf>, String> {
    value
        .map(|value| {
            let path = PathBuf::from(value);
            if path.as_os_str().is_empty() || !path.is_absolute() {
                return Err(format!(
                    "{DATA_ROOT_ENV} doit être un chemin absolu non vide."
                ));
            }
            Ok(path)
        })
        .transpose()
}

/// Manual construction sets the typed absolute path before the manager's fallback.
pub(super) fn defer_windows(
    windows: &mut [WindowConfig],
    explicit_directory: bool,
) -> Vec<WindowConfig> {
    if !explicit_directory {
        return Vec::new();
    }
    windows
        .iter_mut()
        .filter_map(|window| {
            if !window.create {
                return None;
            }
            let configured = window.clone();
            window.create = false;
            Some(configured)
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn data_root_requires_an_explicit_absolute_path_and_creates_nothing() {
        assert_eq!(parse_directory(None).unwrap(), None);
        assert!(parse_directory(Some("".into())).is_err());
        assert!(parse_directory(Some("relative/webview".into())).is_err());
        let expected = std::env::temp_dir().join("webview-data-root-validation-only");
        let before = expected.exists();
        assert_eq!(
            parse_directory(Some(expected.clone().into_os_string())).unwrap(),
            Some(expected.clone())
        );
        assert_eq!(expected.exists(), before);
    }

    #[test]
    fn explicit_directory_defers_only_windows_that_would_be_created_automatically() {
        let mut windows = vec![
            WindowConfig {
                label: "main".into(),
                create: true,
                ..Default::default()
            },
            WindowConfig {
                label: "later".into(),
                create: false,
                ..Default::default()
            },
        ];
        let original = windows.clone();
        assert!(defer_windows(&mut windows, false).is_empty());
        assert_eq!(windows, original);
        let deferred = defer_windows(&mut windows, true);
        assert_eq!(deferred, vec![original[0].clone()]);
        assert!(windows.iter().all(|window| !window.create));
        assert!(defer_windows(&mut windows, true).is_empty());
    }
}
