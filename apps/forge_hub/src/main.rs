#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
#![deny(unsafe_code)]
use forge_delivery::{Result, archive, install, launch, manifest};
use serde::Deserialize;
use serde_json::{Value, json};
use std::{
    path::{Path, PathBuf},
    process::Command,
    sync::{Arc, Mutex},
};

#[derive(Clone)]
struct Host {
    root: Arc<Mutex<PathBuf>>,
    plan: Arc<Mutex<Option<install::Plan>>>,
    operation: Arc<Mutex<()>>,
}

#[derive(Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
enum Request {
    Status,
    Bundled,
    Select,
    SelectRoot,
    SetRoot { path: String },
    Prepare { files: Vec<String> },
    Apply { command_id: String },
    Verify,
    Launch { app: String },
    Rollback,
}

fn value(value: &impl serde::Serialize) -> Result<Value> {
    serde_json::to_value(value).map_err(|e| e.to_string())
}

fn root_snapshot(host: &Host) -> Result<PathBuf> {
    host.root
        .lock()
        .map_err(|_| "Installation state unavailable".into())
        .map(|root| root.clone())
}

fn validate_root(root: PathBuf) -> Result<PathBuf> {
    if !root.is_absolute() {
        return Err("Le dossier d'installation doit être un chemin absolu".into());
    }
    manifest::safe_chain(&root)?;
    match std::fs::metadata(&root) {
        Ok(metadata) if !metadata.is_dir() => {
            Err("Le chemin d'installation doit désigner un dossier".into())
        }
        Ok(_) => Ok(root),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(root),
        Err(error) => Err(error.to_string()),
    }
}

fn change_root(host: &Host, candidate: PathBuf) -> Result<Value> {
    let candidate = validate_root(candidate)?;
    let mut root = host
        .root
        .lock()
        .map_err(|_| "Installation state unavailable")?;
    if *root == candidate {
        return Ok(json!({
            "root": root.clone(),
            "changed": false,
            "plan_invalidated": false
        }));
    }
    *root = candidate.clone();
    *host
        .plan
        .lock()
        .map_err(|_| "Installation state unavailable")? = None;
    Ok(json!({
        "root": candidate,
        "changed": true,
        "plan_invalidated": true
    }))
}

fn powershell(script: &str) -> Command {
    let mut command = Command::new("powershell.exe");
    command.args(["-NoProfile", "-NonInteractive", "-Sta", "-Command", script]);
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    command
}

fn select_packages() -> Result<Vec<String>> {
    // Fixed OS dialog, no user-supplied script or shell command. Kept off the UI thread.
    let script = "$OutputEncoding=[System.Text.Encoding]::UTF8; [Console]::OutputEncoding=$OutputEncoding; Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='Packages Forge (*.forgepack)|*.forgepack'; $d.Multiselect=$true; if($d.ShowDialog() -eq 'OK'){ConvertTo-Json -Compress -InputObject @($d.FileNames)}else{'[]'}";
    let output = powershell(script).output().map_err(|e| e.to_string())?;
    if !output.status.success() {
        return Err("Le sélecteur de packages n'a pas pu s'ouvrir".into());
    }
    serde_json::from_slice(&output.stdout).map_err(|e| e.to_string())
}

fn select_root_dialog() -> Result<Option<PathBuf>> {
    // Fixed OS dialog, no user-supplied script or shell command. Kept off the UI thread.
    let script = "$OutputEncoding=[System.Text.Encoding]::UTF8; [Console]::OutputEncoding=$OutputEncoding; Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.FolderBrowserDialog; $d.Description='Choisir le dossier d’installation OpenRPG Forge'; $d.ShowNewFolderButton=$true; if($d.ShowDialog() -eq 'OK'){ConvertTo-Json -Compress -InputObject $d.SelectedPath}else{'null'}";
    let output = powershell(script).output().map_err(|e| e.to_string())?;
    if !output.status.success() {
        return Err("Le sélecteur de dossier n'a pas pu s'ouvrir".into());
    }
    let selected: Option<String> =
        serde_json::from_slice(&output.stdout).map_err(|e| e.to_string())?;
    Ok(selected.map(PathBuf::from))
}

fn installed_pack_view(root: &Path, pack: &install::InstalledPack) -> Result<Value> {
    let directory = install::package_root(root, pack)?;
    let descriptor: manifest::Descriptor =
        forge_delivery::read_json(&manifest::confined(&directory, "package-manifest.json")?)?;
    descriptor.validate()?;
    if descriptor.id != pack.id
        || descriptor.kind != pack.kind
        || forge_delivery::hash(&forge_delivery::json(&descriptor)?) != pack.revision
    {
        return Err(format!("Contenu installé invalide : {}", pack.id));
    }
    Ok(json!({
        "label": descriptor.label,
        "version": descriptor.version,
        "kind": descriptor.kind
    }))
}

fn bundled() -> Result<Value> {
    let executable = std::env::current_exe().map_err(|e| e.to_string())?;
    let folder = executable.parent().ok_or("Executable directory missing")?;
    let index = folder.join("delivery.json");
    if !index.is_file() {
        return Ok(json!({"files":[]}));
    }
    let index: Value = forge_delivery::read_json(&index)?;
    if index["schema"] != "forge.delivery.index.v1" {
        return Err("Index de livraison incompatible".into());
    }
    let mut files = Vec::new();
    let packages = index["packages"].as_array().ok_or("Missing package list")?;
    if packages.is_empty() || packages.len() > 1024 {
        return Err("Invalid delivery package count".into());
    }
    for package in packages {
        let path = forge_delivery::manifest::confined(
            folder,
            package["file"].as_str().ok_or("Missing package path")?,
        )?;
        if forge_delivery::file_hash(&path)?
            != package["archive_sha256"]
                .as_str()
                .ok_or("Missing package hash")?
        {
            return Err("Livraison corrompue : hash de package différent".into());
        }
        files.push(path.to_str().ok_or("Non-Unicode package path")?.to_owned());
    }
    Ok(json!({"files":files}))
}

fn execute(request: Request, host: &Host) -> Result<Value> {
    let _operation = host
        .operation
        .lock()
        .map_err(|_| "Installation state unavailable")?;
    let root = root_snapshot(host)?;
    match request {
        Request::Status => {
            let state = install::state(&root);
            match state {
                Ok(state) => {
                    let product = install::product(&root, &state.active)?;
                    let packs = std::iter::once(&state.active.core)
                        .chain(state.active.packs.values())
                        .map(|pack| installed_pack_view(&root, pack))
                        .collect::<Result<Vec<_>>>()?;
                    Ok(
                        json!({"installed":true,"root":root,"product":product,"packs":packs,"state":state}),
                    )
                }
                Err(error)
                    if !manifest::confined(&root, "installation.json")?
                        .try_exists()
                        .map_err(|e| e.to_string())? =>
                {
                    Ok(json!({"installed":false,"root":root,"message":error}))
                }
                Err(error) => Err(error),
            }
        }
        Request::Bundled => bundled(),
        Request::Select => value(&select_packages()?),
        Request::SelectRoot => match select_root_dialog()? {
            Some(candidate) => change_root(host, candidate),
            None => Ok(json!({
                "root": root,
                "changed": false,
                "cancelled": true,
                "plan_invalidated": false
            })),
        },
        Request::SetRoot { path } => change_root(host, PathBuf::from(path)),
        Request::Prepare { files } => {
            if files.len() > 1024 {
                return Err("Trop de packages".into());
            }
            let paths: Vec<_> = files.into_iter().map(PathBuf::from).collect();
            let plan = install::prepare(&root, &paths)?;
            let packages: Vec<_> = paths.iter().map(|path| archive::inspect(path).map(|(d, h)| json!({"id":d.id,"label":d.label,"version":d.version,"kind":d.kind,"rights":d.rights,"files":d.files.len(),"bytes":d.files.iter().map(|f| f.bytes).sum::<u64>(),"manifest_sha256":h}))).collect::<Result<_>>()?;
            let response = json!({"plan":plan,"packages":packages});
            *host
                .plan
                .lock()
                .map_err(|_| "Installation state unavailable")? = Some(plan);
            Ok(response)
        }
        Request::Apply { command_id } => {
            let plan = host
                .plan
                .lock()
                .map_err(|_| "Installation state unavailable")?
                .clone()
                .ok_or("Préparer les packages avant installation")?;
            if plan.command_id != command_id {
                return Err("Le plan affiché a changé".into());
            }
            if Path::new(&plan.root) != root.as_path() {
                *host
                    .plan
                    .lock()
                    .map_err(|_| "Installation state unavailable")? = None;
                return Err("Le plan a été invalidé : le dossier d'installation a changé".into());
            }
            value(&install::apply(&plan)?)
        }
        Request::Verify => value(&install::verify(&root)?),
        Request::Launch { app } => {
            if !matches!(app.as_str(), "game" | "editor") {
                return Err("Application inconnue".into());
            }
            Ok(json!({"process_id":launch::start(&root, &app)?.id()}))
        }
        Request::Rollback => {
            let plan = install::prepare_rollback(&root)?;
            *host
                .plan
                .lock()
                .map_err(|_| "Installation state unavailable")? = Some(plan.clone());
            Ok(json!({"plan":plan,"packages":[]}))
        }
    }
}

#[tauri::command]
async fn delivery(request: Request, state: tauri::State<'_, Host>) -> Result<Value> {
    let host = state.inner().clone();
    tauri::async_runtime::spawn_blocking(move || execute(request, &host))
        .await
        .map_err(|e| e.to_string())?
}

fn root() -> Result<PathBuf> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    match args.as_slice() {
        [] => {
            let executable = std::env::current_exe().map_err(|e| e.to_string())?;
            if let Some(parent) = executable.parent() {
                let state = manifest::confined(parent, "installation.json")?;
                if state.try_exists().map_err(|e| e.to_string())? {
                    if !state.is_file() {
                        return Err("Le fichier d'installation voisin est invalide".into());
                    }
                    return validate_root(parent.to_path_buf());
                }
            }
            validate_root(launch::default_root()?)
        }
        [flag, directory] if flag == "--root" && Path::new(directory).is_absolute() => {
            validate_root(PathBuf::from(directory))
        }
        _ => Err("Usage: forge_hub --root ABSOLUTE_INSTALL_DIRECTORY".into()),
    }
}

fn main() {
    let result = root().and_then(|root| {
        tauri::Builder::default()
            .manage(Host {
                root: Arc::new(Mutex::new(root)),
                plan: Arc::new(Mutex::new(None)),
                operation: Arc::new(Mutex::new(())),
            })
            .invoke_handler(tauri::generate_handler![delivery])
            .run(tauri::generate_context!())
            .map_err(|e| e.to_string())
    });
    if let Err(error) = result {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_delivery::manifest::Kind;

    fn pending_plan(root: &Path) -> install::Plan {
        install::Plan {
            schema: "forge.delivery.plan.v1".into(),
            root: root.to_string_lossy().into_owned(),
            user_root: root.join("user").to_string_lossy().into_owned(),
            expected_head: forge_delivery::hash(&[]),
            base_revision: 0,
            command_id: "pending-command".into(),
            operation: "install".into(),
            inputs: Vec::new(),
            target: install::Snapshot {
                core: install::InstalledPack {
                    id: "forge-applications".into(),
                    revision: "a".repeat(64),
                    kind: Kind::Applications,
                },
                packs: std::collections::BTreeMap::new(),
            },
        }
    }

    #[test]
    fn changing_root_invalidates_pending_plan_and_preserves_unicode() -> Result<()> {
        let old_root = std::env::temp_dir().join("forge-hub-old-root");
        let selected = std::env::temp_dir().join("Forge Installé Éditeur");
        let host = Host {
            root: Arc::new(Mutex::new(old_root.clone())),
            plan: Arc::new(Mutex::new(Some(pending_plan(&old_root)))),
            operation: Arc::new(Mutex::new(())),
        };

        let response = change_root(&host, selected.clone())?;

        assert_eq!(response["changed"], true);
        assert_eq!(response["plan_invalidated"], true);
        assert_eq!(root_snapshot(&host)?, selected);
        assert!(
            host.plan
                .lock()
                .map_err(|_| "lock poisoned".to_owned())?
                .is_none()
        );
        Ok(())
    }

    #[test]
    fn apply_rejects_a_plan_bound_to_another_root() -> Result<()> {
        let active_root = std::env::temp_dir().join("forge-hub-active-root");
        let planned_root = std::env::temp_dir().join("forge-hub-planned-root");
        let host = Host {
            root: Arc::new(Mutex::new(active_root)),
            plan: Arc::new(Mutex::new(Some(pending_plan(&planned_root)))),
            operation: Arc::new(Mutex::new(())),
        };

        let error = execute(
            Request::Apply {
                command_id: "pending-command".into(),
            },
            &host,
        )
        .expect_err("a plan for another root must be rejected");

        assert!(error.contains("invalidé"));
        assert!(
            host.plan
                .lock()
                .map_err(|_| "lock poisoned".to_owned())?
                .is_none()
        );
        Ok(())
    }

    #[test]
    fn unsafe_root_paths_are_rejected_before_selection() {
        assert!(validate_root(PathBuf::from("forge-install")).is_err());
        assert!(validate_root(std::env::temp_dir().join("..").join("forge-install")).is_err());
    }
}
