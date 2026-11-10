use forge_aurora_bridge::load_bundle;
use forge_aurora_runtime::{project_module, render_zone_map_svg, run_scripted_visit};
use std::env;
use std::fs;
use std::path::PathBuf;
use std::process::ExitCode;

fn main() -> ExitCode {
    match run(env::args().skip(1).collect()) {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("forge_aurora_visit: {error}");
            ExitCode::FAILURE
        }
    }
}

fn run(arguments: Vec<String>) -> Result<(), String> {
    let mut arguments = arguments.into_iter();
    let mut bundle = None;
    let mut package = None;
    let mut report = None;
    let mut map = None;
    while let Some(flag) = arguments.next() {
        if matches!(flag.as_str(), "--help" | "-h") {
            println!("{}", usage());
            return Ok(());
        }
        let value = arguments
            .next()
            .ok_or_else(|| format!("{flag} requires a value"))?;
        match flag.as_str() {
            "--bundle" => bundle = Some(PathBuf::from(value)),
            "--package-output" => package = Some(PathBuf::from(value)),
            "--report" => report = Some(PathBuf::from(value)),
            "--map" => map = Some(PathBuf::from(value)),
            _ => return Err(format!("unknown option {flag}")),
        }
    }
    let bundle = bundle.ok_or_else(usage)?;
    let imported = load_bundle(&bundle).map_err(|error| error.to_string())?;
    let projected = project_module(&imported).map_err(|error| error.to_string())?;
    if let Some(destination) = package {
        let package_report = projected
            .write_package(&destination)
            .map_err(|error| error.to_string())?;
        println!(
            "{}",
            serde_json::to_string_pretty(&package_report).map_err(|error| error.to_string())?
        );
    }
    let visit = run_scripted_visit(projected.clone())?;
    if let Some(path) = report {
        let bytes = serde_json::to_vec_pretty(&visit).map_err(|error| error.to_string())?;
        fs::write(&path, bytes)
            .map_err(|error| format!("cannot write {}: {error}", path.display()))?;
    }
    if let Some(path) = map {
        fs::write(&path, render_zone_map_svg(&projected, &visit))
            .map_err(|error| format!("cannot write {}: {error}", path.display()))?;
    }
    println!(
        "{}",
        serde_json::to_string_pretty(&visit).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn usage() -> String {
    "forge_aurora_visit --bundle <module-migration-v1> [--package-output <new-directory>] [--report <file.json>] [--map <file.svg>]".to_owned()
}
