#![deny(unsafe_code)]
use forge_delivery::{Result, archive, install, installed_modules, launch, manifest::Descriptor};
use std::{
    io::{self, Write},
    path::{Path, PathBuf},
};

fn print(value: &impl serde::Serialize) -> Result<()> {
    println!(
        "{}",
        serde_json::to_string_pretty(value).map_err(|e| e.to_string())?
    );
    Ok(())
}

fn menu(root: &Path) -> Result<()> {
    loop {
        let state = install::state(root)?;
        let product = install::product(root, &state.active)?;
        println!(
            "\n{} — installation {}\n1. Jouer\n2. Ouvrir l'éditeur\n3. Vérifier l'installation\n4. Revenir à la version précédente\n0. Quitter",
            product.title, state.revision
        );
        print!("> ");
        io::stdout().flush().map_err(|e| e.to_string())?;
        let mut choice = String::new();
        if io::stdin()
            .read_line(&mut choice)
            .map_err(|e| e.to_string())?
            == 0
        {
            return Ok(());
        }
        let result = match choice.trim() {
            "0" => return Ok(()),
            "1" => launch::start(root, "game")
                .map(|child| println!("Jeu lancé (processus {}).", child.id())),
            "2" => launch::start(root, "editor")
                .map(|child| println!("Éditeur lancé (processus {}).", child.id())),
            "3" => install::verify(root).and_then(|s| print(&s)),
            "4" => install::prepare_rollback(root).and_then(|p| approve(&p)),
            _ => Err("Choisir 0, 1, 2, 3 ou 4.".into()),
        };
        if let Err(error) = result {
            eprintln!("{error}");
        }
    }
}

fn approve(plan: &install::Plan) -> Result<()> {
    print(plan)?;
    print!("Appliquer ce plan ? [oui/non] ");
    io::stdout().flush().map_err(|e| e.to_string())?;
    let mut answer = String::new();
    io::stdin()
        .read_line(&mut answer)
        .map_err(|e| e.to_string())?;
    if answer.trim() == "oui" {
        print(&install::apply(plan)?)
    } else {
        Ok(())
    }
}

fn run() -> Result<()> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    match args.first().map(String::as_str) {
        Some("module-check") if args.len() == 4 => {
            let cache=Path::new(&args[3]);
            forge_delivery::manifest::safe_chain(cache)?;
            let area=args[2].parse::<forge_core::AreaId>().map_err(|e|e.to_string())?;
            let loaded=forge_delivery::module_runtime::load(Path::new(&args[1]),area,Some(cache))?;
            let descriptor=forge_delivery::manifest::confined(&loaded.package_root,"package-manifest.json")?;
            let package_manifest_sha256=if descriptor.try_exists().map_err(|e|e.to_string())? {
                let descriptor:Descriptor=forge_delivery::read_json(&descriptor)?;
                let hash=forge_core::bytes_sha256(&forge_delivery::json(&descriptor)?);
                forge_delivery::module_runtime::verify_transport_revision(&loaded,hash)?;
                Some(hash)
            }else{None};
            print(&serde_json::json!({"schema":"forge.module-check","version":1,
                "module_id":loaded.manifest.module_id,"area_id":area,"head_hash":loaded.zone.head().content_hash()?,
                "archive_sha256":forge_core::bytes_sha256(&loaded.archive),"package_manifest_sha256":package_manifest_sha256,
                "module_manifest_canonical_sha256":forge_core::bytes_sha256(&loaded.manifest.to_bytes().map_err(|e|e.to_string())?),
                "package_root":loaded.package_root,"source_blobs":loaded.source_blobs.len(),
                "gpu_started":false,"runtime_qualified":false}))
        }
        Some("modules") if args.len() == 2 => print(&installed_modules::inventory(Path::new(&args[1]))?),
        Some("module-launch-plan") if args.len() == 4 => {
            print(&installed_modules::plan(Path::new(&args[1]), module_selection(&args[2], &args[3])?)?)
        }
        Some("module-launch") if args.len() == 4 => {
            let child=installed_modules::start(Path::new(&args[1]), module_selection(&args[2], &args[3])?)?;
            print(&serde_json::json!({"process_id":child.id()}))
        }
        Some("orpmod-validate") if args.len() == 2 || (args.len() == 3 && args[2] == "--publish") => {
            print(&forge_delivery::orpmod::validate_directory(Path::new(&args[1]), args.len() == 3)?)
        }
        Some("orpmod-pack") if args.len() == 4 || (args.len() == 5 && args[4] == "--publish") => {
            let descriptor: Descriptor = forge_delivery::read_json(Path::new(&args[2]))?;
            let (descriptor, revision) = forge_delivery::orpmod::pack(Path::new(&args[1]), descriptor, Path::new(&args[3]), args.len() == 5)?;
            print(&serde_json::json!({"manifest_sha256":revision,"archive_sha256":forge_delivery::file_hash(Path::new(&args[3]))?,"descriptor":descriptor}))
        }
        Some("orpmod-inspect") if args.len() == 2 || (args.len() == 3 && args[2] == "--publish") => {
            print(&forge_delivery::orpmod::inspect(Path::new(&args[1]), args.len() == 3)?)
        }
        Some("pack") if args.len() == 4 => {
            let descriptor: Descriptor = forge_delivery::read_json(Path::new(&args[2]))?;
            let (descriptor, revision) = archive::pack(Path::new(&args[1]), descriptor, Path::new(&args[3]))?;
            print(&serde_json::json!({"manifest_sha256":revision,"archive_sha256":forge_delivery::file_hash(Path::new(&args[3]))?,"descriptor":descriptor}))
        }
        Some("pack-selected") if args.len() >= 5 => {
            let descriptor: Descriptor = forge_delivery::read_json(Path::new(&args[2]))?;
            let (descriptor, revision) = archive::pack_selected(Path::new(&args[1]), descriptor, Path::new(&args[3]), &args[4..])?;
            print(&serde_json::json!({"manifest_sha256":revision,"archive_sha256":forge_delivery::file_hash(Path::new(&args[3]))?,"descriptor":descriptor}))
        }
        Some("pack-list") if args.len() == 5 => {
            let descriptor: Descriptor = forge_delivery::read_json(Path::new(&args[2]))?;
            let selections: Vec<String> = forge_delivery::read_json(Path::new(&args[4]))?;
            if selections.is_empty() || selections.len() > forge_delivery::manifest::MAX_FILES { return Err("Invalid selection count".into()); }
            let (descriptor, revision) = archive::pack_selected(Path::new(&args[1]), descriptor, Path::new(&args[3]), &selections)?;
            print(&serde_json::json!({"manifest_sha256":revision,"archive_sha256":forge_delivery::file_hash(Path::new(&args[3]))?,"descriptor":descriptor}))
        }
        Some("inspect") if args.len() == 2 => {
            let (descriptor, revision) = archive::inspect(Path::new(&args[1]))?;
            print(&serde_json::json!({"manifest_sha256":revision,"descriptor":descriptor}))
        }
        Some("plan") if args.len() >= 3 => print(&install::prepare(Path::new(&args[1]), &args[2..].iter().map(PathBuf::from).collect::<Vec<_>>())?),
        Some("plan-file") if args.len() >= 4 => {
            let plan = install::prepare(
                Path::new(&args[1]),
                &args[3..].iter().map(PathBuf::from).collect::<Vec<_>>(),
            )?;
            install::write_plan_file(&plan, Path::new(&args[2]))?;
            print(&plan)
        }
        Some("apply") if args.len() == 2 => print(&install::apply(&forge_delivery::read_json(Path::new(&args[1]))?)?),
        Some("verify") if args.len() == 2 => print(&install::verify(Path::new(&args[1]))?),
        Some("status") if args.len() == 2 => print(&install::state(Path::new(&args[1]))?),
        Some("rollback-plan") if args.len() == 2 => print(&install::prepare_rollback(Path::new(&args[1]))?),
        Some("uninstall") if args.len() == 2 => print(&install::uninstall(Path::new(&args[1]))?),
        Some("launch") if args.len() == 3 => print(&serde_json::json!({"process_id":launch::start(Path::new(&args[1]), &args[2])?.id()})),
        Some("launch-plan") if args.len() == 3 => {
            let command = launch::command(Path::new(&args[1]), &args[2])?;
            print(&serde_json::json!({"executable":command.get_program().to_string_lossy(),"arguments":command.get_args().map(|a| a.to_string_lossy().into_owned()).collect::<Vec<_>>(),"working_directory":command.get_current_dir(),"environment":command.get_envs().map(|(k,v)| (k.to_string_lossy().into_owned(),v.map(|s|s.to_string_lossy().into_owned()))).collect::<std::collections::BTreeMap<_,_>>()}))
        }
        Some("launcher") if args.len() == 2 => menu(Path::new(&args[1])),
        Some("setup") if args.len() >= 3 => {
            let root = Path::new(&args[1]);
            let plan = install::prepare(root, &args[2..].iter().map(PathBuf::from).collect::<Vec<_>>())?;
            approve(&plan)
        }
        None => {
            let here = std::env::current_exe().map_err(|e| e.to_string())?;
            let here = here.parent().ok_or("Executable directory missing")?;
            let root = launch::default_root()?;
            let delivery = here.join("delivery.json");
            if delivery.is_file() {
                let index: serde_json::Value = forge_delivery::read_json(&delivery)?;
                if index["schema"] != "forge.delivery.index.v1" { return Err("Unsupported delivery index".into()); }
                let mut archives = Vec::new();
                let packages = index["packages"].as_array().ok_or("Package index missing")?;
                if packages.is_empty() || packages.len() > 1024 { return Err("Invalid delivery package count".into()); }
                for entry in packages {
                    let path = manifest_path(here, entry["file"].as_str().ok_or("Package file missing")?)?;
                    if forge_delivery::file_hash(&path)? != entry["archive_sha256"].as_str().ok_or("Package pin missing")? { return Err("Delivery package integrity mismatch".into()); }
                    archives.push(path);
                }
                approve(&install::prepare(&root, &archives)?)?;
            }
            menu(&root)
        }
        _ => Err("Usage: forge_delivery module-check PACKAGE AREA_ID CACHE_ABSOLUTE | modules ROOT | module-launch-plan ROOT MODULE_ID AREA_ID | module-launch ROOT MODULE_ID AREA_ID | pack SOURCE DESCRIPTOR OUTPUT | inspect PACKAGE | plan ROOT PACKAGE... | plan-file ROOT OUTPUT PACKAGE... | apply PLAN | uninstall ROOT | setup ROOT PACKAGE... | status ROOT | verify ROOT | rollback-plan ROOT | launch ROOT game|editor | launcher ROOT".into()),
    }
}

fn module_selection(module: &str, area: &str) -> Result<installed_modules::ModuleSelection> {
    Ok(installed_modules::ModuleSelection {
        module_id: module
            .parse::<forge_core::ModuleId>()
            .map_err(|e| e.to_string())?,
        area_id: area
            .parse::<forge_core::AreaId>()
            .map_err(|e| e.to_string())?,
    })
}

fn manifest_path(root: &Path, value: &str) -> Result<PathBuf> {
    forge_delivery::manifest::confined(root, value)
}

fn main() {
    if let Err(message) = run() {
        eprintln!("{}", serde_json::json!({"error": message}));
        std::process::exit(1);
    }
}
