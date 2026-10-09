use crate::{Result, archive, install, manifest};
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
};

pub fn default_root() -> Result<PathBuf> {
    if let Some(root) = std::env::var_os("FORGE_DELIVERY_INSTALL_ROOT") {
        let root = PathBuf::from(root);
        if !root.is_absolute() {
            return Err("Install root must be absolute".into());
        }
        return Ok(root);
    }
    let base = std::env::var_os("LOCALAPPDATA")
        .ok_or("LOCALAPPDATA is unavailable; supply an explicit installation root")?;
    Ok(PathBuf::from(base)
        .join("OpenRPG Forge")
        .join("Installation"))
}

pub fn user_root() -> Result<PathBuf> {
    if let Some(root) = std::env::var_os("FORGE_DELIVERY_USER_ROOT") {
        return Ok(PathBuf::from(root));
    }
    let base = std::env::var_os("LOCALAPPDATA").ok_or("LOCALAPPDATA is unavailable")?;
    Ok(PathBuf::from(base).join("OpenRPG Forge").join("UserData"))
}

/// Called only from Apply: copy an immutable module seed once; preserve later edits.
pub fn seed_workspace(
    root: &Path,
    snapshot: &install::Snapshot,
    user: &Path,
    command_id: &str,
) -> Result<()> {
    let definition = install::product(root, snapshot)?;
    let Some(seed) = &definition.authoring_seed else {
        return Ok(());
    };
    manifest::safe_chain(user)?;
    fs::create_dir_all(user).map_err(|e| e.to_string())?;
    for folder in ["captures", "saves", "logs"] {
        fs::create_dir_all(manifest::confined(user, folder)?).map_err(|e| e.to_string())?;
    }
    let workspace = manifest::confined(user, &format!("workspaces/{}", definition.id))?;
    if workspace.try_exists().map_err(|e| e.to_string())? {
        return Ok(());
    }
    let pack = snapshot
        .packs
        .get(&seed.pack_id)
        .ok_or("Seed package missing")?;
    let pack_root = install::package_root(root, pack)?;
    let descriptor = archive::verify_directory(&pack_root, &pack.revision)?;
    let prefix = format!("{}/", seed.path);
    let files: Vec<_> = descriptor
        .files
        .iter()
        .filter_map(|entry| {
            entry
                .path
                .strip_prefix(&prefix)
                .map(|relative| (entry, relative))
        })
        .collect();
    if files.is_empty() {
        return Err("Module workspace seed has no files".into());
    }
    let parent = workspace.parent().ok_or("No workspace parent")?;
    fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    let stage = parent.join(format!(".seed-{command_id}-{}", crate::attempt()?));
    fs::create_dir(&stage).map_err(|e| e.to_string())?;
    for (entry, relative) in files {
        let destination = manifest::confined(&stage, relative)?;
        fs::create_dir_all(destination.parent().ok_or("No file parent")?)
            .map_err(|e| e.to_string())?;
        fs::copy(manifest::confined(&pack_root, &entry.path)?, &destination)
            .map_err(|e| e.to_string())?;
        if crate::file_hash(&destination)? != entry.sha256 {
            return Err("Workspace seed copy mismatch".into());
        }
    }
    fs::rename(stage, workspace).map_err(|e| e.to_string())?;
    Ok(())
}

fn expand(value: &str, substitutions: &BTreeMap<String, String>) -> Result<String> {
    let mut result = String::new();
    let mut rest = value;
    while let Some((before, tail)) = rest.split_once('{') {
        result.push_str(before);
        let (key, next) = tail.split_once('}').ok_or("Unclosed launch template")?;
        result.push_str(
            substitutions
                .get(key)
                .ok_or_else(|| format!("Unknown path binding: {key}"))?,
        );
        rest = next;
    }
    result.push_str(rest);
    Ok(result)
}

/// No shell evaluation: each argument is passed directly to the native process.
pub fn command(root: &Path, app_name: &str) -> Result<Command> {
    let state = install::state(root)?;
    let definition = install::product(root, &state.active)?;
    let app = definition
        .apps
        .get(app_name)
        .ok_or("Unknown application entry point")?;
    let app_root = install::package_root(root, &state.active.core)?;
    let descriptor: manifest::Descriptor =
        crate::read_json(&manifest::confined(&app_root, "package-manifest.json")?)?;
    let executable = manifest::confined(&app_root, &app.executable)?;
    let pin = descriptor
        .files
        .iter()
        .find(|entry| entry.path.eq_ignore_ascii_case(&app.executable))
        .ok_or("Missing executable inventory")?;
    if crate::file_hash(&executable)? != pin.sha256 {
        return Err("Application binary integrity mismatch".into());
    }
    let user = PathBuf::from(&state.user_root);
    let workspace = manifest::confined(&user, &format!("workspaces/{}", definition.id))?;
    let mut substitutions = BTreeMap::from([
        ("app".into(), app_root.to_string_lossy().into_owned()),
        ("install".into(), root.to_string_lossy().into_owned()),
        ("user".into(), user.to_string_lossy().into_owned()),
        ("workspace".into(), workspace.to_string_lossy().into_owned()),
    ]);
    for (id, pack) in &state.active.packs {
        substitutions.insert(
            format!("pack:{id}"),
            install::package_root(root, pack)?
                .to_string_lossy()
                .into_owned(),
        );
    }
    let mut command = Command::new(executable);
    command.current_dir(&app_root);
    for (key, _) in std::env::vars_os() {
        if key.to_str().is_some_and(|key| {
            app.clear_env_prefixes.iter().any(|prefix| {
                key.to_ascii_uppercase()
                    .starts_with(&prefix.to_ascii_uppercase())
            })
        }) {
            command.env_remove(key);
        }
    }
    for argument in &app.args {
        command.arg(expand(argument, &substitutions)?);
    }
    for (name, value) in &app.env {
        let absent_optional = definition.optional_packs.iter().any(|id| {
            !state.active.packs.contains_key(id) && value.contains(&format!("{{pack:{id}}}"))
        });
        if absent_optional {
            command.env_remove(name);
        } else {
            command.env(name, expand(value, &substitutions)?);
        }
    }
    Ok(command)
}

pub fn start(root: &Path, app_name: &str) -> Result<Child> {
    let mut command = command(root, app_name)?;
    manifest::identifier(app_name)?;
    let user = PathBuf::from(install::state(root)?.user_root);
    let logs = manifest::confined(&user, "logs")?;
    fs::create_dir_all(&logs).map_err(|e| e.to_string())?;
    let log_path = manifest::confined(&logs, &format!("{app_name}-{}.log", crate::attempt()?))?;
    let log = fs::OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(log_path)
        .map_err(|e| e.to_string())?;
    command
        .stdin(Stdio::null())
        .stdout(Stdio::from(log.try_clone().map_err(|e| e.to_string())?))
        .stderr(Stdio::from(log));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    command
        .spawn()
        .map_err(|e| format!("Impossible de lancer {app_name} : {e}"))
}
