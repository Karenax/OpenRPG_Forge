//! Local desktop configuration, independent from the launching shell and working directory.
use serde::Deserialize;
use std::{
    ffi::OsString,
    fs::File,
    io::{ErrorKind, Read},
    path::{Path, PathBuf},
    sync::OnceLock,
};

pub const FILE_NAME: &str = "forge-studio.local.json";
const MAX_BYTES: u64 = 65_536;
static CONFIGURATION: OnceLock<Result<Option<Configuration>, String>> = OnceLock::new();

#[derive(Default, Deserialize)]
#[serde(deny_unknown_fields)]
struct Configuration {
    schema_version: u32,
    authoring_root: Option<PathBuf>,
    asset_vault: Option<PathBuf>,
    module_assets: Option<ModuleConfiguration>,
    game: Option<GameConfiguration>,
}

#[derive(Clone, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameConfiguration {
    pub executable: PathBuf,
    pub content_root: PathBuf,
    pub session_root: PathBuf,
}

pub fn game() -> Result<GameConfiguration, String> {
    configuration()?
        .and_then(|c| c.game.clone())
        .ok_or_else(|| "Le jeu séparé n’est pas configuré près du Studio.".into())
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleConfiguration {
    root: PathBuf,
    catalog: String,
    catalog_sha256: String,
}

fn configuration() -> Result<Option<&'static Configuration>, String> {
    CONFIGURATION
        .get_or_init(|| {
            std::env::current_exe()
                .map_err(|e| format!("Chemin du Studio indisponible : {e}"))
                .and_then(|exe| read_configuration(&exe))
        })
        .as_ref()
        .map(Option::as_ref)
        .map_err(Clone::clone)
}

fn read_configuration(exe: &Path) -> Result<Option<Configuration>, String> {
    let directory = exe.parent().ok_or("Dossier du Studio indisponible")?;
    let path = directory.join(FILE_NAME);
    let file = match File::open(&path) {
        Ok(file) => file,
        Err(error) if error.kind() == ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(format!(
                "Configuration {} illisible : {error}",
                path.display()
            ));
        }
    };
    let mut bytes = Vec::new();
    file.take(MAX_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| format!("Lecture de {} impossible : {e}", path.display()))?;
    if bytes.len() as u64 > MAX_BYTES {
        return Err(format!("Configuration {} trop volumineuse", path.display()));
    }
    parse_configuration(&bytes, directory)
        .map(Some)
        .map_err(|e| format!("Configuration {} invalide : {e}", path.display()))
}

fn resolve_path(path: PathBuf, directory: &Path) -> Result<PathBuf, String> {
    if path.as_os_str().is_empty() {
        return Err("Un chemin de configuration est vide".into());
    }
    Ok(if path.is_absolute() {
        path
    } else {
        directory.join(path)
    })
}

fn parse_configuration(bytes: &[u8], directory: &Path) -> Result<Configuration, String> {
    let mut config: Configuration = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if config.schema_version != 1 {
        return Err("Version de configuration non prise en charge".into());
    }
    config.authoring_root = config
        .authoring_root
        .map(|p| resolve_path(p, directory))
        .transpose()?;
    config.asset_vault = config
        .asset_vault
        .map(|p| resolve_path(p, directory))
        .transpose()?;
    if let Some(game) = config.game.as_mut() {
        game.executable = resolve_path(std::mem::take(&mut game.executable), directory)?;
        game.content_root = resolve_path(std::mem::take(&mut game.content_root), directory)?;
        game.session_root = resolve_path(std::mem::take(&mut game.session_root), directory)?;
    }
    if let Some(module) = config.module_assets.as_mut() {
        module.root = resolve_path(std::mem::take(&mut module.root), directory)?;
        if module.catalog.is_empty() {
            return Err("Nom de catalogue vide".into());
        }
        forge_core::ContentHash::parse(&module.catalog_sha256).map_err(|e| e.to_string())?;
    }
    Ok(config)
}

/// Explicit environment values remain authoritative for installers and automation.
pub fn authoring_root() -> Result<Option<PathBuf>, String> {
    if let Some(root) = std::env::var_os("FORGE_AUTHORING_ROOT") {
        return Ok(Some(root.into()));
    }
    Ok(configuration()?.and_then(|c| c.authoring_root.clone()))
}

pub fn asset_vault() -> Result<Option<PathBuf>, String> {
    if let Some(root) = std::env::var_os("OPENRPG_ASSET_VAULT") {
        return Ok(Some(root.into()));
    }
    Ok(configuration()?.and_then(|c| c.asset_vault.clone()))
}

pub type ModuleInputs = (Option<OsString>, Option<OsString>, Option<OsString>);

fn has_module_override(inputs: &ModuleInputs) -> bool {
    inputs.0.is_some() || inputs.1.is_some() || inputs.2.is_some()
}

/// Never complete a partial environment override with values from another profile.
pub fn module_inputs() -> Result<ModuleInputs, String> {
    let inputs = (
        std::env::var_os("FORGE_MODULE_ASSET_ROOT"),
        std::env::var_os("FORGE_MODULE_ASSET_CATALOG"),
        std::env::var_os("FORGE_MODULE_ASSET_CATALOG_SHA256"),
    );
    if has_module_override(&inputs) {
        return Ok(inputs);
    }
    Ok(configuration()?
        .and_then(|c| c.module_assets.as_ref())
        .map_or((None, None, None), |module| {
            (
                Some(module.root.clone().into_os_string()),
                Some(module.catalog.clone().into()),
                Some(module.catalog_sha256.clone().into()),
            )
        }))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn paths_are_relative_to_the_executable_not_the_shell() {
        let directory = std::env::temp_dir().join("forge-desktop-root");
        let config = parse_configuration(
            br#"{"schema_version":1,"authoring_root":"workspace","asset_vault":"vault"}"#,
            &directory,
        )
        .unwrap();
        assert_eq!(config.authoring_root, Some(directory.join("workspace")));
        assert_eq!(config.asset_vault, Some(directory.join("vault")));
    }

    #[test]
    fn invalid_profiles_never_silently_become_an_empty_workspace() {
        let directory = std::env::temp_dir();
        for bytes in [
            br#"{"schema_version":2}"#.as_slice(),
            br#"{"schema_version":1,"authoring_root":""}"#.as_slice(),
            br#"{"schema_version":1,"unknown_root":"x"}"#.as_slice(),
            br#"{"schema_version":1,"module_assets":{"root":"x","catalog":"x"}}"#.as_slice(),
            br#"{"schema_version":1,"module_assets":{"root":"x","catalog":"x","catalog_sha256":"invalid"}}"#.as_slice(),
        ] {
            assert!(parse_configuration(bytes, &directory).is_err());
        }
    }

    #[test]
    fn a_partial_module_override_stays_partial_and_cannot_mix_profiles() {
        assert!(has_module_override(&(
            Some("other-root".into()),
            None,
            None
        )));
        assert!(has_module_override(&(
            None,
            Some("other-catalog".into()),
            None
        )));
        assert!(!has_module_override(&(None, None, None)));
    }

    #[test]
    fn native_game_paths_are_local_and_require_a_complete_profile() {
        let directory = std::env::temp_dir().join("forge-game-config");
        let profile = parse_configuration(br#"{"schema_version":1,"game":{"executable":"game.exe","content_root":"content","session_root":"sessions"}}"#, &directory).unwrap();
        let game = profile.game.unwrap();
        assert_eq!(game.executable, directory.join("game.exe"));
        assert_eq!(game.content_root, directory.join("content"));
        assert_eq!(game.session_root, directory.join("sessions"));
        assert!(
            parse_configuration(
                br#"{"schema_version":1,"game":{"executable":"game.exe"}}"#,
                &directory
            )
            .is_err()
        );
    }

    #[test]
    fn absent_profile_preserves_environment_only_installations() {
        let directory =
            std::env::temp_dir().join(format!("forge-no-profile-{}", std::process::id()));
        assert!(
            read_configuration(&directory.join("forge-studio.exe"))
                .unwrap()
                .is_none()
        );
    }
}
