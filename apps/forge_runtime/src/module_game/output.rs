use std::{
    fs,
    path::{Path, PathBuf},
};

pub fn prepare(save: &Path, capture: Option<&Path>, module: &Path) -> Result<(), String> {
    forge_delivery::manifest::safe_chain(module)?;
    let module = module.canonicalize().map_err(|error| error.to_string())?;
    if !module.is_dir() {
        return Err("The installed module root must be a directory".into());
    }
    for path in std::iter::once(save).chain(capture) {
        forge_delivery::manifest::safe_chain(path)?;
        if prospective(path)?.starts_with(&module) {
            return Err("Runtime output cannot modify the installed module".into());
        }
    }
    if capture.is_some_and(Path::exists) {
        return Err("Refusing to overwrite a module capture".into());
    }
    fs::create_dir_all(save).map_err(|error| error.to_string())?;
    if let Some(parent) = capture.and_then(Path::parent) {
        fs::create_dir_all(parent).map_err(|error| error.to_string())?;
    }
    Ok(())
}

fn prospective(path: &Path) -> Result<PathBuf, String> {
    let mut ancestor = path;
    let mut missing = Vec::new();
    while !ancestor.try_exists().map_err(|error| error.to_string())? {
        missing.push(
            ancestor
                .file_name()
                .ok_or("Runtime output has no existing ancestor")?,
        );
        ancestor = ancestor
            .parent()
            .ok_or("Runtime output has no existing ancestor")?;
    }
    let mut resolved = ancestor.canonicalize().map_err(|error| error.to_string())?;
    for part in missing.into_iter().rev() {
        resolved.push(part);
    }
    Ok(resolved)
}
