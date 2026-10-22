//! Native qualification tool; this is not a Studio import UI.
#[cfg(not(target_arch = "wasm32"))]
fn main() -> Result<(), String> {
    use forge_core::{AreaId, ContentHash};
    use forge_studio_frontend::module_workspace_bootstrap::{
        BootstrapRequest, ModuleSetRequest, prepare_module_set_workspace, prepare_module_workspace,
    };
    use std::{collections::BTreeMap, ffi::OsString, io::Read as _, path::PathBuf};
    let usage = "prepare_module_workspace --module FILE.orpmod --area UUID --expected-package-sha256 SHA256 --destination FRESH_ABSOLUTE_DIRECTORY\nOr: --module-set REQUEST.json. All module areas are preserved; source blobs are explicitly unsupported. No Vault fallback or UI qualification.";
    let mut args = std::env::args_os().skip(1);
    let mut values = BTreeMap::<String, OsString>::new();
    while let Some(flag) = args.next() {
        let flag = flag.to_str().ok_or("Options must be ASCII")?;
        if flag == "--help" {
            println!("{usage}");
            return Ok(());
        }
        if !matches!(
            flag,
            "--module" | "--area" | "--expected-package-sha256" | "--destination" | "--module-set"
        ) {
            return Err(format!("Unknown option {flag}\n{usage}"));
        }
        let value = args
            .next()
            .ok_or_else(|| format!("Missing value for {flag}"))?;
        if values.insert(flag.into(), value).is_some() {
            return Err(format!("Repeated option {flag}"));
        }
    }
    if let Some(path) = values.get("--module-set") {
        if values.len() != 1 {
            return Err("--module-set cannot be combined with single-module options".into());
        }
        let path = PathBuf::from(path);
        forge_delivery::manifest::safe_chain(&path)?;
        let file = std::fs::File::open(&path).map_err(|error| error.to_string())?;
        let mut bytes = Vec::new();
        file.take(64 * 1024 + 1)
            .read_to_end(&mut bytes)
            .map_err(|error| error.to_string())?;
        if bytes.len() > 64 * 1024 {
            return Err("Module set request exceeds its byte budget".into());
        }
        let request: ModuleSetRequest =
            serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
        let report = prepare_module_set_workspace(&request)?;
        println!(
            "{}",
            serde_json::to_string_pretty(&report).map_err(|error| error.to_string())?
        );
        return Ok(());
    }
    let value = |name: &str| {
        values
            .get(name)
            .ok_or_else(|| format!("Missing {name}\n{usage}"))
    };
    let request = BootstrapRequest {
        package_path: PathBuf::from(value("--module")?),
        expected_package_sha256: ContentHash::parse(
            value("--expected-package-sha256")?
                .to_str()
                .ok_or("Package pin must be ASCII")?,
        )
        .map_err(|e| e.to_string())?,
        area_id: value("--area")?
            .to_str()
            .ok_or("Area identity must be ASCII")?
            .parse::<AreaId>()
            .map_err(|e| e.to_string())?,
        destination: PathBuf::from(value("--destination")?),
    };
    let report = prepare_module_workspace(&request)?;
    println!(
        "{}",
        serde_json::to_string_pretty(&report).map_err(|e| e.to_string())?
    );
    Ok(())
}

#[cfg(target_arch = "wasm32")]
fn main() {
    eprintln!("prepare_module_workspace requires the native filesystem adapter");
}
