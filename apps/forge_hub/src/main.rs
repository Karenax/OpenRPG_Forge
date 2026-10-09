#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
#![deny(unsafe_code)]
use forge_delivery::{Result, archive, install, launch};
use serde::Deserialize;
use serde_json::{Value, json};
use std::{
    path::{Path, PathBuf},
    process::Command,
    sync::{Arc, Mutex},
};

#[derive(Clone)]
struct Host {
    root: PathBuf,
    plan: Arc<Mutex<Option<install::Plan>>>,
    operation: Arc<Mutex<()>>,
}

#[derive(Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
enum Request {
    Status,
    Bundled,
    Select,
    Prepare { files: Vec<String> },
    Apply { command_id: String },
    Verify,
    Launch { app: String },
    Rollback,
}

fn value(value: &impl serde::Serialize) -> Result<Value> {
    serde_json::to_value(value).map_err(|e| e.to_string())
}

fn select_packages() -> Result<Vec<String>> {
    // Fixed OS dialog, no user-supplied script or shell command. Kept off the UI thread.
    let script = "Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='Packages Forge (*.forgepack)|*.forgepack'; $d.Multiselect=$true; if($d.ShowDialog() -eq 'OK'){ConvertTo-Json -Compress -InputObject @($d.FileNames)}else{'[]'}";
    let mut command = Command::new("powershell.exe");
    command.args(["-NoProfile", "-NonInteractive", "-Sta", "-Command", script]);
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    let output = command.output().map_err(|e| e.to_string())?;
    if !output.status.success() {
        return Err("Le sélecteur de packages n'a pas pu s'ouvrir".into());
    }
    serde_json::from_slice(&output.stdout).map_err(|e| e.to_string())
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
    match request {
        Request::Status => {
            let state = install::state(&host.root);
            match state {
                Ok(state) => Ok(
                    json!({"installed":true,"root":host.root,"product":install::product(&host.root, &state.active)?,"state":state}),
                ),
                Err(error) if !host.root.join("installation.json").exists() => {
                    Ok(json!({"installed":false,"root":host.root,"message":error}))
                }
                Err(error) => Err(error),
            }
        }
        Request::Bundled => bundled(),
        Request::Select => value(&select_packages()?),
        Request::Prepare { files } => {
            if files.len() > 1024 {
                return Err("Trop de packages".into());
            }
            let paths: Vec<_> = files.into_iter().map(PathBuf::from).collect();
            let plan = install::prepare(&host.root, &paths)?;
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
            value(&install::apply(&plan)?)
        }
        Request::Verify => value(&install::verify(&host.root)?),
        Request::Launch { app } => {
            if !matches!(app.as_str(), "game" | "editor") {
                return Err("Application inconnue".into());
            }
            Ok(json!({"process_id":launch::start(&host.root, &app)?.id()}))
        }
        Request::Rollback => {
            let plan = install::prepare_rollback(&host.root)?;
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
        [] => launch::default_root(),
        [flag, directory] if flag == "--root" && Path::new(directory).is_absolute() => {
            Ok(PathBuf::from(directory))
        }
        _ => Err("Usage: forge_hub --root ABSOLUTE_INSTALL_DIRECTORY".into()),
    }
}

fn main() {
    let result = root().and_then(|root| {
        tauri::Builder::default()
            .manage(Host {
                root,
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
