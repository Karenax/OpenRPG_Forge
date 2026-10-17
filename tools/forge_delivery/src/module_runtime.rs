//! Read an admitted module zone without a workspace, Vault or authoring session.
use crate::{Result, archive, manifest, orpmod};
use forge_core::{AreaId, ContentHash};
use forge_project::{
    module_package::{
        MAX_MODULE_MANIFEST_BYTES, MODULE_MANIFEST_PATH, ModuleFileRole, ModulePackageManifest,
    },
    zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
    fs::{self, File},
    io::Read,
    path::{Path, PathBuf},
};

/// Canonical input and verified source blobs; renderer state is never persisted here.
pub struct LoadedModuleZone {
    pub package_root: PathBuf,
    pub manifest: ModulePackageManifest,
    pub archive: Vec<u8>,
    pub zone: ZoneEditSession,
    pub source_blobs: BTreeMap<ContentHash, Vec<u8>>,
}

impl LoadedModuleZone {
    /// Read a declared payload from the same bounded buffer whose pin is checked.
    pub fn read_payload(&self, path: &str, maximum: usize) -> Result<Vec<u8>> {
        let file = self
            .manifest
            .files
            .iter()
            .find(|file| file.path == path)
            .ok_or("Runtime payload is absent from the module inventory")?;
        read_pinned(&self.package_root, path, file.bytes, file.sha256, maximum)
    }
}

/// Verify the exact installed transport descriptor and every payload, after loading
/// and before scene construction. The zone archive pin remains a separate contract.
pub fn verify_transport_revision(loaded: &LoadedModuleZone, expected: ContentHash) -> Result<()> {
    let actual = inspect_installed(&loaded.package_root, &expected.to_string())?;
    if actual != loaded.manifest {
        return Err("Installed transport changed after the module was loaded".into());
    }
    Ok(())
}

/// `.orpmod` inputs require an absolute cache root. Admitted directories are read in place.
/// Cache revisions are immutable; existing or interrupted directories are never overwritten.
pub fn load(
    package_or_directory: &Path,
    area_id: AreaId,
    cache_root: Option<&Path>,
) -> Result<LoadedModuleZone> {
    let input = step(
        package_or_directory,
        "resolve absolute input",
        absolute(package_or_directory),
    )?;
    step(
        &input,
        "check input ancestor chain",
        manifest::safe_chain(&input),
    )?;
    let input = step(&input, "canonicalize input", fs::canonicalize(&input))?;
    let (root, module) = if input.is_dir() {
        let module = step(&input, "admit module directory", admitted_directory(&input))?;
        require_zone(&module, area_id)?;
        (input, module)
    } else {
        cached_archive(
            &input,
            area_id,
            cache_root.ok_or("An absolute module cache root is required")?,
        )
        .map_err(|error| {
            format!(
                "Module runtime: prepare archive cache for {}: {error}",
                input.display()
            )
        })?
    };
    let selected = module
        .zones
        .iter()
        .find(|zone| zone.area_id == area_id)
        .ok_or("Requested area is absent from this module")?;
    let archive_file = module
        .files
        .iter()
        .find(|file| file.path == selected.archive_path)
        .ok_or("Selected zone archive is absent from module inventory")?;
    let archive = read_pinned(
        &root,
        &archive_file.path,
        archive_file.bytes,
        archive_file.sha256,
        forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES,
    )?;
    step(
        &root.join(&archive_file.path),
        "validate selected zone archive",
        module.validate_zone_archive(area_id, &archive),
    )?;
    let zone = step(
        &root.join(&archive_file.path),
        "decode selected zone archive",
        ZoneEditSession::from_archive_bytes(&archive),
    )?;
    let mut source_blobs = BTreeMap::new();
    for hash in &selected.source_blobs {
        let file = module
            .files
            .iter()
            .find(|file| file.role == ModuleFileRole::SourceBlob && file.sha256 == *hash)
            .ok_or("Selected source blob is absent")?;
        // Raster dimensions cap one blob at 64 MiB; imported-instance blocks
        // have a smaller bound in their canonical source document.
        let bytes = read_pinned(&root, &file.path, file.bytes, *hash, 64 * 1024 * 1024)?;
        source_blobs.insert(*hash, bytes);
    }
    Ok(LoadedModuleZone {
        package_root: root,
        manifest: module,
        archive,
        zone,
        source_blobs,
    })
}

fn absolute(path: &Path) -> Result<PathBuf> {
    if path.is_absolute() {
        Ok(path.to_path_buf())
    } else {
        std::env::current_dir()
            .map(|root| root.join(path))
            .map_err(|error| error.to_string())
    }
}

fn require_zone(module: &ModulePackageManifest, area: AreaId) -> Result<()> {
    if module.zones.iter().any(|zone| zone.area_id == area) {
        Ok(())
    } else {
        Err("Requested area is absent from this module".into())
    }
}

fn admitted_directory(root: &Path) -> Result<ModulePackageManifest> {
    let descriptor_path = step(
        root,
        "resolve installed descriptor path",
        manifest::confined(root, "package-manifest.json"),
    )?;
    if !descriptor_path.try_exists().map_err(|error| {
        format!(
            "Module runtime: inspect descriptor {}: {error}",
            descriptor_path.display()
        )
    })? {
        return step(
            root,
            "admit source directory",
            orpmod::validate_directory(root, false),
        );
    }
    let descriptor: manifest::Descriptor = step(
        &descriptor_path,
        "read installed descriptor",
        crate::read_json(&descriptor_path),
    )?;
    let revision = crate::hash(&crate::json(&descriptor)?);
    inspect_installed(root, &revision)
}

pub(crate) fn inspect_installed(root: &Path, revision: &str) -> Result<ModulePackageManifest> {
    let descriptor = step(
        root,
        "verify installed descriptor and payload inventory",
        archive::verify_directory(root, revision),
    )?;
    let pin = descriptor
        .files
        .iter()
        .find(|file| file.path == MODULE_MANIFEST_PATH)
        .ok_or("Installed directory does not contain an .orpmod v2 manifest")?;
    ModulePackageManifest::from_bytes(&read_pinned(
        root,
        MODULE_MANIFEST_PATH,
        pin.bytes,
        ContentHash::parse(&pin.sha256).map_err(|error| error.to_string())?,
        MAX_MODULE_MANIFEST_BYTES,
    )?)
    .map_err(|error| error.to_string())
}

fn cached_archive(
    input: &Path,
    area: AreaId,
    cache: &Path,
) -> Result<(PathBuf, ModulePackageManifest)> {
    if !input
        .extension()
        .and_then(|extension| extension.to_str())
        .is_some_and(|extension| extension.eq_ignore_ascii_case("orpmod"))
    {
        return Err("Native module loading requires an .orpmod v2 archive".into());
    }
    let expected = step(input, "admit archive", orpmod::inspect(input, false))?;
    require_zone(&expected, area)?;
    let (_, revision) = step(
        input,
        "read archive transport descriptor",
        archive::inspect(input),
    )?;
    if !cache.is_absolute() {
        return Err("An absolute module cache root is required".into());
    }
    step(
        cache,
        "check cache ancestor chain",
        manifest::safe_chain(cache),
    )?;
    let parent = cache.join(expected.module_id.to_string());
    let destination = parent.join(&revision);
    step(
        &destination,
        "check cache revision ancestor chain",
        manifest::safe_chain(&destination),
    )?;
    if destination.try_exists().map_err(|error| {
        format!(
            "Module runtime: inspect cache revision {}: {error}",
            destination.display()
        )
    })? {
        step(
            &destination,
            "verify existing cache revision",
            archive::verify_directory(&destination, &revision),
        )?;
        let actual = admitted_directory(&destination)?;
        if actual != expected {
            return Err("Cached module manifest differs from archive".into());
        }
        return Ok((destination, actual));
    }
    step(
        &parent,
        "create module cache parent",
        fs::create_dir_all(&parent),
    )?;
    let stage = parent.join(format!(".module-{}", crate::attempt()?));
    step(
        &stage,
        &format!("extract archive {}", input.display()),
        archive::extract(input, &stage, &revision),
    )?;
    step(
        &stage,
        "verify staged cache revision",
        archive::verify_directory(&stage, &revision),
    )?;
    let actual = admitted_directory(&stage)?;
    if actual != expected {
        return Err("Module changed while preparing its runtime cache".into());
    }
    if let Err(error) = fs::rename(&stage, &destination) {
        // A concurrent successful loader may have published the same revision.
        // Validate that winner; retain our private staging for diagnosis.
        if !destination.try_exists().map_err(|probe| {
            format!(
                "Module runtime: inspect concurrent cache revision {}: {probe}",
                destination.display()
            )
        })? {
            return Err(format!(
                "Module runtime: promote cache {} to {}: {error}",
                stage.display(),
                destination.display()
            ));
        }
        step(
            &destination,
            "verify concurrent cache revision",
            archive::verify_directory(&destination, &revision),
        )?;
        if admitted_directory(&destination)? != expected {
            return Err("Concurrent module cache revision differs".into());
        }
    }
    Ok((destination, expected))
}

fn read_pinned(
    root: &Path,
    path: &str,
    bytes: u64,
    hash: ContentHash,
    maximum: usize,
) -> Result<Vec<u8>> {
    if bytes > maximum as u64 {
        return Err("Module source exceeds runtime read bound".into());
    }
    let path = step(
        &root.join(path),
        "resolve pinned payload",
        manifest::confined(root, path),
    )?;
    let file = step(&path, "open pinned payload", File::open(&path))?;
    let metadata = step(&path, "inspect pinned payload", file.metadata())?;
    if !metadata.is_file() || metadata.len() != bytes {
        return Err(format!(
            "Module runtime: pinned payload length changed after admission: {}",
            path.display()
        ));
    }
    let mut buffer = Vec::with_capacity(usize::try_from(bytes).map_err(|error| error.to_string())?);
    file.take(bytes + 1)
        .read_to_end(&mut buffer)
        .map_err(|error| {
            format!(
                "Module runtime: read pinned payload {}: {error}",
                path.display()
            )
        })?;
    if buffer.len() as u64 != bytes || forge_core::bytes_sha256(&buffer) != hash {
        return Err(format!(
            "Module runtime: pinned payload integrity changed after admission: {}",
            path.display()
        ));
    }
    Ok(buffer)
}

fn step<T, E: std::fmt::Display>(
    path: &Path,
    operation: &str,
    result: std::result::Result<T, E>,
) -> Result<T> {
    result.map_err(|error| format!("Module runtime: {operation} [{}]: {error}", path.display()))
}
