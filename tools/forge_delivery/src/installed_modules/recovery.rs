//! Recover content revisions before the historical installation is retired.
use crate::{Result, archive, install, manifest};
use forge_project::module_package::MODULE_MANIFEST_PATH;
use serde::Serialize;
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
};

pub const RECOVERED_MODULES_DIRECTORY: &str = "recovered-modules";

#[derive(Debug, Serialize)]
pub struct RecoveredModule {
    pub pack_id: String,
    pub manifest_sha256: String,
    pub archive_sha256: String,
    pub bytes: u64,
    pub rights: manifest::Rights,
    pub package: PathBuf,
}

/// Preserve the active and rollback module revisions in the pinned user directory.
/// Historical content without a semantic module manifest keeps its Forge package format.
pub fn recover_legacy_modules(root: &Path) -> Result<Vec<RecoveredModule>> {
    recover_installation(root, None)
}

/// The Studio installer recovers content only when it adopted the same profile.
/// An orphaned historical registration is equivalent to a fresh install.
pub fn recover_legacy_modules_for_studio(
    root: &Path,
    studio_root: &Path,
) -> Result<Vec<RecoveredModule>> {
    let root = manifest::normalized_directory(root)?;
    if !manifest::confined(&root, "installation.json")?
        .try_exists()
        .map_err(|error| error.to_string())?
    {
        return Ok(Vec::new());
    }
    let studio_root = manifest::normalized_directory(studio_root)?;
    if !install::physically_disjoint(&root, &studio_root)? {
        return Err("Les dossiers historiques et du Studio doivent être distincts.".into());
    }
    install::state(&studio_root)?;
    install::with_lock(&studio_root, || {
        let studio = install::state(&studio_root)?;
        if install::product(&studio_root, &studio.active)?.schema != manifest::STUDIO_PRODUCT_SCHEMA
        {
            return Err("La destination n’est pas une installation du Studio.".into());
        }
        let user = manifest::normalized_directory(Path::new(&studio.user_root))?;
        recover_installation(&root, Some(&user))
    })
}

fn recover_installation(root: &Path, studio_user: Option<&Path>) -> Result<Vec<RecoveredModule>> {
    let root = manifest::normalized_directory(root)?;
    install::state(&root)?;
    install::with_lock(&root, || {
        let state = install::state(&root)?;
        if install::product(&root, &state.active)?.schema != "forge.delivery.product.v1" {
            return Err("Cette récupération concerne une installation Forge historique.".into());
        }
        let user = manifest::normalized_directory(Path::new(&state.user_root))?;
        if studio_user.is_some_and(|profile| profile != user) {
            return Ok(Vec::new());
        }
        if !install::physically_disjoint(&root.join("packages"), &user)? {
            return Err("Le dossier utilisateur historique recouvre les paquets installés.".into());
        }
        let mut packs = BTreeMap::new();
        for snapshot in std::iter::once(&state.active).chain(state.previous.as_ref()) {
            if snapshot.packs.len() > 1024 {
                return Err("Invalid installed snapshot".into());
            }
            for (id, pack) in &snapshot.packs {
                if id != &pack.id || pack.kind == manifest::Kind::Applications {
                    return Err("Invalid installed pack index".into());
                }
                if pack.kind == manifest::Kind::Module {
                    packs.insert((&pack.id, &pack.revision), pack);
                }
            }
        }
        if packs.is_empty() {
            return Ok(Vec::new());
        }
        let output = manifest::confined(&user, RECOVERED_MODULES_DIRECTORY)?;
        fs::create_dir_all(&output).map_err(|error| error.to_string())?;
        packs
            .into_values()
            .map(|pack| recover(&root, &output, pack))
            .collect()
    })
}

fn recover(root: &Path, output: &Path, pack: &install::InstalledPack) -> Result<RecoveredModule> {
    let source = install::package_root(root, pack)?;
    let descriptor: manifest::Descriptor =
        crate::read_json(&manifest::confined(&source, "package-manifest.json")?)?;
    if descriptor.kind != manifest::Kind::Module
        || descriptor.id != pack.id
        || crate::hash(&crate::json(&descriptor)?) != pack.revision
    {
        return Err("Installed module descriptor differs from its revision".into());
    }
    let extension = if descriptor
        .files
        .iter()
        .any(|entry| entry.path == MODULE_MANIFEST_PATH)
    {
        "orpmod"
    } else {
        "forgepack"
    };
    let package = manifest::confined(
        output,
        &format!("{}-{}.{extension}", pack.id, pack.revision),
    )?;
    if !package.try_exists().map_err(|error| error.to_string())? {
        let staged = manifest::confined(output, &format!(".recover-{}.tmp", crate::attempt()?))?;
        archive::export_installed(&source, &pack.revision, &staged)?;
        let published = forge_project::workspace::publish_no_replace(&staged, &package);
        // The publisher consumes the stage on Windows. A successful link or a
        // competing publication can leave our own stage to remove on Unix.
        if !matches!(published, Ok(true)) {
            fs::remove_file(staged).map_err(|error| error.to_string())?;
        }
        match published {
            Ok(_) => (),
            Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => (),
            Err(error) => return Err(error.to_string()),
        }
    }
    archive::verify_payload(&package, &pack.revision)?;
    Ok(RecoveredModule {
        pack_id: pack.id.clone(),
        manifest_sha256: pack.revision.clone(),
        archive_sha256: crate::file_hash(&package)?,
        bytes: fs::metadata(&package)
            .map_err(|error| error.to_string())?
            .len(),
        rights: descriptor.rights,
        package,
    })
}
